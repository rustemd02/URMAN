#!/usr/bin/env python3
"""Small, explicit Meshy API client for local image/GLB assets, rigging, and animation.

Expected paid usage (official API pricing checked 2026-10-20):
  submit-image: 30 credits — meshy-7.1, standard geometry, 4K PBR texture;
    --geometry-only costs 20 credits.
  submit-multi-image: 30 credits — meshy-7.1, 1–4 geometry views, standard
    geometry, 4K PBR texture; --geometry-only costs 20 credits.
  submit-retexture: 10 credits — 4K PBR; optional 8K costs 15 credits.
  submit-remesh: 5 credits — one 100,000-face remesh task by default.
  submit-rig: 5 credits — rig one successful textured model task.
One image model plus one default remesh, retexture and rig is 50 credits.
Animation applies 1–10 unique preset actions to a successful rig task in one
file, with one clip per action at 3 credits per action.
Each submit verb prints its estimate and requires the matching
--spend-credits value before its single POST. Meshy reports actual
consumed_credits on task status. No retries.

The CLI intentionally has no text-to-3D or image-generation commands.
API reference: https://docs.meshy.ai/en/api
Pricing: https://docs.meshy.ai/en/api/pricing
Animation: https://docs.meshy.ai/en/api/animation
"""

import argparse
import base64
import hashlib
import json
import math
import os
import re
import ssl
import stat
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from urllib.parse import urlsplit


API_BASE = "https://api.meshy.ai/openapi/v1"
KEY_PATH = Path.home() / ".config" / "urman-meshy" / "api-key"
REQUEST_TIMEOUT_SECONDS = 60
MAX_JSON_BYTES = 8 * 1024 * 1024
MAX_IMAGE_BYTES = 20_000_000
MAX_MODEL_DATA_URI_BYTES = 100_000_000
TASK_TYPES = ("image-to-3d", "multi-image-to-3d", "retexture", "remesh", "rigging")
TASK_ENDPOINTS = {task_type: task_type for task_type in TASK_TYPES}
TASK_ENDPOINTS["animation"] = "animations"
TASK_CHOICES = tuple(TASK_ENDPOINTS)
TASK_OBJECT_TYPES = {*TASK_TYPES, "rig", "animate"}
TASK_STATUSES = {"PENDING", "IN_PROGRESS", "SUCCEEDED", "FAILED", "CANCELED"}
FORMATS = {"glb", "gltf", "obj", "fbx", "stl", "usdz", "blend", "3mf", "pre_remeshed_glb"}
DOWNLOAD_FORMATS = {
    "glb": ("model/gltf-binary, application/octet-stream", (b"glTF",)),
    "fbx": ("application/octet-stream", (b"Kaydara FBX Binary  \x00\x1a\x00", b"; FBX")),
}
TASK_ID_RE = re.compile(r"[A-Za-z0-9_-]{1,128}")
IMAGE_INPUTS = {
    ".png": ("image/png", b"\x89PNG\r\n\x1a\n"),
    ".jpg": ("image/jpeg", b"\xff\xd8\xff"),
    ".jpeg": ("image/jpeg", b"\xff\xd8\xff"),
    ".webp": ("image/webp", ((0, b"RIFF"), (8, b"WEBP"))),
}


class ClientError(Exception):
    """Safe-to-display error that never includes credentials or signed URLs."""


def load_key():
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
    try:
        fd = os.open(KEY_PATH, flags)
    except OSError as error:
        raise ClientError("Meshy API key file is unavailable at ~/.config/urman-meshy/api-key") from error
    try:
        info = os.fstat(fd)
        if not stat.S_ISREG(info.st_mode):
            raise ClientError("Meshy API key must be a regular file")
        if os.name != "nt" and stat.S_IMODE(info.st_mode) & 0o077:
            raise ClientError("Meshy API key file permissions must be private (0600)")
        raw = os.read(fd, 8193)
        if len(raw) > 8192:
            raise ClientError("Meshy API key file is unexpectedly large")
    finally:
        os.close(fd)
    try:
        key = raw.decode("ascii").strip()
    except UnicodeDecodeError as error:
        raise ClientError("Meshy API key file is not valid ASCII") from error
    if not key or any(character.isspace() for character in key):
        raise ClientError("Meshy API key file must contain one non-empty key")
    return key


