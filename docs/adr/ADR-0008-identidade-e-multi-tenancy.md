---
id: ADR-0008
title: Identidade — gerente com ASP.NET Core Identity (cookie), cliente com token de sessão por comanda; multi-tenancy por VenueId
status: accepted
origin: decision
date: 2026-09-30
decision_makers: [thomas]
consulted: [claude]
informed: []
supersedes:
superseded_by:
enforced_by: SPEC-0009:IT-01..IT-04 (autorização por política) e SPEC-0009:IT-05 (isolamento entre bares)
pillars: [seguranca-acesso]
---

# ADR-0008 — Identidade e multi-tenancy

## Contexto e Problema
Há dois tipos de ator: o **cliente do bar**, que não pode ter cadastro (lê o QR, digita a comanda e o PIN), e a **equipe do bar** (gerente), que precisa de login. Vários bares compartilham a mesma instância. Como autenticar e isolar?

## Direcionadores da Decisão
- Cliente sem cadastro, sem e-mail, sem senha.
- A TV precisa se identificar como dispositivo do bar sem ninguém digitar senha nela toda noite.
- Isolamento absoluto entre bares.
- Menos infraestrutura: nada de IdP externo no MVP.

## Opções Consideradas
- ASP.NET Core Identity (cookie) para staff + token de sessão assinado para o cliente + token de dispositivo para a TV, com `VenueId` em todas as tabelas e filtro global
- Keycloak/IdP externo para staff + token para o cliente
- Clientes anônimos por cookie sem PIN

## Resultado da Decisão
**Opção escolhida:** "Identity + tokens próprios", porque cobre os três atores sem serviço extra.
- **Staff:** ASP.NET Core Identity com cookie HttpOnly/Secure/SameSite=Lax, papel `Manager` com claim `venue_id`. Criação de bar e do primeiro gerente por comando administrativo (`juker admin create-venue`), sem autocadastro no MVP.
- **Cliente:** `POST /api/v1/b/{slug}/sessions` valida o PIN da TV e a comanda e emite um cookie de sessão assinado (Data Protection) com `venue_id`, `comanda`, `device_id` e `night` (data da noite de negócio). Expira na virada da noite.
- **TV:** pareamento por código de uso único gerado no painel (`/tv/pair`). Emite um token de dispositivo de longa duração, revogável no painel.
- **Tenancy:** `VenueId` obrigatório em toda entidade de módulo. Filtro global do EF resolvido do contexto autenticado (`ITenantContext`), nunca de parâmetro de URL sem validação.

### Consequências
- **Boa**, porque zero IdP para operar e o cliente entra em segundos.
- **Ruim**, porque sem SSO nem MFA para staff no MVP. Pendência para a fase SaaS.

### Confirmação (G3)
SPEC-0009:IT-01..IT-04 (cada política: anônimo, cliente, TV, gerente) e SPEC-0009:IT-05 (gerente do bar A não lê nem altera dados do bar B).

## Prós e Contras das Opções
| Critério (peso) | Identity + tokens | Keycloak | Anônimo sem PIN |
|---|---|---|---|
| Operação simples (5) | 5 | 2 | 5 |
| Segurança para o caso de uso (5) | 4 | 5 | 1 |
| Atrito para o cliente (4) | 4 | 4 | 5 |
| **Total ponderado** | **61** | **51** | **50** |

## Mais Informações
- ASP.NET Core Identity e Data Protection (.NET 10) — documentação oficial Microsoft Learn (consultado em 2026-09-30):
  https://learn.microsoft.com/aspnet/core/security/authentication/identity e
  https://learn.microsoft.com/aspnet/core/security/data-protection/introduction
