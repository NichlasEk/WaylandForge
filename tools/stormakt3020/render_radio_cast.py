#!/usr/bin/env python3
from __future__ import annotations

import argparse
import base64
import hashlib
import json
import time
import urllib.error
import urllib.request
from pathlib import Path


DEFAULT_CAST = Path("assets/stormakt3020/radio/skanska-cast.json")
RADIO_ROOT = Path("assets/stormakt3020/radio")


def request_json(url: str, payload: dict | None = None) -> dict:
    data = None if payload is None else json.dumps(payload, ensure_ascii=False).encode("utf-8")
    request = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.loads(response.read())


def submit_and_download(service: str, payload: dict, output: Path) -> tuple[str, dict]:
    accepted = request_json(f"{service}/v1/tts/jobs", payload)
    job_id = accepted["id"]
    status_url = f"{service}{accepted['status_url']}"
    while True:
        status = request_json(status_url)
        if status["status"] == "done":
            break
        if status["status"] in {"failed", "cancelled"}:
            raise RuntimeError(f"TTS job {job_id} {status['status']}: {status.get('error') or status.get('message')}")
        print(f"{job_id}: {status['status']} {status.get('progress', 0):.0%} {status.get('message', '')}")
        time.sleep(2)
    audio_url = status.get("audio_url") or accepted["audio_url"]
    with urllib.request.urlopen(f"{service}{audio_url}", timeout=120) as response:
        audio = response.read()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(audio)
    return job_id, status


def wav_base64(path: Path) -> str:
    return base64.b64encode(path.read_bytes()).decode("ascii")


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description="Render synthetic Stormakt casting references and Dots radio lines.")
    parser.add_argument("phase", choices=["references", "lines", "all"])
    parser.add_argument("--cast", type=Path, default=DEFAULT_CAST)
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--line", action="append", default=[], help="Render only this dialogue line id (repeatable).")
    args = parser.parse_args()

    cast = json.loads(args.cast.read_text())
    service = cast["service"].rstrip("/")
    roles = {role["id"]: role for role in cast["roles"]}
    records: list[dict] = []

    if args.phase in {"references", "all"}:
        for role in cast["roles"]:
            output = RADIO_ROOT / "references" / f"{role['id']}-reference.wav"
            if output.exists() and not args.force:
                print(f"Keeping {output}")
                continue
            payload = {
                "text": role["reference_text"],
                "language": role["language"],
                "voice_instruction": role["voice_instruction"],
                "model_backend": cast["reference_backend"],
                "output_format": "wav",
                "normalize": False,
                "seed": role["reference_seed"],
            }
            job_id, _ = submit_and_download(service, payload, output)
            records.append({"kind": "reference", "role": role["id"], "job_id": job_id, "file": str(output), "sha256": sha256(output), "request": payload})
            print(f"Wrote {output}")

    if args.phase in {"lines", "all"}:
        for line in cast["lines"]:
            if args.line and line["id"] not in args.line:
                continue
            role = roles[line["role"]]
            reference = RADIO_ROOT / "references" / f"{role['id']}-reference.wav"
            if not reference.exists():
                raise FileNotFoundError(f"Missing casting reference: {reference}; run phase 'references' first")
            language = role["language"].lower()
            output = RADIO_ROOT / "raw" / f"{line['id']}-{language}-raw.wav"
            if output.exists() and not args.force:
                print(f"Keeping {output}")
                continue
            payload = {
                "text": line["text"],
                "language": role["language"],
                "model_backend": cast["dialogue_backend"],
                "output_format": "wav",
                "normalize": False,
                "reference_wav_base64": wav_base64(reference),
                "prompt_text": role["reference_text"],
                "seed": line["seed"],
                "dots_num_steps": 4,
                "dots_guidance_scale": 1.2,
                "dots_speaker_scale": 1.5,
            }
            request_path = RADIO_ROOT / "raw" / f"{line['id']}-{language}-request.json"
            request_path.write_text(json.dumps({key: value for key, value in payload.items() if key != "reference_wav_base64"}, ensure_ascii=False, indent=2) + "\n")
            job_id, _ = submit_and_download(service, payload, output)
            records.append({"kind": "line", "line": line["id"], "role": role["id"], "job_id": job_id, "file": str(output), "sha256": sha256(output), "request": str(request_path), "reference_sha256": sha256(reference)})
            print(f"Wrote {output}")

    if records:
        manifest = RADIO_ROOT / "skanska-generation-manifest.json"
        prior = json.loads(manifest.read_text()) if manifest.exists() else {"records": []}
        keys = {(record.get("kind"), record.get("role"), record.get("line")) for record in records}
        prior["records"] = [record for record in prior["records"] if (record.get("kind"), record.get("role"), record.get("line")) not in keys] + records
        prior["service"] = service
        prior["reference_backend"] = cast["reference_backend"]
        prior["dialogue_backend"] = cast["dialogue_backend"]
        prior["reference_ownership"] = cast["reference_ownership"]
        manifest.write_text(json.dumps(prior, ensure_ascii=False, indent=2) + "\n")
        print(f"Updated {manifest}")


if __name__ == "__main__":
    main()
