# The Lineup

Unity detective deduction game plus a local case-content generation pipeline.

## Repository Layout

- `Assets/` - Unity content, scripts, scenes, and ScriptableObject data.
- `Packages/` - Unity package manifest and lockfile.
- `ProjectSettings/` - Unity project configuration.
- `case_workflow/` - case generation, validation, rendering, and import tooling.
- `Docs/` - project-level operational docs.

## Core Gameplay Runtime

- Main case flow controller: `Assets/Scripts/Cases/CaseScreenController.cs`
- Runtime case data models:
  - `Assets/Scripts/Cases/CaseDefinitionSO.cs`
  - `Assets/Scripts/Cases/SuspectProfileSO.cs`
  - `Assets/Scripts/Cases/EvidenceProfileSO.cs`
- Unity generated-case importer: `Assets/Editor/GeneratedCaseImporter.cs`

## Case Workflow Entry Points

From repo root:

```bash
python case_workflow/validate_case_schema.py
python case_workflow/pipeline_health_check.py --strict
python case_workflow/render_case.py --strict-validation
python case_workflow/sync_curated_cases.py
```

Detailed process docs:

- `case_workflow/docs/PROCESS.md`
- `case_workflow/docs/CASE_SCHEMA.md`

## Git Notes

- This repo is configured to ignore Unity-generated folders (`Library`, `Temp`, `Logs`, etc.).
- Local model files and render outputs are ignored by default.
- Unity text assets (`.unity`, `.prefab`, `.asset`, `.meta`) are marked as text in `.gitattributes` to keep merges sane.
