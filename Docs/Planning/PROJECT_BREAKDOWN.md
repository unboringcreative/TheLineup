# Project Breakdown

This is a cleaned-up written version of the repo breakdown I gave earlier, saved in a durable internal planning spot so it can travel with the project.

## Core Shape

- The repo is two connected systems:
  - the Unity game in `Assets/`
  - the local case-content pipeline in `case_workflow/`
- That split is already reflected in `README.md` and `Docs/PROJECT_STRUCTURE.md`.
- The game is built on Unity `6000.4.0f1` with URP, uGUI, the new Input System, and the Unity Test Framework package.
- Only one gameplay scene is currently in the build: `Assets/Scenes/Game.unity`.

## Top-Level Structure

- `Assets/`
  - Unity-authoritative content: scene, runtime scripts, editor tooling, curated case assets, fonts, render settings.
- `Packages/`
  - Unity package manifest and lockfile only.
- `ProjectSettings/`
  - Player settings, build settings, input settings, render settings, and other project config.
- `case_workflow/`
  - The external content pipeline for authoring, validating, rendering, and syncing cases.
- `Docs/`
  - Repo-level docs and operational references.

## Runtime Mental Model

- The de facto application shell is `Assets/Scripts/Cases/CaseScreenController.cs`.
- That script currently does all of the following:
  - ensures the controller exists on the scene `Canvas`
  - auto-binds scene UI by name
  - applies the selected case data
  - constructs several overlays dynamically at runtime
  - handles interviews, evidence popups, verdict flow, case selection, and main menu behavior
  - applies layout and theme styling to the scene
- This is the single biggest architectural hotspot in the project.

## Startup Flow

1. Unity loads `Assets/Scenes/Game.unity`.
2. `CaseScreenController` ensures it is attached to the active `Canvas`.
3. `Assets/Scripts/InputSystemEventSystemFix.cs` swaps legacy EventSystem UI input over to `InputSystemUIInputModule`.
4. `CaseScreenController.OnEnable()` auto-binds UI, ensures runtime overlays exist, and either opens case selection or applies the current case directly.
5. The current scene config points at `CaseLibrary_Main.asset` and has case selection enabled, while bypassing it in the editor.

## Runtime Data Model

- Runtime case data is ScriptableObject-driven.
- Primary data assets:
  - `Assets/Scripts/Cases/CaseDefinitionSO.cs`
  - `Assets/Scripts/Cases/SuspectProfileSO.cs`
  - `Assets/Scripts/Cases/EvidenceProfileSO.cs`
  - `Assets/Scripts/Cases/CaseLibrarySO.cs`
- Core shape assumptions are fixed across the runtime and tooling:
  - exactly 5 suspects
  - exactly 3 evidence items
  - 1 guilty suspect slot

## Frontend / UI Features In The Scene

The game uses one Unity UI scene with multiple panels and runtime-built overlays.

Main visible systems:

- case selection overlay
- suspect lineup screen
- case file / details panel
- evidence strip with examine behavior
- suspect interview popup
- accusation / verdict selection overlay
- result panel
- lightweight main menu overlay

Important scene objects include:

- `Canvas`
- `TopBar`
- `MainPanel`
- `SuspectGrid`
- `DetailedInfoPanel`
- `EvidencePanel`
- `BottomBar`
- `ResultPanel`
- `ConfirmButton`
- `CloseVerdictButton`
- `EventSystem`

## Case Content Pipeline

The case workflow is a separate local authoring and rendering pipeline.

Canonical flow:

1. Author case JSON in `case_workflow/cases/`
2. Validate content with `case_workflow/validate_case_schema.py`
3. Build prompt queues via `case_workflow/src/case_workflow/prompt_builder.py`
4. Render images with `case_workflow/render_case.py`
5. Emit a Unity import bundle under `Assets/GeneratedCases/to_import/<CASE_ID>`
6. Sync or import into curated Unity assets using:
   - `case_workflow/sync_curated_cases.py`
   - or `Assets/Editor/GeneratedCaseImporter.cs`
7. Verify the case in the Unity scene

## Source Of Truth And Curated Content

- The authoring source of truth is JSON in `case_workflow/cases/`.
- The generated import bundle in `Assets/GeneratedCases/to_import/` is staging output, not authoritative source content.
- Curated in-project Unity case assets currently live under `Assets/Scripts/Cases/CASE_*`.
- Current curated set appears to be `CASE_009` through `CASE_014`.

## Tooling And Stack

### Unity Side

- Unity 6 / `6000.4.0f1`
- uGUI, not UIToolkit
- Universal Render Pipeline
- Input System package installed
- Unity Test Framework package installed, though practical test coverage is thin

