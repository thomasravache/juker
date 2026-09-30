namespace Juker.ArchitectureTests.Rules;

/// <summary>Uma violação de regra de arquitetura: a regra (ARQ-NN), quem viola (tipo ou assembly) e o motivo.</summary>
/// <param name="Rule">Identificador da regra, por exemplo <c>ARQ-01</c>.</param>
/// <param name="Offender">Nome completo do tipo (ou do assembly) que viola a regra.</param>
/// <param name="Reason">Descrição da dependência proibida, citando o alvo.</param>
public sealed record RuleViolation(string Rule, string Offender, string Reason);
