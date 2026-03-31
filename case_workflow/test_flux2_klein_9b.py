from pathlib import Path
import runpy

_SCRIPT = Path(__file__).resolve().parent / "tests" / "test_flux2_klein_9b.py"
runpy.run_path(str(_SCRIPT), run_name="__main__")
