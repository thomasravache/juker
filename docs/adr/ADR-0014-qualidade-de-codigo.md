---
id: ADR-0014
title: Qualidade de código — analisadores do SDK + Meziantou, APIs proibidas no build, formatação, cobertura do diff e mutation testing nas regras críticas
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: SPEC-0002:IT-03 (build quebra com API proibida e regra do Meziantou), SPEC-0003:UT-02 (tokens do design) e SPEC-0005:IT-06 (cobertura do diff)
---

# ADR-0014 — Qualidade de código

## Contexto e Problema
A SPEC-0002 já prevê `.editorconfig`, `dotnet format`, `Nullable`, `TreatWarningsAsErrors` e `AnalysisLevel=latest-recommended`, mas não decide: quais analisadores extras usar, como transformar as regras dos ADRs em verificação automática (tempo via `TimeProvider`, `HttpClient` pela factory, sem `sleep`), como medir cobertura, se haverá mutation testing, como proteger o design system do Stitch e se entra uma plataforma externa de qualidade. Ligar analisadores depois que o código existe custa caro (centenas de avisos de uma vez); decidir agora sai quase de graça.

## Direcionadores da Decisão
- Um dev sozinho: pouca configuração, pouco ruído, nada de painel extra para acompanhar.
- Regras dos ADRs verificadas pela máquina, não só pela revisão do G4.
- App em pt-BR: erros de cultura (datas, números, comparação de texto) precisam ser pegos.
- As regras centrais do produto (fila justa, votos, player, PIN) não podem ter testes "de fachada".
- Licenças permissivas e ferramentas mantidas.
- Nada de Node.js no build (ADR-0003).

## Opções Consideradas
- **A.** Analisadores do SDK + **Meziantou.Analyzer** + **BannedApiAnalyzers**; `dotnet format` no CI e no pre-commit; cobertura **das linhas alteradas** ≥ 80%; **Stryker.NET** só nas regras críticas; teste de tokens do design; sem plataforma externa
- **B.** Analisadores do SDK + StyleCop + Roslynator + SonarCloud, com meta global de cobertura
- **C.** Só os analisadores do SDK e `dotnet format`, sem cobertura nem mutação

## Resultado da Decisão
**Opção escolhida:** "A", porque cobre os erros que importam (async, cultura, APIs proibidas pelos ADRs) com dois pacotes, sem painel externo, e concentra o esforço de qualidade de testes onde o produto se decide.