def verified_tls_context():
    if sys.platform == "darwin":
        system_ca = Path("/etc/ssl/cert.pem")
        if not system_ca.is_file():
            raise ClientError("macOS system CA bundle /etc/ssl/cert.pem is unavailable")
        try:
            context = ssl.create_default_context(cafile=str(system_ca))
        except (OSError, ssl.SSLError) as error:
            raise ClientError("could not load macOS system CA bundle") from error
    else:
        context = ssl.create_default_context()
    if context.verify_mode != ssl.CERT_REQUIRED or not context.check_hostname:
        raise ClientError("TLS certificate and hostname verification must remain enabled")
    return context


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, file_pointer, code, message, headers, new_url):
        return None


def is_trusted_meshy_https_url(value):
    try:
        parsed = urlsplit(value)
        hostname = (parsed.hostname or "").lower()
        return (parsed.scheme == "https" and hostname.endswith(".meshy.ai")
                and parsed.username is None and parsed.password is None
                and parsed.port in (None, 443))
    except ValueError:
        return False


class MeshyHttpsRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, file_pointer, code, message, headers, new_url):
        if not is_trusted_meshy_https_url(new_url):
            return None
        return super().redirect_request(request, file_pointer, code, message, headers, new_url)


class MeshyClient:
    def __init__(self, key):
        self.key = key
        context = verified_tls_context()
        self.api_opener = urllib.request.build_opener(
            NoRedirect(), urllib.request.HTTPSHandler(context=context)
        )
        self.download_opener = urllib.request.build_opener(
            MeshyHttpsRedirect(), urllib.request.HTTPSHandler(context=context)
        )

    def request_json(self, method, path, payload=None):
        body = None if payload is None else json.dumps(
            payload, separators=(",", ":"), ensure_ascii=True
        ).encode("utf-8")
        request = urllib.request.Request(
            API_BASE + path,
            data=body,
            method=method,
            headers={
                "Authorization": "Bearer " + self.key,
                "Accept": "application/json",
                "User-Agent": "urman-meshy-assets/1.0",
                **({"Content-Type": "application/json"} if body is not None else {}),
            },
        )
        try:
            with self.api_opener.open(request, timeout=REQUEST_TIMEOUT_SECONDS) as response:
                raw = response.read(MAX_JSON_BYTES + 1)
        except urllib.error.HTTPError as error:
            raise ClientError(f"Meshy API returned HTTP {error.code}; response body was not logged") from None
        except (urllib.error.URLError, TimeoutError, OSError, ssl.SSLError):
            raise ClientError("Meshy API request failed; credentials and response details were not logged") from None
        if len(raw) > MAX_JSON_BYTES:
            raise ClientError("Meshy API response exceeded the 8 MiB safety limit")
        try:
            return json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            raise ClientError("Meshy API response was not valid JSON") from None

    def download(self, signed_url, destination, file_format="glb"):
        if not is_trusted_meshy_https_url(signed_url):
            raise ClientError("Meshy returned a model URL outside the trusted HTTPS Meshy domain")
        download_format = DOWNLOAD_FORMATS.get(file_format)
        if download_format is None:
            raise ClientError("unsupported model download format")
        accept, signatures = download_format
        if os.path.lexists(destination):
            raise ClientError("download destination already exists")
        if not destination.parent.is_dir():
            raise ClientError("download destination parent directory does not exist")
        request = urllib.request.Request(
            signed_url,
            headers={"Accept": accept, "User-Agent": "urman-meshy-assets/1.0"},
        )
        digest = hashlib.sha256()
        byte_count = 0
        created = False
        try:
            with self.download_opener.open(request, timeout=REQUEST_TIMEOUT_SECONDS) as response:
                with destination.open("xb") as output:
                    created = True
                    first = response.read(max(map(len, signatures)))
                    if not any(first.startswith(signature) for signature in signatures):
                        raise ClientError(f"downloaded asset is not a valid {file_format.upper()} file")
                    output.write(first)
                    digest.update(first)
                    byte_count = len(first)
                    while True:
                        chunk = response.read(1024 * 1024)
                        if not chunk:
                            break
                        output.write(chunk)
                        digest.update(chunk)
                        byte_count += len(chunk)
        except ClientError:
            if created:
                destination.unlink(missing_ok=True)
            raise
        except urllib.error.HTTPError as error:
            if created:
                destination.unlink(missing_ok=True)
            raise ClientError(f"Meshy model download returned HTTP {error.code}; signed URL was not logged") from None
        except (urllib.error.URLError, TimeoutError, OSError, ssl.SSLError):
            if created:
                destination.unlink(missing_ok=True)
            raise ClientError("Meshy model download failed; signed URL was not logged") from None
        return byte_count, digest.hexdigest()


