import os
from pathlib import Path


WORKFLOW_DIR = Path(__file__).resolve().parents[2]
ROOT_DIR = WORKFLOW_DIR
PROJECT_DIR = WORKFLOW_DIR.parent
CASES_DIR = Path(os.environ.get("CASE_INPUT_ROOT", WORKFLOW_DIR / "cases"))
STYLES_DIR = Path(os.environ.get("STYLE_INPUT_ROOT", WORKFLOW_DIR / "styles"))
OUTPUTS_DIR = Path(os.environ.get("CASE_OUTPUT_ROOT", WORKFLOW_DIR / "outputs"))
MODELS_DIR = Path(os.environ.get("DIFFUSION_MODEL_ROOT", r"I:\models\case_workflow"))
QWEN_LORA_DIR = Path(os.environ.get("DIFFUSION_LORA_ROOT", r"I:\models\Lora"))
UNITY_IMPORT_ROOT = Path(os.environ.get("UNITY_IMPORT_ROOT", PROJECT_DIR / "Assets" / "GeneratedCases" / "to_import"))


def model_path(name: str) -> str:
    return str(MODELS_DIR / name)
