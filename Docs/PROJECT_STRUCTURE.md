# Project Structure (Studio-Style)

This repository is organized into stable, domain-oriented top-level areas:

- `Assets/` - all Unity-authoritative game content.
- `Packages/` - package definitions only.
- `ProjectSettings/` - environment-agnostic Unity settings.
- `case_workflow/` - external content pipeline tooling.
- `Docs/` - repo-level docs and conventions.

## Unity Asset Conventions

- Keep runtime code under `Assets/Scripts/` with feature folders (`Cases/`, etc.).
- Keep editor-only tooling under `Assets/Editor/`.
- Keep generated case import staging under `Assets/GeneratedCases/to_import/` and do not treat it as source-of-truth.
- Treat `Assets/Scripts/Cases/CASE_*` folders as curated game data assets.

## Pipeline Conventions

- Author cases in `case_workflow/cases/`.
- Use `case_workflow/src/case_workflow/` for reusable Python modules.
- Keep user-facing scripts at `case_workflow/*.py` entry points.
- Keep historical output experiments in `case_workflow/artifacts/` (ignored by default).

## Scaling Guidance

- New gameplay systems: add feature folder under `Assets/Scripts/`.
- New pipeline domain modules: add package module under `case_workflow/src/case_workflow/` and keep a thin entry script.
- Avoid hardcoding absolute local paths; use env vars from `case_workflow/src/case_workflow/paths.py`.