def data_uri(path, expected_suffix, mime_type, maximum_bytes, signature=None, include_metadata=False):
    source = Path(path).expanduser().resolve(strict=True)
    if not source.is_file() or source.suffix.lower() != expected_suffix:
        raise ClientError(f"input must be a local {expected_suffix} file")
    try:
        source_size = source.stat().st_size
    except OSError as error:
        raise ClientError("could not read local input file") from error
    if source_size > maximum_bytes:
        raise ClientError(f"local input must be non-empty and no larger than {maximum_bytes} bytes")
    try:
        payload = source.read_bytes()
    except OSError as error:
        raise ClientError("could not read local input file") from error
    if not payload or len(payload) > maximum_bytes:
        raise ClientError(f"local input must be non-empty and no larger than {maximum_bytes} bytes")
    if signature is not None:
        signature_checks = ((0, signature),) if isinstance(signature, bytes) else signature
        if not all(payload[offset:offset + len(marker)] == marker
                   for offset, marker in signature_checks):
            raise ClientError(f"local input does not have a valid {expected_suffix} file signature")
    value = "data:" + mime_type + ";base64," + base64.b64encode(payload).decode("ascii")
    if include_metadata:
        return value, {"bytes": len(payload), "sha256": hashlib.sha256(payload).hexdigest()}
    return value


def mesh_image_data_uri(path):
    suffix = Path(path).suffix.lower()
    image_input = IMAGE_INPUTS.get(suffix)
    if image_input is None:
        raise ClientError("multi-image inputs must be local PNG, JPEG, or WebP files")
    mime_type, signature = image_input
    return data_uri(path, suffix, mime_type, MAX_IMAGE_BYTES, signature=signature,
                    include_metadata=True)


def require_task_id(value):
    if not isinstance(value, str) or not TASK_ID_RE.fullmatch(value):
        raise ClientError("task ID must contain only URL-safe letters, digits, underscore, or hyphen")
    return value


def safe_task(task):
    if not isinstance(task, dict):
        raise ClientError("Meshy returned an unexpected task object")
    result = {}
    task_id = task.get("id")
    if isinstance(task_id, str) and TASK_ID_RE.fullmatch(task_id):
        result["id"] = task_id
    task_type = task.get("type")
    if isinstance(task_type, str) and task_type in TASK_OBJECT_TYPES:
        result["type"] = task_type
    status = task.get("status")
    if isinstance(status, str) and status in TASK_STATUSES:
        result["status"] = status
    for key in ("progress", "created_at", "started_at", "finished_at", "expires_at", "consumed_credits"):
        value = task.get(key)
        if type(value) is int and value >= 0:
            result[key] = value
    model_urls = task.get("model_urls")
    if isinstance(model_urls, dict):
        result["available_formats"] = sorted(
            name for name in model_urls if isinstance(name, str) and name in FORMATS
        )
    result_urls = task.get("result")
    if isinstance(result_urls, dict):
        output_keys = {
            "rigged_character_glb_url": "glb",
            "rigged_character_fbx_url": "fbx",
            "animation_glb_url": "glb",
            "animation_fbx_url": "fbx",
        }
        available = set(result.get("available_formats", []))
        available.update(
            format_name for key, format_name in output_keys.items()
            if isinstance(result_urls.get(key), str)
        )
        if available:
            result["available_formats"] = sorted(available)
    texture_urls = task.get("texture_urls")
    if isinstance(texture_urls, list):
        result["texture_set_count"] = len(texture_urls)
    return result


