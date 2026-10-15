---
id: SPEC-0030
title: Reforço dos testes do Juker.Infrastructure (mutation score)
tier: lite
type: refactor
user_facing: false
status: proposed
created: 2026-10-15
parent: SPEC-0001
depends_on: [SPEC-0004, SPEC-0005]
consumes_contract: []
touches: [tests/Juker.BuildingBlocks.UnitTests/**]
adrs: [ADR-0011, ADR-0014]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0030 — Reforço dos testes do Juker.Infrastructure (mutation score)

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: refactor | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
O mutation testing da SPEC-0005 (`bash tools/ci/mutation.sh -f tests/Juker.BuildingBlocks.UnitTests/stryker-config.json`, alvo da regra 6 do ADR-0014 enquanto as classes de regra do MVP não existem) fecha em **46,94%**, abaixo do `break` de 80%: 23 mortos, 11 sobreviventes e 15 sem cobertura (execução local de 2026-10-15; mesma medida no G2 da SPEC-0005). Pela regra 6 do ADR-0014, score abaixo do mínimo abre uma spec lite para reforçar os testes. O código de produção está correto: o que falta são testes que detectem mudanças de comportamento.

## 2. Causa Raiz
Os testes de unidade da SPEC-0004 cobrem os caminhos felizes, mas não verificam:
- `ValidationQueryHandlerDecorator.cs`: **nenhum teste unitário** (15 mutantes sem cobertura; o decorator de consulta só é exercitado indiretamente).
- `ValidationCommandHandlerDecorator.cs`: o código e a mensagem do `Error` (linha 38), o `ToCamelCase` com `PropertyName` vazio (regra no nível do objeto, linha 51) e o `ThrowIfNull(command)` (linha 15).
- `ResultHttpExtensions.cs`: os `ThrowIfNull` (linhas 12, 19, 20) e o `&&` de `error.Type == ErrorType.Validation && error.Details is not null` (linha 42: erro de validação **sem** `Details` e erro não-validação **com** `Details`).
- `TenantQueryFilterConvention.cs`: o `ThrowIfNull(modelBuilder)` (linha 16).
- Mutantes equivalentes, impossíveis de matar: `ConfigureAwait(false)` → `true` (linhas 22 e 47 dos dois decorators), que não muda o comportamento num host sem `SynchronizationContext`.

## 3. Mudança Proposta
Só testes e a configuração do Stryker; **nenhuma linha de `src/` muda**.
- Testes unitários novos/estendidos em `tests/Juker.BuildingBlocks.UnitTests` (pastas `Pipeline`, `Http`, `Tenancy`) para cada lacuna da seção 2.
- `stryker-config.json`: `ignore-methods: ["*ConfigureAwait"]`, para não contar os mutantes equivalentes do `ConfigureAwait`; o motivo fica registrado no Relatório de Entrega. Os quatro arquivos-alvo e o `break` 80 não mudam.

Não muda: o código de produção, o alvo do Stryker (regra 6 do ADR-0014), o `mutation.yml` e o `break`.

**Padrão seguido:** testes existentes de `tests/Juker.BuildingBlocks.UnitTests/Pipeline` e `Http` (xUnit v3, AwesomeAssertions, NSubstitute — ADR-0011), tag `SPEC-0030:<ID>`.

**Rollback:** revert do commit.

**Outras ocorrências:** Nenhuma — os demais building blocks (outbox/inbox, `ModuleDbContext`) são cobertos por testes de integração e ficam fora do alvo do Stryker até a regra 6 apontar para as classes do MVP.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0030:<ID>`. Modo privado: sem tag no código; aponte o teste no fim da linha com `→ caminho::nome` (ex.: `→ tests/test_soma.py::test_soma`). Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — o comportamento atual já está fixado pelos testes da SPEC-0004; os testes novos passam no código atual por construção (só reforçam), e o "vermelho" desta spec é o IT-01.
- **UT-01** — Dado o `ValidationQueryHandlerDecorator` com um validador que reprova duas propriedades (`Nome`, `Idade`) e outro que aprova, quando a consulta é tratada, então o resultado é falha com `Error.Code = "validation"`, mensagem "Falha de validação da consulta.", `ErrorType.Validation`, `Details` com as chaves `nome` e `idade` e o handler interno não é chamado; sem validadores ou com todos aprovando, o handler interno é chamado uma vez e seu resultado é devolvido; consulta nula lança `ArgumentNullException`.
- **UT-02** — Dado o `ValidationCommandHandlerDecorator` com um validador que reprova uma regra do objeto (`PropertyName` vazio), quando o comando é tratado, então `Details` tem a chave `""` sem exceção, e o erro tem código "validation" e mensagem "Falha de validação do comando."; comando nulo lança `ArgumentNullException`.
- **UT-03** — Dado `ResultHttpExtensions`, quando recebe resultado nulo ou `onSuccess` nulo, então lança `ArgumentNullException`; um erro de validação **sem** `Details` vira `ProblemDetails` 400 com `code` e sem a extensão `errors`, e um erro de outro tipo (ex.: `Conflict`, 409) **com** `Details` também sai sem `errors`.
- **UT-04** — Dado `TenantQueryFilterConvention`, quando aplicada com `modelBuilder` nulo, então lança `ArgumentNullException`.
- **IT-01** — Dado o `stryker-config.json` do `Juker.BuildingBlocks.UnitTests`, quando `bash tools/ci/mutation.sh -f tests/Juker.BuildingBlocks.UnitTests/stryker-config.json` roda, então o score é ≥ 80% (falha antes: 46,94% em 2026-10-15) e nenhum mutante dos quatro arquivos fica sem cobertura.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 1fa8ef3 | 2026-10-15 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue

### Como foi feito

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

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
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
