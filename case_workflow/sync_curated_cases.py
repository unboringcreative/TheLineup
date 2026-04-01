from __future__ import annotations

import json
import struct
import shutil
import uuid
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parent.parent
CASES_JSON_DIR = Path(__file__).resolve().parent / "cases"
UNITY_CASES_DIR = PROJECT_ROOT / "Assets" / "Scripts" / "Cases"
IMPORT_ROOT = PROJECT_ROOT / "Assets" / "GeneratedCases" / "to_import"
LIBRARY_ASSET = UNITY_CASES_DIR / "CaseLibrary_Main.asset"

KEEP_CASE_IDS = ("CASE_009", "CASE_010", "CASE_011", "CASE_012", "CASE_013", "CASE_014", "CASE_015", "CASE_016")
CASE_JSON_FILES = {
    "CASE_009": CASES_JSON_DIR / "case_009_the_neon_receipt.json",
    "CASE_010": CASES_JSON_DIR / "case_010_the_ghost_docket.json",
    "CASE_011": CASES_JSON_DIR / "case_011_the_sable_cartridge.json",
    "CASE_012": CASES_JSON_DIR / "case_012_the_ivory_switch.json",
    "CASE_013": CASES_JSON_DIR / "case_013_the_amber_turnstile.json",
    "CASE_014": CASES_JSON_DIR / "case_014_the_green_room_key.json",
    "CASE_015": CASES_JSON_DIR / "case_015_the_indigo_armature_key.json",
    "CASE_016": CASES_JSON_DIR / "case_016_the_vermilion_platform_seal.json",
}
FOLDER_REMAP = {
    "CASE_009": "CASE_006",
}

CASE_DEFINITION_SCRIPT_GUID = "0d7eb3c794a57b14fa77815382270587"
SUSPECT_SCRIPT_GUID = "4fb761c0fd2597b4e8ff14be87102695"
EVIDENCE_SCRIPT_GUID = "a6f08364790497b44845ca143098c02b"


def read_guid(meta_path: Path) -> str:
    for line in meta_path.read_text(encoding="utf-8").splitlines():
        if line.startswith("guid: "):
            return line.split(": ", 1)[1].strip()
    raise ValueError(f"Could not find guid in {meta_path}")


def format_scalar(value: str) -> str:
    return value if value else ""


def write_text(path: Path, content: str) -> None:
    path.write_text(content, encoding="utf-8", newline="\n")


def replace_meta_guid(meta_path: Path) -> None:
    lines = meta_path.read_text(encoding="utf-8").splitlines()
    for index, line in enumerate(lines):
        if line.startswith("guid: "):
            lines[index] = f"guid: {uuid.uuid4().hex}"
            break
    write_text(meta_path, "\n".join(lines) + "\n")


def read_png_size(path: Path) -> tuple[int, int]:
    with path.open("rb") as f:
        header = f.read(24)
    if len(header) < 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"Not a valid PNG file: {path}")
    return struct.unpack(">II", header[16:24])


def ensure_png_meta(image_dir: Path, png_path: Path) -> None:
    meta_path = png_path.with_suffix(png_path.suffix + ".meta")
    if meta_path.exists():
        return

    template_meta = next((candidate for candidate in image_dir.glob("*.png.meta") if candidate.exists()), None)
    if template_meta is None:
        raise FileNotFoundError(f"No PNG meta template found in {image_dir}")

    width, height = read_png_size(png_path)
    base_name = png_path.stem
    content = template_meta.read_text(encoding="utf-8")
    content = content.replace("guid: " + read_guid(template_meta), "guid: " + uuid.uuid4().hex)
    content = content.replace("featured_00001_0001_0", f"{base_name}_0")
    content = content.replace("featured_00001_0001", base_name)
    content = content.replace("width: 1664", f"width: {width}")
    content = content.replace("height: 928", f"height: {height}")
    write_text(meta_path, content)


