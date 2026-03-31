# The Lineup Process Guide

This document describes how game case content is authored, rendered, validated, and synced into Unity.

## 1) Game Context

The Lineup is a deduction game where players:

1. Review one featured case scene.
2. Interview five suspects.
3. Inspect three evidence items.
4. Choose the guilty suspect.
5. Read verdict + explanation.

Core gameplay runtime is Unity-side (`Assets/Scripts/Cases`).

## 2) Pipeline Architecture

Case pipeline source-of-truth is JSON authored in `case_workflow/cases`.

Flow:

1. **Author case JSON** (`cases/*.json`)
2. **Validate schema/quality** (`validate_case_schema.py`)
3. **Build prompt queue** (`src/case_workflow/prompt_builder.py`)
4. **Render assets** (`render_case.py`)
5. **Emit import bundle** (`Assets/GeneratedCases/to_import/<CASE_ID>`)
6. **Import/sync into Unity assets** (`sync_curated_cases.py` or Unity importer)
7. **Run in-game QA** in Unity scene

## 3) Folder Structure

- `case_workflow/cases/`
  - Canon case JSON files.
- `case_workflow/styles/`
  - Style presets for image generation.
- `case_workflow/outputs/`
  - Rendered PNGs and manifests (`case.json`, `style.json`, `prompt_manifest.json`, `render_manifest.json`).
- `case_workflow/src/case_workflow/`
  - `paths.py` - env-based directory/model resolution.
  - `case_validation.py` - schema + quality validation engine.
  - `prompt_builder.py` - prompt assembly and render queue generation.
- `case_workflow/tests/`
  - Pipeline smoke tests and local model tests.
- `case_workflow/docs/`
  - `CASE_SCHEMA.md`, `UNIVERSE_RULES.md`, `PROCESS.md`, generator docs.
- `case_workflow/artifacts/`
  - Non-production scratch/reference files.

## 4) Key Scripts (Entry Points)

- `case_workflow/validate_case_schema.py`
  - Validates one case or all cases. Use `--strict` for quality-enforced checks.
- `case_workflow/pipeline_health_check.py`
  - Non-render dry run for paths, model existence, case validity, and queue build sanity.
- `case_workflow/render_case.py`
  - Renders suspect/evidence/featured images and writes import bundle.
  - CPU offload is always enabled.
- `case_workflow/sync_curated_cases.py`
  - Writes curated case ScriptableObject assets and trims `CaseLibrary_Main.asset` to keep-case IDs.
- `case_workflow/gradio_flux2_studio.py`
  - Manual local generation UI for prompt iteration.

## 5) Unity Runtime + Import Touchpoints

- Importer: `Assets/Editor/GeneratedCaseImporter.cs`
  - Reads generated case JSON + image stems and maps data to ScriptableObject assets.
- Runtime data:
  - `Assets/Scripts/Cases/CaseDefinitionSO.cs`
  - `Assets/Scripts/Cases/SuspectProfileSO.cs`
  - `Assets/Scripts/Cases/EvidenceProfileSO.cs`
- Runtime UI/controller:
  - `Assets/Scripts/Cases/CaseScreenController.cs`

## 6) Standard Workflow

### A. Create/Update Case

1. Edit or add JSON in `case_workflow/cases`.
2. Validate:

```bash
python case_workflow/validate_case_schema.py --strict
```

3. Run pipeline health check:

```bash
python case_workflow/pipeline_health_check.py --strict
```

### B. Generate Assets

```bash
python case_workflow/render_case.py --strict-validation
```

Output lands in:

- `case_workflow/outputs/<CASE_ID>/<STYLE_ID>`
- `Assets/GeneratedCases/to_import/<CASE_ID>`

### C. Sync to Unity

```bash
python case_workflow/sync_curated_cases.py
```

Then in Unity:

1. Let asset import finish.
2. Open case scene.
3. Verify suspect/evidence/verdict flow.

## 7) Data Contracts to Protect

Do not remove these fields without updating both pipeline and importer:

- Case: `caseId`, `caseTitle`, `caseDescription`, `location`, `featuredImageStem`, `featuredImagePromptBase`, `verdictTitle`, `explanation`, `guiltySlot`.
- Suspect: `slot`, `displayName`, `occupation`, `nationality`, `height`, `weight`, `keyPersonalityTrait`, `dialogue`, `portraitStem`, `portraitPromptBase`.
- Evidence: `slot`, `title`, `description`, `discoveryLocation`, `imageStem`, `imagePromptBase`.

`visualIdentity` is required for strict-quality content.

## 8) Troubleshooting

- **Validation fails**
  - Run `python case_workflow/validate_case_schema.py` and resolve field errors/warnings in the named JSON.
- **Render lock exists**
  - Remove stale `.render.lock` under `case_workflow/outputs/...` only if no active render is running.
- **Missing PNG in import**
  - Confirm stems map to `*_0001.png` naming in generated output and `Assets/GeneratedCases/to_import/<CASE_ID>`.
- **Unity case not updating**
  - Re-run sync, then refresh Unity and inspect `CaseLibrary_Main.asset` entries.

## 9) Change Management Rules

When changing schema or folder layout:

1. Update `docs/CASE_SCHEMA.md`.
2. Update `src/case_workflow/case_validation.py`.
3. Update prompt build or import scripts as needed.
4. Run validation and health checks.
5. Smoke test one full case in Unity.

## 10) Compatibility Notes

- Root-level `paths.py`, `prompt_builder.py`, and `case_validation.py` are compatibility shims to keep older commands/imports working.
- If scripts or external tooling hardcode old doc paths, use the moved docs under `case_workflow/docs`.
