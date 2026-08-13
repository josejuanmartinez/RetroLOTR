#!/usr/bin/env python3
"""Small dependency-free ElevenLabs client for RetroLOTR audio production."""

from __future__ import annotations

import argparse
import base64
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any

if os.name == "nt":
    import winreg


API_BASE = "https://api.elevenlabs.io"
SAFE_ID = re.compile(r"^[a-z0-9][a-z0-9_-]*$")


class ApiError(RuntimeError):
    pass


def windows_environment_value(name: str) -> str | None:
    if os.name != "nt":
        return None
    locations = (
        (winreg.HKEY_CURRENT_USER, r"Environment"),
        (winreg.HKEY_LOCAL_MACHINE, r"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"),
    )
    for hive, subkey in locations:
        try:
            with winreg.OpenKey(hive, subkey) as handle:
                value, _ = winreg.QueryValueEx(handle, name)
        except (FileNotFoundError, OSError):
            continue
        if isinstance(value, str) and value.strip():
            return value
    return None


def api_key(required: bool = True) -> str | None:
    key = os.environ.get("ELEVENLABS_KEY") or os.environ.get("ELEVENLABS_API_KEY")
    if not key:
        key = windows_environment_value("ELEVENLABS_KEY") or windows_environment_value("ELEVENLABS_API_KEY")
    if required and not key:
        raise ApiError("Set ELEVENLABS_KEY (preferred) or ELEVENLABS_API_KEY in the process, Windows User, or Windows Machine environment.")
    return key


def request(
    method: str,
    path: str,
    *,
    body: dict[str, Any] | None = None,
    require_key: bool = True,
    expect_json: bool = True,
) -> tuple[Any, dict[str, str]]:
    key = api_key(require_key)
    data = json.dumps(body).encode("utf-8") if body is not None else None
    headers = {"Accept": "application/json" if expect_json else "audio/*"}
    if data is not None:
        headers["Content-Type"] = "application/json"
    if key:
        headers["xi-api-key"] = key
    req = urllib.request.Request(API_BASE + path, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=120) as response:
            payload = response.read()
            response_headers = {k.lower(): v for k, v in response.headers.items()}
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")[:2000]
        raise ApiError(f"ElevenLabs HTTP {exc.code}: {detail}") from exc
    except urllib.error.URLError as exc:
        raise ApiError(f"ElevenLabs request failed: {exc.reason}") from exc
    if expect_json:
        try:
            return json.loads(payload.decode("utf-8")), response_headers
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise ApiError("ElevenLabs returned an invalid JSON response.") from exc
    return payload, response_headers