def safe_task_list(tasks):
    if not isinstance(tasks, list):
        raise ClientError("Meshy returned an unexpected task list")
    return [safe_task(task) for task in tasks]


def print_json(value):
    print(json.dumps(value, indent=2, sort_keys=True))


def require_spend_ack(operation, credits, acknowledged):
    print(f"Estimated Meshy cost: {credits} credits for {operation}.", file=sys.stderr, flush=True)
    if acknowledged != credits:
        raise ClientError(f"paid POST refused; pass --spend-credits {credits} to match this estimate")


def add_task_type_argument(parser, choices=TASK_CHOICES):
    parser.add_argument("task_type", choices=choices)


def get_download_url(task, task_type, file_format):
    if task_type == "rigging":
        result = task.get("result")
        key = {"glb": "rigged_character_glb_url", "fbx": "rigged_character_fbx_url"}.get(file_format)
        return result.get(key) if isinstance(result, dict) and key else None
    if task_type == "animation":
        result = task.get("result")
        key = {"glb": "animation_glb_url", "fbx": "animation_fbx_url"}.get(file_format)
        return result.get(key) if isinstance(result, dict) and key else None
    model_urls = task.get("model_urls")
    return model_urls.get(file_format) if isinstance(model_urls, dict) else None


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("balance", help="read available and monthly credits")

    list_parser = commands.add_parser("list", help="list recent tasks by API type")
    add_task_type_argument(list_parser)
    list_parser.add_argument("--page-num", type=int, default=1)
    list_parser.add_argument("--page-size", type=int, default=10)
    list_parser.add_argument("--sort-by", choices=("-created_at", "+created_at"), default="-created_at")

    status_parser = commands.add_parser("status", help="read one task without printing signed URLs")
    add_task_type_argument(status_parser)
    status_parser.add_argument("task_id")

    image_parser = commands.add_parser("submit-image", help="create a meshy-7.1 image-to-3D task")
    image_parser.add_argument("image", type=Path, help="local PNG image (maximum 20 MB)")
    image_parser.add_argument("--geometry-only", action="store_true", help="skip texture generation; estimated cost 20 credits")
    image_parser.add_argument("--spend-credits", type=int, help="must equal the printed estimate before a paid task is created")

    multi_image_parser = commands.add_parser(
        "submit-multi-image", help="create a meshy-7.1 model from 1–4 local views"
    )
    multi_image_parser.add_argument(
        "images", nargs="+", type=Path,
        help="1–4 local PNG, JPEG, or WebP views; the first image is the front view (maximum 20 MB each)",
    )
    multi_image_parser.add_argument(
        "--texture-image", type=Path,
        help="optional single local PNG, JPEG, or WebP reference for generated textures (maximum 20 MB)",
    )
    multi_image_parser.add_argument(
        "--geometry-only", action="store_true",
        help="skip texture generation; estimated cost 20 credits and disallows --texture-image",
    )
    multi_image_parser.add_argument(
        "--spend-credits", type=int,
        help="must equal the printed estimate before a paid task is created",
    )

    retexture_parser = commands.add_parser("submit-retexture", help="create a PBR retexture task from a local GLB")
    retexture_parser.add_argument("model", type=Path, help="local GLB model (maximum 100 MB)")
    style = retexture_parser.add_mutually_exclusive_group(required=True)
    style.add_argument("--style-prompt", help="Meshy retexture style prompt, up to 800 characters")
    style.add_argument("--style-image", type=Path, help="local PNG style reference (maximum 20 MB)")
    retexture_parser.add_argument("--texture-resolution", choices=("4k", "8k"), default="4k")
    retexture_parser.add_argument("--keep-uv", action="store_true", help="preserve the input model's UV layout")
    retexture_parser.add_argument("--spend-credits", type=int, help="must equal the printed estimate before a paid task is created")

    remesh_parser = commands.add_parser("submit-remesh", help="remesh a local GLB or an existing successful Meshy task")
    remesh_parser.add_argument("model", nargs="?", type=Path, help="local GLB model (maximum 100 MB)")
    remesh_parser.add_argument("--input-task-id", help="successful Image-to-3D, Text-to-3D or Retexture task; avoids re-uploading its model")
    remesh_parser.add_argument("--target-polycount", type=int, default=100000)
    remesh_parser.add_argument("--spend-credits", type=int, help="must equal the printed estimate before a paid task is created")

    rig_parser = commands.add_parser("submit-rig", help="create a rigging task from a successful textured Meshy task")
    rig_parser.add_argument("--input-task-id", required=True, help="successful textured model task ID; model must have at most 300,000 faces")
    rig_parser.add_argument("--height-meters", required=True, type=float, help="approximate character height; must be positive")
    rig_parser.add_argument("--spend-credits", type=int, help="must equal the 5-credit estimate before a paid task is created")

    animation_parser = commands.add_parser("submit-animation", help="apply 1–10 preset clips to a successful rig task")
    animation_parser.add_argument("--rig-task-id", required=True, help="successful rigging task ID")
    animation_parser.add_argument(
        "--action-ids", required=True, nargs="+", type=int,
        help="1–10 unique IDs from GET /animations/library; one clip is added per ID in this order",
    )
    animation_parser.add_argument("--spend-credits", type=int, help="must equal 3 credits per requested action")

    download_parser = commands.add_parser("download-glb", help="download GLB only after task status is SUCCEEDED")
    add_task_type_argument(download_parser)
    download_parser.add_argument("task_id")
    download_parser.add_argument("--output", type=Path, required=True, help="new local file path; existing files are never overwritten")

    fbx_parser = commands.add_parser("download-fbx", help="download rigged or animated FBX only after task status is SUCCEEDED")
    add_task_type_argument(fbx_parser, choices=("rigging", "animation"))
    fbx_parser.add_argument("task_id")
    fbx_parser.add_argument("--output", type=Path, required=True, help="new local file path; existing files are never overwritten")
    return parser


