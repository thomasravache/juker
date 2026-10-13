---
id: ADR-0004
title: PostgreSQL 18 com EF Core 10, schema e migrations por módulo, busca com pg_trgm
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: ARQ-03 (DbContext por módulo) e testes de integração com Testcontainers (SPEC-0003)
pillars: [dados-migracoes]
---

# ADR-0004 — PostgreSQL 18 com EF Core 10, schema e migrations por módulo, busca com pg_trgm

## Contexto e Problema
Precisamos de um banco para bares (tenants), sessões, catálogo, fila, votos e histórico, com busca textual tolerante a erro de digitação e acentos no catálogo. Consistência forte importa na fila (limite por comanda, votos únicos).

## Direcionadores da Decisão
- Consistência transacional (limite de 3 por comanda, 1 voto por comanda por música).
- Busca tolerante a erro de digitação sem subir Elasticsearch.
- Custo baixo e roda no mesmo VPS no piloto.
- Isolamento por módulo (ADR-0001).

## Opções Consideradas
- PostgreSQL 18 + EF Core 10 (Npgsql) + `pg_trgm` + `unaccent`
- SQL Server 2025 + EF Core
- PostgreSQL + Elasticsearch/OpenSearch para busca

## Resultado da Decisão
**Opção escolhida:** "PostgreSQL 18 + EF Core 10 + pg_trgm/unaccent", porque tem transações, índices trigram para busca fuzzy, é gratuito e leve. Um **schema por módulo** (`venues`, `sessions`, `catalog`, `queue`, `playback`), um `DbContext` e um histórico de migrations por módulo (`__EFMigrationsHistory` no schema do módulo). As migrations são aplicadas **pelo pipeline**, com um bundle de migrations, antes da nova versão subir (SPEC-0006), sempre compatíveis com a versão anterior (expand → migrate → contract). Multi-tenancy por coluna `VenueId` com filtro global de query (ADR-0008). Cache: `HybridCache` do .NET em memória (L1) no MVP; Valkey só quando houver mais de uma instância (ver ADR-0005).

### Consequências
- **Boa**, porque um único banco resolve dados e busca no piloto.
- **Ruim**, porque o Postgres no mesmo VPS exige backup próprio (pg_dump agendado para um storage externo, SPEC-0006); migrar para um gerenciado depois é direto.

### Confirmação (G3)
ARQ-03 no ArchUnitNET. IT com Testcontainers (Postgres 18) em cada spec que persiste, incluindo as extensões `pg_trgm`/`unaccent` criadas pelas migrations.

## Prós e Contras das Opções
| Critério (peso) | Postgres + pg_trgm | SQL Server | Postgres + Elastic |
|---|---|---|---|
| Custo de licença/infra (5) | 5 | 2 | 3 |
| Busca fuzzy sem infra extra (4) | 4 | 2 | 5 |
| Operação simples no VPS (4) | 5 | 3 | 2 |
| Consistência transacional (4) | 5 | 5 | 5 |
| Familiaridade .NET/EF (3) | 4 | 5 | 4 |
| **Total ponderado** | **93** | **67** | **75** |

## Mais Informações
- PostgreSQL 18: versão menor mais recente 18.6 (11/08/2026), fim de suporte em nov/2030 — https://endoflife.date/postgresql e https://www.postgresql.org/about/news/ (consultado em 2026-09-30).
- EF Core 10 acompanha o .NET 10 LTS (ADR-0002). A versão do provedor Npgsql compatível será fixada em `Directory.Packages.props` na SPEC-0002.