def write_bytes(path: Path, data: bytes, overwrite: bool) -> None:
    if path.exists() and not overwrite:
        raise ApiError(f"Refusing to overwrite existing file: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def write_json(path: Path, value: Any, overwrite: bool = True) -> None:
    if path.exists() and not overwrite:
        raise ApiError(f"Refusing to overwrite existing file: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def read_text(path: str) -> str:
    value = Path(path).read_text(encoding="utf-8").strip()
    if not value:
        raise ApiError(f"Text file is empty: {path}")
    return value


def validate_item_id(value: str) -> str:
    if not SAFE_ID.fullmatch(value):
        raise ApiError(f"Invalid item id '{value}'; use lowercase letters, digits, _ or -.")
    return value


def audio_extension(output_format: str) -> str:
    if output_format.startswith("mp3_"):
        return ".mp3"
    if output_format.startswith("wav_"):
        return ".wav"
    if output_format.startswith("pcm_"):
        return ".pcm"
    if output_format.startswith("opus_"):
        return ".opus"
    raise ApiError(f"Cannot infer extension for output format: {output_format}")


def cost_headers(headers: dict[str, str]) -> dict[str, str]:
    keys = ("character-cost", "request-id", "x-trace-id")
    return {key: headers[key] for key in keys if key in headers}


def command_search_shared(args: argparse.Namespace) -> None:
    query: dict[str, Any] = {
        "page_size": args.limit,
        "search": args.query,
        "language": args.language,
        "sort": args.sort,
        "include_custom_rates": str(args.include_custom_rates).lower(),
        "include_live_moderated": str(args.include_live_moderated).lower(),
    }
    if args.gender:
        query["gender"] = args.gender
    if args.min_notice_period_days is not None:
        query["min_notice_period_days"] = args.min_notice_period_days
    path = "/v1/shared-voices?" + urllib.parse.urlencode(query)
    result, _ = request("GET", path)
    voices = result.get("voices", [])[: args.limit]
    compact = []
    for voice in voices:
        compact.append(
            {
                "name": voice.get("name"),
                "voice_id": voice.get("voice_id"),
                "public_owner_id": voice.get("public_owner_id"),
                "gender": voice.get("gender"),
                "age": voice.get("age"),
                "accent": voice.get("accent"),
                "descriptive": voice.get("descriptive"),
                "use_case": voice.get("use_case"),
                "description": voice.get("description"),
                "rate": voice.get("rate"),
                "notice_period": voice.get("notice_period"),
                "preview_url": voice.get("preview_url"),
            }
        )
    print(json.dumps(compact, indent=2, ensure_ascii=False))
    if args.save_json:
        write_json(Path(args.save_json), compact, overwrite=args.overwrite)
    if args.download_previews:
        output_dir = Path(args.download_previews)
        output_dir.mkdir(parents=True, exist_ok=True)
        for index, voice in enumerate(compact, 1):
            url = voice.get("preview_url")
            if not url:
                continue
            parsed = urllib.parse.urlparse(url)
            if parsed.scheme != "https":
                raise ApiError(f"Refusing non-HTTPS preview URL for {voice.get('name')}")
            target = output_dir / f"{index:02d}_{voice['voice_id']}.mp3"
            with urllib.request.urlopen(url, timeout=120) as response:
                write_bytes(target, response.read(), args.overwrite)
        write_json(output_dir / "voices.json", compact)


def command_list_voices(args: argparse.Namespace) -> None:
    query = {"page_size": args.limit}
    if args.query:
        query["search"] = args.query
    result, _ = request("GET", "/v2/voices?" + urllib.parse.urlencode(query))
    voices = result.get("voices", [])
    compact = [
        {
            "name": voice.get("name"),
            "voice_id": voice.get("voice_id"),
            "category": voice.get("category"),
            "labels": voice.get("labels"),
            "description": voice.get("description"),
            "preview_url": voice.get("preview_url"),
        }
        for voice in voices
    ]
    print(json.dumps(compact, indent=2, ensure_ascii=False))


def require_confirmation(args: argparse.Namespace, flag_name: str) -> None:
    if not getattr(args, flag_name):
        dashed = "--" + flag_name.replace("_", "-")
        raise ApiError(f"This operation is mutating or billable. Re-run with {dashed} after approval.")


def command_add_shared(args: argparse.Namespace) -> None:
    require_confirmation(args, "confirm_add")
    body = {"new_name": args.name, "bookmarked": True}
    path = f"/v1/voices/add/{urllib.parse.quote(args.public_owner_id)}/{urllib.parse.quote(args.voice_id)}"
    result, _ = request("POST", path, body=body)
    print(json.dumps(result, indent=2, ensure_ascii=False))


def command_design_voice(args: argparse.Namespace) -> None:
    require_confirmation(args, "confirm_spend")
    description = read_text(args.description_file)
    body: dict[str, Any] = {
        "voice_description": description,
        "model_id": args.model,
        "seed": args.seed,
        "guidance_scale": args.guidance_scale,
        "should_enhance": args.enhance,
    }
    if args.preview_text_file:
        body["text"] = read_text(args.preview_text_file)
        body["auto_generate_text"] = False
    else:
        body["auto_generate_text"] = True
    result, headers = request("POST", "/v1/text-to-voice/design", body=body)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    metadata = {"text": result.get("text"), "description": description, "previews": [], "headers": cost_headers(headers)}
    for index, preview in enumerate(result.get("previews", []), 1):
        media_type = preview.get("media_type", "audio/mpeg")
        extension = ".wav" if "wav" in media_type else ".mp3"
        filename = f"preview_{index:02d}{extension}"
        write_bytes(output_dir / filename, base64.b64decode(preview["audio_base_64"]), args.overwrite)
        metadata["previews"].append(
            {
                "file": filename,
                "generated_voice_id": preview.get("generated_voice_id"),
                "duration_secs": preview.get("duration_secs"),
                "language": preview.get("language"),
                "media_type": media_type,
            }
        )
    write_json(output_dir / "design.json", metadata)
    print(json.dumps(metadata, indent=2, ensure_ascii=False))


def command_create_voice(args: argparse.Namespace) -> None:
    require_confirmation(args, "confirm_create")
    body = {
        "voice_name": args.name,
        "voice_description": read_text(args.description_file),
        "generated_voice_id": args.generated_voice_id,
    }
    result, _ = request("POST", "/v1/text-to-voice", body=body)
    print(json.dumps(result, indent=2, ensure_ascii=False))


def load_items(path: str, required_fields: tuple[str, ...]) -> list[dict[str, Any]]:
    value = json.loads(Path(path).read_text(encoding="utf-8"))
    if not isinstance(value, list) or not value:
        raise ApiError(f"Expected a non-empty JSON array: {path}")
    for item in value:
        if not isinstance(item, dict) or any(field not in item for field in required_fields):
            raise ApiError(f"Each item in {path} must contain: {', '.join(required_fields)}")
        validate_item_id(str(item["id"]))
    return value


def preflight_targets(output_dir: Path, items: list[dict[str, Any]], extension: str, overwrite: bool) -> None:
    if overwrite:
        return
    existing = [output_dir / (str(item["id"]) + extension) for item in items]
    conflicts = [str(path) for path in existing if path.exists()]
    if conflicts:
        raise ApiError("Refusing to overwrite existing files:\n" + "\n".join(conflicts))


def command_tts_batch(args: argparse.Namespace) -> None:
    require_confirmation(args, "confirm_spend")
    items = load_items(args.lines_json, ("id", "text"))
    output_dir = Path(args.output_dir)
    extension = audio_extension(args.output_format)
    preflight_targets(output_dir, items, extension, args.overwrite)
    output_dir.mkdir(parents=True, exist_ok=True)
    generated = []
    for item in items:
        item_id = str(item["id"])
        query = urllib.parse.urlencode({"output_format": args.output_format})
        body = {"text": str(item["text"]), "model_id": args.model}
        audio, headers = request(
            "POST",
            f"/v1/text-to-speech/{urllib.parse.quote(args.voice_id)}?{query}",
            body=body,
            expect_json=False,
        )
        target = output_dir / (item_id + extension)
        write_bytes(target, audio, args.overwrite)
        generated.append({"id": item_id, "text": item["text"], "file": target.name, "headers": cost_headers(headers)})
    manifest = {"voice_id": args.voice_id, "model": args.model, "output_format": args.output_format, "generated": generated}
    write_json(output_dir / "generation.json", manifest)
    print(json.dumps(manifest, indent=2, ensure_ascii=False))


def command_sfx_batch(args: argparse.Namespace) -> None:
    require_confirmation(args, "confirm_spend")
    items = load_items(args.items_json, ("id", "prompt", "duration_seconds"))
    for item in items:
        duration = float(item["duration_seconds"])
        if not 0.5 <= duration <= 30:
            raise ApiError(f"SFX duration for {item['id']} must be between 0.5 and 30 seconds.")
    output_dir = Path(args.output_dir)
    extension = audio_extension(args.output_format)
    preflight_targets(output_dir, items, extension, args.overwrite)
    output_dir.mkdir(parents=True, exist_ok=True)
    generated = []
    for item in items:
        item_id = str(item["id"])
        query = urllib.parse.urlencode({"output_format": args.output_format})
        body = {
            "text": str(item["prompt"]),
            "duration_seconds": float(item["duration_seconds"]),
            "prompt_influence": args.prompt_influence,
            "loop": False,
        }
        audio, headers = request("POST", f"/v1/sound-generation?{query}", body=body, expect_json=False)
        target = output_dir / (item_id + extension)
        write_bytes(target, audio, args.overwrite)
        generated.append(
            {
                "id": item_id,
                "prompt": item["prompt"],
                "duration_seconds": item["duration_seconds"],
                "file": target.name,
                "headers": cost_headers(headers),
            }
        )
    manifest = {"output_format": args.output_format, "generated": generated}
    write_json(output_dir / "generation.json", manifest)
    print(json.dumps(manifest, indent=2, ensure_ascii=False))


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)

    p = sub.add_parser("search-shared", help="Search the shared Voice Library and optionally download previews.")
    p.add_argument("query")
    p.add_argument("--gender")
    p.add_argument("--language", default="en")
    p.add_argument("--limit", type=int, default=10, choices=range(1, 101))
    p.add_argument("--sort", default="usage_character_count_1y", choices=("created_date", "usage_character_count_1y", "trending", "cloned_by_count"))
    p.add_argument("--min-notice-period-days", type=int)
    p.add_argument("--include-custom-rates", action="store_true")
    p.add_argument("--include-live-moderated", action="store_true")
    p.add_argument("--download-previews")
    p.add_argument("--save-json")
    p.add_argument("--overwrite", action="store_true")
    p.set_defaults(func=command_search_shared)

    p = sub.add_parser("list-voices", help="List voices currently available to the account.")
    p.add_argument("--query")
    p.add_argument("--limit", type=int, default=100, choices=range(1, 101))
    p.set_defaults(func=command_list_voices)

    p = sub.add_parser("add-shared", help="Add a shared voice to the account collection.")
    p.add_argument("--public-owner-id", required=True)
    p.add_argument("--voice-id", required=True)
    p.add_argument("--name", required=True)
    p.add_argument("--confirm-add", action="store_true")
    p.set_defaults(func=command_add_shared)

    p = sub.add_parser("design-voice", help="Generate three billable Voice Design previews.")
    p.add_argument("--description-file", required=True)
    preview = p.add_mutually_exclusive_group(required=True)
    preview.add_argument("--preview-text-file")
    preview.add_argument("--auto-generate-text", action="store_true")
    p.add_argument("--output-dir", required=True)
    p.add_argument("--model", default="eleven_ttv_v3", choices=("eleven_ttv_v3", "eleven_multilingual_ttv_v2"))
    p.add_argument("--seed", type=int)
    p.add_argument("--guidance-scale", type=float, default=5.0)
    p.add_argument("--enhance", action="store_true")
    p.add_argument("--overwrite", action="store_true")
    p.add_argument("--confirm-spend", action="store_true")
    p.set_defaults(func=command_design_voice)

    p = sub.add_parser("create-voice", help="Save an approved generated voice preview.")
    p.add_argument("--name", required=True)
    p.add_argument("--description-file", required=True)
    p.add_argument("--generated-voice-id", required=True)
    p.add_argument("--confirm-create", action="store_true")
    p.set_defaults(func=command_create_voice)

    p = sub.add_parser("tts-batch", help="Generate one TTS file per line in a JSON array.")
    p.add_argument("--voice-id", required=True)
    p.add_argument("--lines-json", required=True)
    p.add_argument("--output-dir", required=True)
    p.add_argument("--model", default="eleven_v3")
    p.add_argument("--output-format", default="mp3_44100_128")
    p.add_argument("--overwrite", action="store_true")
    p.add_argument("--confirm-spend", action="store_true")
    p.set_defaults(func=command_tts_batch)

    p = sub.add_parser("sfx-batch", help="Generate one SFX file per item in a JSON array.")
    p.add_argument("--items-json", required=True)
    p.add_argument("--output-dir", required=True)
    p.add_argument("--output-format", default="mp3_44100_128")
    p.add_argument("--prompt-influence", type=float, default=0.5)
    p.add_argument("--overwrite", action="store_true")
    p.add_argument("--confirm-spend", action="store_true")
    p.set_defaults(func=command_sfx_batch)
    return parser


def main() -> int:
    try:
        args = build_parser().parse_args()
        args.func(args)
        return 0
    except (ApiError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
