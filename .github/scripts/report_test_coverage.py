"""Merge Coverlet JSON reports and report handwritten/generated SDK coverage separately."""

import argparse
import json
from pathlib import Path


MODULES = {
    "JKToolKit.CodexSDK.dll",
    "JKToolKit.CodexSDK.AgentFramework.dll",
    "JKToolKit.CodexSDK.SemanticKernel.dll",
}


def source_name(value):
    value = value.replace("\\", "/")
    marker = "/src/"
    if marker in value:
        return "src/" + value.split(marker, 1)[1]
    if value.startswith("src/"):
        return value
    raise ValueError(f"Coverage source is outside src: {value}")


def summarize(reports, require_all_modules=False):
    files = {}
    for report in reports:
        data = json.loads(Path(report).read_text(encoding="utf-8-sig"))
        for module, documents in data.items():
            module = module.replace("\\", "/").rsplit("/", 1)[-1]
            if module not in MODULES:
                continue
            for source, classes in documents.items():
                name = source_name(source)
                entry = files.setdefault(name, {"module": module, "lines": {}, "branches": {}})
                for class_name, methods in classes.items():
                    for method_name, method in methods.items():
                        for line, hits in method["Lines"].items():
                            line = int(line)
                            entry["lines"][line] = entry["lines"].get(line, False) or hits > 0
                        for branch in method["Branches"]:
                            # A line can contain several conditions; offsets and paths retain each arm.
                            identity = (class_name, method_name, branch["Offset"], branch["EndOffset"],
                                        branch["Path"], branch["Ordinal"])
                            prior = entry["branches"].get(identity, (branch["Line"], False))
                            entry["branches"][identity] = (branch["Line"], prior[1] or branch["Hits"] > 0)

    if not files:
        raise ValueError("No SDK coverage found; refusing to report an empty run as covered.")

    rows = []
    for name, entry in sorted(files.items()):
        generated = "/Generated/" in name or "/obj/" in name or name.endswith(".g.cs")
        rows.append({
            "file": name, "module": entry["module"], "generated": generated,
            "lines_covered": sum(entry["lines"].values()), "lines_total": len(entry["lines"]),
            "branches_covered": sum(hit for _, hit in entry["branches"].values()),
            "branches_total": len(entry["branches"]),
            "uncovered_lines": sorted(n for n, hit in entry["lines"].items() if not hit),
            "uncovered_branch_lines": sorted({n for n, hit in entry["branches"].values() if not hit}),
        })
    measured_modules = {r["module"] for r in rows if not r["generated"] and r["lines_total"] > 0}
    if require_all_modules and (missing := MODULES - measured_modules):
        raise ValueError(f"Coverage is missing SDK assemblies with handwritten lines: {', '.join(sorted(missing))}")
    return {"reports": [str(Path(p)) for p in reports], "handwritten": totals(r for r in rows if not r["generated"]),
            "generated": totals(r for r in rows if r["generated"]),
            "modules": {module: totals(r for r in rows if r["module"] == module and not r["generated"])
                        for module in sorted({r["module"] for r in rows})}, "files": rows}


def totals(rows):
    result = dict.fromkeys(("lines_covered", "lines_total", "branches_covered", "branches_total"), 0)
    for row in rows:
        for name in result:
            result[name] += row[name]
    for kind in ("lines", "branches"):
        denominator = result[f"{kind}_total"]
        result[f"{kind}_percent"] = 100 * result[f"{kind}_covered"] / denominator if denominator else None
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reports", nargs="+", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--minimum-line", type=float, default=98)
    parser.add_argument("--minimum-branch", type=float, default=95)
    args = parser.parse_args()
    if not (0 <= args.minimum_line <= 100 and 0 <= args.minimum_branch <= 100):
        parser.error("Coverage thresholds must be percentages between 0 and 100.")
    result = summarize(args.reports, require_all_modules=True)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    for name in ("handwritten", "generated"):
        row = result[name]
        line = row["lines_percent"]
        branch = row["branches_percent"]
        line_text = f"{line:.2f}%" if line is not None else "n/a"
        branch_text = f"{branch:.2f}%" if branch is not None else "n/a"
        print(f"{name}: lines {row['lines_covered']}/{row['lines_total']} ({line_text}); "
              f"branches {row['branches_covered']}/{row['branches_total']} ({branch_text})")
    handwritten = result["handwritten"]
    return 0 if (handwritten["lines_percent"] is not None and handwritten["branches_percent"] is not None
                 and handwritten["lines_percent"] >= args.minimum_line
                 and handwritten["branches_percent"] >= args.minimum_branch) else 1


if __name__ == "__main__":
    raise SystemExit(main())
