---
id: SPEC-0027
title: Pendências do review da SPEC-0003
tier: lite
type: refactor
user_facing: false
status: approved
created: 2026-10-01
parent: SPEC-0001
depends_on: [SPEC-0003]
consumes_contract: []
touches: [tests/Juker.ArchitectureTests/**, tests/Juker.TestKit/**, tests/Juker.IntegrationTests/Harness/**, tests/Juker.IntegrationTests/Fixtures/**, docs/testing.md]
adrs: [ADR-0012, ADR-0014]
external: []
size: S
approved_by: thomas (autorização prévia de 2026-09-30, a confirmar no overview)
approved_at: 2026-09-30
---

# SPEC-0027 — Pendências do review da SPEC-0003

## 1. Problema
O review independente da SPEC-0003 (G4 aprovado em ef40d71) deixou quatro lacunas de cobertura no próprio harness:
1. O ramo do ARQ-08 que detecta mocks de `HttpMessageHandler` via NSubstitute/Moq (argumento genérico) funciona, mas não tem caso no fixture `Violations`: uma regressão nele passaria despercebida.
2. Um projeto de teste novo que não seja referenciado pelo `Juker.ArchitectureTests.csproj` (com `ReferenceOutputAssembly=false`) só é percebido num `arch_test` a partir de um build limpo.
3. O caminho do banco-modelo que aplica as migrations reais (`TemplateDatabase`) ainda não é exercitado, porque não existe nenhum `DbContext` na solução.
4. **Teste instável:** o CT-01 da SPEC-0003 (`Comando_test_integration_lista_somente_os_testes_Integration`) roda um `dotnet test --solution` aninhado dentro da suíte e falhou de forma intermitente em execuções completas (visto duas vezes em 2026-10-01), provavelmente por disputa de `bin`/`obj` com outros builds aninhados.
5. **Opt-in de zero testes com filtro:** projetos de teste só com testes `Unit` (ex.: `Juker.BuildingBlocks.UnitTests`) ficam vazios sob o filtro `Category=Integration` e precisam manter `JukerAllowZeroTests=true` mesmo tendo testes, o que enfraquece a proteção contra projeto que perde todos os testes (apontado na SPEC-0004).
6. A política de tokens do design aponta como cor qualquer trecho hexadecimal em string C# ou em atributo fora da lista de âncoras (ex.: `NavigateTo("#add")`, `id="#abc"`): é um falso positivo seguro, mas não documentado.

## 2. Causa Raiz
Casos não cobertos pelo plano de testes original da SPEC-0003 (achados minor do G4, aceitos como pendência).

## 3. Mudança Proposta
- Fixture `Violations`: tipos fake nos namespaces `NSubstitute` (`Substitute.For<T>()`) e `Moq` (`Mock<T>`), um infrator de cada que usa `HttpMessageHandler` e um controle que mocka outro tipo; os infratores entram no conjunto exato do ARQ-08.
- Teste de convenção: todo projeto `tests/**` do `Juker.slnx` (exceto fixtures) é `ProjectReference` do `Juker.ArchitectureTests.csproj`.
- Fixture de `DbContext` "limpo" (com migration) aplicado pelo `TemplateDatabase`: o banco da classe tem a tabela dessa migration.
- CT-01 da SPEC-0003 isolado: o `dotnet test` aninhado usa `--no-build` sobre artefatos próprios (`--artifacts-path` temporário) ou a coleção do teste é serializada em relação aos outros builds aninhados; aceitar a correção só com 10 execuções completas seguidas sem falha.
- Regra de zero testes que distingue "projeto vazio sob filtro" de "projeto sem testes": o `JukerAllowZeroTests` só é aceito em execuções filtradas, ou uma verificação de contagem mínima por projeto na execução sem filtro.
- `docs/testing.md`: como escrever navegação por fragmento em `.razor` sem acionar a política de tokens.
- **Não muda:** contrato do `Juker.TestKit` (snapshot), regras ARQ-01..ARQ-08 em produção.

**Padrão seguido:** `tests/Juker.ArchitectureTests/Fixtures/Violations/**`, `tests/Juker.ArchitectureTests/Conventions/**`, `tests/Juker.IntegrationTests/Harness/**` (SPEC-0003).

**Rollback:** revert dos commits da spec.

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — área já coberta pelos testes da SPEC-0003, que continuam valendo.
- **UT-01** — (guarda: passa antes da mudança) Dado o fixture com `Substitute.For<HttpMessageHandler>()` e `new Mock<HttpMessageHandler>()`, quando o ARQ-08 avalia, então os dois são apontados e o controle `Substitute.For<IDisposable>()` não é.
- **UT-02** — Dado o `Juker.slnx`, quando o teste de convenção compara os projetos `tests/**` com as `ProjectReference` do `Juker.ArchitectureTests.csproj`, então falha listando qualquer projeto ausente (hoje passa; com um projeto de exemplo ausente num fixture de solução, falha).
- **IT-02** — (regressão do teste instável) 10 execuções seguidas de `dotnet test --solution Juker.slnx -c Release` terminam sem nenhuma falha do CT-01 da SPEC-0003 (evidência: script de repetição e saídas).
- **IT-01** — Dado um `DbContext` de fixture com uma migration, quando o `TemplateDatabase` prepara o modelo, então o banco clonado da classe contém a tabela criada pela migration.

## 5. Questões em Aberto
Nenhuma.

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação

**Fase 1: Regressão**
- [ ] Red: escrever UT-01, UT-02, IT-01, IT-02 com a tag `SPEC-0027:<ID>`; confirmar que as regressões falham pelo motivo certo (guardas marcadas passam antes da mudança)
- [ ] Green: implementar a mudança proposta
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura + lint (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração (G5) e aprovação (H2)
- [ ] Deploy via pipeline (G6) — N/A enquanto o CD estiver adiado
- [ ] Relatório de Entrega e CHANGELOG (G7)

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