def run(args):
    if args.command == "list" and (args.page_num < 1 or not 1 <= args.page_size <= 100):
        raise ClientError("page-num must be positive and page-size must be from 1 to 100")
    if args.command == "submit-retexture" and args.style_prompt is not None:
        if not args.style_prompt.strip() or len(args.style_prompt) > 800:
            raise ClientError("style-prompt must be non-empty and at most 800 characters")
    if args.command == "submit-remesh" and not 100 <= args.target_polycount <= 300000:
        raise ClientError("target-polycount must be from 100 to 300000")
    if args.command == "submit-rig":
        args.input_task_id = require_task_id(args.input_task_id)
        if not math.isfinite(args.height_meters) or args.height_meters <= 0:
            raise ClientError("height-meters must be a positive finite number")
    if args.command == "submit-animation":
        args.rig_task_id = require_task_id(args.rig_task_id)
        if not 1 <= len(args.action_ids) <= 10:
            raise ClientError("animation requires from 1 to 10 action IDs")
        if any(action_id < 0 for action_id in args.action_ids):
            raise ClientError("animation action IDs must be non-negative integers")
        if len(set(args.action_ids)) != len(args.action_ids):
            raise ClientError("animation action IDs must be unique")
    if args.command == "submit-multi-image":
        if not 1 <= len(args.images) <= 4:
            raise ClientError("multi-image generation requires from 1 to 4 geometry views")
        if args.geometry_only and args.texture_image is not None:
            raise ClientError("--texture-image requires texturing and cannot be used with --geometry-only")
        geometry_views = []
        input_receipt = []
        for index, image_path in enumerate(args.images):
            image, metadata = mesh_image_data_uri(image_path)
            geometry_views.append(image)
            input_receipt.append({
                "role": "geometry_view_front" if index == 0 else f"geometry_view_{index + 1}",
                **metadata,
            })
        texture_image = None
        if args.texture_image is not None:
            texture_image, metadata = mesh_image_data_uri(args.texture_image)
            input_receipt.append({"role": "texture_reference", **metadata})
        args.multi_image_payload = {
            "image_urls": geometry_views,
            "ai_model": "meshy-7.1",
            "geometry_resolution": "standard",
            "should_texture": not args.geometry_only,
            "should_remesh": False,
            "target_formats": ["glb"],
            "image_enhancement": False,
        }
        if not args.geometry_only:
            args.multi_image_payload.update({
                "enable_pbr": True,
                "texture_resolution": "4k",
            })
            if texture_image is not None:
                args.multi_image_payload["texture_image_url"] = texture_image
        args.multi_image_credits = 20 if args.geometry_only else 30
        args.multi_image_input_receipt = input_receipt
    if args.command == "submit-remesh":
        if (args.model is None) == (args.input_task_id is None):
            raise ClientError("provide exactly one local model or --input-task-id")
        if args.input_task_id is not None:
            args.input_task_id = require_task_id(args.input_task_id)

    if args.command in ("download-glb", "download-fbx"):
        args.task_id = require_task_id(args.task_id)
        destination = args.output.expanduser().absolute()
        if os.path.lexists(destination):
            raise ClientError("download destination already exists")
        if not destination.parent.is_dir():
            raise ClientError("download destination parent directory does not exist")

    client = MeshyClient(load_key())
    if args.command == "balance":
        value = client.request_json("GET", "/balance")
        if not isinstance(value, dict):
            raise ClientError("Meshy returned an unexpected balance object")
        result = {}
        for name in ("balance", "monthly_credits", "permanent_credits", "monthly_resets_at"):
            item = value.get(name)
            if name == "monthly_resets_at" and item is None:
                result[name] = None
            elif type(item) is int and item >= 0:
                result[name] = item
        print_json(result)
        return 0

    if args.command == "list":
        query = urllib.parse.urlencode({
            "page_num": args.page_num,
            "page_size": args.page_size,
            "sort_by": args.sort_by,
        })
        tasks = client.request_json("GET", "/" + TASK_ENDPOINTS[args.task_type] + "?" + query)
        print_json(safe_task_list(tasks))
        return 0

    if args.command == "status":
        task_id = require_task_id(args.task_id)
        task = client.request_json("GET", "/" + TASK_ENDPOINTS[args.task_type] + "/" + task_id)
        print_json(safe_task(task))
        return 0

    if args.command == "submit-image":
        image = data_uri(args.image, ".png", "image/png", MAX_IMAGE_BYTES,
                         signature=b"\x89PNG\r\n\x1a\n")
        credits = 20 if args.geometry_only else 30
        operation = "meshy-7.1 image-to-3D geometry-only" if args.geometry_only else "meshy-7.1 image-to-3D with 4K PBR textures"
        require_spend_ack(operation, credits, args.spend_credits)
        payload = {
            "image_url": image,
            "model_type": "standard",
            "ai_model": "meshy-7.1",
            "should_texture": not args.geometry_only,
            "should_remesh": False,
            "target_formats": ["glb"],
        }
        if not args.geometry_only:
            payload.update({"texture_resolution": "4k", "enable_pbr": True})
        task = client.request_json("POST", "/image-to-3d", payload)
        result = task.get("result") if isinstance(task, dict) else None
        task_id = require_task_id(result) if isinstance(result, str) else None
        if task_id is None:
            raise ClientError("Meshy accepted no recognizable task ID; response details were not logged")
        print_json({"task_id": task_id, "estimated_credits": credits})
        return 0

    if args.command == "submit-multi-image":
        credits = args.multi_image_credits
        mode = "geometry-only" if args.geometry_only else "4K PBR textures"
        require_spend_ack(f"meshy-7.1 multi-image-to-3D with {mode}", credits, args.spend_credits)
        task = client.request_json("POST", "/multi-image-to-3d", args.multi_image_payload)
        result = task.get("result") if isinstance(task, dict) else None
        task_id = require_task_id(result) if isinstance(result, str) else None
        if task_id is None:
            raise ClientError("Meshy accepted no recognizable task ID; response details were not logged")
        print_json({
            "task_id": task_id,
            "estimated_credits": credits,
            "inputs": args.multi_image_input_receipt,
        })
        return 0

    if args.command == "submit-rig":
        credits = 5
        require_spend_ack("automatic rigging", credits, args.spend_credits)
        task = client.request_json("POST", "/rigging", {
            "input_task_id": args.input_task_id,
            "height_meters": args.height_meters,
        })
        result = task.get("result") if isinstance(task, dict) else None
        task_id = require_task_id(result) if isinstance(result, str) else None
        if task_id is None:
            raise ClientError("Meshy accepted no recognizable task ID; response details were not logged")
        print_json({"task_id": task_id, "estimated_credits": credits})
        return 0

    if args.command == "submit-animation":
        credits = 3 * len(args.action_ids)
        require_spend_ack(f"{len(args.action_ids)} preset animation clip(s)", credits, args.spend_credits)
        task = client.request_json("POST", "/animations", {
            "rig_task_id": args.rig_task_id,
            "action_ids": args.action_ids,
        })
        result = task.get("result") if isinstance(task, dict) else None
        task_id = require_task_id(result) if isinstance(result, str) else None
        if task_id is None:
            raise ClientError("Meshy accepted no recognizable animation task ID; response details were not logged")
        print_json({"task_id": task_id, "estimated_credits": credits, "action_ids": args.action_ids})
        return 0

    if args.command == "submit-retexture":
        model = data_uri(args.model, ".glb", "application/octet-stream", MAX_MODEL_DATA_URI_BYTES,
                         signature=b"glTF")
        style_payload = ({"text_style_prompt": args.style_prompt} if args.style_prompt is not None else {
            "image_style_url": data_uri(args.style_image, ".png", "image/png", MAX_IMAGE_BYTES,
                                         signature=b"\x89PNG\r\n\x1a\n")
        })
        credits = 10 if args.texture_resolution == "4k" else 15
        require_spend_ack(f"{args.texture_resolution.upper()} PBR retexture", credits, args.spend_credits)
        payload = {
            "model_url": model,
            **style_payload,
            "ai_model": "meshy-7",
            "texture_resolution": args.texture_resolution,
            "enable_pbr": True,
            "enable_original_uv": args.keep_uv,
            "target_formats": ["glb"],
        }
        task = client.request_json("POST", "/retexture", payload)
        result = task.get("result") if isinstance(task, dict) else None
        task_id = require_task_id(result) if isinstance(result, str) else None
        if task_id is None:
            raise ClientError("Meshy accepted no recognizable task ID; response details were not logged")
        print_json({"task_id": task_id, "estimated_credits": credits})
        return 0

    if args.command == "submit-remesh":
        source = ({"input_task_id": args.input_task_id} if args.input_task_id is not None else
                  {"model_url": data_uri(args.model, ".glb", "application/octet-stream", MAX_MODEL_DATA_URI_BYTES,
                                         signature=b"glTF")})
        credits = 5
        require_spend_ack(f"{args.target_polycount:,}-face triangle remesh", credits, args.spend_credits)
        task = client.request_json("POST", "/remesh", {
            **source,
            "target_formats": ["glb"],
            "topology": "triangle",
            "target_polycount": args.target_polycount,
        })
        result = task.get("result") if isinstance(task, dict) else None
        task_id = require_task_id(result) if isinstance(result, str) else None
        if task_id is None:
            raise ClientError("Meshy accepted no recognizable task ID; response details were not logged")
        print_json({"task_id": task_id, "estimated_credits": credits})
        return 0

    if args.command in ("download-glb", "download-fbx"):
        task = client.request_json("GET", "/" + TASK_ENDPOINTS[args.task_type] + "/" + args.task_id)
        safe_task(task)
        if task.get("status") != "SUCCEEDED":
            raise ClientError("task must be SUCCEEDED before model download")
        file_format = "glb" if args.command == "download-glb" else "fbx"
        signed_url = get_download_url(task, args.task_type, file_format)
        if not isinstance(signed_url, str):
            raise ClientError(f"successful task has no {file_format.upper()} output")
        byte_count, digest = client.download(signed_url, destination, file_format)
        print_json({"path": str(destination), "bytes": byte_count, "sha256": digest})
        return 0

    raise ClientError("unsupported command")


def main():
    parser = build_parser()
    args = parser.parse_args()
    try:
        return run(args)
    except (ClientError, FileNotFoundError, PermissionError, OSError) as error:
        print(f"meshy-assets: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
