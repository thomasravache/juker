---
id: ADR-0003
title: Frontend em Blazor Web App com render mode por área, Tailwind v4 e tokens do Stitch
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: SPEC-0007:IT-03 (orçamento de payload do app do cliente) e ARQ-05; revisão G4 para tokens visuais
---

# ADR-0003 — Frontend em Blazor Web App com render mode por área, Tailwind v4 e tokens do Stitch

## Contexto e Problema
Há três superfícies com necessidades diferentes: o **cliente** (celular, 4G ruim do bar, acesso único via QR), a **TV** (ligada a noite toda, sem interação) e o **painel do gerente** (tablet/celular, rede estável). O fundador quer Blazor e design feito no Stitch (HTML + Tailwind). Como renderizar cada superfície e aplicar o design?

## Direcionadores da Decisão
- Primeiro acesso do cliente rápido no 4G (o QR é o momento decisivo do produto).
- TV resistente a deploy/reinício do servidor.
- Painel simples de construir, com acesso direto ao backend.
- Reaproveitar quase 1:1 o HTML/Tailwind gerado pelo Stitch.

## Opções Consideradas
- Blazor Web App único, render mode por área: `/b/*` cliente em **InteractiveWebAssembly com prerender**, `/tv/*` em **InteractiveWebAssembly**, `/admin/*` em **InteractiveServer**
- Tudo em InteractiveServer
- Tudo em InteractiveAuto
- Cliente em SSR estático + JS mínimo (cliente SignalR em JS), TV e admin em Blazor

## Resultado da Decisão
**Opção escolhida:** "render mode por área", porque o prerender entrega a primeira pintura na hora e o runtime WASM (trimmed + brotli) baixa em segundo plano e fica em cache via service worker; a TV em WASM sobrevive a reinícios (o circuito do modo Server cai e perde o estado); o admin em Server é o mais simples. **Plano B registrado:** se o orçamento de payload do cliente estourar (SPEC-0007:IT-03), o cliente passa a SSR estático + JS mínimo, com uma emenda no ADR.

Design: Tailwind CSS v4 via **CLI standalone** (sem Node), integrado ao build do `Juker.Web`. Tokens (cores, tipografia Oswald/Inter, raios, espaçamentos) copiados do `designMd` do projeto Stitch "Juker Rock Bar Design System" (8904741919458823023) para o `@theme` do Tailwind. PWA: manifest + service worker para o cliente (cache do runtime, sem pedir instalação nem push) e para o admin (instalável).

### Consequências
- **Boa**, porque um único projeto web, componentes compartilhados entre cliente e admin, e HTML do Stitch portável.
- **Ruim**, porque os componentes do cliente precisam funcionar em prerender e em WASM (acesso a dados via `HttpClient` tipado, não via serviço de servidor).
- **Ruim**, porque o primeiro download do WASM existe; mitigado pelo orçamento e pelo plano B.

### Confirmação (G3)
SPEC-0007:IT-03 falha se o payload compactado de `/b/{slug}` passar do orçamento (≤ 3,0 MB brotli no primeiro acesso, medido no CI). ARQ-05 impede que o Web referencie projetos internos de módulos. No G4, a revisão confere que não há cores ou fontes fora dos tokens.

## Prós e Contras das Opções
| Critério (peso) | Por área | Tudo Server | Tudo Auto | Cliente SSR+JS |
|---|---|---|---|---|
| 1º acesso no 4G do cliente (5) | 4 | 3 | 4 | 5 |
| Robustez em rede instável (5) | 5 | 1 | 3 | 5 |
| TV sobrevive a deploy (4) | 5 | 1 | 3 | 5 |
| Produtividade (1 stack, Blazor) (4) | 4 | 5 | 3 | 2 |
| Complexidade (3) | 3 | 5 | 2 | 3 |
| **Total ponderado** | **89** | **63** | **65** | **83** |

## Mais Informações
- Tailwind v4 standalone em Blazor — https://dev.to/cristiansifuentes/tailwind-css-v4-standalone-in-blazor-webassembly-a-clean-native-integration-for-the-net-26lk e https://blazorise.com/blog/blazor-and-tailwind-quick-setup-without-npm (2026-09-30).
- ASP.NET Core Blazor render modes (.NET 10) — documentação oficial da Microsoft Learn; o orçamento exato de payload do .NET 10 não foi medido nesta decisão: por isso a medição virou teste (SPEC-0007:IT-03).
- Projeto Stitch: "Juker Rock Bar Design System", id 8904741919458823023 (telas revisadas em 2026-09-30).
