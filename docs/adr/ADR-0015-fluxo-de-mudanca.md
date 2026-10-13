---
id: ADR-0015
title: Fluxo de mudança — PR para a main com merge commit, checks obrigatórios e merge automático autorizado com CI verde
status: proposed
origin: decision
date: 2026-10-13
pillars: [fluxo-mudanca]
baseline: fluxo-mudanca@2026.09
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: Proteção da branch main no GitHub (checks da SPEC-0005); `change_flow: pr` e `h2_policy: auto-on-green` no sdd-config; `spec_graph.py validate` e `pr-check`
---

# ADR-0015 — Fluxo de mudança — PR para a main com merge commit, checks obrigatórios e merge automático autorizado com CI verde

## Contexto e Problema
O Juker tem um único desenvolvedor (o fundador) e agentes de IA que escrevem testes, código e reviews. Sem um caminho único para a `main`, um agente pode publicar mudança sem spec, sem gates ou sem os testes do plano. Até a SPEC-0004 as ondas entravam por merge local e push direto.

## Direcionadores da Decisão
- `main` sempre verde e implantável (ADR-0009).
- Evidência dos gates citando SHAs que não podem sumir.
- Um só revisor humano (o fundador), que não quer aprovar cada merge à mão quando o CI está verde.

## Opções Consideradas
- PR no GitHub da branch de integração (`sdd/SPEC-0001`) para a `main`, com merge commit e checks obrigatórios
- Merge local na `main` e push (fluxo anterior)
- PR com squash merge

## Resultado da Decisão
**Opção escolhida:** PR no GitHub com merge commit, porque mantém os SHAs citados nas evidências, permite tornar os checks da SPEC-0005 obrigatórios e deixa o histórico de cada onda auditável num lugar só.

**Regras (verificáveis):**
- Toda mudança entra na `main` por PR (código, specs, ADRs e registros SDD inclusive).
- Checks obrigatórios: os jobs do `ci.yml` da SPEC-0005 (build, lint, unit, integration, architecture, e2e, security, coverage, sdd). Até a SPEC-0005 existir, a verificação local completa do Architect, registrada no PR, faz esse papel.
- Revisão: o fundador autorizou (2026-10-13) o merge pelo agente quando todos os checks passarem (`h2_policy: auto-on-green`); a revisão independente de código continua sendo o G4 de cada spec. Deploy em produção nunca é coberto por essa autorização.
- Sem push direto nem force-push na `main`, inclusive para o administrador (proteção de branch ativada pelo fundador, SPEC-0005).
- Merge com merge commit, nunca squash nem rebase.
- Arquivos de controle (`tools/sdd`, `docs/specs/sdd-config.yml`, `docs/adr`, `.githooks`, `.github/workflows`) só mudam por PR; com um único mantenedor, não há CODEOWNERS.
- Correção urgente segue o mesmo caminho, com spec lite criada na hora.

### Consequências
- **Boa**, porque nada chega à `main` sem spec aprovada e gates registrados, e o histórico de cada onda fica num PR.
- **Ruim**, porque cada onda exige um PR a mais, e o merge automático depende de o CI ser confiável (daí a SPEC-0027 antes da SPEC-0005). Regra removida do modelo: "pelo menos N revisões humanas" — com um único humano, a revisão é o G4 independente e o H2 por política registrada.

### Confirmação (G3)
Proteção de branch no GitHub com os checks obrigatórios (`docs/ci.md`, SPEC-0005); `change_flow: pr` no sdd-config; `validate` aponta commits diretos e merges fora de PR.

## Prós e Contras das Opções
| Critério (peso) | PR + merge commit | Merge local | PR + squash |
|---|---|---|---|
| Checks obrigatórios antes da `main` (5) | 5 | 1 | 5 |
| SHAs das evidências preservados (4) | 5 | 5 | 1 |
| Esforço para um dev solo (3) | 4 | 5 | 4 |

## Mais Informações
Referências neutras: práticas de trunk-based development. Proteção de branch do GitHub: https://docs.github.com/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches (não verificado nesta data; conferir ao ativar na SPEC-0005).
