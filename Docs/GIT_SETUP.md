# Git Setup Checklist

Use this checklist before first push.

## 1) Verify ignored files

```bash
git status --short
```

Expected: Unity generated folders (`Library/`, `Temp/`, `Logs/`, etc.) and local model/output folders should not appear.

## 2) Stage source-only project files

Stage only:

- `Assets/`
- `Packages/`
- `ProjectSettings/`
- `case_workflow/` (excluding ignored artifacts)
- `Docs/`
- `.gitignore`, `.gitattributes`, `.editorconfig`, `README.md`

## 3) Optional large file strategy

If you intentionally commit large binary assets (concept art, audio masters), use Git LFS before adding them.

## 4) Pre-commit sanity

```bash
python case_workflow/validate_case_schema.py
python case_workflow/test_prompt_build.py
```

## 5) First commit recommendation

Use a foundational message, for example:

`Initialize The Lineup Unity project and case workflow pipeline structure`
