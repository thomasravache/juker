---
id: ADR-0012
title: Estratégia de testes de integração — Postgres real, banco por classe a partir de modelo migrado, esquemas de auth reais e outbox determinístico
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: SPEC-0003:IT-04, SPEC-0003:IT-05, ARQ-08 (SPEC-0003:IT-02) e SPEC-0004:IT-05
---

# ADR-0012 — Estratégia de testes de integração

## Contexto e Problema
Quase toda spec do Juker tem testes de integração (IT). As regras mais importantes do produto dependem da infraestrutura real: busca com `pg_trgm`, lock por bar na fila, filtro de tenant entre bares, autorização por esquema, entrega de eventos pelo outbox e o hub SignalR. O ADR-0011 escolhe as ferramentas, mas não diz **como** os IT isolam dados, sobem o banco, autenticam, lidam com tempo e com eventos assíncronos. Sem isso, cada agente faria de um jeito, e os testes ficariam lentos ou instáveis.

## Direcionadores da Decisão
- Comportamento idêntico ao de produção nas partes críticas (Postgres, autorização, outbox).
- Determinismo: nenhum teste depende de timer, ordem de execução ou dados de outro teste.
- Velocidade: suíte de IT abaixo de 5 min no CI, com as classes em paralelo.
- Uma única forma de escrever IT em todos os módulos.

## Opções Consideradas
- **A.** Testcontainers com um container Postgres por execução + um banco por classe criado de um **banco-modelo migrado** (`CREATE DATABASE … TEMPLATE`) + WebApplicationFactory com os esquemas de auth reais + outbox drenado sob comando
- **B.** Um banco compartilhado limpo entre testes com Respawn + handler de autenticação de teste que libera tudo + dispatcher do outbox ligado com esperas
- **C.** EF Core InMemory/SQLite para a maioria dos IT e Postgres só para alguns

## Resultado da Decisão
**Opção escolhida:** "A", porque isola cada classe em cerca de 100 ms sem limpeza, exercita a autorização e o isolamento entre bares de verdade e elimina as esperas por timer.

Regras:
1. **Banco:** Postgres 18 real via Testcontainers, **um container por execução** (fixture de assembly). EF Core InMemory e SQLite são proibidos em projetos de teste (**ARQ-08**).
2. **Isolamento:** na subida, a fixture aplica **as migrations reais de todos os módulos** num banco `juker_template`. Cada classe de teste recebe um banco próprio clonado do modelo. Classes rodam em paralelo; testes da mesma classe, em sequência. Cada teste cria os próprios dados por builders, com um bar (`VenueId`/slug) único.
3. **Migrations:** sempre as migrations reais (nunca `EnsureCreated`). Um teste falha se algum `DbContext` tiver mudanças no modelo sem migration (`HasPendingModelChanges`).
4. **Porta de entrada:** por padrão, **caixa-preta via HTTP** (`JukerApiFactory` → endpoint → handler → banco). Contratos entre módulos (`IQueueModule`, `ISessionsModule` etc.) são exercitados pelo DI da própria factory.
5. **Autenticação:** os esquemas reais `Staff`, `Guest` e `Tv` (SPEC-0009) com emissores de teste que produzem credenciais válidas. Não existe handler de teste que ignore a autorização.
6. **Outbox:** nos IT, o dispatcher em segundo plano fica **desligado** (`Outbox:DispatcherEnabled=false`). O teste chama `DrainOutboxAsync()` para entregar tudo o que está pendente, de forma síncrona. Só os IT do próprio outbox (SPEC-0004) ligam o dispatcher.
7. **Tempo:** `FakeTimeProvider` injetado na factory e exposto ao teste. Nada de `DateTime.Now` no código de produção (verificado no G4).
8. **HTTP externo:** somente WireMock.Net com **respostas gravadas** da API real (`Recordings/*.json`). Mock de `HttpClient`/`HttpMessageHandler` é proibido. Um script `tools/recordings/refresh-youtube.sh` regrava as respostas com uma chave real quando a API mudar.
9. **Tempo real:** cliente SignalR real contra o TestServer (WebSockets), autenticado pelos emissores de teste.
10. **Orçamento:** suíte de IT abaixo de 5 min no CI. A reutilização de containers do Testcontainers é opcional e só na máquina local (desligada no CI).

A fronteira com os outros níveis: **IT = um processo, infraestrutura real, sem navegador.** Jornadas com navegador são E2E (ADR-0013).

### Consequências
- **Boa**, porque os bugs que importam (tenant vazando, limite furado sob concorrência, evento perdido, migration esquecida) aparecem no IT e não em produção.
- **Boa**, porque o banco-modelo evita migrar o banco a cada classe, e o `DrainOutboxAsync` elimina a espera por timer.
- **Ruim**, porque os IT exigem Docker na máquina e no CI; aceitável (já exigido pelo AppHost).
- **Ruim**, porque as respostas gravadas do YouTube podem ficar desatualizadas; mitigado pelo script de regravação e pelo teste manual com a chave real (SPEC-0014, 7.6).

### Confirmação (G3)
- ARQ-08 (ArchUnitNET/inspeção de referências): nenhum projeto de teste referencia `Microsoft.EntityFrameworkCore.InMemory`, `Microsoft.EntityFrameworkCore.Sqlite` ou `Moq`/`NSubstitute` sobre `HttpMessageHandler` — SPEC-0003:IT-02.
- SPEC-0003:IT-04 (banco por classe a partir do modelo) e SPEC-0003:IT-05 (migrations pendentes detectadas).
- SPEC-0004:IT-05 (`DrainOutboxAsync` com o dispatcher desligado).

## Prós e Contras das Opções
| Critério (peso) | A. Modelo + auth real + drain | B. Respawn + auth fake + esperas | C. InMemory/SQLite |
|---|---|---|---|
| Fidelidade a produção (5) | 5 | 3 | 1 |
| Determinismo (5) | 5 | 2 | 4 |
| Velocidade (4) | 4 | 3 | 5 |
| Paralelismo (3) | 5 | 1 | 5 |
| Simplicidade de escrever (3) | 4 | 4 | 4 |
| **Total ponderado** | **89** | **53** | **72** |

## Mais Informações
- PostgreSQL — `CREATE DATABASE … TEMPLATE` (documentação oficial, "Template Databases") — https://www.postgresql.org/docs/current/manage-ag-templatedbs.html (consultado em 2026-09-30).
- Testcontainers for .NET 4.15.0 — https://www.nuget.org/packages/Testcontainers/ (2026-09-30).
- EF Core — `HasPendingModelChanges` (disponível desde o EF Core 8) — documentação oficial Microsoft Learn (2026-09-30).
- Relacionados: ADR-0004 (Postgres e migrations), ADR-0006 (outbox), ADR-0008 (esquemas de auth), ADR-0011 (ferramentas), ADR-0013 (E2E).
