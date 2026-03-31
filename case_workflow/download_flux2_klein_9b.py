from huggingface_hub import snapshot_download

from paths import model_path

TARGET_DIR = model_path("FLUX.2-klein-9B")

print(f"Downloading black-forest-labs/FLUX.2-klein-9B to: {TARGET_DIR}")

snapshot_download(
    repo_id="black-forest-labs/FLUX.2-klein-9B",
    local_dir=TARGET_DIR,
    resume_download=True,
    token=True,  # uses your HF login token from CLI login
)

print("Download complete.")
