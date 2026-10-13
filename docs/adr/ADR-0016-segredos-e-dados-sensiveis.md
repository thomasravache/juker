---
id: ADR-0016
title: Segredos e dados sensíveis — GitHub Environments, .env 600 na VPS, gitleaks no CI e nenhum dado pessoal do cliente
status: proposed
origin: decision
date: 2026-10-13
pillars: [segredos-dados]
baseline: segredos-dados@2026.09
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: SPEC-0005:IT-03 (gitleaks bloqueia segredo), SPEC-0006 (segredos por ambiente, .env 600, bucket privado com SSE) e SPEC-0009 (logs sem e-mail completo)
---

# ADR-0016 — Segredos e dados sensíveis — GitHub Environments, .env 600 na VPS, gitleaks no CI e nenhum dado pessoal do cliente

## Contexto e Problema
Credencial em código ou log e dado pessoal sem controle são as falhas de segurança e de conformidade mais comuns. O Juker guarda credenciais de banco, da API do YouTube, do Object Storage e do backend de observabilidade; os únicos dados pessoais são os do staff do bar (e-mail e hash de senha). O cliente entra só com número da comanda e o PIN da TV, sem cadastro.

## Direcionadores da Decisão
- Dev solo, sem equipe de operações: o caminho seguro precisa ser o caminho padrão.
- Custo mínimo no piloto (ADR-0009).
- LGPD: coletar o mínimo de dados pessoais.

## Opções Consideradas
- GitHub Environments + `.env` com permissão 600 na VPS, gravado pelo deploy (ADR-0009)
- Gerenciador de segredos dedicado (Vault, Infisical, Doppler)

## Resultado da Decisão
**Opção escolhida:** GitHub Environments + `.env` 600 na VPS, porque já está no fluxo de deploy do ADR-0009, não custa nada e separa staging de produção sem outro serviço para operar.

**Regras (verificáveis):**
- Nenhum segredo no repositório; `gitleaks detect` no job `security` do CI bloqueia o merge (SPEC-0005).
- Segredos ficam nos GitHub Environments `staging` e `production`, separados por ambiente; na VPS, só no `.env` com permissão 600, fora do repositório. Localmente, `dotnet user-secrets` ou o Aspire; `appsettings.*.local.json` e `.env*` no `.gitignore`.
- Rotação de credenciais a cada 12 meses e imediata em caso de vazamento (runbook em `docs/operations.md`, SPEC-0006).
- Dados pessoais: só os do staff (e-mail e hash de senha, base legal: execução do contrato com o bar), mantidos enquanto o gerente estiver ativo. Nenhum dado pessoal do cliente: a comanda e o token de sessão não identificam a pessoa.
- Dados pessoais e segredos nunca aparecem em logs: e-mail só com o domínio e tokens nunca logados (SPEC-0009, verificado por teste).
- Criptografia em trânsito (Caddy com TLS automático e HSTS) e em repouso nos backups (SSE do Object Storage, bucket privado).

### Consequências
- **Boa**, porque não há serviço extra para operar e o vazamento acidental mais comum (segredo commitado) é barrado no CI.
- **Ruim**, porque o disco da VPS não é criptografado (o Postgres e o `.env` ficam em texto no volume) e a rotação é manual; aceitável enquanto não há dado pessoal do cliente.

### Confirmação (G3)
`gitleaks` no `security_scan` (SPEC-0005); teste de que logs de login não trazem o e-mail completo (SPEC-0009); IT-03 da SPEC-0006 (bucket privado).

## Prós e Contras das Opções
| Critério (peso) | GitHub Environments + .env | Gerenciador dedicado |
|---|---|---|
| Custo no piloto (5) | 5 | 3 |
| Esforço de operação (4) | 4 | 2 |
| Rotação e auditoria (3) | 2 | 5 |

## Mais Informações
Referências neutras: OWASP ASVS; LGPD (Lei 13.709/2018). GitHub Environments: https://docs.github.com/actions/deployment/targeting-different-environments/using-environments-for-deployment e gitleaks: https://github.com/gitleaks/gitleaks (não verificados nesta data; versões fixadas na SPEC-0005).