def clone_case_folder_from_template(case_id: str, template_case_id: str = "CASE_011") -> Path:
    template_dir = UNITY_CASES_DIR / template_case_id
    template_meta = UNITY_CASES_DIR / f"{template_case_id}.meta"
    target_dir = UNITY_CASES_DIR / case_id
    target_meta = UNITY_CASES_DIR / f"{case_id}.meta"

    if not template_dir.exists():
        raise FileNotFoundError(f"Template case folder does not exist: {template_dir}")

    shutil.copytree(template_dir, target_dir)
    shutil.copy2(template_meta, target_meta)

    old_definition = target_dir / f"{template_case_id}_Definition.asset"
    old_definition_meta = target_dir / f"{template_case_id}_Definition.asset.meta"
    new_definition = target_dir / f"{case_id}_Definition.asset"
    new_definition_meta = target_dir / f"{case_id}_Definition.asset.meta"
    old_definition.rename(new_definition)
    old_definition_meta.rename(new_definition_meta)

    replace_meta_guid(target_meta)
    for meta_path in target_dir.rglob("*.meta"):
        replace_meta_guid(meta_path)

    return target_dir


def ensure_case_folder(case_id: str) -> Path:
    target = UNITY_CASES_DIR / case_id
    if target.exists():
        return target

    source_name = FOLDER_REMAP.get(case_id)
    if not source_name:
        raise FileNotFoundError(f"No existing Unity case folder available to repurpose for {case_id}")

    source = UNITY_CASES_DIR / source_name
    if not source.exists():
        raise FileNotFoundError(f"Expected source folder for remap does not exist: {source}")

    source.rename(target)

    old_definition = target / f"{source_name}_Definition.asset"
    old_meta = target / f"{source_name}_Definition.asset.meta"
    new_definition = target / f"{case_id}_Definition.asset"
    new_meta = target / f"{case_id}_Definition.asset.meta"
    if old_definition.exists():
        old_definition.rename(new_definition)
    if old_meta.exists():
        old_meta.rename(new_meta)

    return target


def provision_case_folder(case_id: str) -> Path:
    target = UNITY_CASES_DIR / case_id
    if target.exists():
        return target

    source_name = FOLDER_REMAP.get(case_id)
    if source_name:
        return ensure_case_folder(case_id)

    return clone_case_folder_from_template(case_id)


def sync_images(case_id: str, unity_case_dir: Path) -> None:
    import_dir = IMPORT_ROOT / case_id
    image_dir = unity_case_dir / "Images"
    pngs = sorted(image_dir.glob("*.png"))
    if not pngs:
        raise FileNotFoundError(f"No destination PNG files found in {image_dir}")

    if not import_dir.exists():
        return

    for src_png in sorted(import_dir.glob("*.png")):
        dest_png = image_dir / src_png.name
        shutil.copy2(src_png, dest_png)
        ensure_png_meta(image_dir, dest_png)


def build_case_definition_yaml(case_id: str, case_data: dict, unity_case_dir: Path) -> str:
    definition_asset = unity_case_dir / f"{case_id}_Definition.asset"
    featured_guid = read_guid((unity_case_dir / "Images" / "featured_00001_0001.png.meta"))
    location_meta = unity_case_dir / "Images" / "location_00001_0001.png.meta"
    location_guid = read_guid(location_meta) if location_meta.exists() else featured_guid

    suspect_guids = [read_guid((unity_case_dir / f"SuspectProfile {index}.asset.meta")) for index in range(1, 6)]
    evidence_guids = [read_guid((unity_case_dir / f"EvidenceProfile {index}.asset.meta")) for index in range(1, 4)]

    suspects_yaml = "\n".join(f"  - {{fileID: 11400000, guid: {guid}, type: 2}}" for guid in suspect_guids)
    evidence_yaml = "\n".join(f"  - {{fileID: 11400000, guid: {guid}, type: 2}}" for guid in evidence_guids)
    description = case_data["caseDescription"]
    explanation = case_data["explanation"]

    return (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {CASE_DEFINITION_SCRIPT_GUID}, type: 3}}\n"
        f"  m_Name: {definition_asset.stem}\n"
        "  m_EditorClassIdentifier: Assembly-CSharp::CaseDefinitionSO\n"
        f"  caseId: {case_id}\n"
        f"  caseTitle: {case_data['caseTitle']}\n"
        f"  caseDescription: {description}\n"
        f"  locationAddressOrBusiness: {case_data['location']['addressOrBusiness']}\n"
        f"  locationCity: {case_data['location']['city']}\n"
        f"  locationCountry: {case_data['location']['country']}\n"
        f"  locationImage: {{fileID: 21300000, guid: {location_guid}, type: 3}}\n"
        f"  featuredImage: {{fileID: 21300000, guid: {featured_guid}, type: 3}}\n"
        "  suspects:\n"
        f"{suspects_yaml}\n"
        "  evidence:\n"
        f"{evidence_yaml}\n"
        f"  guiltySuspectIndex: {case_data['guiltySlot'] - 1}\n"
        f"  verdictTitle: {case_data['verdictTitle']}\n"
        f"  explanation: {explanation}\n"
    )


