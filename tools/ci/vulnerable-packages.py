#!/usr/bin/env python3
"""Lê a saída JSON de `dotnet list package --vulnerable --include-transitive --format json` (job security do ci.yml).

Falha (código 1) quando algum pacote, direto ou transitivo, tem vulnerabilidade High ou Critical (ADR-0017); Low e
Moderate só aparecem no relatório. Um "problem" do NuGet (projeto sem restore, fonte inacessível) também falha, porque
a varredura não pôde ser feita. Com GITHUB_STEP_SUMMARY definido, escreve a tabela no resumo do job.

Uso: python3 -I tools/ci/vulnerable-packages.py <arquivo.json>
"""

import json
import os
import sys

BLOCKING = {"high", "critical"}


def main(path: str) -> int:
    with open(path, encoding="utf-8") as handle:
        report = json.load(handle)

    rows = []
    problems = [problem.get("text", str(problem)) for problem in report.get("problems", [])]
    for project in report.get("projects", []):
        name = os.path.basename(project.get("path", "?"))
        problems += [f"{name}: {problem.get('text', problem)}" for problem in project.get("problems", [])]
        for framework in project.get("frameworks", []):
            for kind in ("topLevelPackages", "transitivePackages"):
                for package in framework.get(kind, []):
                    for vulnerability in package.get("vulnerabilities", []):
                        rows.append((
                            vulnerability.get("severity", "?"),
                            package.get("id", "?"),
                            package.get("resolvedVersion", "?"),
                            "direto" if kind == "topLevelPackages" else "transitivo",
                            name,
                            vulnerability.get("advisoryurl", ""),
                        ))

    unique = sorted(set(rows), key=lambda row: (row[0].lower() not in BLOCKING, row[1], row[4]))
    blocking = [row for row in unique if row[0].lower() in BLOCKING]

    lines = ["### Pacotes vulneráveis (`dotnet list package --vulnerable --include-transitive`)", ""]
    if unique:
        lines += ["| Severidade | Pacote | Versão | Referência | Projeto | Aviso |", "|---|---|---|---|---|---|"]
        lines += [f"| {' | '.join(row)} |" for row in unique]
    else:
        lines.append("Nenhum pacote vulnerável conhecido.")
    if problems:
        lines += ["", "Problemas da varredura:"] + [f"- {problem}" for problem in problems]
    text = "\n".join(lines) + "\n"

    print(text)
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as handle:
            handle.write(text)

    for row in blocking:
        print(f"::error title=Pacote vulnerável ({row[0]})::{row[1]} {row[2]} ({row[3]}, {row[4]}): {row[5]}")
    if problems:
        print("::error title=Varredura de pacotes incompleta::" + "; ".join(problems))
    if blocking or problems:
        print(f"{len(blocking)} vulnerabilidade(s) High/Critical; {len(problems)} problema(s) na varredura.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("uso: vulnerable-packages.py <arquivo.json>", file=sys.stderr)
        sys.exit(2)
    sys.exit(main(sys.argv[1]))
