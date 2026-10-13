---
id: SPEC-0006
title: Ambientes, IaC, segredos, migrations e backup
tier: full
type: foundation
user_facing: false
status: approved
created: 2026-09-30
parent: SPEC-0001
depends_on: []
consumes_contract: [SPEC-0005@2]
contract_version: 1
touches: [deploy/**, .github/workflows/deploy.yml, src/Aspire/Juker.AppHost/Publish/**, tests/Juker.DeployTests/**, docs/operations.md]
adrs: [ADR-0004, ADR-0009]
external: []
size: M
approved_by: thomas
approved_at: 2026-09-30
---

# SPEC-0006 — Ambientes, IaC, segredos, migrations e backup

## 1. Visão Geral
Cria os ambientes **staging** e **produção** como código em VPS da Hetzner: provisionamento reproduzível (cloud-init), Docker Compose gerado pelo `aspire publish`, Caddy com HTTPS automático, segredos vindos dos GitHub Environments, migrations aplicadas pelo pipeline antes da troca de versão, health check pós-deploy com rollback automático, backup diário do Postgres com restauração testada e o workflow `deploy.yml`.

## 2. Motivação & Escopo
**Motivação:** cada entrega do MVP precisa chegar ao bar pelo pipeline, de forma reversível, e os dados da noite (fila, votos, catálogo) não podem se perder.

**Objetivos (dentro do escopo):**
- `deploy/cloud-init.yaml`: usuário `deploy` sem senha (chave SSH), Docker Engine, `ufw` (22 restrita às faixas do GitHub Actions + IP do fundador, 80/443 abertas), `unattended-upgrades`, `fail2ban`, fuso `America/Sao_Paulo`.
- Publicação: `aspire publish` com o ambiente Docker Compose (`src/Aspire/Juker.AppHost/Publish`) gerando `deploy/out/docker-compose.yaml` + `.env.template`; serviços `juker`, `postgres` (volume nomeado) e `caddy` (volume para os certificados).
- `deploy/Caddyfile`: `{$JUKER_DOMAIN}` → `juker:8080`, HSTS, compressão br/gzip.
- `.github/workflows/deploy.yml`: `workflow_run` do CI na `main` → **staging** automático; `workflow_dispatch` com `env` e `version` → **production**, com o environment `production` exigindo aprovação.
- Passos do deploy: copiar compose/Caddyfile por SSH → gravar `.env` a partir dos segredos do environment (permissão 600) → `docker compose pull` → rodar o **bundle de migrations** do EF (container efêmero) → `docker compose up -d juker` → aguardar `/health/ready` por até 90 s → se falhar, voltar à imagem anterior e marcar o workflow como falho.
- Backup: `deploy/backup.sh` agendado (systemd timer, 04:30 BRT) com `pg_dump -Fc` enviado para o Hetzner Object Storage (compatível com S3), retenção de 14 dias; `deploy/restore.sh`.
- `docs/operations.md`: runbook de deploy, rollback, restauração e rotação de segredos.
- Preenche `deploy_staging`, `deploy_production`, `staging_url` e `production_url` no `sdd-config.yml`.
- **Verificações em ambiente real herdadas** (transferidas quando o CD foi adiado): smoke E2E-03 da SPEC-0007 após cada deploy e o Lighthouse em staging; bar `juker-smoke` criado em staging e produção; ativação dos alertas e do uptime check da SPEC-0008 e disparo real do alerta `juker-ready-down`; verificação com securityheaders.com da SPEC-0009.

**Não-objetivos (fora do escopo):**
- Observabilidade e alertas (SPEC-0008).
- Múltiplas réplicas ou balanceador (ADR-0005).
- Terraform/Pulumi para a Hetzner (proposta: as VPS são criadas pelo fundador no console da Hetzner usando o `cloud-init.yaml` versionado; IaC completo depois do piloto).

## 3. Dependências
- **Implementações necessárias:** N/A.
- **Contratos consumidos:** SPEC-0005@2 — o output `image_digest` e a tag `ghcr.io/<owner>/juker:<sha>` do job `image`.
- **Pré-requisitos externos:** **domínio registrado** e DNS apontando para as VPS (`staging.<domínio>` e `<domínio>`), a ser definido pelo fundador; conta Hetzner com duas VPS (CX22 ou equivalente) e um bucket de Object Storage; segredos cadastrados nos GitHub Environments `staging` e `production`.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0009 (Hetzner + compose do Aspire) e ADR-0004 (migrations pelo pipeline, backup).

**Decisão:** um VPS por ambiente, com o mesmo compose e a imagem imutável por SHA; as migrations rodam antes da troca da imagem e são compatíveis com a versão anterior.

**Justificativa:** o mesmo artefato em staging e produção dá paridade, e o rollback vira uma troca de tag.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** Kubernetes (k3s) no VPS (complexidade sem ganho no piloto); migrations na subida da aplicação (corrida entre réplicas e rollback mais difícil).

**ADRs:** ADR-0004, ADR-0009.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** deploy completo em staging abaixo de 10 min, e indisponibilidade na troca de versão abaixo de 15 s — medidos no IT-02.
- **Segurança:** somente 80/443 públicas; SSH só por chave e restrito por firewall; `.env` com permissão 600, fora do repositório; imagem referenciada por digest; TLS via Caddy (Let's Encrypt) — verificado no IT-01 e no G4.
- **Privacidade e dados pessoais:** backups criptografados no servidor (SSE) e retenção de 14 dias; o bucket não é público — IT-03.
- **Disponibilidade e resiliência:** rollback automático se `/health/ready` não ficar 200 em 90 s — IT-04.
- **Acessibilidade (UI):** N/A.
- **Custo:** duas VPS CX22 + Object Storage — estimativa abaixo de €15/mês, confirmada na primeira fatura.

## 6. Artefato A — Contrato
**Interface:** `deploy.yml` (entradas e comportamento) + layout do servidor.

```text
deploy.yml
  on:
    workflow_run: { workflows: ["ci"], types: [completed], branches: [main] }  → env=staging, version=<head_sha>
    workflow_dispatch: inputs { env: staging|production (obrigatório), version: <sha> (obrigatório) }
  environment: <inputs.env>   # production exige aprovação
  secrets por environment: SSH_HOST, SSH_USER, SSH_KEY, JUKER_DOMAIN, POSTGRES_PASSWORD,
                           S3_ENDPOINT, S3_BUCKET, S3_ACCESS_KEY, S3_SECRET_KEY, OTLP_ENDPOINT, OTLP_HEADERS,
                           YOUTUBE_API_KEY
  resultado: job "deploy" verde ⇔ /health/ready = 200 na nova versão
             job vermelho + serviço restaurado na versão anterior ⇔ health falhou

Servidor (/opt/juker):
  docker-compose.yaml · Caddyfile · .env (600) · current_version · previous_version
  systemd: juker-backup.timer (04:30 America/Sao_Paulo) → backup.sh
```

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Compose publicado correto | `aspire publish` | compose com `juker`, `postgres` e `caddy`, sem segredos literais, imagem parametrizada por digest | UT-01 |
| Primeiro deploy | VPS recém-provisionada | `https://staging.<domínio>/health/ready` = 200 com certificado válido | IT-01 |
| Nova versão com migration | v2 adiciona uma coluna (expand) | dados da v1 preservados; v2 no ar; troca com menos de 15 s de indisponibilidade | IT-02 |
| Rollback manual | `deploy.yml -f version=<v1>` depois da v2 | v1 no ar e saudável com o schema da v2 | IT-02 |
| Backup e restauração | `backup.sh` e depois `restore.sh` num banco temporário | mesmas contagens de linhas por tabela; objeto no bucket privado | IT-03 |
| Health falha | imagem que não fica pronta | rollback automático para a versão anterior; workflow falho | IT-04 |
| Produção protegida | `workflow_dispatch env=production` | fica aguardando aprovação do environment | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado o `docker-compose.yaml` gerado pelo `aspire publish`, quando lido em `tests/Juker.DeployTests`, então contém os serviços `juker`, `postgres` e `caddy`; toda variável sensível é referência `${...}`; a imagem do `juker` é `${JUKER_IMAGE}`.

### 7.3 Testes de Integração
<!-- Executados contra o VPS de staging real; evidência = URL da execução do workflow + saída dos comandos. -->
- **IT-01** — Deploy da versão v1 numa VPS de staging recém-provisionada: `curl -fsS https://staging.<domínio>/health/ready` = 200, com o certificado emitido pela Let's Encrypt.
- **IT-02** — Deploy de uma v2 de teste com migration aditiva: as linhas gravadas na v1 continuam lá, a indisponibilidade medida (sonda a cada 1 s) fica abaixo de 15 s; em seguida, o rollback para a v1 fica saudável.
- **IT-03** — `backup.sh` gera o objeto no bucket (listagem via S3 API; acesso anônimo negado); `restore.sh` num banco temporário produz as mesmas contagens por tabela.
- **IT-04** — Deploy de uma imagem que nunca fica pronta: após 90 s, a versão anterior volta e o job termina vermelho.

### 7.4 Testes de Contrato
- **CT-01** — `tests/Juker.DeployTests` lê `deploy.yml` e verifica: as entradas `env`/`version`, o `environment` igual ao input `env`, o consumo do output/tag do CI (SPEC-0005@2) e que o environment `production` está declarado (a exigência de aprovação é conferida na configuração do repositório, com print como evidência).

### 7.5 Testes E2E
- N/A — user_facing: false (o smoke E2E pós-deploy entra com a SPEC-0007).

### 7.6 Outros
- Segurança: `nmap` a partir de fora mostra apenas as portas 22 (filtrada), 80 e 443 — evidência anexada ao G6.

**Dublês e dados de teste:** imagens de teste `juker:v1-test`/`v2-test` e `juker:never-ready` construídas só para os IT; banco temporário para a restauração.

**Ambiente de execução:** VPS de staging na Hetzner.

## 8. Plano de Rollout
- **Estratégia:** deploy direto do workflow; staging primeiro, produção depois do IT-01..IT-04 verdes em staging.
- **Dados/schema:** somente o volume do Postgres; nenhuma migration de negócio nesta spec.
- **Compatibilidade:** regra permanente — toda migration é compatível com a versão anterior da aplicação (expand → migrate → contract).
- **Observabilidade:** saída do workflow com a versão implantada; `current_version` no servidor.
- **Rollback:** `gh workflow run deploy.yml -f env=<env> -f version=<sha-anterior>`, em menos de 5 min, decidido pelo fundador (ou automático quando o health falha).
- **Etapas de migração/coexistência:** N/A.

## 9. Questões em Aberto
- [x] Domínio — ainda não definido; registrado como pré-requisito externo. O deploy de produção fica bloqueado até existir (thomas, 2026-09-30)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação

**Fase 1: Regras e componentes (unitário)**
- [ ] Red: escrever UT-01 com a tag `SPEC-0006:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs de fundação e o padrão de slices
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + lint + suíte de UT (G2)

**Fase 2: Integração e contrato**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, CT-01 com a tag `SPEC-0006:<ID>` (Postgres real, esquemas de auth reais, `DrainOutboxAsync`, WireMock — ADR-0012) e confirmar que falham pelo motivo certo
- [ ] Green: implementar endpoints, persistência, migrations e integrações até passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura + cobertura do diff ≥ 80% (G2/G3)

**Fase 3: Verificações adicionais (seção 7.6)**
- [ ] Executar as verificações da seção 7.6 e anexar a evidência ao Relatório de Entrega

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy via pipeline com smoke/E2E no ambiente (G6)
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | `spec_graph.py validate SPEC-0006` → G0 [SPEC-0006]: PASS — 0 erro(s), 0 aviso(s); checklist de julgamento do G0 revisado pelo Architect | 2026-09-30 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-09-30 | G1 | externo | Faltam domínio com DNS, VPS de staging na Hetzner, bucket de Object Storage e segredos no GitHub Environment 'staging' — os IT-01..IT-04 rodam contra o staging real | Spec adiada na onda 2 por decisão do fundador (2026-09-30) | thomas (provisionar a infraestrutura e cadastrar os segredos) |  |  |

## 14. Relatório de Entrega

### O que foi entregue

### Como foi feito

### Prova de Correção

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Todos os testes do plano passando e listados na Verificação
- [ ] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [ ] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [ ] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [ ] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6)
- [ ] Observabilidade e rollback prontos conforme o Plano de Rollout
- [ ] Documentação raiz e CHANGELOG atualizados (G7)
- [ ] Pendências registradas como novas specs (ou nenhuma)
- [ ] Textos da interface em português do Brasil, sem conceitos fora do produto (contas, créditos, mesa, música ao vivo)
- [ ] Tokens visuais vindos do design system do Stitch (projeto 8904741919458823023), sem cores/fontes avulsas

### Deploy

### Pendências

## 15. Emendas
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| escopo | 2026-09-30 | Recebe as verificações em ambiente real das SPECs 0007, 0008 e 0009 | CD e ambientes adiados por decisão do fundador; testes locais e CI primeiro | SPEC-0007, SPEC-0008, SPEC-0009 | thomas — autorização prévia no chat (2026-09-30: "deixar a infra de fora… optar pela recomendação"), confirmada por thomas no chat em 2026-10-13 |
