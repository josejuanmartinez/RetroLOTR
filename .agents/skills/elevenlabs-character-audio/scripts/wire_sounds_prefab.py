#!/usr/bin/env python3
"""Dry-run or replace serialized AudioClip lists in the RetroLOTR Sounds prefab."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


FIELD_RE = re.compile(r"^  ([A-Za-z_][A-Za-z0-9_]*):(?: .*)?$", re.MULTILINE)
GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)
AUDIO_EXTENSIONS = {".mp3", ".wav", ".ogg", ".aif", ".aiff"}


class WiringError(RuntimeError):
    pass


def repo_root() -> Path:
    current = Path.cwd().resolve()
    for candidate in (current, *current.parents):
        if (candidate / "Assets").is_dir() and (candidate / "ProjectSettings").is_dir():
            return candidate
    raise WiringError("Run from the RetroLOTR repository or pass paths relative to it.")


def asset_guid(root: Path, asset_value: str) -> str:
    normalized = asset_value.replace("\\", "/")
    if not normalized.startswith("Assets/"):
        raise WiringError(f"Audio asset must be under Assets/: {asset_value}")
    asset = (root / normalized).resolve()
    try:
        asset.relative_to((root / "Assets").resolve())
    except ValueError as exc:
        raise WiringError(f"Asset resolves outside Assets/: {asset_value}") from exc
    if asset.suffix.lower() not in AUDIO_EXTENSIONS:
        raise WiringError(f"Unsupported audio extension: {asset_value}")
    if not asset.is_file():
        raise WiringError(f"Audio asset does not exist: {asset_value}")
    meta = Path(str(asset) + ".meta")
    if not meta.is_file():
        raise WiringError(f"Unity .meta is missing; import/refresh first: {meta}")
    match = GUID_RE.search(meta.read_text(encoding="utf-8-sig"))
    if not match:
        raise WiringError(f"Could not read Unity GUID: {meta}")
    return match.group(1)


def locate_field(text: str, field: str) -> tuple[int, int]:
    matches = list(FIELD_RE.finditer(text))
    target_index = next((index for index, match in enumerate(matches) if match.group(1) == field), None)
    if target_index is None:
        raise WiringError(f"Serialized field not found in prefab: {field}")
    start = matches[target_index].start()
    end = matches[target_index + 1].start() if target_index + 1 < len(matches) else len(text)
    return start, end


def replacement(field: str, guids: list[str]) -> str:
    lines = [f"  {field}:\n"]
    for guid in guids:
        lines.append(f"  - {{fileID: 8300000, guid: {guid}, type: 3}}\n")
    return "".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, help="JSON object mapping serialized field names to Assets audio paths.")
    parser.add_argument("--prefab", default="Assets/GameObjects/Sounds.prefab")
    parser.add_argument("--apply", action="store_true", help="Write changes. Without this flag, only print the proposed lists.")
    args = parser.parse_args()
    try:
        root = repo_root()
        manifest_path = Path(args.manifest).resolve()
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        if not isinstance(manifest, dict) or not manifest:
            raise WiringError("Manifest must be a non-empty JSON object.")
        prefab = (root / args.prefab).resolve() if not Path(args.prefab).is_absolute() else Path(args.prefab).resolve()
        if not prefab.is_file():
            raise WiringError(f"Prefab not found: {prefab}")
        text = prefab.read_text(encoding="utf-8-sig")
        resolved: dict[str, list[str]] = {}
        for field, assets in manifest.items():
            if not isinstance(field, str) or not isinstance(assets, list) or not assets:
                raise WiringError(f"Field '{field}' must map to a non-empty array of asset paths.")
            locate_field(text, field)
            resolved[field] = [asset_guid(root, str(asset)) for asset in assets]
        for field, guids in resolved.items():
            start, end = locate_field(text, field)
            text = text[:start] + replacement(field, guids) + text[end:]
        print(json.dumps({field: {"count": len(guids), "guids": guids} for field, guids in resolved.items()}, indent=2))
        if not args.apply:
            print("Dry run only. Re-run with --apply after reviewing the mapping.")
            return 0
        original = prefab.read_text(encoding="utf-8-sig")
        newline = "\r\n" if "\r\n" in original else "\n"
        prefab.write_text(text.replace("\r\n", "\n").replace("\n", newline), encoding="utf-8", newline="")
        print(f"Updated {prefab}")
        return 0
    except (WiringError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