### Python Pipeline Side

- Python `>=3.10`
- Reusable Python modules live in `case_workflow/src/case_workflow/`
- Primary modules:
  - `paths.py`
  - `case_validation.py`
  - `prompt_builder.py`
- Rendering stack includes local-only model loading through `torch`, `diffusers`, and optional background removal tooling

### Experimental / Supporting Tools

- `case_workflow/gradio_flux2_studio.py` provides a local manual image-generation UI
- Qwen and FLUX-related scripts/docs exist, but they are clearly auxiliary to the main shipped runtime

## Major Strengths

- Clear conceptual split between runtime game and authoring pipeline
- Strong data-driven direction through ScriptableObjects and case JSON
- Solid content rule thinking in `case_workflow/docs/CASE_SCHEMA.md` and `case_workflow/docs/UNIVERSE_RULES.md`
- Curated case workflow already exists end-to-end
- The project is still small enough to understand and improve without fighting extreme scale

## Major Risks And Hotspots

### 1. `CaseScreenController` Is Too Large

`Assets/Scripts/Cases/CaseScreenController.cs` is a monolith that mixes:

- startup behavior
- UI binding
- layout rules
- theming
- popup creation
- gameplay flow
- data application
- verdict logic

That makes it the highest-value refactor target.

### 2. String-Name Scene Binding Is Brittle

The runtime relies heavily on recursive name lookups like `FindDeep("TopBar")` and object-name matching.

Risks:

- scene renames can break behavior quietly
- duplicate names can cause ambiguous binding
- onboarding becomes slower because correctness depends on scene naming discipline

### 3. Code-Generated UI Is Useful But Hard To Maintain

Several overlays and panels are created entirely in code. That is fast for iteration, but it makes polish, reuse, and designer-facing edits harder than prefab-driven UI.

### 4. Curated Sync Is Powerful But Brittle

`case_workflow/sync_curated_cases.py` directly writes Unity YAML and hardcodes the active curated case list.

That gives strong control, but it also means:

- assumptions are easy to break
- metadata drift is possible
- small format changes can become painful

### 5. Machine-Local Path Assumptions Exist

`case_workflow/src/case_workflow/paths.py` still carries Windows-drive defaults like `E:\models\diffusers` and `I:\models\Lora`.

That is partially softened by env vars, but portability is still weaker than it should be.

### 6. Shipping Metadata Still Has Template Leftovers

`ProjectSettings/ProjectSettings.asset` still uses template-style values such as:

- `DefaultCompany`
- default application identifiers

This is small, but it is a clear shipping-readiness cleanup item.

### 7. Test Coverage Is Light

- Unity tests: package installed, but no meaningful gameplay test suite found
- Python tests: present, but mostly smoke or utility scripts rather than robust automated regression coverage

## Best Immediate Improvement Targets

If the goal is to unlock faster, safer progress, the highest-leverage next steps are:

1. Break up `Assets/Scripts/Cases/CaseScreenController.cs`
2. Replace or reduce name-based scene binding
3. Add real runtime persistence and progression
4. Strengthen validation, testing, and import reporting
5. Clean template metadata and machine-specific assumptions

## Working Mental Model Going Forward

When making changes in this project, the right frame is:

- authored case JSON is the narrative/content source of truth
- curated Unity assets are the shipped runtime representation
- `CaseScreenController` is currently the gameplay shell, but it should not stay the long-term architecture
- the best future direction is a cleaner split between:
  - case loading/data validation
  - gameplay flow
  - UI presentation
  - persistence/progression
  - content authoring/import tooling

## Recommended Reading Order For Future Work

1. `README.md`
2. `Docs/PROJECT_STRUCTURE.md`
3. `case_workflow/docs/PROCESS.md`
4. `Assets/Scripts/Cases/CaseScreenController.cs`
5. `Assets/Scripts/Cases/CaseDefinitionSO.cs`
6. `Assets/Scripts/Cases/SuspectProfileSO.cs`
7. `Assets/Scripts/Cases/EvidenceProfileSO.cs`
8. `Assets/Editor/GeneratedCaseImporter.cs`
9. `case_workflow/src/case_workflow/case_validation.py`
10. `case_workflow/src/case_workflow/prompt_builder.py`
11. `case_workflow/render_case.py`
12. `case_workflow/sync_curated_cases.py`

## Short Take

This is already a compelling small project with a good core concept and a real authoring pipeline. The main limitations are structural, not conceptual: the runtime shell is over-concentrated, the UI binding is brittle, and the testing/operational layer needs reinforcement. Those are fixable, and the project is still at a size where strong cleanup work will pay off quickly.
