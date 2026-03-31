# The Lineup Case Schema

This is the canonical JSON contract for generated cases.

## Source Of Truth

- Author-editable case JSON lives in `case_workflow/cases`.
- Render outputs live in `case_workflow/outputs/<CASE_ID>/<STYLE_ID>`.
- Unity-ready import bundles live in `Assets/GeneratedCases/to_import/<CASE_ID>`.
- `Assets/GeneratedCases/to_import` is generated output, not the authoring source.
- The Unity importer should read from the generated bundle and never assume a legacy `incoming` folder.

## Required Top-Level Fields

```json
{
  "caseId": "CASE_011",
  "caseTitle": "The Sable Cartridge",
  "caseDescription": "...",
  "location": {
    "addressOrBusiness": "Municipal Bond Archive Annex",
    "city": "Montreal",
    "country": "Canada"
  },
  "featuredImageStem": "featured_00001_",
  "featuredImagePromptBase": "...",
  "instruction": "...",
  "verdictTitle": "Case Conclusion",
  "explanation": "...",
  "guiltySlot": 3,
  "suspects": ["exactly five entries"],
  "evidence": ["exactly three entries"]
}
```

## Suspects

Each case contains exactly 5 suspects with unique `slot` values `1..5`.

Required render/import fields:

- `slot`
- `displayName`
- `occupation`
- `nationality`
- `height`
- `weight`
- `keyPersonalityTrait`
- `dialogue`
- `portraitStem`
- `portraitPromptBase`

Required for new high-quality canon cases:

- `profile`
- `motive`
- `notes`
- `visualIdentity`

Recommended:

- `sex`

`visualIdentity` should define the visual hooks that make the lineup readable at a glance:

- `ageRead`
- `build`
- `silhouette`
- `wardrobe`
- `hair`
- `accessorySignature`
- `props`
- `paletteAccent`
- `poseEnergy`

Example:

```json
"visualIdentity": {
  "ageRead": "late 40s",
  "build": "lean, narrow frame",
  "silhouette": "long belted raincoat with sharp shoulders",
  "wardrobe": "pinstripe overcoat, dark gloves, pressed slacks",
  "hair": "slicked-back black hair with silver at the temples",
  "accessorySignature": "brass tie pin shaped like a rail switch",
  "props": "folded umbrella and leather folio",
  "paletteAccent": "oxblood scarf accent",
  "poseEnergy": "upright guarded stance"
}
```

## Evidence

Each case contains exactly 3 evidence items with unique `slot` values `1..3`.

Required per evidence item:

- `slot`
- `title`
- `description`
- `discoveryLocation`
- `imageStem`
- `imagePromptBase`

## Canon And Quality Rules

- One and only one true culprit.
- One and only one red herring.
- `explanation` should name the guilty suspect and guilty slot.
- Evidence should form a coherent proof chain, not three near-duplicates of the same clue.
- Image prompts should not depend on readable text.
- Names should feel grounded and geographically plausible for the setting.
- Location, institution, and suspect background details should agree with each other.
- Suspects in the same case should not collapse into the same silhouette, accessories, or wardrobe language.

## Validation Commands

Validate all cases:

```bash
python case_workflow/validate_case_schema.py
```

Validate one case in stricter quality mode:

```bash
python case_workflow/validate_case_schema.py case_workflow/cases/case_011_the_sable_cartridge.json --strict
```

Run non-rendering pipeline checks:

```bash
python case_workflow/pipeline_health_check.py --strict
```
