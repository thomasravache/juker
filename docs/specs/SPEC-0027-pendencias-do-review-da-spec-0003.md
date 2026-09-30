---
id: SPEC-0027
title: Pendências do review da SPEC-0003
tier: lite
type: refactor
user_facing: false
status: proposed
created: 2026-10-01
parent: SPEC-0001
depends_on: [SPEC-0003]
consumes_contract: []
touches: [tests/Juker.ArchitectureTests/**, tests/Juker.IntegrationTests/Harness/**, tests/Juker.IntegrationTests/Fixtures/**, docs/testing.md]
adrs: [ADR-0012, ADR-0014]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0027 — Pendências do review da SPEC-0003

## 1. Problema
O review independente da SPEC-0003 (G4 aprovado em ef40d71) deixou quatro lacunas de cobertura no próprio harness:
1. O ramo do ARQ-08 que detecta mocks de `HttpMessageHandler` via NSubstitute/Moq (argumento genérico) funciona, mas não tem caso no fixture `Violations`: uma regressão nele passaria despercebida.
2. Um projeto de teste novo que não seja referenciado pelo `Juker.ArchitectureTests.csproj` (com `ReferenceOutputAssembly=false`) só é percebido num `arch_test` a partir de um build limpo.
3. O caminho do banco-modelo que aplica as migrations reais (`TemplateDatabase`) ainda não é exercitado, porque não existe nenhum `DbContext` na solução.
4. A política de tokens do design aponta como cor qualquer trecho hexadecimal em string C# ou em atributo fora da lista de âncoras (ex.: `NavigateTo("#add")`, `id="#abc"`): é um falso positivo seguro, mas não documentado.

## 2. Causa Raiz
Casos não cobertos pelo plano de testes original da SPEC-0003 (achados minor do G4, aceitos como pendência).

## 3. Mudança Proposta
- Fixture `Violations`: tipos fake nos namespaces `NSubstitute` (`Substitute.For<T>()`) e `Moq` (`Mock<T>`), um infrator de cada que usa `HttpMessageHandler` e um controle que mocka outro tipo; os infratores entram no conjunto exato do ARQ-08.
- Teste de convenção: todo projeto `tests/**` do `Juker.slnx` (exceto fixtures) é `ProjectReference` do `Juker.ArchitectureTests.csproj`.
- Fixture de `DbContext` "limpo" (com migration) aplicado pelo `TemplateDatabase`: o banco da classe tem a tabela dessa migration.
- `docs/testing.md`: como escrever navegação por fragmento em `.razor` sem acionar a política de tokens.
- **Não muda:** contrato do `Juker.TestKit` (snapshot), regras ARQ-01..ARQ-08 em produção.

**Padrão seguido:** `tests/Juker.ArchitectureTests/Fixtures/Violations/**`, `tests/Juker.ArchitectureTests/Conventions/**`, `tests/Juker.IntegrationTests/Harness/**` (SPEC-0003).

**Rollback:** revert dos commits da spec.

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — área já coberta pelos testes da SPEC-0003, que continuam valendo.
- **UT-01** — (guarda: passa antes da mudança) Dado o fixture com `Substitute.For<HttpMessageHandler>()` e `new Mock<HttpMessageHandler>()`, quando o ARQ-08 avalia, então os dois são apontados e o controle `Substitute.For<IDisposable>()` não é.
- **UT-02** — Dado o `Juker.slnx`, quando o teste de convenção compara os projetos `tests/**` com as `ProjectReference` do `Juker.ArchitectureTests.csproj`, então falha listando qualquer projeto ausente (hoje passa; com um projeto de exemplo ausente num fixture de solução, falha).
- **IT-01** — Dado um `DbContext` de fixture com uma migration, quando o `TemplateDatabase` prepara o modelo, então o banco clonado da classe contém a tabela criada pela migration.

## 5. Questões em Aberto
Nenhuma.

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação

## 8. Registro de Gates
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | `spec_graph.py validate SPEC-0027` → 0 erros; aguardando H1 do fundador | 2026-10-01 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega

### O que foi entregue

### Como foi feito

### Prova de Correção

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Teste de regressão falhou antes e passa depois da correção
- [ ] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão existente mantido
- [ ] Disponível no ambiente-alvo via pipeline (G6)
- [ ] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [ ] Outras ocorrências registradas como novas specs (ou nenhuma)

### Deploy

### Pendências

## 11. Emendas
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
