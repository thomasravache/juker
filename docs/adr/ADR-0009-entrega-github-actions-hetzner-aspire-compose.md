---
id: ADR-0009
title: Entrega com GitHub Actions, imagens no GHCR e VPS Hetzner via Docker Compose gerado pelo Aspire
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: pipeline de deploy (SPEC-0005/SPEC-0006) — smoke obrigatório após cada deploy
pillars: [entrega]
---

# ADR-0009 — Entrega com GitHub Actions, GHCR e VPS Hetzner via Docker Compose gerado pelo Aspire

## Contexto e Problema
O fundador quer usar o Aspire para facilitar o deploy. O piloto é um bar, o orçamento é mínimo e ele escolheu GitHub + GitHub Actions e Hetzner. Como o código chega a staging e à produção?

## Direcionadores da Decisão
- Custo mínimo no piloto (poucos euros por mês).
- Usar o Aspire como orquestração local e fonte do artefato de deploy.
- Deploy e rollback em um comando, pelo pipeline.
- Migrar para PaaS no futuro sem reescrever.

## Opções Consideradas
- Aspire `publish` → `docker-compose.yaml` + imagens no GHCR → VPS Hetzner (staging e produção) com Caddy (TLS automático), deploy por SSH no GitHub Actions
- Azure Container Apps via `azd`
- Fly.io / Render (container gerenciado)

## Resultado da Decisão
**Opção escolhida:** "Aspire compose + Hetzner", porque a publicação em Docker Compose é estável desde o Aspire 13.2 e o VPS custa uma fração do PaaS. Detalhes:
- Trunk-based: PR → CI; merge em `main` → build da imagem `ghcr.io/<owner>/juker:<sha>` → deploy automático em **staging** + smoke/E2E; **produção** pelo mesmo workflow, com aprovação do GitHub Environment `production` (G6).
- Serviços no compose: `juker` (host), `postgres:18` com volume, `caddy` (reverse proxy + HTTPS). Um VPS por ambiente.
- Migrations: bundle do EF executado como passo do deploy antes de trocar a imagem (ADR-0004).
- Rollback: redeploy da tag anterior (`deploy.yml -f version=<sha-anterior>`), com migrations sempre compatíveis com a versão anterior.
- Segredos: GitHub Environments → `.env` no VPS com permissão 600. Nunca no repositório.
- Backups: `pg_dump` diário para um bucket de object storage compatível com S3, com retenção de 14 dias e restauração testada.

### Consequências
- **Boa**, porque o custo fica em poucos euros por mês e dá para migrar para ACA/Fly depois com o mesmo artefato de container.
- **Ruim**, porque patching do SO, backup e firewall ficam por nossa conta; mitigado por checklist no IaC (SPEC-0006) e updates automáticos de segurança.
- **Ruim**, porque a latência Brasil–Europa é de ~120–180 ms, aceitável para o piloto. Se necessário, trocar para a região de Ashburn (EUA).

### Confirmação (G3)
N/A por teste de arquitetura; garantido pelo workflow: nenhum deploy sem imagem assinada por SHA, migrations aplicadas e smoke verde (SPEC-0006:IT-02, SPEC-0007:E2E-01 como smoke).

## Prós e Contras das Opções
| Critério (peso) | Aspire compose + Hetzner | ACA via azd | Fly/Render |
|---|---|---|---|
| Custo no piloto (5) | 5 | 2 | 3 |
| Integração com Aspire (4) | 4 | 5 | 3 |
| Operação (menos trabalho) (3) | 2 | 5 | 4 |
| Portabilidade (3) | 5 | 3 | 4 |
| **Total ponderado** | **62** | **50** | **51** |

## Mais Informações
- Aspire 13.5 (18/08/2026) é a versão estável mais recente; publicação Docker Compose estável desde 13.2 (23/03/2026) — https://aspire.dev/whats-new/aspire-13-2/ e https://devblogs.microsoft.com/aspire/whats-new-aspire-13-5/ (consultado em 2026-09-30).
- Atualização em 2026-09-30 (verificação do Test-writer da SPEC-0002 no índice do nuget.org): a versão estável mais recente dos pacotes Aspire é **13.6.0**; a decisão não muda, e a SPEC-0002 fixa a estável vigente.
- Domínio: **a definir** pelo fundador antes do primeiro deploy (pré-requisito externo de SPEC-0006).
