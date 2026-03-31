from huggingface_hub import snapshot_download

from paths import model_path


BASE_TARGET = model_path("FLUX.2-dev")
TURBO_TARGET = model_path("FLUX.2-dev-Turbo")


print(f"Downloading black-forest-labs/FLUX.2-dev to: {BASE_TARGET}")
snapshot_download(
    repo_id="black-forest-labs/FLUX.2-dev",
    local_dir=BASE_TARGET,
    token=True,
)

print(f"Downloading fal/FLUX.2-dev-Turbo to: {TURBO_TARGET}")
snapshot_download(
    repo_id="fal/FLUX.2-dev-Turbo",
    local_dir=TURBO_TARGET,
)

print("Done.")
