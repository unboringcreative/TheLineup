from __future__ import annotations

import argparse
import sys
from pathlib import Path

from case_validation import ValidationResult, load_case_json, validate_case_data
from paths import CASES_DIR


def resolve_case_paths(input_path: str | None) -> list[Path]:
    if not input_path:
        return sorted(CASES_DIR.glob("*.json"))

    path = Path(input_path)
    if path.is_dir():
        return sorted(path.glob("*.json"))

    return [path]


def print_result(path: Path, result: ValidationResult) -> None:
    status = "OK" if result.ok else "FAIL"
    print(f"[{status}] {path}")
    for error in result.errors:
        print(f"  ERROR: {error}")
    for warning in result.warnings:
        print(f"  WARN:  {warning}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate The Lineup case JSON schema and quality rules.")
    parser.add_argument("path", nargs="?", help="Case JSON file or directory. Defaults to case_workflow/cases.")
    parser.add_argument("--strict", action="store_true", help="Promote richer content requirements and stricter prompt checks.")
    args = parser.parse_args()

    case_paths = resolve_case_paths(args.path)
    if not case_paths:
        print("No case JSON files found.")
        return 1

    any_errors = False
    for case_path in case_paths:
        try:
            case_data = load_case_json(case_path)
        except Exception as exc:
            any_errors = True
            print(f"[FAIL] {case_path}")
            print(f"  ERROR: Could not read JSON: {exc}")
            continue

        result = validate_case_data(case_data, strict_quality=args.strict)
        print_result(case_path, result)
        any_errors = any_errors or not result.ok

    return 1 if any_errors else 0


if __name__ == "__main__":
    sys.exit(main())