def build_suspect_yaml(index: int, suspect: dict, portrait_guid: str) -> str:
    return (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SUSPECT_SCRIPT_GUID}, type: 3}}\n"
        f"  m_Name: SuspectProfile {index}\n"
        "  m_EditorClassIdentifier: Assembly-CSharp::SuspectProfileSO\n"
        f"  displayName: {suspect['displayName']}\n"
        f"  sex: {format_scalar(suspect.get('sex', ''))}\n"
        f"  occupation: {suspect['occupation']}\n"
        f"  nationality: {suspect['nationality']}\n"
        f"  height: {suspect['height']}\n"
        f"  weight: {suspect['weight']}\n"
        f"  keyPersonalityTrait: {suspect['keyPersonalityTrait']}\n"
        f"  dialogue: {suspect['dialogue']}\n"
        f"  portrait: {{fileID: 21300000, guid: {portrait_guid}, type: 3}}\n"
    )


def build_evidence_yaml(index: int, evidence: dict, image_guid: str) -> str:
    return (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {EVIDENCE_SCRIPT_GUID}, type: 3}}\n"
        f"  m_Name: EvidenceProfile {index}\n"
        "  m_EditorClassIdentifier: Assembly-CSharp::EvidenceProfileSO\n"
        f"  title: {evidence['title']}\n"
        f"  description: {evidence['description']}\n"
        f"  discoveryLocation: {evidence['discoveryLocation']}\n"
        f"  evidenceRole: {evidence.get('role', 'neutral')}\n"
        f"  pointsToSuspectSlot: {int(evidence.get('pointsToSuspectSlot') or 0)}\n"
        f"  image: {{fileID: 21300000, guid: {image_guid}, type: 3}}\n"
    )


def sync_case_assets(case_id: str, case_data: dict) -> None:
    unity_case_dir = provision_case_folder(case_id)
    sync_images(case_id, unity_case_dir)

    definition_asset = unity_case_dir / f"{case_id}_Definition.asset"
    write_text(definition_asset, build_case_definition_yaml(case_id, case_data, unity_case_dir))

    for index, suspect in enumerate(case_data["suspects"], start=1):
        portrait_guid = read_guid(unity_case_dir / "Images" / f"suspect_{index:05d}_0001.png.meta")
        suspect_asset = unity_case_dir / f"SuspectProfile {index}.asset"
        write_text(suspect_asset, build_suspect_yaml(index, suspect, portrait_guid))

    for index, evidence in enumerate(case_data["evidence"], start=1):
        image_guid = read_guid(unity_case_dir / "Images" / f"evidence_{index:05d}_0001.png.meta")
        evidence_asset = unity_case_dir / f"EvidenceProfile {index}.asset"
        write_text(evidence_asset, build_evidence_yaml(index, evidence, image_guid))


def trim_case_library() -> None:
    kept_lines = []
    for case_id in KEEP_CASE_IDS:
        meta_path = UNITY_CASES_DIR / case_id / f"{case_id}_Definition.asset.meta"
        guid = read_guid(meta_path)
        kept_lines.append(f"  - {{fileID: 11400000, guid: {guid}, type: 2}}")

    lines = LIBRARY_ASSET.read_text(encoding="utf-8").splitlines()
    output: list[str] = []
    in_cases = False
    for line in lines:
        if line.startswith("  cases:"):
            output.append(line)
            output.extend(kept_lines)
            in_cases = True
            continue
        if in_cases:
            if line.startswith("  - "):
                continue
            in_cases = False
        output.append(line)

    write_text(LIBRARY_ASSET, "\n".join(output) + "\n")


def main() -> None:
    for case_id in KEEP_CASE_IDS:
        case_data = json.loads(CASE_JSON_FILES[case_id].read_text(encoding="utf-8"))
        sync_case_assets(case_id, case_data)

    trim_case_library()
    print("Synced curated cases into Unity assets.")


if __name__ == "__main__":
    main()
