from huggingface_hub import snapshot_download

from paths import model_path

print("Downloading Qwen/Qwen-Image to local folder...")

snapshot_download(
    repo_id="Qwen/Qwen-Image",
    local_dir=model_path("Qwen-Image-diffusers"),
    local_dir_use_symlinks=False,
    resume_download=True,
)

print("Done.")