Regras:
1. **Base (já na SPEC-0002):** `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, gerenciamento central de pacotes.
2. **Meziantou.Analyzer** em todos os projetos, via `Directory.Build.props`. Regras desligadas só no `.editorconfig`, cada uma com um comentário do motivo. Roslynator e StyleCop ficam de fora (sobreposição e ruído).
3. **BannedApiAnalyzers** com `BannedSymbols.txt` na raiz, aplicado a `src/` e `tests/`:
   - `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow` → usar `TimeProvider` (ADR-0002/0012);
   - `Thread.Sleep` e `Task.Delay` em projetos de teste → esperar por condição (ADR-0013);
   - `new HttpClient()` → `IHttpClientFactory`/cliente tipado;
   - `Task.Result`, `Task.Wait()`, `GetAwaiter().GetResult()` fora do `Program`/CLI → `await`;
   - `Console.*` fora de `src/Juker.Host/Cli/**` → `ILogger` (a CLI administrativa é a única exceção, com supressão no nível do arquivo).
4. **Formatação:** `.editorconfig` com as severidades revisadas; `dotnet format --verify-no-changes` no job `lint` do CI e no hook de pre-commit (SPEC-0002 instala os hooks; a ativação é decisão do fundador).
5. **Cobertura:** coverlet (coleta nos jobs `unit` e `integration`) + ReportGenerator (junção em Cobertura) + `diff-cover` com **≥ 80% das linhas alteradas** do PR, em um job `coverage` **bloqueante**. **Sem meta global** de cobertura.
6. **Mutation testing:** Stryker.NET só em `FairQueuePolicy`, `VotePolicy`, `PlaybackStateMachine` e `PinVerifier` (e na classificação de karaokê quando existir), com score mínimo de 80%, num job `mutation` **não bloqueante** (agendado toda noite e disparável à mão). Score abaixo do mínimo abre uma spec `lite` para reforçar os testes.
7. **Design system:** um teste de política falha se aparecer cor literal (`#rgb`, `#rrggbb`, `rgb(`, `hsl(`, valores arbitrários como `bg-[#…]`) ou `font-family` em `.razor`, `.razor.css` e `.css` fora do arquivo de tokens do Tailwind (`src/Web/Juker.Web/Styles/theme.css`). Warnings de Razor também são erros.
8. **Complexidade:** sem métrica automática no MVP; avaliada na revisão do G4. Reavaliar se o time crescer.
9. **Plataformas externas** (SonarCloud, Codacy): fora do MVP. A licença atual dos analisadores da Sonar não foi verificada nesta decisão; se um dia entrarem, isso precisa ser checado.

### Consequências
- **Boa**, porque as regras de tempo, HTTP e espera dos ADRs viram erro de compilação: um agente não consegue violá-las nem por esquecimento.
- **Boa**, porque a cobertura do diff garante teste no código novo sem incentivar teste inútil para bater meta global.
- **Boa**, porque o Stryker prova que os testes da fila justa e do player detectam mudanças de comportamento.
- **Ruim**, porque o Meziantou traz algumas regras discutíveis; mitigado por desligar pontualmente no `.editorconfig`, com motivo.
- **Ruim**, porque o Stryker é lento; por isso roda fora do caminho do PR.

### Confirmação (G3)
- SPEC-0002:IT-03 — um projeto de fixture fora da solução, que importa o `Directory.Build.props`, **falha no build** com `DateTime.Now` (RS0030) e com `string.Equals(a, b)` sem `StringComparison` (Meziantou).
- SPEC-0003:UT-02 — o teste de tokens do design falha com uma cor literal num `.razor` de fixture.
- SPEC-0005:IT-06 — um PR com código novo sem teste falha no job `coverage`.

## Prós e Contras das Opções
| Critério (peso) | A. SDK + Meziantou + Banned + diff + Stryker focado | B. StyleCop + Roslynator + Sonar + meta global | C. Só SDK + format |
|---|---|---|---|
| Pega erros reais (async, cultura, APIs dos ADRs) (5) | 5 | 4 | 2 |
| Ruído e configuração para 1 dev (4) | 4 | 2 | 5 |
| Qualidade dos testes nas regras críticas (4) | 5 | 2 | 1 |
| Sem conta ou painel externo (3) | 5 | 1 | 5 |
| Licenças permissivas (3) | 5 | 3 | 5 |
| **Total ponderado** | **90** | **46** | **62** |

## Mais Informações
- Meziantou.Analyzer 3.0.290 (23/09/2026), licença MIT — https://www.nuget.org/profiles/meziantou (consultado em 2026-09-30).
- Microsoft.CodeAnalysis.BannedApiAnalyzers 5.6.0 (02/07/2026) — https://www.nuget.org/packages/Microsoft.CodeAnalysis.BannedApiAnalyzers (2026-09-30).
- Stryker.NET (`dotnet-stryker`) 5.0.0 (11/09/2026), compatível com .NET 10, licença Apache-2.0 — https://www.nuget.org/packages/dotnet-stryker e https://github.com/stryker-mutator/stryker-net/blob/master/LICENSE (2026-09-30).
- coverlet (MIT), ReportGenerator (Apache-2.0) e diff-cover (Apache-2.0): as versões serão fixadas na SPEC-0005, verificadas no momento da implementação.
- Relacionados: ADR-0002, ADR-0003, ADR-0011, ADR-0012, ADR-0013.
