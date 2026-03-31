# Case Workflow

This folder contains the end-to-end content pipeline for The Lineup.

## Layout

- `cases/` - authored case JSON files.
- `styles/` - visual style JSON presets.
- `outputs/` - generated renders and manifests.
- `src/case_workflow/` - reusable Python modules.
- `tests/` - smoke and utility tests.
- `docs/` - schema, universe, process, and operations docs.
- `artifacts/` - scratch and historical reference outputs.

## Main Commands

From project root:

```bash
python case_workflow/validate_case_schema.py
python case_workflow/pipeline_health_check.py --strict
python case_workflow/render_case.py --strict-validation
python case_workflow/sync_curated_cases.py
```

## Backward Compatibility

Root-level modules `paths.py`, `prompt_builder.py`, and `case_validation.py`
remain as compatibility shims that forward to `src/case_workflow/`.
