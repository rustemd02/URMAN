"""Private, single-job URMAN worker. Start in an interactive user session."""
import argparse
import ctypes
import datetime
import hashlib
import hmac
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import math
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import threading
import time
import uuid
import zipfile

from remote_common import (MAX_ARCHIVE, MAX_BLOB, MAX_CANDIDATE_RECEIPT, MAX_RESULT_BYTES,
                           MAX_RESULT_FILES, STUDIO_AUTHORING_FILES, canonical, digest,
                           unpack_candidate, unpack_snapshot, validate_candidate_manifest,
                           validate_candidate_guard_project, validate_candidate_receipt,
                           validate_manifest, verify_source)

# Incremental snapshots: the station keeps one blob per distinct file content, so a
# new build sends only the files that actually changed instead of the whole tree.
KEEP_MANIFESTS = 50
KEEP_CANDIDATES = 3
KEEP_ARCHIVES = 1        # the newest archive is what an empty blob store is seeded from
BLOB_GRACE = 24 * 3600   # content a client may still be uploading is not collected
STUDIO_SCENE = 'res://studio/studio_main.tscn'
FULL_GAME_SCENE = 'res://scenes/full_game.tscn'
ACT1_DEMO_SCENE = 'res://scenes/act1_demo.tscn'
PHOTO_WORLDS_GAME_SCENE = 'res://scenes/photoworlds_game.tscn'
APPROVED_PLAY_SCENES = (ACT1_DEMO_SCENE, FULL_GAME_SCENE, PHOTO_WORLDS_GAME_SCENE)
NORMAL_UI_TRANSPORT_VERSION = 4
UI_KEY_BASES = ('{ENTER}', '{ESC}', '{DOWN}', '{UP}', '{SPACE}', 'a', 'f', 'v', 'z')
UI_KEY_ADDITIONS = ('c', 'j', '{TAB}', '+{TAB}', '{LEFT}', '{RIGHT}')
UI_KEY_CHORDS = frozenset(
    [*UI_KEY_BASES,
     *(modifier + key for modifier in ('^', '+', '^+') for key in UI_KEY_BASES),
     *UI_KEY_ADDITIONS])
UI_MOVE_KEYS = frozenset(('w', 'a', 's', 'd', 'e'))
UI_LOOK_DELTA_LIMIT = 300
UI_HELPER_FAILURE_CODES = {
    'The requested game window is not verified.': 'ui_window_unverified',
    'The owned game window changed process.': 'ui_window_process_changed',
    'The owned game window title changed.': 'ui_window_title_changed',
    'The owned game window lost foreground focus.': 'ui_window_focus_lost',
    'The helper could not attach to the foreground input thread.': 'ui_focus_attach_failed',
    'The foreground window changed during focus acquisition.': 'ui_focus_foreground_changed',
    'Windows refused game-window activation.': 'ui_focus_activation_refused',
    'The helper could not detach from the foreground input thread.': 'ui_focus_detach_failed',
    'The owned game caption point is unavailable.': 'ui_focus_caption_point_unavailable',
    'The owned game caption bounds changed.': 'ui_focus_caption_bounds_changed',
    'The owned game caption hit window changed.': 'ui_focus_caption_hit_window_changed',
    'The owned game caption hit owner changed.': 'ui_focus_caption_hit_owner_changed',
    'The pointer moved during the pre-move caption check.': 'ui_focus_caption_pointer_moved_pre_move',
    'The owned game caption point changed after positioning.': 'ui_focus_caption_point_changed_post_move',
    'The pointer moved after caption positioning.': 'ui_focus_caption_pointer_moved_post_move',
    'Windows refused to raise the owned game window.': 'ui_focus_raise_refused',
    'The pointer moved during the caption hit test.': 'ui_focus_caption_pointer_moved_post_hit_test',
    'The owned game caption pointer is unavailable.': 'ui_focus_caption_pointer_unavailable',
    'The owned game caption input was refused.': 'ui_focus_caption_input_refused',
    'The owned game window lost foreground focus after activation.': 'ui_focus_post_activation_lost',
    'The owned game window is minimized.': 'ui_window_minimized',
    'The owned game window is hidden.': 'ui_window_hidden',
    'The owned game window bounds are unavailable.': 'ui_window_bounds_unavailable',
    'The owned game window bounds changed during the request.': 'ui_window_bounds_changed',
    'The owned game window bounds are outside the supported range.': 'ui_window_bounds_invalid',
    'Expected exactly one approved URMAN window for the owned Godot PID.': 'ui_window_missing_or_ambiguous',
    'The selected window is not owned by the requested PID.': 'ui_process_guard_refused',
    'The requested PID is not one live Godot process.': 'ui_process_guard_refused',
    'The owned Godot command must have exactly one --path argument.': 'ui_source_guard_refused',
    'The requested Godot PID is not running this job source.': 'ui_source_guard_refused',
    'The candidate executable does not match this job.': 'ui_process_guard_refused',
    'The candidate executable hash changed.': 'ui_process_guard_refused',
    'The candidate process is not owned by the live runner.': 'ui_process_guard_refused',
    'Unsupported game UI key.': 'ui_action_refused',
    'Unsupported game UI action.': 'ui_action_refused',
    'Look deltas must be non-zero integers between -300 and 300.': 'ui_action_refused',
}
UI_HELPER_REASON_CODES = frozenset(UI_HELPER_FAILURE_CODES.values()) | frozenset({
    'ui_helper_launch_failed', 'ui_helper_timeout', 'ui_helper_invalid_result', 'ui_helper_refused',
})
UI_ACTION_LIMIT = 4096
UI_HELPER_TIMEOUT_SECONDS = 20
INTERACTIVE_FRAME_TIMEOUT_SECONDS = 30
INTERACTIVE_FRAME_REQUEST_LIMIT = 1024
INTERACTIVE_FRAME_RESULT_LIMIT = 8 * 1024
INTERACTIVE_FRAME_PNG_LIMIT = 16 * 1024**2
INTERACTIVE_FRAME_DIMENSION_LIMIT = 4096
STUDIO_OWNER = 'urman.world:act1/kit/house-old-pc-babai-approach'
STUDIO_DIRECT_FILE_OWNERS = {
    'urman.studio:shop-catalog': 'game/content/urman.shop.v1.json',
    'urman.studio:vehicle-catalog': 'game/content/vehicles/act1_vehicles.v1.json',
}
STUDIO_CHECKPOINT_OWNER_PREFIX = 'urman.studio:checkpoint/'
STUDIO_CHECKPOINT_BASE_PATHS = (
    'assets/source/blender/act1/urman_village_exterior_kit.py',
    'assets/source/blender/act1/urman_village_exterior_kit.blend',
    'game/assets/models/act1/urman_village_exterior_kit.glb',
)
STUDIO_CHECKPOINT_SOURCE_ROOTS = (
    'content/modules', 'content/campaigns', 'content/studio', 'game/content/world',
    'game/content/cutscenes', 'game/content/animations',
)
STUDIO_CHECKPOINT_HERO_RECIPE = 'game/content/studio/hero_house.recipe.json'
STUDIO_ROAD_OPERATION = 'road.segment.smooth'
STUDIO_ROAD_OWNER_PREFIX = 'urman.studio:' + STUDIO_ROAD_OPERATION + '/'
STUDIO_ROAD_SOURCE_PATHS = frozenset((
    'game/content/world/act1_open_part.world.v1.json',
    'game/content/world/act1_far_bank.world.v1.json',
))
STUDIO_PRESERVE_MARKER = '.preserve-source'
STUDIO_MAX_REQUESTS = 32
STUDIO_MAX_TRANSACTIONS = 32
STUDIO_MAX_RUNS = 32
STUDIO_MAX_REQUEST = 256 * 1024
STUDIO_MAX_PROPOSAL = 32 * 1024
STUDIO_MAX_PREVIEW_FAILURE = 4 * 1024
STUDIO_MAX_ACTIONS = 32
STUDIO_MAX_ACTION_RECEIPT = 8 * 1024
STUDIO_MAX_ACTION_FRAME = 16 * 1024**2
STUDIO_MAX_JOURNAL = 64 * 1024
STUDIO_MAX_CHECKPOINT_JOURNAL = 8 * 1024**2
STUDIO_MAX_CHECKPOINTS = 32
STUDIO_MAX_CHECKPOINT_FILES = 2000
STUDIO_MAX_CHECKPOINT_BYTES = 8 * 1024**2
STUDIO_MAX_AUTHORED_JSON = 8 * 1024**2
STUDIO_MAX_ROAD_SOURCE = 512 * 1024
STUDIO_MAX_RUN_MANIFEST = 16 * 1024
STUDIO_MAX_RUN_LOG = 8 * 1024**2
STUDIO_MAX_RUN_FRAME = STUDIO_MAX_ACTION_FRAME
STUDIO_MAX_RUN_FRAME_JSON = 256 * 1024
STUDIO_HOUSE_WINDOW_IDS = (
    'B20_windows_exterior', 'B20_windows_interior', 'B20_windows_seni',
    'B20_windows_rear', 'B20_windows_left', 'B20_windows_right',
)
STUDIO_RUN_BASE_FILES = ('game.log', 'manifest.json')
STUDIO_RUN_HOUSE_WINDOW_FILES = (
    *STUDIO_RUN_BASE_FILES,
    *(f'{checkpoint}.{extension}' for checkpoint in STUDIO_HOUSE_WINDOW_IDS
      for extension in ('png', 'json')),
    '_capture-summary.json',
)
STUDIO_MAX_BINARY = 128 * 1024**2
STUDIO_MAX_EXPORT = 512 * 1024**2


def _studio_reparse(info):
    return stat.S_ISLNK(info.st_mode) or bool(
        getattr(info, 'st_file_attributes', 0) & getattr(stat, 'FILE_ATTRIBUTE_REPARSE_POINT', 0x0400))


def _studio_path(root, relative, kind='file', missing=False):
    """Check every component below a source/output root without following links."""
    if not isinstance(relative, str) or not relative or '\\' in relative or ':' in relative:
        raise ValueError('invalid Studio evidence path')
    parts = relative.split('/')
    if any(part in ('', '.', '..') for part in parts):
        raise ValueError('invalid Studio evidence path')
    root = Path(root)
    try:
        root_info = root.lstat()
    except OSError as error:
        raise ValueError('Studio evidence root is missing or unreadable') from error
    if _studio_reparse(root_info) or not stat.S_ISDIR(root_info.st_mode):
        raise ValueError('Studio evidence root is not a regular directory')

    current = root
    for index, part in enumerate(parts):
        current = current / part
        try:
            info = current.lstat()
        except FileNotFoundError:
            if missing:
                return None
            raise ValueError('Studio evidence file is missing: ' + relative)
        except OSError as error:
            raise ValueError('Studio evidence path is unreadable: ' + relative) from error
        if _studio_reparse(info):
            raise ValueError('Studio evidence path contains a link or reparse point: ' + relative)
        final = index == len(parts) - 1
        if not final and not stat.S_ISDIR(info.st_mode):
            raise ValueError('Studio evidence path component is not a directory: ' + relative)
        if final and kind == 'file' and not stat.S_ISREG(info.st_mode):
            raise ValueError('Studio evidence path is not a regular file: ' + relative)
        if final and kind == 'dir' and not stat.S_ISDIR(info.st_mode):
            raise ValueError('Studio evidence path is not a directory: ' + relative)
    return current


def _studio_children(directory, limit, label):
    info = Path(directory).lstat()
    if _studio_reparse(info) or not stat.S_ISDIR(info.st_mode):
        raise ValueError(label + ' is not a regular directory')
    children = []
    with os.scandir(directory) as entries:
        for entry in entries:
            if len(children) >= limit:
                raise ValueError(label + ' exceeds its entry limit')
            children.append(Path(entry.path))
    return sorted(children, key=lambda item: item.name)


def _bounded_readiness_command(command, timeout=4, output_limit=8 * 1024):
    """Run one fixed probe inside the same kill-on-close Job Object as guarded runs."""
    if sys.platform != 'win32':
        return None, b'', False
    try:
        import protected_run as guard
        job = guard._windows_create_kill_job()
    except Exception:
        return None, b'', False

    process = None
    assigned = False
    contained = False
    cleanup_ok = True
    launch_failed = False
    reader = None
    reader_started = False
    output = bytearray()
    overflow = threading.Event()
    drained = threading.Event()
    exit_code = None
    failed = False
    try:
        try:
            process = subprocess.Popen(
                command, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                cwd=tempfile.gettempdir(),
                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.CREATE_NO_WINDOW | 0x00000004,
            )
        except OSError:
            launch_failed = True
        if process is not None:
            try:
                guard._windows_assign_process(job, process.pid)
                assigned = True
                guard._windows_resume_primary_thread(process.pid)
                contained = True
            except Exception:
                failed = True

            if contained:
                def drain():
                    try:
                        while True:
                            chunk = process.stdout.read(1024)
                            if not chunk:
                                break
                            if len(output) + len(chunk) > output_limit:
                                overflow.set()
                            elif not overflow.is_set():
                                output.extend(chunk)
                    except (OSError, ValueError):
                        pass
                    finally:
                        try:
                            process.stdout.close()
                        except (OSError, ValueError):
                            pass
                        drained.set()

                try:
                    reader = threading.Thread(target=drain, daemon=True)
                    reader.start()
                    reader_started = True
                except RuntimeError:
                    failed = True
                    reader = None
                if reader_started:
                    deadline = time.monotonic() + timeout
                    while True:
                        if overflow.is_set() or time.monotonic() >= deadline:
                            failed = True
                            break
                        exit_code = process.poll()
                        if exit_code is not None:
                            break
                        time.sleep(0.025)
    except Exception:
        failed = True
    finally:
        process_exited = False
        process_handle_released = False
        if process is not None:
            if assigned:
                if exit_code is None:
                    try:
                        guard._windows_terminate_job(job)
                    except Exception:
                        cleanup_ok = False
                try:
                    exit_code = process.wait(timeout=0.75)
                    process_exited = True
                except subprocess.TimeoutExpired:
                    try:
                        guard._windows_terminate_job(job)
                        exit_code = process.wait(timeout=0.25)
                        process_exited = True
                    except Exception:
                        cleanup_ok = False
                if process_exited:
                    try:
                        process_handle = getattr(process, '_handle', None)
                        close_process_handle = getattr(process_handle, 'Close', None)
                        if not callable(close_process_handle):
                            raise RuntimeError('probe process handle is unavailable')
                        close_process_handle()
                        process_handle_released = True
                    except Exception:
                        cleanup_ok = False
                    else:
                        deadline = time.monotonic() + 0.75
                        while True:
                            try:
                                active = guard._windows_job_active_processes(job)
                            except Exception:
                                cleanup_ok = False
                                break
                            if active == 0:
                                break
                            try:
                                guard._windows_terminate_job(job)
                            except Exception:
                                cleanup_ok = False
                            if time.monotonic() >= deadline:
                                cleanup_ok = False
                                break
                            time.sleep(0.05)
                else:
                    cleanup_ok = False
            else:
                # A child that failed assignment is still suspended and has not run.
                try:
                    if process.poll() is None:
                        process.kill()
                    exit_code = process.wait(timeout=0.75)
                    process_exited = True
                except Exception:
                    cleanup_ok = False
                if process_exited:
                    try:
                        process_handle = getattr(process, '_handle', None)
                        close_process_handle = getattr(process_handle, 'Close', None)
                        if not callable(close_process_handle):
                            raise RuntimeError('probe process handle is unavailable')
                        close_process_handle()
                        process_handle_released = True
                    except Exception:
                        cleanup_ok = False

        try:
            guard._windows_close_handle(job)
        except Exception:
            cleanup_ok = False
        if process is not None and not process_exited:
            try:
                process.wait(timeout=0.25)
                process_exited = True
            except Exception:
                cleanup_ok = False
        if process is not None and process_exited and not process_handle_released:
            try:
                process_handle = getattr(process, '_handle', None)
                close_process_handle = getattr(process_handle, 'Close', None)
                if not callable(close_process_handle):
                    raise RuntimeError('probe process handle is unavailable')
                close_process_handle()
            except Exception:
                cleanup_ok = False
        if reader_started and reader is not None:
            reader.join(timeout=0.5)
            if reader.is_alive():
                cleanup_ok = False
        elif process is not None:
            try:
                process.stdout.close()
            except (OSError, ValueError):
                cleanup_ok = False
            drained.set()

    safe_to_continue = cleanup_ok and (contained or launch_failed)
    if not safe_to_continue:
        return None, b'', False
    if failed or overflow.is_set() or not drained.is_set():
        return None, b'', True
    return exit_code, bytes(output), True


def _studio_json(raw, limit, label):
    if len(raw) > limit:
        raise ValueError(label + ' exceeds its size limit')

    def object_without_duplicates(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(label + ' contains a duplicate JSON field')
            result[key] = value
        return result

    try:
        value = json.loads(raw.decode('utf-8'), object_pairs_hook=object_without_duplicates,
                           parse_constant=lambda _: (_ for _ in ()).throw(ValueError('invalid JSON number')))
    except (UnicodeDecodeError, json.JSONDecodeError, RecursionError) as error:
        raise ValueError(label + ' is not bounded strict UTF-8 JSON') from error
    pending = [(value, 1)]
    while pending:
        item, depth = pending.pop()
        if depth > 64:
            raise ValueError(label + ' exceeds the JSON depth limit')
        if isinstance(item, dict):
            pending.extend((child, depth + 1) for child in item.values())
        elif isinstance(item, list):
            pending.extend((child, depth + 1) for child in item)
    return value


def _studio_read(root, relative, limit, label):
    path = _studio_path(root, relative)
    initial_size = path.lstat().st_size
    if initial_size > limit:
        raise ValueError(label + ' exceeds its size limit')
    # read_bytes() can consume an arbitrarily growing file. Read one byte past
    # the contract limit so concurrent growth is rejected without an unbounded read.
    with path.open('rb') as stream:
        raw = stream.read(limit + 1)
    if len(raw) > limit:
        raise ValueError(label + ' grew beyond its size limit')
    if len(raw) != initial_size or path.lstat().st_size != initial_size:
        raise ValueError(label + ' changed while being read')
    return path, raw


def _studio_hash(path, limit, expected_prefix=None):
    path = Path(path)
    info = path.lstat()
    if _studio_reparse(info) or not stat.S_ISREG(info.st_mode):
        raise ValueError('Studio evidence file is not a regular file')
    size = info.st_size
    if size > limit:
        raise ValueError('Studio evidence file exceeds its size limit')
    h = hashlib.sha256()
    total = 0
    prefix = bytearray()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024**2), b''):
            total += len(chunk)
            if total > limit:
                raise ValueError('Studio authored target grew beyond its size limit')
            if expected_prefix is not None and len(prefix) < len(expected_prefix):
                prefix.extend(chunk[:len(expected_prefix) - len(prefix)])
            h.update(chunk)
    final_info = path.lstat()
    if (_studio_reparse(final_info) or not stat.S_ISREG(final_info.st_mode)
            or total != size or final_info.st_size != size):
        raise ValueError('Studio evidence file changed while being read')
    if expected_prefix is not None and bytes(prefix) != expected_prefix:
        raise ValueError('Studio evidence PNG signature is invalid')
    return size, h.hexdigest()


def _studio_checkpoint_relative_path(relative):
    return (isinstance(relative, str) and bool(relative) and '\\' not in relative and ':' not in relative
            and not any(ord(character) < 32 or 127 <= ord(character) <= 159 for character in relative)
            and all(part and part not in ('.', '..') for part in relative.split('/'))
            and not relative.split('/')[0].startswith('.'))


def _studio_ordinal_key(value):
    # C# StringComparer.Ordinal sorts UTF-16 code units.
    return value.encode('utf-16-be', errors='strict')


def _studio_checkpoint_source_path(relative):
    if not _studio_checkpoint_relative_path(relative):
        return False
    if relative == STUDIO_CHECKPOINT_HERO_RECIPE or relative in STUDIO_DIRECT_FILE_OWNERS.values():
        return True
    if relative.endswith('.json'):
        return any(relative.startswith(root + '/') for root in STUDIO_CHECKPOINT_SOURCE_ROOTS)
    return relative.endswith('.md') and relative.startswith('content/modules/')


def _studio_road_text(value, label, maximum_length):
    if not isinstance(value, str):
        raise ValueError('Studio road request has invalid ' + label)
    try:
        utf16_length = len(value.encode('utf-16-le', errors='strict')) // 2
        value.encode('utf-8', errors='strict')
    except UnicodeEncodeError as error:
        raise ValueError('Studio road request has invalid Unicode in ' + label) from error
    if (not value or utf16_length > maximum_length or value.strip() != value
            or any(ord(character) < 32 or 127 <= ord(character) <= 159 for character in value)):
        raise ValueError('Studio road request has invalid ' + label)
    return value


def _studio_road_number(value, label, *, positive=False):
    if type(value) not in (int, float):
        raise ValueError('Studio road request has invalid ' + label)
    try:
        number = float(value)
    except (OverflowError, ValueError) as error:
        raise ValueError('Studio road request has invalid ' + label) from error
    if (not math.isfinite(number) or abs(number) > 3.4028234663852886e38
            or (positive and number <= 0)):
        raise ValueError('Studio road request has invalid ' + label)
    return number


def _studio_road_json_is_compact(raw):
    quoted = False
    escaped = False
    for byte in raw:
        if quoted:
            if escaped:
                escaped = False
            elif byte == 0x5c:
                escaped = True
            elif byte == 0x22:
                quoted = False
        elif byte == 0x22:
            quoted = True
        elif byte in (0x20, 0x09, 0x0a, 0x0d):
            return False
    return not quoted and not escaped


def _studio_validate_road_record(record, label, maximum_points):
    if (not isinstance(record, dict) or list(record) != ['id', 'street', 'width', 'points']):
        raise ValueError('Studio road request has invalid ' + label)
    _studio_road_text(record['id'], label + ' ID', 256)
    _studio_road_text(record['street'], label + ' street', 256)
    _studio_road_number(record['width'], label + ' width', positive=True)
    points = record['points']
    if not isinstance(points, list) or not 2 <= len(points) <= maximum_points:
        raise ValueError('Studio road request has invalid ' + label + ' points')
    for point in points:
        if not isinstance(point, dict) or list(point) != ['x', 'z']:
            raise ValueError('Studio road request has invalid ' + label + ' point')
        _studio_road_number(point['x'], label + ' point X')
        _studio_road_number(point['z'], label + ' point Z')
    return len(points)


def _studio_validate_road_request(request, request_bytes, request_id):
    """Accept only the frozen road.segment.smooth schema emitted by Core."""
    expected_fields = [
        'kind', 'schemaVersion', 'requestId', 'instruction', 'project',
        'selection', 'source', 'contextRoads', 'capabilities',
    ]
    if (not isinstance(request, dict) or list(request) != expected_fields
            or request.get('kind') != 'urman.studio.authoring-request'
            or type(request.get('schemaVersion')) is not int or request['schemaVersion'] != 3
            or request.get('requestId') != request_id
            or not re.fullmatch('[a-f0-9]{32}', request_id)):
        raise ValueError('Studio road request identity or schema is invalid: ' + request_id)
    if not _studio_road_json_is_compact(request_bytes):
        raise ValueError('Studio road request is not in Core compact JSON form: ' + request_id)

    instruction = request['instruction']
    if not isinstance(instruction, str):
        raise ValueError('Studio road request instruction is invalid: ' + request_id)
    try:
        instruction_bytes = instruction.encode('utf-8', errors='strict')
    except UnicodeEncodeError as error:
        raise ValueError('Studio road request instruction has invalid Unicode: ' + request_id) from error
    if (not 1 <= len(instruction_bytes) <= 16 * 1024
            or any((ord(character) < 32 and character not in '\r\n\t')
                   or 127 <= ord(character) <= 159 for character in instruction)):
        raise ValueError('Studio road request instruction is outside its bounds: ' + request_id)

    project = request['project']
    if (not isinstance(project, dict) or list(project) != ['id', 'snapshotHash']
            or project['id'] != 'github:rustemd02/URMAN'
            or not isinstance(project['snapshotHash'], str)
            or not re.fullmatch('[a-f0-9]{64}', project['snapshotHash'])):
        raise ValueError('Studio road request project identity is invalid: ' + request_id)

    selection = request['selection']
    if (not isinstance(selection, dict)
            or list(selection) != ['kind', 'sourcePath', 'roadId', 'startIndex', 'endIndex']
            or selection['kind'] != 'road-segment'
            or not isinstance(selection['sourcePath'], str)
            or selection['sourcePath'] not in STUDIO_ROAD_SOURCE_PATHS):
        raise ValueError('Studio road request selection is invalid: ' + request_id)
    road_id = _studio_road_text(selection['roadId'], 'selected road ID', 256)
    start_index, end_index = selection['startIndex'], selection['endIndex']
    if type(start_index) is not int or type(end_index) is not int or start_index < 0 or end_index < start_index:
        raise ValueError('Studio road request range is invalid: ' + request_id)

    source = request['source']
    if (not isinstance(source, dict) or list(source) != ['path', 'sha256', 'selectedRoad']
            or source['path'] != selection['sourcePath']
            or not isinstance(source['sha256'], str)
            or not re.fullmatch('[a-f0-9]{64}', source['sha256'])):
        raise ValueError('Studio road request source identity is invalid: ' + request_id)
    selected_points = _studio_validate_road_record(source['selectedRoad'], 'selected road', 8192)
    if (source['selectedRoad']['id'] != road_id or end_index >= selected_points
            or end_index - start_index < 4):
        raise ValueError('Studio road request range does not match the selected road: ' + request_id)

    context = request['contextRoads']
    if not isinstance(context, list) or len(context) > 1024:
        raise ValueError('Studio road request context exceeds its road limit: ' + request_id)
    context_points = 0
    for index, road in enumerate(context):
        context_points += _studio_validate_road_record(road, 'context road ' + str(index), 8192)
        if context_points > 32768:
            raise ValueError('Studio road request context exceeds its point limit: ' + request_id)

    capabilities = request['capabilities']
    if (not isinstance(capabilities, list) or len(capabilities) != 1
            or not isinstance(capabilities[0], dict)
            or list(capabilities[0]) != ['name', 'version']
            or capabilities[0]['name'] != STUDIO_ROAD_OPERATION
            or type(capabilities[0]['version']) is not int or capabilities[0]['version'] != 1):
        raise ValueError('Studio road request capability is invalid: ' + request_id)
    snapshot_hash = project['snapshotHash']
    project_marker = (b',"project":{"id":"github:rustemd02/URMAN","snapshotHash":"'
                      + snapshot_hash.encode('ascii') + b'"},"selection":')
    if request_bytes.count(project_marker) != 1:
        raise ValueError('Studio road request has no unique canonical snapshot-hash field: ' + request_id)
    blank_marker = b',"project":{"id":"github:rustemd02/URMAN","snapshotHash":""},"selection":'
    canonical_without_hash = request_bytes.replace(project_marker, blank_marker, 1)
    if hashlib.sha256(canonical_without_hash).hexdigest() != snapshot_hash:
        raise ValueError('Studio road request snapshot hash does not match its frozen canonical bytes: ' + request_id)
    return selection['sourcePath']


def _studio_validate_road_proposal(proposal, request, request_id):
    expected_fields = [
        'operation', 'requestId', 'snapshotHash', 'sourcePath', 'roadId',
        'startIndex', 'endIndex', 'strength', 'maxDeviation', 'summary',
    ]
    selection = request['selection']
    if (not isinstance(proposal, dict) or list(proposal) != expected_fields
            or proposal.get('operation') != STUDIO_ROAD_OPERATION
            or proposal.get('requestId') != request_id
            or proposal.get('snapshotHash') != request['project']['snapshotHash']
            or proposal.get('sourcePath') != selection['sourcePath']
            or proposal.get('roadId') != selection['roadId']
            or type(proposal.get('startIndex')) is not int or proposal['startIndex'] != selection['startIndex']
            or type(proposal.get('endIndex')) is not int or proposal['endIndex'] != selection['endIndex']):
        raise ValueError('Studio road request and proposal identity do not match: ' + request_id)
    strength = _studio_road_number(proposal['strength'], 'proposal strength')
    deviation = _studio_road_number(proposal['maxDeviation'], 'proposal deviation')
    if not 0.05 <= strength <= 1 or not 0.05 <= deviation <= 1:
        raise ValueError('Studio road proposal parameters are outside their bounds: ' + request_id)
    summary = proposal['summary']
    if not isinstance(summary, str):
        raise ValueError('Studio road proposal summary is invalid: ' + request_id)
    try:
        summary_length = len(summary.encode('utf-16-le', errors='strict')) // 2
        summary.encode('utf-8', errors='strict')
    except UnicodeEncodeError as error:
        raise ValueError('Studio road proposal summary has invalid Unicode: ' + request_id) from error
    if (not 1 <= summary_length <= 500 or summary.strip() != summary
            or any(ord(character) < 32 or 127 <= ord(character) <= 159 for character in summary)):
        raise ValueError('Studio road proposal summary is outside its bounds: ' + request_id)


def _studio_checkpoint_strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for child in value.values():
            yield from _studio_checkpoint_strings(child)
    elif isinstance(value, list):
        for child in value:
            yield from _studio_checkpoint_strings(child)


def _studio_checkpoint_source_values(raw, relative):
    """Validate a saved authored source and expose string values Core snapshots."""
    try:
        text = raw.decode('utf-8')
    except UnicodeDecodeError as error:
        raise ValueError('Studio checkpoint source is not strict UTF-8: ' + relative) from error
    if relative.endswith('.json'):
        value = _studio_json(raw, STUDIO_MAX_CHECKPOINT_BYTES, 'Studio checkpoint source')
        if not isinstance(value, (dict, list)):
            raise ValueError('Studio checkpoint JSON source has an unsupported root: ' + relative)
        if isinstance(value, dict) and isinstance(value.get('entities'), list):
            value = value['entities']
        return list(_studio_checkpoint_strings(value))

    normalized = text.replace('\r\n', '\n')
    if not normalized.startswith('---\n'):
        raise ValueError('Studio checkpoint Markdown source has no front matter: ' + relative)
    closing = normalized.find('\n---\n', 4)
    if closing < 0:
        raise ValueError('Studio checkpoint Markdown source has incomplete front matter: ' + relative)
    entity = {}
    body = normalized[closing + 5:]
    for line in normalized[4:closing].split('\n'):
        if not line.strip() or line.lstrip().startswith('#'):
            continue
        match = re.fullmatch(r'([A-Za-z][A-Za-z0-9_-]*):\s*(.*)', line)
        if not match:
            raise ValueError('Studio checkpoint Markdown source has invalid front matter: ' + relative)
        scalar = match.group(2).strip()
        if not scalar:
            entity[match.group(1)] = ''
        elif (scalar in ('null', 'true', 'false')
              or (scalar.startswith('"') and scalar.endswith('"'))
              or (scalar.startswith('[') and scalar.endswith(']'))
              or (scalar.startswith('{') and scalar.endswith('}'))):
            try:
                parsed = json.loads(scalar, parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
            except (ValueError, json.JSONDecodeError) as error:
                raise ValueError('Studio checkpoint Markdown source has invalid JSON front matter: ' + relative) from error
            entity[match.group(1)] = parsed
        elif re.fullmatch(r'-?(?:0|[1-9]\d*)(?:\.\d+)?', scalar):
            entity[match.group(1)] = json.loads(scalar)
        else:
            entity[match.group(1)] = scalar
    entity['bodyMarkdown'] = body
    return list(_studio_checkpoint_strings(entity))


def _studio_checkpoint_resource_path(resource):
    if not resource.startswith('res://'):
        return None
    relative = 'game/' + resource[len('res://'):]
    if relative.endswith('/'):
        relative = relative[:-1]
        if not _studio_checkpoint_relative_path(relative):
            raise ValueError('Studio checkpoint contains an invalid res:// reference')
        return None
    if not _studio_checkpoint_relative_path(relative):
        raise ValueError('Studio checkpoint contains an invalid res:// reference')
    return relative


def _studio_checkpoint_capsules(source, payloads):
    root_relative = '.urman-studio/checkpoints'
    checkpoints_root = _studio_path(source, root_relative, kind='dir', missing=True)
    if checkpoints_root is None:
        return {}
    directories = _studio_children(checkpoints_root, STUDIO_MAX_CHECKPOINTS, 'Studio checkpoint directory')
    root_hash = hashlib.sha256(os.path.abspath(os.fspath(source)).encode('utf-8')).hexdigest()
    by_id = {}
    for directory in directories:
        info = directory.lstat()
        checkpoint_id = directory.name
        if (_studio_reparse(info) or not stat.S_ISDIR(info.st_mode)
                or not re.fullmatch(r'[a-f0-9]{32}', checkpoint_id)):
            raise ValueError('Studio checkpoint directory is not a canonical checkpoint ID')
        checkpoint_relative = f'{root_relative}/{checkpoint_id}'
        _, manifest_bytes = _studio_read(source, checkpoint_relative + '/manifest.json',
                                         STUDIO_MAX_CHECKPOINT_BYTES, 'Studio checkpoint manifest')
        manifest = _studio_json(manifest_bytes, STUDIO_MAX_CHECKPOINT_BYTES, 'Studio checkpoint manifest')
        if (not isinstance(manifest, dict) or set(manifest) != {'Version', 'RootHash', 'Metadata', 'Sources', 'Resources'}
                or type(manifest['Version']) is not int or manifest['Version'] != 1
                or manifest['RootHash'] != root_hash or not isinstance(manifest['Metadata'], dict)
                or not isinstance(manifest['Sources'], list) or not isinstance(manifest['Resources'], list)):
            raise ValueError('Studio checkpoint manifest schema or source-root hash is invalid: ' + checkpoint_id)
        metadata = manifest['Metadata']
        if (set(metadata) != {'Id', 'Name', 'Author', 'CreatedAt', 'Revision', 'FileCount', 'ResourceCount', 'ResourceCapture'}
                or metadata['Id'] != checkpoint_id or not isinstance(metadata['Name'], str)
                or not metadata['Name'].strip() or len(metadata['Name'].encode('utf-16-le')) // 2 > 128
                or any(ord(character) < 32 or 127 <= ord(character) <= 159 for character in metadata['Name'])
                or not isinstance(metadata['Author'], str) or not metadata['Author'].strip()
                or len(metadata['Author'].encode('utf-16-le')) // 2 > 128
                or any(ord(character) < 32 or 127 <= ord(character) <= 159 for character in metadata['Author'])
                or not isinstance(metadata['CreatedAt'], str)
                or not isinstance(metadata['Revision'], str)
                or not re.fullmatch(r'(?:[a-f0-9]{40}|[a-f0-9]{64})', metadata['Revision'])
                or type(metadata['FileCount']) is not int or metadata['FileCount'] != len(manifest['Sources'])
                or type(metadata['ResourceCount']) is not int or metadata['ResourceCount'] != len(manifest['Resources'])
                or metadata['ResourceCapture'] != 'literal-res-paths-record-only'):
            raise ValueError('Studio checkpoint metadata is invalid: ' + checkpoint_id)
        try:
            timestamp = datetime.datetime.fromisoformat(metadata['CreatedAt'].replace('Z', '+00:00'))
        except (ValueError, TypeError) as error:
            raise ValueError('Studio checkpoint timestamp is invalid: ' + checkpoint_id) from error
        if (timestamp.tzinfo is None or timestamp.utcoffset() != datetime.timedelta(0)
                or timestamp == datetime.datetime.min.replace(tzinfo=datetime.timezone.utc)):
            raise ValueError('Studio checkpoint timestamp is not UTC: ' + checkpoint_id)

        sources = manifest['Sources']
        resources = manifest['Resources']
        if (not 1 <= len(sources) <= STUDIO_MAX_CHECKPOINT_FILES
                or len(resources) > STUDIO_MAX_CHECKPOINT_FILES):
            raise ValueError('Studio checkpoint source or resource count exceeds its limit: ' + checkpoint_id)
        expected_images = {'manifest.json'}
        source_paths = []
        discovered_resources = set(STUDIO_CHECKPOINT_BASE_PATHS)
        total_source_bytes = len(manifest_bytes)
        for index, item in enumerate(sources):
            if (not isinstance(item, dict) or set(item) != {'Path', 'Image', 'Bytes', 'Sha256'}
                    or not _studio_checkpoint_source_path(item['Path'])
                    or item['Image'] != f'source-{index:04d}.txt'
                    or type(item['Bytes']) is not int or not 0 <= item['Bytes'] <= STUDIO_MAX_CHECKPOINT_BYTES
                    or not isinstance(item['Sha256'], str) or not re.fullmatch(r'[a-f0-9]{64}', item['Sha256'])):
                raise ValueError('Studio checkpoint source receipt is invalid: ' + checkpoint_id)
            relative = item['Path']
            if source_paths and _studio_ordinal_key(source_paths[-1]) >= _studio_ordinal_key(relative):
                raise ValueError('Studio checkpoint sources are not unique ordinal paths: ' + checkpoint_id)
            source_paths.append(relative)
            _studio_path(source, relative)
            image_relative = checkpoint_relative + '/' + item['Image']
            _, image_bytes = _studio_read(source, image_relative, STUDIO_MAX_CHECKPOINT_BYTES,
                                          'Studio checkpoint source image')
            total_source_bytes += len(image_bytes)
            if (len(image_bytes) != item['Bytes']
                    or hashlib.sha256(image_bytes).hexdigest() != item['Sha256']
                    or total_source_bytes > STUDIO_MAX_CHECKPOINT_BYTES):
                raise ValueError('Studio checkpoint source image hash, size, or aggregate budget is invalid: ' + checkpoint_id)
            for value in _studio_checkpoint_source_values(image_bytes, relative):
                resource = _studio_checkpoint_resource_path(value)
                if resource is not None:
                    discovered_resources.add(resource)
            expected_images.add(item['Image'])
            payloads.append((image_relative, image_bytes, item['Sha256']))

        resource_paths = []
        resource_hashes = {}
        for item in resources:
            if (not isinstance(item, dict) or set(item) != {'Path', 'Sha256'}
                    or not isinstance(item['Path'], str)
                    or (not item['Path'].startswith('game/') and item['Path'] not in STUDIO_CHECKPOINT_BASE_PATHS)
                    or not _studio_checkpoint_relative_path(item['Path'])
                    or (item['Sha256'] is not None and (not isinstance(item['Sha256'], str)
                                                        or not re.fullmatch(r'[a-f0-9]{64}', item['Sha256'])))):
                raise ValueError('Studio checkpoint resource receipt is invalid: ' + checkpoint_id)
            _studio_path(source, item['Path'], missing=True)
            resource_paths.append(item['Path'])
            resource_hashes[item['Path']] = item['Sha256']
        if (any(_studio_ordinal_key(left) >= _studio_ordinal_key(right)
               for left, right in zip(resource_paths, resource_paths[1:]))
                or len(resource_paths) != len(set(resource_paths))
                or set(resource_paths) != discovered_resources
                or any(resource_hashes.get(path) is None for path in STUDIO_CHECKPOINT_BASE_PATHS)):
            raise ValueError('Studio checkpoint resource receipts are incomplete or do not match snapshots: ' + checkpoint_id)

        expected_images.update(item['Image'] for item in sources)
        contents = _studio_children(directory, STUDIO_MAX_CHECKPOINT_FILES + 1,
                                    'Studio checkpoint directory contents')
        if {item.name for item in contents} != expected_images:
            raise ValueError('Studio checkpoint has missing or unexpected artifacts: ' + checkpoint_id)
        if any(_studio_reparse(item.lstat()) or not stat.S_ISREG(item.lstat().st_mode) for item in contents):
            raise ValueError('Studio checkpoint contains a link or non-file: ' + checkpoint_id)
        payloads.append((checkpoint_relative + '/manifest.json', manifest_bytes, None))
        by_id[checkpoint_id] = set(source_paths)
    return by_id


def _studio_export_file_limit(relative):
    run_prefix = r'\.urman-studio/runs/run-[0-9]{8}-[0-9]{6}-[a-f0-9]{6}/'
    window_ids = '|'.join(re.escape(checkpoint) for checkpoint in STUDIO_HOUSE_WINDOW_IDS)
    if re.fullmatch(run_prefix + r'game\.log', relative):
        return STUDIO_MAX_RUN_LOG
    if re.fullmatch(run_prefix + rf'(?:{window_ids})\.png', relative):
        return STUDIO_MAX_RUN_FRAME
    if re.fullmatch(run_prefix + rf'(?:(?:{window_ids})\.json|_capture-summary\.json)', relative):
        return STUDIO_MAX_RUN_FRAME_JSON
    if re.fullmatch(r'\.urman-studio/ai-jobs/[a-f0-9]{32}/actions/[a-f0-9]{32}\.png', relative):
        return STUDIO_MAX_ACTION_FRAME
    if re.fullmatch(r'\.urman-studio/checkpoints/[a-f0-9]{32}/(?:manifest\.json|source-[0-9]{4}\.txt)', relative):
        return STUDIO_MAX_CHECKPOINT_BYTES
    if re.fullmatch(r'\.urman-studio/transactions/[a-f0-9]{32}/journal\.json', relative):
        return STUDIO_MAX_CHECKPOINT_JOURNAL
    if _studio_checkpoint_source_path(relative):
        return STUDIO_MAX_AUTHORED_JSON
    return STUDIO_MAX_BINARY


def _studio_write_marker(job):
    marker = Path(job) / STUDIO_PRESERVE_MARKER
    try:
        with marker.open('xb') as stream:
            stream.write(b'preserve source until Studio evidence is archived\n')
            stream.flush()
            os.fsync(stream.fileno())
    except FileExistsError:
        return


def _studio_write_durable_json(path, value):
    path = Path(path)
    temp = path.with_suffix('.tmp')
    with temp.open('wb') as stream:
        stream.write(canonical(value))
        stream.flush()
        os.fsync(stream.fileno())
    temp.replace(path)


def export_studio_session(source, output):
    """Export only bounded pilot request, transaction, and play-run evidence."""
    source = Path(source)
    output = Path(output)
    payloads = []
    request_ids = {}
    road_request_paths = {}
    road_request_hashes = {}
    road_source_targets = set()
    request_hashes = {}
    proposal_ids = set()
    proposal_hashes = {}
    action_transaction_refs = []
    requests_root = _studio_path(source, '.urman-studio/ai-jobs', kind='dir', missing=True)
    if requests_root is not None:
        request_dirs = _studio_children(requests_root, STUDIO_MAX_REQUESTS, 'Studio request directories')
        for directory in request_dirs:
            info = directory.lstat()
            if _studio_reparse(info) or not stat.S_ISDIR(info.st_mode) or not re.fullmatch('[a-f0-9]{32}', directory.name):
                raise ValueError('Studio request directory is not a canonical request ID')
            request_id = directory.name
            contents = _studio_children(directory, 4, 'Studio request directory')
            names = {item.name for item in contents}
            base_names = names - {'actions'}
            if base_names not in ({'request.json'}, {'request.json', 'proposal.json'},
                                  {'request.json', 'proposal.json', 'preview-failed.json'}):
                raise ValueError('Studio request directory has missing or unexpected files: ' + request_id)
            for item in contents:
                item_info = item.lstat()
                if _studio_reparse(item_info):
                    raise ValueError('Studio request evidence contains a link or reparse point: ' + request_id)
                if item.name == 'actions':
                    if not stat.S_ISDIR(item_info.st_mode):
                        raise ValueError('Studio actions entry is not a directory: ' + request_id)
                elif not stat.S_ISREG(item_info.st_mode):
                    raise ValueError('Studio request evidence contains a non-file: ' + request_id)
            _, request_bytes = _studio_read(source, f'.urman-studio/ai-jobs/{request_id}/request.json',
                                            STUDIO_MAX_REQUEST, 'Studio request')
            request = _studio_json(request_bytes, STUDIO_MAX_REQUEST, 'Studio request')
            road_source_path = None
            if (isinstance(request, dict) and request.get('kind') == 'urman.studio.authoring-request'
                    and type(request.get('schemaVersion')) is int and request['schemaVersion'] == 2
                    and request.get('requestId') == request_id):
                pass
            elif (isinstance(request, dict) and request.get('kind') == 'urman.studio.authoring-request'
                    and type(request.get('schemaVersion')) is int and request['schemaVersion'] == 3):
                road_source_path = _studio_validate_road_request(request, request_bytes, request_id)
            else:
                raise ValueError('Studio request identity is invalid: ' + request_id)
            request_ids[request_id] = request
            if road_source_path is not None:
                road_request_paths[request_id] = road_source_path
                road_request_hashes[request_id] = request['source']['sha256']
                road_source_targets.add(road_source_path)
            request_hashes[request_id] = hashlib.sha256(request_bytes).hexdigest()
            payloads.append((f'.urman-studio/ai-jobs/{request_id}/request.json', request_bytes, None))
            proposal_bytes = None
            if 'proposal.json' in names:
                proposal_limit = 8 * 1024 if road_source_path is not None else STUDIO_MAX_PROPOSAL
                _, proposal_bytes = _studio_read(source, f'.urman-studio/ai-jobs/{request_id}/proposal.json',
                                                 proposal_limit, 'Studio proposal')
                proposal = _studio_json(proposal_bytes, proposal_limit, 'Studio proposal')
                if road_source_path is not None:
                    if not _studio_road_json_is_compact(proposal_bytes):
                        raise ValueError('Studio road proposal is not in Core compact JSON form: ' + request_id)
                    _studio_validate_road_proposal(proposal, request, request_id)
                elif (not isinstance(proposal, dict) or proposal.get('requestId') != request_id
                        or not isinstance(request.get('project'), dict)
                        or proposal.get('snapshotHash') != request['project'].get('snapshotHash')):
                    raise ValueError('Studio request and proposal identity do not match: ' + request_id)
                proposal_ids.add(request_id)
                proposal_hashes[request_id] = hashlib.sha256(proposal_bytes).hexdigest()
                payloads.append((f'.urman-studio/ai-jobs/{request_id}/proposal.json', proposal_bytes, None))
            if 'preview-failed.json' in names:
                if proposal_bytes is None:
                    raise ValueError('Studio preview-failure receipt has no matching proposal: ' + request_id)
                _, failure_bytes = _studio_read(source, f'.urman-studio/ai-jobs/{request_id}/preview-failed.json',
                                                 STUDIO_MAX_PREVIEW_FAILURE, 'Studio preview-failure receipt')
                failure = _studio_json(failure_bytes, STUDIO_MAX_PREVIEW_FAILURE, 'Studio preview-failure receipt')
                if (not isinstance(failure, dict) or list(failure) != ['requestId', 'requestHash', 'proposalHash']
                        or any(not isinstance(failure.get(key), str) for key in ('requestId', 'requestHash', 'proposalHash'))
                        or failure['requestId'] != request_id
                        or not re.fullmatch('[a-f0-9]{64}', failure['requestHash'])
                        or not re.fullmatch('[a-f0-9]{64}', failure['proposalHash'])
                        or failure_bytes != json.dumps(failure, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
                        or hashlib.sha256(request_bytes).hexdigest() != failure['requestHash']
                        or hashlib.sha256(proposal_bytes).hexdigest() != failure['proposalHash']):
                    raise ValueError('Studio preview-failure receipt is invalid: ' + request_id)
                payloads.append((f'.urman-studio/ai-jobs/{request_id}/preview-failed.json', failure_bytes, None))

            if 'actions' in names:
                if proposal_bytes is None:
                    raise ValueError('Studio action evidence has no matching proposal: ' + request_id)
                actions_relative = f'.urman-studio/ai-jobs/{request_id}/actions'
                actions_directory = _studio_path(source, actions_relative, kind='dir')
                children = _studio_children(actions_directory, STUDIO_MAX_ACTIONS * 2,
                                            'Studio action directory')
                action_files = {}
                for child in children:
                    child_info = child.lstat()
                    if _studio_reparse(child_info) or not stat.S_ISREG(child_info.st_mode):
                        raise ValueError('Studio action evidence contains a link or non-file: ' + request_id)
                    match = re.fullmatch(r'([a-f0-9]{32})\.(json|png)', child.name)
                    if not match:
                        raise ValueError('Studio action directory contains an unexpected file: ' + request_id)
                    action_id, suffix = match.groups()
                    pair = action_files.setdefault(action_id, {})
                    if suffix in pair:
                        raise ValueError('Studio action directory contains a duplicate record: ' + action_id)
                    pair[suffix] = child
                if len(action_files) > STUDIO_MAX_ACTIONS:
                    raise ValueError('Studio request exceeds 32 action records: ' + request_id)

                project = request.get('project')
                snapshot_hash = project.get('snapshotHash') if isinstance(project, dict) else None
                if not isinstance(snapshot_hash, str):
                    raise ValueError('Studio request snapshot identity is missing: ' + request_id)
                expected_action_fields = {
                    'schemaVersion', 'actionId', 'action', 'requestId', 'requestHash',
                    'proposalHash', 'snapshotHash', 'transactionId', 'frameHash', 'captureError',
                }
                for action_id, pair in sorted(action_files.items()):
                    if 'json' not in pair:
                        # The UI publishes the JSON receipt last; an orphan PNG means
                        # capture was interrupted and the source must remain on station.
                        raise ValueError('Studio action PNG has no final receipt: ' + action_id)
                    action_relative = f'{actions_relative}/{action_id}.json'
                    _, action_bytes = _studio_read(source, action_relative,
                                                   STUDIO_MAX_ACTION_RECEIPT, 'Studio action receipt')
                    action_receipt = _studio_json(action_bytes, STUDIO_MAX_ACTION_RECEIPT,
                                                  'Studio action receipt')
                    if not isinstance(action_receipt, dict) or set(action_receipt) != expected_action_fields:
                        raise ValueError('Studio action receipt has missing or unexpected fields: ' + action_id)
                    action_name = action_receipt['action']
                    if (type(action_receipt['schemaVersion']) is not int or action_receipt['schemaVersion'] != 1
                            or action_receipt['actionId'] != action_id
                            or action_name not in ('preview', 'apply', 'undo', 'redo')
                            or action_receipt['requestId'] != request_id
                            or action_receipt['requestHash'] != request_hashes[request_id]
                            or action_receipt['proposalHash'] != proposal_hashes[request_id]
                            or action_receipt['snapshotHash'] != snapshot_hash):
                        raise ValueError('Studio action identity or hashes do not match: ' + action_id)
                    transaction_id = action_receipt['transactionId']
                    if action_name == 'preview':
                        if transaction_id is not None:
                            raise ValueError('Studio preview action has a transaction ID: ' + action_id)
                    elif not isinstance(transaction_id, str) or not re.fullmatch('[a-f0-9]{32}', transaction_id):
                        raise ValueError('Studio mutation action has an invalid transaction ID: ' + action_id)

                    frame_hash = action_receipt['frameHash']
                    capture_error = action_receipt['captureError']
                    if frame_hash is not None and (not isinstance(frame_hash, str)
                                                   or not re.fullmatch('[a-f0-9]{64}', frame_hash)):
                        raise ValueError('Studio action frame hash is invalid: ' + action_id)
                    if (capture_error is not None
                            and (not isinstance(capture_error, str) or not capture_error.strip()
                                 or len(capture_error.encode('utf-16-le')) // 2 > 1024)):
                        raise ValueError('Studio action capture error is invalid: ' + action_id)
                    png_relative = f'{actions_relative}/{action_id}.png'
                    if 'png' in pair:
                        if frame_hash is None or capture_error is not None:
                            raise ValueError('Studio action PNG must have a successful frame receipt: ' + action_id)
                        png_path = _studio_path(source, png_relative)
                        _, actual_frame_hash = _studio_hash(
                            png_path, STUDIO_MAX_ACTION_FRAME, b'\x89PNG\r\n\x1a\n')
                        if actual_frame_hash != frame_hash:
                            raise ValueError('Studio action frame hash does not match PNG: ' + action_id)
                        payloads.append((png_relative, png_path, frame_hash))
                    elif frame_hash is not None or capture_error is None:
                        raise ValueError('Studio action without PNG requires a capture error: ' + action_id)
                    payloads.append((action_relative, action_bytes, None))
                    if action_name != 'preview':
                        action_transaction_refs.append((request_id, action_id, transaction_id))

    checkpoint_sources = _studio_checkpoint_capsules(source, payloads)
    fence_relative = '.urman-studio/history-fence.json'
    fence_path = _studio_path(source, fence_relative, missing=True)
    if fence_path is not None:
        _, fence_bytes = _studio_read(source, fence_relative, STUDIO_MAX_JOURNAL,
                                      'Studio history fence')
        fence = _studio_json(fence_bytes, STUDIO_MAX_JOURNAL, 'Studio history fence')
        if (not isinstance(fence, dict) or set(fence) != {'version', 'scope', 'reason'}
                or type(fence['version']) is not int or fence['version'] != 1
                or fence['scope'] != 'studio-command-history'
                or fence['reason'] != 'unsupported-command-order'):
            raise ValueError('Studio history fence is invalid; preserve source')
        payloads.append((fence_relative, fence_bytes, None))

    transactions_by_request = {}
    direct_transaction_targets = set()
    road_transaction_hash_pairs = {}
    checkpoint_transaction_targets = set()
    maximum_accepted_order = 0
    transactions_root = _studio_path(source, '.urman-studio/transactions', kind='dir', missing=True)
    if transactions_root is not None:
        transaction_dirs = _studio_children(transactions_root, STUDIO_MAX_TRANSACTIONS,
                                            'Studio transaction directories')
        for directory in transaction_dirs:
            info = directory.lstat()
            if _studio_reparse(info) or not stat.S_ISDIR(info.st_mode) or not re.fullmatch('[a-f0-9]{32}', directory.name):
                raise ValueError('Studio transaction directory is not a canonical transaction ID')
            transaction_id = directory.name
            journal_relative = f'.urman-studio/transactions/{transaction_id}/journal.json'
            _, journal_bytes = _studio_read(source, journal_relative, STUDIO_MAX_CHECKPOINT_JOURNAL,
                                            'Studio transaction journal')
            journal = _studio_json(journal_bytes, STUDIO_MAX_CHECKPOINT_JOURNAL,
                                   'Studio transaction journal')
            required_journal_keys = {'version', 'id', 'owner', 'status', 'rollbackSide', 'targetSide', 'side', 'files'}
            optional_journal_keys = {'accepted', 'acceptedOrder', 'commandLabel'}
            if (not isinstance(journal, dict)
                    or not required_journal_keys.issubset(journal)
                    or set(journal) - required_journal_keys - optional_journal_keys):
                raise ValueError('Studio transaction journal has missing or unexpected fields: ' + transaction_id)
            if type(journal['version']) is not int or journal['version'] != 1 or journal['id'] != transaction_id:
                raise ValueError('Studio transaction journal version or ID is invalid: ' + transaction_id)
            owner = journal['owner']
            checkpoint_owner = (re.fullmatch(re.escape(STUDIO_CHECKPOINT_OWNER_PREFIX) + r'([a-f0-9]{32})', owner)
                                if isinstance(owner, str) else None)
            road_owner = (re.fullmatch(re.escape(STUDIO_ROAD_OWNER_PREFIX) + r'([a-f0-9]{32})', owner)
                          if isinstance(owner, str) else None)
            checkpoint_id = checkpoint_owner.group(1) if checkpoint_owner else None
            is_checkpoint_transaction = checkpoint_id is not None
            is_road_transaction = road_owner is not None
            if (not isinstance(owner, str)
                    or (owner != STUDIO_OWNER and owner not in STUDIO_DIRECT_FILE_OWNERS
                        and not is_road_transaction
                        and (not is_checkpoint_transaction or checkpoint_id not in checkpoint_sources))):
                raise ValueError('Unexpected Studio transaction owner; source must be preserved: ' + transaction_id)
            if not is_checkpoint_transaction and len(journal_bytes) > STUDIO_MAX_JOURNAL:
                raise ValueError('Studio transaction journal exceeds its ordinary size limit: ' + transaction_id)
            is_direct_file_transaction = owner in STUDIO_DIRECT_FILE_OWNERS
            status_value = journal['status']
            sides = (journal['rollbackSide'], journal['targetSide'], journal['side'])
            accepted = journal.get('accepted', status_value == 'committed')
            accepted_order = journal.get('acceptedOrder')
            command_label = journal.get('commandLabel')
            if type(accepted) is not bool:
                raise ValueError('Studio transaction accepted state is invalid: ' + transaction_id)
            if (accepted_order is not None
                    and (type(accepted_order) is not int or not 0 < accepted_order <= (1 << 63) - 1)):
                raise ValueError('Studio transaction accepted order is invalid: ' + transaction_id)
            if accepted_order is not None:
                maximum_accepted_order = max(maximum_accepted_order, accepted_order)
            if (command_label is not None
                    and (not isinstance(command_label, str) or not command_label.strip()
                         or len(command_label.encode('utf-16-le')) // 2 > 1024)):
                raise ValueError('Studio transaction command label is invalid: ' + transaction_id)
            if accepted_order is not None and command_label is None:
                raise ValueError('Studio transaction command label is missing: ' + transaction_id)
            if status_value in ('committed', 'retired'):
                if (accepted is not True or sides[0] not in ('before', 'after')
                        or sides != (sides[0], sides[0], sides[0])):
                    raise ValueError('Studio transaction current accepted state is invalid: ' + transaction_id)
            elif status_value == 'recovered':
                if (accepted is not False or accepted_order is not None or command_label is not None
                        or sides not in (('before', 'after', 'before'), ('before', 'before', 'before'))):
                    raise ValueError('Studio recovered transaction state is invalid: ' + transaction_id)
            elif status_value in ('staging', 'applying'):
                raise ValueError('Unresolved Studio transaction must remain on station: ' + transaction_id)
            else:
                raise ValueError('Studio transaction status is invalid: ' + transaction_id)
            if is_checkpoint_transaction:
                valid_file_count = isinstance(journal['files'], list) and 1 <= len(journal['files']) <= STUDIO_MAX_CHECKPOINT_FILES
            elif is_road_transaction:
                valid_file_count = isinstance(journal['files'], list) and len(journal['files']) == 1
            else:
                allowed_file_counts = (1,) if is_direct_file_transaction else (3, 4)
                valid_file_count = isinstance(journal['files'], list) and len(journal['files']) in allowed_file_counts
            if not valid_file_count:
                raise ValueError('Studio transaction file set is invalid: ' + transaction_id)

            indexed_paths = []
            expected_files = {'relativePath', 'expectedSha256', 'beforeSha256', 'afterSha256', 'beforeImage', 'afterImage'}
            request_id = None
            images = set()
            for index, item in enumerate(journal['files']):
                if not isinstance(item, dict) or set(item) != expected_files:
                    raise ValueError('Studio transaction file receipt is invalid: ' + transaction_id)
                relative = item['relativePath']
                if not isinstance(relative, str) or relative in indexed_paths:
                    raise ValueError('Studio transaction has a duplicate or invalid target: ' + transaction_id)
                indexed_paths.append(relative)
                for key in ('expectedSha256', 'beforeSha256', 'afterSha256'):
                    value = item[key]
                    if value is not None and (not isinstance(value, str) or not re.fullmatch('[a-f0-9]{64}', value)):
                        raise ValueError('Studio transaction hash is invalid: ' + transaction_id)
                if item['expectedSha256'] != item['beforeSha256']:
                    raise ValueError('Studio transaction expected hash differs from before image: ' + transaction_id)
                for side in ('before', 'after'):
                    file_hash = item[side + 'Sha256']
                    image = item[side + 'Image']
                    expected_image = f'{side}-{index:04d}.bin' if file_hash is not None else None
                    if image != expected_image:
                        raise ValueError('Studio transaction image name is invalid: ' + transaction_id)
                    if image is not None:
                        images.add(image)
                        image_relative = f'.urman-studio/transactions/{transaction_id}/{image}'
                        image_path = _studio_path(source, image_relative)
                        image_size = image_path.stat().st_size
                        transaction_image_limit = (STUDIO_MAX_CHECKPOINT_BYTES if is_checkpoint_transaction
                                                   else STUDIO_MAX_ROAD_SOURCE if is_road_transaction
                                                   else STUDIO_MAX_BINARY)
                        if image_size > transaction_image_limit:
                            raise ValueError('Studio transaction image exceeds its owner-specific size limit: '
                                             + transaction_id)
                        payloads.append((image_relative, image_path, file_hash))

                if is_direct_file_transaction:
                    if relative != STUDIO_DIRECT_FILE_OWNERS[owner]:
                        raise ValueError('Studio direct-file transaction targets an unapproved owner/path pair: ' + transaction_id)
                    if item['beforeSha256'] is None or item['afterSha256'] is None:
                        raise ValueError('Studio direct-file transaction must replace an existing source file: ' + transaction_id)
                elif is_road_transaction:
                    if (relative not in STUDIO_ROAD_SOURCE_PATHS
                            or item['beforeSha256'] is None or item['afterSha256'] is None):
                        raise ValueError('Studio road transaction must replace exactly one approved road source: ' + transaction_id)
                    if accepted and status_value == 'committed':
                        road_transaction_hash_pairs.setdefault(relative, []).append(
                            (item['beforeSha256'], item['afterSha256']))
                elif is_checkpoint_transaction:
                    if (relative not in checkpoint_sources[checkpoint_id]
                            or not _studio_checkpoint_source_path(relative)
                            or item['beforeSha256'] is None or item['afterSha256'] is None):
                        raise ValueError('Studio checkpoint transaction targets a non-source or non-replacement path: ' + transaction_id)
                else:
                    match = re.fullmatch(r'game/assets/models/studio/hero-house-([a-f0-9]{32})\.glb', relative)
                    if match:
                        if request_id is not None:
                            raise ValueError('Studio transaction has multiple request-scoped models: ' + transaction_id)
                        request_id = match.group(1)
                    elif relative != 'game/content/studio/hero_house.recipe.json' and not re.fullmatch(
                            r'game/assets/models/studio/hero-house-[a-f0-9]{32}\.glb\.manifest\.json', relative) and not re.fullmatch(
                            r'assets/source/blender/studio/hero-house-[a-f0-9]{32}\.blend', relative):
                        raise ValueError('Studio transaction contains a target outside the pilot allowlist: ' + transaction_id)

            if is_direct_file_transaction:
                if indexed_paths != [STUDIO_DIRECT_FILE_OWNERS[owner]] or request_id is not None:
                    raise ValueError('Studio direct-file transaction does not contain exactly its owner-approved target: ' + transaction_id)
                direct_transaction_targets.add(indexed_paths[0])
            elif is_road_transaction:
                if len(indexed_paths) != 1 or indexed_paths[0] not in STUDIO_ROAD_SOURCE_PATHS or request_id is not None:
                    raise ValueError('Studio road transaction does not contain exactly one approved target: ' + transaction_id)
                road_source_targets.add(indexed_paths[0])
            elif is_checkpoint_transaction:
                target_paths = set(indexed_paths)
                if (len(target_paths) != len(indexed_paths)
                        or not target_paths.issubset(checkpoint_sources[checkpoint_id])):
                    raise ValueError('Studio checkpoint transaction targets are outside their checkpoint source set: ' + transaction_id)
                checkpoint_transaction_targets.update(target_paths)
            else:
                if request_id is None or request_id not in request_ids or request_id not in proposal_ids:
                    raise ValueError('Studio transaction has no matching exported request: ' + transaction_id)
                transactions_by_request.setdefault(request_id, set()).add(transaction_id)
                model = f'game/assets/models/studio/hero-house-{request_id}.glb'
                manifest = model + '.manifest.json'
                blend = f'assets/source/blender/studio/hero-house-{request_id}.blend'
                if not {model, manifest, blend}.issubset(indexed_paths) or len(set(indexed_paths)) != len(indexed_paths):
                    raise ValueError('Studio transaction does not contain the exact request-scoped target set: ' + transaction_id)
                if any(re.fullmatch(r'game/assets/models/studio/hero-house-[a-f0-9]{32}\.glb\.manifest\.json', p)
                       and p != manifest for p in indexed_paths) or any(
                           re.fullmatch(r'assets/source/blender/studio/hero-house-[a-f0-9]{32}\.blend', p)
                           and p != blend for p in indexed_paths):
                    raise ValueError('Studio transaction mixes request IDs: ' + transaction_id)
                if 'game/content/studio/hero_house.recipe.json' in indexed_paths and len(indexed_paths) != 4:
                    raise ValueError('Studio transaction recipe target set is invalid: ' + transaction_id)
                if 'game/content/studio/hero_house.recipe.json' not in indexed_paths and len(indexed_paths) != 3:
                    raise ValueError('Studio transaction target count is invalid: ' + transaction_id)

            expected_contents = {'journal.json', *images}
            contents = _studio_children(directory, 2 * STUDIO_MAX_CHECKPOINT_FILES + 1 if is_checkpoint_transaction else 9,
                                        'Studio transaction directory')
            if {item.name for item in contents} != expected_contents:
                raise ValueError('Studio transaction directory has missing or unexpected files: ' + transaction_id)
            if any(_studio_reparse(item.lstat()) or not stat.S_ISREG(item.lstat().st_mode) for item in contents):
                raise ValueError('Studio transaction evidence contains a link or non-file: ' + transaction_id)
            payloads.append((journal_relative, journal_bytes, None))

    if maximum_accepted_order > 0:
        sequence_relative = '.urman-studio/history-sequence.json'
        _, sequence_bytes = _studio_read(source, sequence_relative, STUDIO_MAX_JOURNAL,
                                         'Studio history sequence')
        sequence = _studio_json(sequence_bytes, STUDIO_MAX_JOURNAL, 'Studio history sequence')
        if (not isinstance(sequence, dict)
                or set(sequence) != {'version', 'scope', 'lastOrder'}
                or type(sequence['version']) is not int or sequence['version'] != 1
                or sequence['scope'] != 'studio-file-history'
                or type(sequence['lastOrder']) is not int
                or not maximum_accepted_order <= sequence['lastOrder'] <= (1 << 63) - 1):
            raise ValueError('Studio history sequence is invalid or older than an exported transaction')
        payloads.append((sequence_relative, sequence_bytes, None))

    runs_root = _studio_path(source, '.urman-studio/runs', kind='dir', missing=True)
    if runs_root is not None:
        required_run_fields = {'runId', 'revision', 'started', 'pinned', 'files'}
        provenance_fields = {'evidenceKind', 'campaignFingerprint', 'houseSources'}
        optional_run_fields = {'captureKind'}
        base_house_sources = {
            'game/content/studio/hero_house.recipe.json',
            'game/content/world/act1_village_kit.world.v1.json',
            'game/assets/models/act1/urman_village_exterior_kit.glb',
            'game/assets/generated/urman_modular_kit.glb',
        }
        run_pattern = r'run-[0-9]{8}-[0-9]{6}-[a-f0-9]{6}'
        staged_model_prefix = 'game/assets/models/studio/hero-house-'
        run_dirs = _studio_children(runs_root, STUDIO_MAX_RUNS, 'Studio run directories')
        for directory in run_dirs:
            info = directory.lstat()
            if _studio_reparse(info) or not stat.S_ISDIR(info.st_mode) or not re.fullmatch(run_pattern, directory.name):
                raise ValueError('Studio run directory is not a canonical run ID')
            try:
                time.strptime(directory.name[4:19], '%Y%m%d-%H%M%S')
            except ValueError as error:
                raise ValueError('Studio run directory has an invalid timestamp: ' + directory.name) from error

            run_id = directory.name
            manifest_relative = f'.urman-studio/runs/{run_id}/manifest.json'
            log_relative = f'.urman-studio/runs/{run_id}/game.log'
            contents = _studio_children(directory, len(STUDIO_RUN_HOUSE_WINDOW_FILES), 'Studio run directory')
            names = {item.name for item in contents}
            if any(_studio_reparse(item.lstat()) or not stat.S_ISREG(item.lstat().st_mode) for item in contents):
                raise ValueError('Studio run evidence contains a link or non-file: ' + run_id)

            _, manifest_bytes = _studio_read(source, manifest_relative, STUDIO_MAX_RUN_MANIFEST,
                                             'Studio run manifest')
            manifest = _studio_json(manifest_bytes, STUDIO_MAX_RUN_MANIFEST, 'Studio run manifest')
            if (not isinstance(manifest, dict) or not required_run_fields.issubset(manifest)
                    or set(manifest) - required_run_fields - provenance_fields - optional_run_fields):
                raise ValueError('Studio run manifest has missing or unexpected fields: ' + run_id)
            capture_files = manifest['files'] == list(STUDIO_RUN_HOUSE_WINDOW_FILES)
            legacy_files = manifest['files'] == list(STUDIO_RUN_BASE_FILES)
            if not capture_files and not legacy_files:
                raise ValueError('Studio run file ownership or capture kind is invalid: ' + run_id)
            if ((capture_files and manifest.get('captureKind') != 'house-windows')
                    or (legacy_files and 'captureKind' in manifest)
                    or 'manifest.json' not in names or not names.issubset(set(manifest['files']))):
                raise ValueError('Studio run file ownership or capture kind is invalid: ' + run_id)
            if (manifest['runId'] != run_id or not isinstance(manifest['revision'], str)
                    or not manifest['revision'].strip() or not isinstance(manifest['started'], str)
                    or not manifest['started'].strip() or type(manifest['pinned']) is not bool):
                raise ValueError('Studio run manifest identity is invalid: ' + run_id)

            present_provenance = provenance_fields & set(manifest)
            if present_provenance and present_provenance != provenance_fields:
                raise ValueError('Studio run provenance fields are incomplete: ' + run_id)
            if present_provenance:
                house_sources = manifest['houseSources']
                if (manifest['evidenceKind'] != 'prelaunch-source'
                        or not isinstance(manifest['campaignFingerprint'], str)
                        or not re.fullmatch('[a-f0-9]{64}', manifest['campaignFingerprint'])
                        or not isinstance(house_sources, dict) or not 4 <= len(house_sources) <= 5
                        or not base_house_sources.issubset(house_sources)):
                    raise ValueError('Studio run provenance is invalid: ' + run_id)
                extra_sources = set(house_sources) - base_house_sources
                if (len(extra_sources) > 1
                        or any(not path.startswith(staged_model_prefix) or not path.lower().endswith('.glb')
                               or '/' in path[len(staged_model_prefix):]
                               or any(part in path for part in ('..', '\\', ':')) for path in extra_sources)
                        or any(not isinstance(sha, str) or not re.fullmatch('[a-f0-9]{64}', sha)
                               for sha in house_sources.values())):
                    raise ValueError('Studio run house-source evidence is invalid: ' + run_id)

            payloads.append((manifest_relative, manifest_bytes, hashlib.sha256(manifest_bytes).hexdigest()))
            if 'game.log' in names:
                log_path = _studio_path(source, log_relative)
                _, log_hash = _studio_hash(log_path, STUDIO_MAX_RUN_LOG)
                payloads.append((log_relative, log_path, log_hash))

            if capture_files:
                frame_metadata = {}
                capture_process_id = None
                for checkpoint in STUDIO_HOUSE_WINDOW_IDS:
                    png_name = f'{checkpoint}.png'
                    json_name = f'{checkpoint}.json'
                    has_png = png_name in names
                    has_json = json_name in names
                    if has_png != has_json:
                        raise ValueError('Studio house-window frame and sidecar are incomplete: ' + checkpoint)
                    if not has_png:
                        continue

                    png_relative = f'.urman-studio/runs/{run_id}/{png_name}'
                    json_relative = f'.urman-studio/runs/{run_id}/{json_name}'
                    png_path = _studio_path(source, png_relative)
                    _, png_hash = _studio_hash(png_path, STUDIO_MAX_RUN_FRAME, b'\x89PNG\r\n\x1a\n')
                    _, frame_bytes = _studio_read(source, json_relative, STUDIO_MAX_RUN_FRAME_JSON,
                                                   'Studio house-window frame metadata')
                    frame = _studio_json(frame_bytes, STUDIO_MAX_RUN_FRAME_JSON,
                                         'Studio house-window frame metadata')
                    if (not isinstance(frame, dict) or frame.get('studioRunId') != run_id
                            or type(frame.get('studioProcessId')) is not int or frame['studioProcessId'] <= 0
                            or not isinstance(frame.get('pngSha256'), str)
                            or not re.fullmatch('[a-f0-9]{64}', frame['pngSha256'])
                            or frame['pngSha256'] != png_hash
                            or frame.get('point') != checkpoint):
                        raise ValueError('Studio house-window frame metadata is invalid: ' + checkpoint)
                    if capture_process_id is not None and frame['studioProcessId'] != capture_process_id:
                        raise ValueError('Studio house-window frames came from different child processes: ' + run_id)
                    capture_process_id = frame['studioProcessId']
                    frame_metadata[checkpoint] = frame
                    payloads.append((png_relative, png_path, png_hash))
                    payloads.append((json_relative, frame_bytes, hashlib.sha256(frame_bytes).hexdigest()))

                summary_name = '_capture-summary.json'
                if summary_name in names:
                    summary_relative = f'.urman-studio/runs/{run_id}/{summary_name}'
                    _, summary_bytes = _studio_read(source, summary_relative, STUDIO_MAX_RUN_FRAME_JSON,
                                                     'Studio house-window capture summary')
                    summary = _studio_json(summary_bytes, STUDIO_MAX_RUN_FRAME_JSON,
                                           'Studio house-window capture summary')
                    summary_frames = summary.get('frames') if isinstance(summary, dict) else None
                    if (not isinstance(summary_frames, list) or len(summary_frames) > len(STUDIO_HOUSE_WINDOW_IDS)
                            or type(summary.get('pointCount')) is not int
                            or summary['pointCount'] != len(summary_frames)):
                        raise ValueError('Studio house-window capture summary is invalid: ' + run_id)
                    summary_ids = []
                    for frame in summary_frames:
                        checkpoint = frame.get('point') if isinstance(frame, dict) else None
                        if (checkpoint not in STUDIO_HOUSE_WINDOW_IDS or checkpoint not in frame_metadata
                                or checkpoint in summary_ids):
                            raise ValueError('Studio house-window capture summary frame is invalid: ' + run_id)
                        metadata = frame_metadata[checkpoint]
                        if (frame.get('studioRunId') != run_id
                                or frame.get('studioProcessId') != metadata['studioProcessId']
                                or frame.get('pngSha256') != metadata['pngSha256']):
                            raise ValueError('Studio house-window capture summary does not match its frame: ' + checkpoint)
                        summary_ids.append(checkpoint)
                    if set(summary_ids) != set(frame_metadata):
                        raise ValueError('Studio house-window capture summary does not match saved frames: ' + run_id)
                    payloads.append((summary_relative, summary_bytes,
                                     hashlib.sha256(summary_bytes).hexdigest()))

    for request_id, action_id, transaction_id in action_transaction_refs:
        if transaction_id not in transactions_by_request.get(request_id, set()):
            raise ValueError('Studio action transaction does not belong to its request: ' + action_id)

    current_targets = {}
    inspected_bytes = 0
    for request_id in sorted(request_ids):
        if request_id in road_request_paths:
            targets = [road_request_paths[request_id]]
        else:
            model = f'game/assets/models/studio/hero-house-{request_id}.glb'
            targets = sorted({'game/content/studio/hero_house.recipe.json', model, model + '.manifest.json',
                              f'assets/source/blender/studio/hero-house-{request_id}.blend'})
        entries = []
        for relative in targets:
            if relative in current_targets:
                entries.append(current_targets[relative])
                continue
            path = _studio_path(source, relative, missing=True)
            if path is None:
                if request_id in road_request_paths:
                    raise ValueError('Studio road request source is missing from the current tree: ' + relative)
                entry = {'path': relative, 'present': False, 'bytes': 0, 'sha256': None}
            else:
                size, sha = _studio_hash(path, STUDIO_MAX_ROAD_SOURCE if relative in STUDIO_ROAD_SOURCE_PATHS
                                         else STUDIO_MAX_BINARY)
                if request_id in road_request_paths and sha != road_request_hashes[request_id]:
                    pairs = road_transaction_hash_pairs.get(relative, ())
                    reachable = {road_request_hashes[request_id]}
                    frontier = [road_request_hashes[request_id]]
                    while frontier and sha not in reachable:
                        current_hash = frontier.pop()
                        for before_hash, after_hash in pairs:
                            neighbor = after_hash if before_hash == current_hash else (
                                before_hash if after_hash == current_hash else None)
                            if neighbor is not None and neighbor not in reachable:
                                reachable.add(neighbor)
                                frontier.append(neighbor)
                    if sha not in reachable:
                        raise ValueError('Studio road source and frozen request are not joined by active committed transaction hashes: '
                                         + relative)
                inspected_bytes += size
                if inspected_bytes > STUDIO_MAX_EXPORT:
                    raise ValueError('Studio current-target evidence exceeds the 512 MiB inspection budget')
                entry = {'path': relative, 'present': True, 'bytes': size, 'sha256': sha}
                payloads.append((relative, path, sha))
            current_targets[relative] = entry
            entries.append(entry)
        request_ids[request_id]['_currentTargets'] = entries

    receipt = {
        'kind': 'urman.studio-session-current-targets',
        'schemaVersion': 1,
        'evidenceOnly': True,
        'note': 'Current target presence and hashes are observations of the source tree; no local Undo history import or asset replay is claimed.',
        'requests': [{'requestId': request_id, 'targets': request_ids[request_id]['_currentTargets']}
                     for request_id in sorted(request_ids)],
    }
    receipt_bytes = canonical(receipt)
    payloads.append(('current-targets.json', receipt_bytes, None))

    for relative in sorted(direct_transaction_targets | road_source_targets | checkpoint_transaction_targets):
        _, target_bytes = _studio_read(source, relative,
                                       STUDIO_MAX_ROAD_SOURCE if relative in STUDIO_ROAD_SOURCE_PATHS
                                       else STUDIO_MAX_AUTHORED_JSON,
                                       'Studio authored source')
        _studio_checkpoint_source_values(target_bytes, relative)
        target_hash = hashlib.sha256(target_bytes).hexdigest()
        if relative in current_targets:
            recorded_target = current_targets[relative]
            if (not recorded_target['present'] or recorded_target['bytes'] != len(target_bytes)
                    or recorded_target['sha256'] != target_hash):
                raise ValueError('Studio current-target receipt changed before evidence export: ' + relative)
        else:
            payloads.append((relative, target_bytes, target_hash))

    total_bytes = 0
    seen_payload_paths = set()
    for relative, source_or_bytes, expected_hash in payloads:
        if relative in seen_payload_paths:
            raise ValueError('Studio evidence contains a duplicate exported path')
        seen_payload_paths.add(relative)
        file_limit = _studio_export_file_limit(relative)
        if isinstance(source_or_bytes, bytes):
            size = len(source_or_bytes)
        else:
            size = _studio_path(source, relative).lstat().st_size
        if size > file_limit:
            raise ValueError('Studio evidence file exceeds its size limit')
        total_bytes += size
        if total_bytes > STUDIO_MAX_EXPORT:
            raise ValueError('Studio evidence exceeds the 512 MiB export budget')

    studio_output = output / 'studio-session'
    if studio_output.exists() or studio_output.is_symlink():
        raise ValueError('Studio evidence output already exists')
    studio_output.mkdir()
    _studio_path(output, 'studio-session', kind='dir')
    copied_bytes = 0
    for relative, source_or_bytes, expected_hash in payloads:
        parent = studio_output
        parts = relative.split('/')
        for part in parts[:-1]:
            parent = parent / part
            if not parent.exists():
                parent.mkdir()
            _studio_path(output, 'studio-session/' + str(parent.relative_to(studio_output)).replace('\\', '/'), kind='dir')
        destination_relative = 'studio-session/' + relative
        destination = _studio_path(output, destination_relative, missing=True)
        if destination is not None:
            raise ValueError('Studio evidence output path already exists')
        destination = output / destination_relative
        hasher = hashlib.sha256()
        written = 0
        source_size = len(source_or_bytes) if isinstance(source_or_bytes, bytes) else None
        file_limit = _studio_export_file_limit(relative)
        with destination.open('xb') as stream:
            if isinstance(source_or_bytes, bytes):
                if len(source_or_bytes) > file_limit:
                    raise ValueError('Studio evidence file grew beyond its size limit')
                if copied_bytes + len(source_or_bytes) > STUDIO_MAX_EXPORT:
                    raise ValueError('Studio evidence grew beyond the 512 MiB export budget')
                stream.write(source_or_bytes)
                hasher.update(source_or_bytes)
                written = len(source_or_bytes)
            else:
                source_path = _studio_path(source, relative)
                source_info = source_path.lstat()
                source_size = source_info.st_size
                if source_size > file_limit:
                    raise ValueError('Studio evidence file exceeds its size limit')
                with source_path.open('rb') as incoming:
                    for chunk in iter(lambda: incoming.read(1024**2), b''):
                        next_written = written + len(chunk)
                        if next_written > file_limit:
                            raise ValueError('Studio evidence file grew beyond its size limit')
                        if copied_bytes + next_written > STUDIO_MAX_EXPORT:
                            raise ValueError('Studio evidence grew beyond the 512 MiB export budget')
                        stream.write(chunk)
                        hasher.update(chunk)
                        written = next_written
        if written != source_size:
            raise ValueError('Studio evidence changed while being exported: ' + relative)
        if expected_hash is not None and hasher.hexdigest() != expected_hash:
            raise ValueError('Studio evidence hash mismatch: ' + relative)
        copied_bytes += written
        if copied_bytes > STUDIO_MAX_EXPORT:
            raise ValueError('Studio evidence grew beyond the 512 MiB export budget')


def _archive_result(path, output):
    job_info = Path(path).lstat()
    if _studio_reparse(job_info) or not stat.S_ISDIR(job_info.st_mode):
        raise ValueError('result job root is not a regular directory')
    output_info = Path(output).lstat()
    if _studio_reparse(output_info) or not stat.S_ISDIR(output_info.st_mode):
        raise ValueError('result output root is not a regular directory')
    files = []
    total = 0
    pending = [(Path(output), '')]
    directories = 0
    while pending:
        directory, prefix = pending.pop()
        if prefix:
            directory = _studio_path(output, prefix, kind='dir')
        else:
            directory_info = directory.lstat()
            if _studio_reparse(directory_info) or not stat.S_ISDIR(directory_info.st_mode):
                raise ValueError('result output path changed during archive scan')
        with os.scandir(directory) as entries:
            for entry in entries:
                info = entry.stat(follow_symlinks=False)
                if _studio_reparse(info):
                    raise ValueError('result contains a link or reparse point')
                relative = f'{prefix}/{entry.name}' if prefix else entry.name
                if stat.S_ISDIR(info.st_mode):
                    directories += 1
                    if directories > MAX_RESULT_FILES:
                        raise ValueError('result exceeds the bounded directory-entry limit')
                    pending.append((Path(entry.path), relative))
                    continue
                if not stat.S_ISREG(info.st_mode):
                    raise ValueError('result contains a non-regular file')
                total += info.st_size
                if total > MAX_RESULT_BYTES or len(files) >= MAX_RESULT_FILES:
                    raise ValueError('result exceeds the shared size or file-count limit')
                files.append((relative, info.st_size))
    files.sort(key=lambda item: item[0])
    temp_path = Path(path) / 'result.zip.tmp'
    result_path = Path(path) / 'result.zip'
    for candidate in (temp_path, result_path):
        try:
            candidate_info = candidate.lstat()
        except FileNotFoundError:
            continue
        if _studio_reparse(candidate_info) or not stat.S_ISREG(candidate_info.st_mode):
            raise ValueError('result archive path is not a regular file')
        raise ValueError('result archive path already exists')
    actual_total = 0
    with zipfile.ZipFile(temp_path, 'x', zipfile.ZIP_DEFLATED) as archive:
        for relative, expected_size in files:
            # Recheck the complete source path immediately before opening it;
            # archive.write() would reopen it internally after this check.
            item = _studio_path(output, relative)
            info = item.lstat()
            if _studio_reparse(info) or not stat.S_ISREG(info.st_mode) or info.st_size != expected_size:
                raise ValueError('result file changed before archive: ' + relative)
            actual_size = 0
            with item.open('rb') as incoming, archive.open(relative, 'w', force_zip64=True) as entry:
                for chunk in iter(lambda: incoming.read(1024**2), b''):
                    actual_size += len(chunk)
                    actual_total += len(chunk)
                    if actual_total > MAX_RESULT_BYTES:
                        raise ValueError('result contents grew beyond the shared size limit')
                    entry.write(chunk)
                    if archive.fp.tell() > MAX_RESULT_BYTES:
                        raise ValueError('result archive grew beyond the shared size limit')
            if actual_size != expected_size or item.lstat().st_size != expected_size:
                raise ValueError('result file changed while being archived: ' + relative)
    temp_info = temp_path.lstat()
    if (_studio_reparse(temp_info) or not stat.S_ISREG(temp_info.st_mode)
            or temp_info.st_size > MAX_RESULT_BYTES):
        raise ValueError('result archive exceeds the shared size limit')
    with zipfile.ZipFile(temp_path, 'r') as archive:
        if (len(archive.infolist()) != len(files)
                or sum(entry.file_size for entry in archive.infolist()) != actual_total):
            raise ValueError('result archive verification failed')
        if archive.testzip() is not None:
            raise ValueError('result archive CRC verification failed')
    # Windows fsync uses CRT _commit, which needs a writable handle.
    # Open the verified completed archive without truncating its contents.
    with temp_path.open('r+b') as stream:
        os.fsync(stream.fileno())
    result_hash = digest(temp_path)
    temp_path.replace(result_path)
    return result_hash


def _write_result_unavailable(path, status, error):
    status.update(status='FAIL', exit_code=1, error=str(error)[:2048],
                  result_unavailable=True, finished=time.time())
    status.pop('result_sha256', None)
    try:
        _studio_write_durable_json(Path(path) / 'status.json', status)
    except OSError as write_error:
        print('station terminal status write failed:', write_error, file=sys.stderr)

def tree_bytes(root):
    return sum(p.stat().st_size for p in root.rglob('*') if p.is_file())

def blob_path(blobs, sha):
    return blobs / sha[:2] / sha

def store_blob(blobs, sha, stream, size, *, maximum=MAX_BLOB):
    """Store one content-addressed file. The path is its hash, so no name can escape."""
    if (type(maximum) is not int or not 0 < maximum <= MAX_ARCHIVE
            or not re.fullmatch('[a-f0-9]{64}', sha) or not 0 < size <= maximum):
        raise ValueError('invalid blob')
    target = blob_path(blobs, sha)
    if target.is_file() and target.stat().st_size == size:
        return False
    target.parent.mkdir(parents=True, exist_ok=True)
    partial = target.with_name(target.name + '.part')
    h = hashlib.sha256(); written = 0
    with partial.open('wb') as out:
        while written < size:
            chunk = stream.read(min(1024**2, size - written))
            if not chunk:
                raise ValueError('incomplete blob upload')
            out.write(chunk); h.update(chunk); written += len(chunk)
    if h.hexdigest() != sha:
        partial.unlink(missing_ok=True)
        raise ValueError('blob hash mismatch')
    partial.replace(target)
    return True

def seed_blobs_from_zip(archive, blobs):
    """Reuse an already uploaded snapshot so an empty blob store is not paid for twice."""
    stored = 0
    with zipfile.ZipFile(archive) as z:
        manifest = json.loads(z.read('snapshot-manifest.json'))
        validate_manifest(manifest)
        for name, meta in manifest['files'].items():
            if store_blob(blobs, meta['sha256'], z.open(name), meta['bytes']):
                stored += 1
    return stored

def materialize_from_blobs(source, manifest, blobs):
    """Rebuild an exact source tree from cached blobs; every file hash is checked again."""
    validate_manifest(manifest)
    for name, meta in manifest['files'].items():
        src = blob_path(blobs, meta['sha256'])
        if not src.is_file() or src.stat().st_size != meta['bytes']:
            raise ValueError('missing blob for ' + name)
        dest = source / name; dest.parent.mkdir(parents=True, exist_ok=True)
        h = hashlib.sha256(); size = 0
        with src.open('rb') as incoming, dest.open('wb') as outgoing:
            for chunk in iter(lambda: incoming.read(1024**2), b''):
                outgoing.write(chunk); h.update(chunk); size += len(chunk)
        if size != meta['bytes'] or h.hexdigest() != meta['sha256']:
            raise ValueError('blob content mismatch: ' + name)
    (source / 'snapshot-manifest.json').write_bytes(canonical(manifest))
    return manifest

def write_json(path, value):
    tmp = path.with_suffix('.tmp')
    tmp.write_bytes(canonical(value)); tmp.replace(path)

def desktop():
    from ctypes import wintypes

    session = ctypes.c_ulong()
    ctypes.windll.kernel32.ProcessIdToSessionId(os.getpid(), ctypes.byref(session))
    user32 = ctypes.windll.user32
    user32.OpenInputDesktop.restype = ctypes.c_void_p
    handle = user32.OpenInputDesktop(0, False, 1)
    unlocked = bool(handle)
    if handle:
        user32.CloseDesktop.argtypes = [ctypes.c_void_p]; user32.CloseDesktop(handle)
    result = {'session_id': session.value, 'interactive': session.value != 0, 'input_desktop': unlocked,
              'foreground_hwnd': None, 'foreground_pid': None,
              'cursor_clip_rect': None, 'virtual_screen_rect': None}
    # Observe this worker session's desktop only; these calls never activate a
    # window, move input, or change cursor confinement. Missing data stays unknown.
    try:
        user32.GetForegroundWindow.argtypes = []
        user32.GetForegroundWindow.restype = wintypes.HWND
        user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
        user32.GetWindowThreadProcessId.restype = wintypes.DWORD
        foreground = user32.GetForegroundWindow()
        if foreground:
            result['foreground_hwnd'] = int(foreground)
            pid = wintypes.DWORD()
            if user32.GetWindowThreadProcessId(foreground, ctypes.byref(pid)) and pid.value:
                result['foreground_pid'] = pid.value
    except (OSError, AttributeError, ctypes.ArgumentError):
        pass
    try:
        user32.GetClipCursor.argtypes = [ctypes.POINTER(wintypes.RECT)]
        user32.GetClipCursor.restype = wintypes.BOOL
        clip = wintypes.RECT()
        if user32.GetClipCursor(ctypes.byref(clip)):
            result['cursor_clip_rect'] = [clip.left, clip.top, clip.right, clip.bottom]
        user32.GetSystemMetrics.argtypes = [ctypes.c_int]
        user32.GetSystemMetrics.restype = ctypes.c_int
        x, y, width, height = (user32.GetSystemMetrics(index) for index in (76, 77, 78, 79))
        if width > 0 and height > 0:
            result['virtual_screen_rect'] = [x, y, x + width, y + height]
    except (OSError, AttributeError, ctypes.ArgumentError):
        pass
    return result


def _windows_process_table():
    if sys.platform != 'win32':
        return {}
    from ctypes import wintypes

    class ProcessEntry32W(ctypes.Structure):
        _fields_ = [
            ('dwSize', wintypes.DWORD), ('cntUsage', wintypes.DWORD),
            ('th32ProcessID', wintypes.DWORD), ('th32DefaultHeapID', ctypes.c_size_t),
            ('th32ModuleID', wintypes.DWORD), ('cntThreads', wintypes.DWORD),
            ('th32ParentProcessID', wintypes.DWORD), ('pcPriClassBase', wintypes.LONG),
            ('dwFlags', wintypes.DWORD), ('szExeFile', wintypes.WCHAR * 260),
        ]

    kernel32 = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel32.CreateToolhelp32Snapshot.argtypes = [wintypes.DWORD, wintypes.DWORD]
    kernel32.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
    kernel32.Process32FirstW.argtypes = [wintypes.HANDLE, ctypes.POINTER(ProcessEntry32W)]
    kernel32.Process32FirstW.restype = wintypes.BOOL
    kernel32.Process32NextW.argtypes = [wintypes.HANDLE, ctypes.POINTER(ProcessEntry32W)]
    kernel32.Process32NextW.restype = wintypes.BOOL
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.CloseHandle.restype = wintypes.BOOL

    snapshot = kernel32.CreateToolhelp32Snapshot(0x00000002, 0)
    if snapshot == wintypes.HANDLE(-1).value:
        raise OSError(ctypes.get_last_error(), 'cannot enumerate station processes')
    processes = {}
    try:
        entry = ProcessEntry32W()
        entry.dwSize = ctypes.sizeof(entry)
        if kernel32.Process32FirstW(snapshot, ctypes.byref(entry)):
            while True:
                pid = int(entry.th32ProcessID)
                processes[pid] = (int(entry.th32ParentProcessID), str(entry.szExeFile))
                entry.dwSize = ctypes.sizeof(entry)
                if not kernel32.Process32NextW(snapshot, ctypes.byref(entry)):
                    break
    finally:
        kernel32.CloseHandle(snapshot)
    return processes


def _windows_process_image(pid):
    if sys.platform != 'win32':
        return None
    from ctypes import wintypes

    kernel32 = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel32.OpenProcess.restype = wintypes.HANDLE
    kernel32.QueryFullProcessImageNameW.argtypes = [
        wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
    kernel32.QueryFullProcessImageNameW.restype = wintypes.BOOL
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.CloseHandle.restype = wintypes.BOOL

    handle = kernel32.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
    if not handle:
        return None
    try:
        buffer = ctypes.create_unicode_buffer(32768)
        length = wintypes.DWORD(len(buffer))
        if not kernel32.QueryFullProcessImageNameW(handle, 0, buffer, ctypes.byref(length)):
            return None
        return buffer.value
    finally:
        kernel32.CloseHandle(handle)


def _windows_descendants(root_pid):
    processes = _windows_process_table()
    if root_pid not in processes:
        return {}
    children = {}
    for pid, (parent, name) in processes.items():
        children.setdefault(parent, []).append((pid, name))
    descendants = {}
    pending = [root_pid]
    while pending:
        parent = pending.pop()
        for pid, name in children.get(parent, ()):
            if pid in descendants:
                continue
            descendants[pid] = name
            pending.append(pid)
    return descendants


class _CandidateExecutablePin:
    """Bind one verified EXE to a live handle that denies write/delete sharing."""
    def __init__(self, path, manifest):
        import msvcrt
        from ctypes import wintypes
        class FileInfo(ctypes.Structure):
            _fields_ = [(name, wintypes.DWORD) for name in (
                'attributes', 'creation_low', 'creation_high', 'access_low', 'access_high',
                'write_low', 'write_high', 'volume', 'size_high', 'size_low',
                'links', 'index_high', 'index_low')]
        if ctypes.sizeof(FileInfo) != 52:
            raise ValueError('unsupported candidate pin file identity ABI')
        self.kernel32 = ctypes.WinDLL('kernel32', use_last_error=True)
        self.kernel32.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                                            ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
        self.kernel32.CreateFileW.restype = wintypes.HANDLE
        self.kernel32.GetFileInformationByHandle.argtypes = [wintypes.HANDLE, ctypes.POINTER(FileInfo)]
        self.kernel32.GetFileInformationByHandle.restype = wintypes.BOOL
        self.kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
        self.kernel32.CloseHandle.restype = wintypes.BOOL
        self.info_type = FileInfo
        self.os_handle = msvcrt.get_osfhandle
        self.path = os.path.normcase(os.path.abspath(path))
        self.sha256 = manifest['windows_exe_sha256']
        self.size = manifest['windows_exe_bytes']
        self.stream = None
        handle = self._open(path)
        try:
            descriptor = msvcrt.open_osfhandle(handle, os.O_RDONLY | os.O_BINARY)
        except Exception:
            self.kernel32.CloseHandle(handle)
            raise
        try:
            self.stream = os.fdopen(descriptor, 'rb')
            self.identity = self._identity(handle)
            actual = hashlib.sha256()
            for chunk in iter(lambda: self.stream.read(1024**2), b''):
                actual.update(chunk)
            if actual.hexdigest() != self.sha256:
                raise ValueError('candidate executable pin SHA256 mismatch')
            self.verify(path, manifest)
        except Exception:
            if self.stream is None:
                os.close(descriptor)
            else:
                self.close()
            raise

    def _open(self, path):
        # OPEN_EXISTING + OPEN_REPARSE_POINT; never allow write/delete sharing.
        handle = self.kernel32.CreateFileW(str(path), 0x80000000, 1, None, 3, 0x00200000, None)
        if handle == ctypes.c_void_p(-1).value:
            raise OSError(ctypes.get_last_error(), 'candidate executable pin open failed')
        return handle

    def _identity(self, handle):
        info = self.info_type()
        if (not self.kernel32.GetFileInformationByHandle(handle, ctypes.byref(info))
                or info.attributes & 0x0410
                or not (info.index_high or info.index_low)
                or (info.size_high << 32 | info.size_low) != self.size):
            raise ValueError('candidate executable pin identity unavailable')
        return (f'{info.volume:08x}:{info.index_high:08x}{info.index_low:08x}:'
                f'{info.size_high:08x}{info.size_low:08x}:{info.write_high:08x}{info.write_low:08x}')

    def verify(self, path, manifest):
        if (self.stream is None or self.stream.closed
                or os.path.normcase(os.path.abspath(path)) != self.path
                or manifest['windows_exe_sha256'] != self.sha256 or manifest['windows_exe_bytes'] != self.size
                or self._identity(self.os_handle(self.stream.fileno())) != self.identity):
            raise ValueError('candidate executable pin changed')
        other = self._open(path)
        try:
            if self._identity(other) != self.identity:
                raise ValueError('candidate executable path no longer names its pinned file')
        finally:
            self.kernel32.CloseHandle(other)

    def close(self):
        if self.stream is not None:
            self.stream.close()

class Station:
    def __init__(self, config):
        self.config = config
        self.repo = Path(config['repository']).resolve()
        self.worker_code_sha256 = digest(Path(__file__))
        self.data = Path(config['data']).resolve(); self.data.mkdir(parents=True, exist_ok=True)
        self.lock = threading.Lock(); self.busy = None
        self.job_owners = {}
        self.ui_refusal_attempted = set()
        self.active_spec = None
        self.active_source = None
        self.active_runner = None
        self.active_candidate = None
        self.active_candidate_pin = None
        self.active_candidate_recovery = False
        self.active_candidate_helper_sha256 = None
        self.snapshots = self.data / 'snapshots'; self.snapshots.mkdir(exist_ok=True)
        self.jobs = self.data / 'runs'; self.jobs.mkdir(exist_ok=True)
        self.blobs = self.data / 'blobs'; self.blobs.mkdir(exist_ok=True)
        self.manifests = self.data / 'manifests'; self.manifests.mkdir(exist_ok=True)
        self.candidates = self.data / 'candidates'; self.candidates.mkdir(exist_ok=True)
        self.seeded = False
        # One worker instance per account, including startup/restart races.
        import msvcrt
        self.instance = (self.data / 'worker.lock').open('a+b')
        self.instance.write(b' '); self.instance.flush(); self.instance.seek(0)
        msvcrt.locking(self.instance.fileno(), msvcrt.LK_NBLCK, 1)
        for status in self.jobs.glob('*/status.json'):
            value = json.loads(status.read_text(encoding='utf-8-sig'))
            if value['status'] == 'RUNNING':
                value.update(status='FAIL', exit_code=1,
                             error='Worker restarted during job; inspect recovery marker and processes',
                             finished=time.time(), result_unavailable=True)
                value.pop('result_sha256', None)
                _studio_write_durable_json(status, value)

    def manifest_for(self, sid):
        path = self.manifests / (sid + '.json')
        return json.loads(path.read_text(encoding='utf-8-sig')) if path.is_file() else None

    def candidate_for(self, candidate_id):
        path = self.candidates / (candidate_id + '.json')
        if not path.is_file():
            return None
        record = json.loads(path.read_text(encoding='utf-8-sig'))
        if (not isinstance(record, dict) or record.get('candidate_id') != candidate_id
                or validate_candidate_manifest(record.get('manifest', {})) != candidate_id
                or not isinstance(record.get('owner'), str) or not record['owner']):
            raise ValueError('stored Windows candidate manifest is invalid')
        return record

    def plan_candidate(self, manifest, principal):
        if not isinstance(principal, str) or not principal:
            return 401, {'error': 'unambiguous candidate owner required'}
        candidate_id = validate_candidate_manifest(manifest)
        path = self.candidates / (candidate_id + '.json')
        with self.lock:
            if self.busy:
                return 409, {'error': 'station busy', 'job_id': self.busy}
            health = self.health(candidate_boot=True)
            if not health['ready']:
                return 503, {'error': 'station not ready', 'health': health}
            if path.is_file():
                record = self.candidate_for(candidate_id)
                if record['owner'] != principal:
                    return 403, {'error': 'candidate belongs to another principal'}
                if record['manifest'] != manifest:
                    return 409, {'error': 'candidate identity collision'}
                record['last_used'] = time.time()
            else:
                now = time.time()
                record = {'candidate_id': candidate_id, 'manifest': manifest,
                          'owner': principal, 'created': now, 'last_used': now}
            _studio_write_durable_json(path, record)
            # Drop stale rebuildable candidate upload cache before the next large blob
            # upload reaches the disk-space guard. The new plan is kept by its fresh mtime.
            self.reclaim(None)
        missing = sorted({manifest[name + '_sha256'] for name in ('receipt', 'windows_zip')
                          if not blob_path(self.blobs, manifest[name + '_sha256']).is_file()})
        return 200, {'candidate_id': candidate_id, 'known': not missing, 'missing': missing}

    def verified_snapshot_manifest(self, sid):
        manifest = self.manifest_for(sid)
        if manifest is not None:
            if validate_manifest(manifest) != sid:
                raise ValueError('snapshot manifest identity does not match requested snapshot')
            return manifest
        archive_path = self.snapshots / (sid + '.zip')
        if not archive_path.is_file():
            return None
        with zipfile.ZipFile(archive_path) as archive:
            try:
                entry = archive.getinfo('snapshot-manifest.json')
            except KeyError as error:
                raise ValueError('snapshot manifest is missing') from error
            if entry.file_size > 8 * 1024**2:
                raise ValueError('snapshot manifest exceeds the size limit')
            manifest = json.loads(archive.read(entry))
        if validate_manifest(manifest) != sid:
            raise ValueError('snapshot manifest identity does not match requested snapshot')
        return manifest

    def blob_usage(self):
        files = 0; total = 0
        for path in self.blobs.glob('*/*'):
            if path.is_file():
                files += 1; total += path.stat().st_size
        return {'files': files, 'megabytes': round(total / 1024**2, 1)}

    def seed_from_latest_snapshot(self):
        """Once: fill the blob store from the newest cached snapshot, so the first
        incremental run sends only what changed instead of the whole tree again."""
        self.seeded = True
        archives = sorted(self.snapshots.glob('*.zip'), key=lambda p: p.stat().st_mtime, reverse=True)
        if not archives:
            return 0
        try:
            return seed_blobs_from_zip(archives[0], self.blobs)
        except (ValueError, OSError, KeyError, zipfile.BadZipFile) as error:
            print('blob seed skipped:', error, file=sys.stderr)
            return 0

    def prune_manifests(self):
        extra = sorted(self.manifests.glob('*.json'), key=lambda p: p.stat().st_mtime, reverse=True)
        for path in extra[KEEP_MANIFESTS:]:
            path.unlink(missing_ok=True)

    def reclaim(self, used_snapshot):
        """Free what the station can rebuild by itself: unpacked source trees, superseded
        archives, interrupted uploads and blobs no stored manifest references. Receipts,
        logs, frames and result.zip stay, so finished checks remain auditable."""
        freed = 0
        # Only this job runs at a time, so every leftover source tree here is already finished.
        for source in self.jobs.glob('*/source'):
            if source.is_dir():
                try:
                    (source.parent / STUDIO_PRESERVE_MARKER).lstat()
                    continue
                except FileNotFoundError:
                    pass
                except OSError:
                    # An unreadable marker is not proof that the source is disposable.
                    continue
                freed += tree_bytes(source)
                shutil.rmtree(source, ignore_errors=True)
        cutoff = time.time() - BLOB_GRACE
        live = set()
        for manifest in self.manifests.glob('*.json'):
            try:
                live |= {meta['sha256'] for meta in
                         json.loads(manifest.read_text(encoding='utf-8-sig'))['files'].values()}
            except (ValueError, KeyError, OSError):
                continue
        candidate_plans = sorted(self.candidates.glob('*.json'),
                                 key=lambda path: path.stat().st_mtime, reverse=True)
        newest_candidates = {path.name for path in candidate_plans[:KEEP_CANDIDATES]}
        active_candidate = (self.active_spec.get('candidate_id')
                            if isinstance(self.active_spec, dict)
                            and self.active_spec.get('mode') in {'candidate-boot', 'candidate-play'} else None)
        for candidate in candidate_plans:
            if (candidate.name in newest_candidates or candidate.stem == active_candidate
                    or candidate.stat().st_mtime >= cutoff):
                continue
            candidate.unlink(missing_ok=True)
        for candidate in self.candidates.glob('*.json'):
            try:
                manifest = json.loads(candidate.read_text(encoding='utf-8-sig'))['manifest']
                validate_candidate_manifest(manifest)
                live |= {manifest[name + '_sha256'] for name in ('receipt', 'windows_zip')}
            except (ValueError, KeyError, OSError):
                continue
        archives = sorted(self.snapshots.glob('*.zip'), key=lambda p: p.stat().st_mtime, reverse=True)
        for path in archives[KEEP_ARCHIVES:]:
            if path.stem == used_snapshot or path.stat().st_mtime > cutoff:
                continue
            freed += path.stat().st_size
            path.unlink(missing_ok=True)
        for path in self.blobs.glob('*/*'):
            if not path.is_file() or path.stat().st_mtime > cutoff:
                continue
            if path.suffix != '.part' and path.name in live:
                continue
            freed += path.stat().st_size
            path.unlink(missing_ok=True)
        return freed

    def disk(self):
        use = shutil.disk_usage(self.data)
        return {'path': str(self.data), 'free_gb': round(use.free / 1024**3, 1),
                'total_gb': round(use.total / 1024**3, 1)}

    def recovery(self):
        if self.active_candidate_recovery:
            return True
        marker = Path(os.environ['APPDATA']) / 'Godot/app_userdata/.URMAN.protected-run.lock'
        if not marker.exists():
            return False
        with marker.open('rb') as stream:
            stream.seek(1)
            return bool(stream.read().strip())

    def health(self, candidate_boot=False):
        if candidate_boot:
            python = str(self.config['python'])
            python_path = Path(python) if Path(python).is_file() else shutil.which(python)
            system_root = os.environ.get('SystemRoot', '')
            powershell = Path(system_root) / 'System32/WindowsPowerShell/v1.0/powershell.exe'
            tools = (python_path is not None and powershell.is_file() and (self.repo / '.tools').is_dir()
                     and all((self.repo/'eng'/name).is_file() for name in
                             ('run-windows-check.ps1','protected_run.py','remote_common.py')))
            d = {'interactive': None, 'input_desktop': None, 'required': False}
            pins = None
            godot = None
        else:
            pins = json.loads((self.repo / 'eng/toolchain.json').read_text(encoding='utf-8-sig'))
            v = pins['godot']['version'].split('.stable')[0]
            godot = self.repo / f'.tools/godot/windows/Godot_v{v}-stable_mono_win64/Godot_v{v}-stable_mono_win64_console.exe'
            dotnet = self.repo / '.tools/dotnet/dotnet.exe'
            tools = (godot.is_file() and dotnet.is_file() and Path(self.config['python']).is_file()
                     and all((self.repo/'eng'/name).is_file() for name in
                             ('run-windows-check.ps1','protected_run.py','remote_common.py','windows-game-ui.ps1')))
            d = desktop()
        pending = self.recovery()
        pipeline_locked = False
        try:
            with (self.repo/'.tools/windows-station.lock').open('ab'):
                pass
        except OSError:
            pipeline_locked = True
        # Restart with an orphan runner is blocked even if the source lock is gone.
        if candidate_boot:
            orphaned = pipeline_locked and not self.busy
        else:
            # The scheduled worker runs through pythonw: health must not create a
            # console window that steals focus and auto-pauses the owned game.
            orphan = subprocess.run(['tasklist','/FI','IMAGENAME eq '+godot.name,'/FO','CSV','/NH'],capture_output=True,text=True,creationflags=subprocess.CREATE_NO_WINDOW).stdout
            main_name = godot.name.replace('_console.exe', '.exe')
            orphan += subprocess.run(['tasklist','/FI','IMAGENAME eq '+main_name,'/FO','CSV','/NH'],capture_output=True,text=True,creationflags=subprocess.CREATE_NO_WINDOW).stdout
            orphaned = (godot.name.lower() in orphan.lower() or main_name.lower() in orphan.lower() or pipeline_locked) and not self.busy
        candidate_exes = list(self.jobs.glob('*/candidate/windows/URMAN.exe'))
        candidate_orphaned = False
        if candidate_exes and not self.busy:
            for pid, (_, name) in _windows_process_table().items():
                if name.lower() != 'urman.exe':
                    continue
                image = _windows_process_image(pid)
                image_path = os.path.normcase(os.path.abspath(image)) if image else None
                if image_path is None or any(
                        image_path == os.path.normcase(str(path.resolve(strict=False)))
                        for path in candidate_exes):
                    candidate_orphaned = True
                    break
        orphaned = orphaned or candidate_orphaned
        ready = bool(tools and not pending and not orphaned
                     and (candidate_boot or (d['interactive'] and d['input_desktop'])))
        result = {'ready': ready, 'host': os.environ['COMPUTERNAME'], 'desktop': d, 'tools': tools,
                  'userdata_recovery_pending': pending, 'orphan_game_process': bool(orphaned),
                  'orphan_candidate_process': candidate_orphaned, 'busy_job': self.busy,
                  'blob_store': self.blob_usage(), 'disk': self.disk()}
        if not candidate_boot:
            result.update(godot=pins['godot']['version'],
                          normal_ui_transport_version=NORMAL_UI_TRANSPORT_VERSION,
                          sdk=json.loads((self.repo/'global.json').read_text(encoding='utf-8-sig'))['sdk']['version'],
                          gpu_acceptance=self.config.get('gpu_acceptance', 'not-run'))
        else:
            native_desktop = desktop()
            native_tools = (self.repo / 'eng/windows-game-ui.ps1').is_file()
            result.update(candidate_native_play_version=1,
                          native_play_ready=bool(ready and native_tools
                                                 and native_desktop['interactive']
                                                 and native_desktop['input_desktop']),
                          native_play_desktop=native_desktop, native_play_tools=native_tools)
        return result

    def studio_readiness(self):
        result = {
            'ready': False, 'runtime_verified': False, 'state': 'checked', 'station_ready': False,
            'codex': {'found': False, 'version': None, 'version_confirmed': False, 'logged_in': False},
            'blender': {'found': False, 'version': None, 'version_match': False},
        }
        if not self.lock.acquire(timeout=0.1):
            result['state'] = 'busy'
            return result
        try:
            if self.busy:
                result['state'] = 'busy'
                return result

            try:
                pins = json.loads((self.repo / 'eng/toolchain.json').read_text(encoding='utf-8-sig'))
                godot_version = pins['godot']['version'].split('.stable')[0]
                godot = self.repo / f'.tools/godot/windows/Godot_v{godot_version}-stable_mono_win64/Godot_v{godot_version}-stable_mono_win64_console.exe'
                dotnet = self.repo / '.tools/dotnet/dotnet.exe'
                tools = (godot.is_file() and dotnet.is_file() and Path(self.config['python']).is_file()
                         and all((self.repo / 'eng' / name).is_file() for name in
                                 ('run-windows-check.ps1', 'protected_run.py', 'remote_common.py')))
                d = desktop()
                pending = self.recovery()
                pipeline_locked = False
                try:
                    with (self.repo / '.tools/windows-station.lock').open('ab'):
                        pass
                except OSError:
                    pipeline_locked = True
                if not (tools and d['interactive'] and d['input_desktop'] and not pending and not pipeline_locked):
                    result['state'] = 'station_unavailable'
                    return result
                system_root = os.environ.get('SystemRoot')
                if not system_root:
                    result['state'] = 'station_unavailable'
                    return result
                main_name = godot.name.replace('_console.exe', '.exe')
                code, output, cleanup_ok = _bounded_readiness_command(
                    [str(Path(system_root) / 'System32' / 'tasklist.exe'), '/FO', 'CSV', '/NH'],
                    timeout=4, output_limit=128 * 1024)
                if not cleanup_ok:
                    result['state'] = 'cleanup_unconfirmed'
                    return result
                process_list = output.decode('utf-8', 'replace').lower() if code == 0 else ''
                if code != 0 or godot.name.lower() in process_list or main_name.lower() in process_list:
                    result['state'] = 'station_unavailable'
                    return result
                result['station_ready'] = True
            except (OSError, ValueError, KeyError, TypeError):
                result['state'] = 'station_unavailable'
                return result

            # Keep the existing native-executable precedence; never resolve shell wrappers.
            codex = None
            local = os.environ.get('LOCALAPPDATA')
            if local:
                for relative in ('Programs/OpenAI/Codex/bin/codex.exe', 'Programs/Codex/codex.exe'):
                    candidate = Path(local) / relative
                    if candidate.is_file():
                        codex = candidate
                        break
            if codex is None:
                for directory in os.environ.get('PATH', '').split(os.pathsep):
                    if directory:
                        candidate = Path(directory) / 'codex.exe'
                        if candidate.is_file():
                            codex = candidate
                            break

            if codex is not None:
                result['codex']['found'] = True
                code, output, cleanup_ok = _bounded_readiness_command([str(codex), '--version'])
                if not cleanup_ok:
                    result['state'] = 'cleanup_unconfirmed'
                    return result
                if code == 0:
                    match = re.fullmatch(r'codex-cli (\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?)',
                                         output.decode('utf-8', 'replace').strip())
                    if match:
                        result['codex']['version'] = match.group(1)
                        result['codex']['version_confirmed'] = True
                code, output, cleanup_ok = _bounded_readiness_command([str(codex), 'login', 'status'])
                if not cleanup_ok:
                    result['state'] = 'cleanup_unconfirmed'
                    return result
                if code == 0:
                    lines = [line.strip() for line in output.decode('utf-8', 'replace').splitlines()]
                    positive = [line for line in lines if re.fullmatch(r'Logged in using ChatGPT', line, re.I)]
                    statuses = [line for line in lines if re.match(
                        r'(?:not\s+)?(?:logged|signed)\s+(?:in|out)\b', line, re.I)]
                    has_api_status = any(re.search(r'\bAPI\s+key\b', line, re.I) for line in lines)
                    result['codex']['logged_in'] = (len(positive) == 1 and len(statuses) == 1
                                                    and statuses[0] == positive[0] and not has_api_status)

            try:
                blender_pin = pins['blender']['windows']
                expected = blender_pin['version']
                folder = blender_pin['folder']
                if (isinstance(expected, str) and re.fullmatch(r'\d+\.\d+\.\d+', expected)
                        and isinstance(folder, str) and folder not in ('.', '..')
                        and re.fullmatch(r'[A-Za-z0-9._-]{1,80}', folder)):
                    blender = self.repo / '.tools' / 'blender' / 'windows' / folder / 'blender.exe'
                    if blender.is_file():
                        result['blender']['found'] = True
                        code, output, cleanup_ok = _bounded_readiness_command([str(blender), '--version'])
                        if not cleanup_ok:
                            result['state'] = 'cleanup_unconfirmed'
                            return result
                        if code == 0:
                            first_line = output.decode('utf-8', 'replace').splitlines()[0:1]
                            match = (re.fullmatch(r'Blender\s+(\d+\.\d+\.\d+)(?:\s+.*)?', first_line[0].strip())
                                     if first_line else None)
                            if match:
                                result['blender']['version'] = match.group(1)
                                result['blender']['version_match'] = match.group(1) == expected
            except (OSError, ValueError, KeyError, TypeError):
                pass

            result['ready'] = bool(
                result['station_ready'] and result['codex']['found']
                and result['codex']['version_confirmed'] and result['codex']['logged_in']
                and result['blender']['found'] and result['blender']['version_match'])
            return result
        finally:
            self.lock.release()

    def submit(self, spec, principal):
        if isinstance(spec, dict) and spec.get('mode') in {'candidate-boot', 'candidate-play'}:
            return self.submit_candidate(spec, principal)
        if not isinstance(principal, str) or not principal:
            raise ValueError('authenticated job principal required')
        if set(spec) - {'job_id','snapshot_id','mode','scene','timeout','headless','points','phase','fov','studio_restarts','acceptance_profile'}:
            raise ValueError('unknown job field')
        jid = spec.get('job_id', '')
        if not re.fullmatch('[a-zA-Z0-9_-]{8,64}', jid):
            raise ValueError('invalid job_id')
        sid = spec.get('snapshot_id', '')
        if not re.fullmatch('[a-f0-9]{64}', sid):
            raise ValueError('unknown snapshot')
        if not (self.snapshots / (sid + '.zip')).is_file():
            # Incremental snapshot: the manifest plus every referenced blob must be here.
            manifest = self.manifest_for(sid)
            if manifest is None:
                raise ValueError('unknown snapshot')
            missing = [meta['sha256'] for meta in manifest['files'].values()
                       if not blob_path(self.blobs, meta['sha256']).is_file()]
            if missing:
                raise ValueError(f'snapshot blobs incomplete ({len(missing)} missing); re-run the client')
        mode = spec.get('mode')
        if mode not in ('smoke', 'capture', 'play', 'studio', 'export-release'):
            raise ValueError('unsupported mode')
        release = mode == 'export-release'
        if release and set(spec) != {'job_id', 'snapshot_id', 'mode', 'timeout'}:
            raise ValueError('export-release accepts only job_id, snapshot_id, mode, and timeout')
        restarts = spec.get('studio_restarts', 0)
        if (type(restarts) is not int or not 0 <= restarts <= 2
                or ('studio_restarts' in spec and mode != 'studio')):
            raise ValueError('studio_restarts requires Studio mode and an integer of 0..2')
        scene = spec.get('scene', '')
        timeout_max = (1800 if release else
                       7200 if mode == 'play' and scene == PHOTO_WORLDS_GAME_SCENE else
                       3600 if mode in ('studio', 'play') else 300)
        if type(spec.get('timeout')) is not int or not 1 <= spec['timeout'] <= timeout_max:
            raise ValueError(f'timeout must be 1..{timeout_max} seconds')
        if not release and type(spec.get('headless')) is not bool:
            raise ValueError('headless must be boolean')
        if not release and (not re.fullmatch(r'res://[\w/.-]+\.tscn', scene) or '..' in scene):
            raise ValueError('invalid scene')
        if 'acceptance_profile' in spec:
            acceptance_profile = spec['acceptance_profile']
            if (mode != 'play' or scene != PHOTO_WORLDS_GAME_SCENE
                    or spec.get('headless') is not False):
                raise ValueError('acceptance_profile requires native PhotoWorlds product play')
            if not isinstance(acceptance_profile, str) or not re.fullmatch(
                    r'[a-z0-9][a-z0-9-]{0,47}', acceptance_profile):
                raise ValueError('invalid acceptance_profile')
        if scene == STUDIO_SCENE and mode != 'studio':
            raise ValueError('Studio scene is only available in fixed Studio mode')
        if scene == FULL_GAME_SCENE and mode != 'play':
            raise ValueError('full-game scene is only available in fixed native play mode')
        if scene == PHOTO_WORLDS_GAME_SCENE and mode != 'play':
            raise ValueError('PhotoWorlds product scene is only available in fixed native play mode')
        if mode == 'studio':
            if scene != STUDIO_SCENE or spec['headless']:
                raise ValueError('Studio mode requires the fixed native Studio scene')
            if spec.get('points', ''):
                raise ValueError('Studio mode does not accept capture points')
            manifest = self.verified_snapshot_manifest(sid)
            if manifest is None:
                raise ValueError('Studio mode requires a verified snapshot manifest')
            manifest_files = set(manifest['files'])
            if (manifest_files & STUDIO_AUTHORING_FILES != STUDIO_AUTHORING_FILES
                    or 'game/content/studio/hero_house.recipe.json' not in manifest_files
                    or 'game/studio/studio_main.tscn' not in manifest_files):
                raise ValueError('Studio snapshot requires the fixed scene, two authoring inputs, and HeroHouse recipe')
        if mode == 'play':
            if (scene not in (FULL_GAME_SCENE, ACT1_DEMO_SCENE, PHOTO_WORLDS_GAME_SCENE) or spec['headless']
                    or spec.get('points', '') or spec.get('phase', '') or spec.get('fov', '')
                    or 'studio_restarts' in spec
                    or (scene in (FULL_GAME_SCENE, PHOTO_WORLDS_GAME_SCENE)
                        and not re.fullmatch('[a-f0-9]{32}', jid))):
                raise ValueError('play requires an approved fixed native scene, bounded job ID, and no capture options')
        if mode == 'smoke' and not scene.startswith('res://tests/'):
            raise ValueError('smoke requires an existing test scene')
        if mode == 'capture' and (scene != 'res://scenes/act1_demo.tscn' or spec['headless']):
            raise ValueError('capture requires native act1_demo')
        points = spec.get('points', '')
        number = r'-?\d+(?:\.\d+)?'
        # Authored node IDs include hyphens; wildcard and path characters stay excluded.
        vector = rf'(?:[A-Za-z][A-Za-z0-9_-]*@)?{number},{number},{number}'
        entry = rf'[A-Za-z0-9_-]{{1,48}}:{vector}>{vector}'
        if not isinstance(points, str) or len(points) > 1000 or (points and not re.fullmatch(rf'{entry}(?:;{entry}){{0,7}}', points)):
            raise ValueError('invalid capture points')
        if mode == 'capture' and not points:
            raise ValueError('capture points required')
        phase = spec.get('phase', '')
        if not isinstance(phase, str) or (phase and (mode != 'capture' or not re.fullmatch('[a-z0-9-]{1,48}', phase))):
            raise ValueError('invalid atmosphere phase')
        fov = spec.get('fov', '')
        if not isinstance(fov, str) or (fov and (mode != 'capture' or not re.fullmatch(r'player|\d{2,3}(?:\.\d+)?', fov))):
            raise ValueError('invalid view fov')
        with self.lock:
            path = self.jobs / jid
            if path.exists():
                if not (path/'request.json').is_file() or not (path/'status.json').is_file():
                    return 409, {'error':'incomplete existing job; source is preserved', 'job_id':jid}
                if json.loads((path/'request.json').read_text(encoding='utf-8-sig')) != spec:
                    raise ValueError('job_id reused with different parameters')
                return 200, json.loads((path/'status.json').read_text(encoding='utf-8-sig'))
            if self.busy:
                return 409, {'error':'station busy', 'job_id':self.busy}
            health = self.health()
            if not health['ready']:
                return 503, {'error':'station not ready', 'health':health}
            path.mkdir()
            if scene == STUDIO_SCENE:
                _studio_write_marker(path)
            write_json(path/'request.json', spec)
            status = {'job_id':jid, 'snapshot_id':sid, 'status':'RUNNING', 'started':time.time()}
            write_json(path/'status.json', status)
            self.busy = jid
            self.job_owners[jid] = principal
            self.active_spec = dict(spec)
            self.active_source = path/'source'
            self.active_runner = None
            threading.Thread(target=self.execute, args=(path,spec,status), daemon=False).start()
            return 202, status

    def submit_candidate(self, spec, principal):
        if not isinstance(principal, str) or not principal:
            return 401, {'error': 'unambiguous candidate job principal required'}
        if (set(spec) != {'job_id', 'mode', 'candidate_id', 'timeout'}
                or spec.get('mode') not in {'candidate-boot', 'candidate-play'}):
            raise ValueError('candidate jobs accept only job_id, mode, candidate_id, and timeout')
        jid = spec.get('job_id', '')
        candidate_id = spec.get('candidate_id', '')
        if not re.fullmatch('[a-f0-9]{32}', jid) or not re.fullmatch('[a-f0-9]{64}', candidate_id):
            raise ValueError('invalid candidate job or candidate ID')
        maximum_timeout = 3600 if spec['mode'] == 'candidate-play' else 300
        if type(spec.get('timeout')) is not int or not 1 <= spec['timeout'] <= maximum_timeout:
            raise ValueError(f'candidate timeout must be 1..{maximum_timeout} seconds')
        record = self.candidate_for(candidate_id)
        if record is None:
            raise ValueError('unknown Windows candidate')
        if record['owner'] != principal:
            return 403, {'error': 'candidate belongs to another principal'}
        manifest = record['manifest']
        for name in ('receipt', 'windows_zip'):
            path = blob_path(self.blobs, manifest[name + '_sha256'])
            if not path.is_file() or path.stat().st_size != manifest[name + '_bytes']:
                raise ValueError('candidate upload is incomplete; re-run candidate-boot')
        with self.lock:
            path = self.jobs / jid
            if path.exists():
                if not (path/'request.json').is_file() or not (path/'status.json').is_file():
                    return 409, {'error': 'incomplete existing job; source is preserved', 'job_id': jid}
                owner_path = path / 'owner.json'
                if not owner_path.is_file() or json.loads(owner_path.read_text(encoding='utf-8-sig')).get('principal') != principal:
                    return 403, {'error': 'candidate job belongs to another principal'}
                if json.loads((path/'request.json').read_text(encoding='utf-8-sig')) != spec:
                    raise ValueError('job_id reused with different parameters')
                return 200, json.loads((path/'status.json').read_text(encoding='utf-8-sig'))
            if self.busy:
                return 409, {'error': 'station busy', 'job_id': self.busy}
            health = self.health(candidate_boot=True)
            if not health['native_play_ready' if spec['mode'] == 'candidate-play' else 'ready']:
                return 503, {'error': 'station not ready', 'health': health}
            path.mkdir()
            write_json(path/'request.json', spec)
            _studio_write_durable_json(path/'owner.json', {'principal': principal})
            status = {'job_id': jid, 'candidate_id': candidate_id, 'status': 'RUNNING', 'started': time.time()}
            write_json(path/'status.json', status)
            self.busy = jid
            self.job_owners[jid] = principal
            self.active_spec = dict(spec)
            self.active_source = None
            self.active_runner = None
            self.active_candidate = None
            self.active_candidate_helper_sha256 = None
            threading.Thread(target=self.execute, args=(path, spec, status), daemon=False).start()
            return 202, status

    def _active_game_pid(self, jid):
        runner = self.active_runner
        source = self.active_source
        spec = self.active_spec
        if (spec is None or spec.get('mode') not in {'play', 'candidate-play'}
                or spec.get('job_id') != jid or self.busy != jid
                or runner is None or runner.poll() is not None):
            return None
        try:
            if spec['mode'] == 'candidate-play':
                candidate = self.active_candidate
                if candidate is None:
                    return None
                expected_candidate = _studio_path(self.jobs, f'{jid}/candidate', kind='dir')
                if candidate.resolve(strict=True) != expected_candidate.resolve(strict=True):
                    return None
                executable = _studio_path(candidate, 'windows/URMAN.exe')
                record = self.candidate_for(spec['candidate_id'])
                owner = self.job_owners.get(jid)
                if (record is None or not owner or record['owner'] != owner
                        or self.active_candidate_pin is None
                        or not self.active_candidate_helper_sha256
                        or digest(_studio_path(self.repo, 'eng/windows-game-ui.ps1'))
                        != self.active_candidate_helper_sha256):
                    return None
                self.active_candidate_pin.verify(executable, record['manifest'])
                expected_image = os.path.normcase(os.path.normpath(str(executable)))
                matches = []
                for pid, name in _windows_descendants(runner.pid).items():
                    if name.casefold() != 'urman.exe':
                        continue
                    image = _windows_process_image(pid)
                    if image and os.path.normcase(os.path.normpath(image)) == expected_image:
                        matches.append(pid)
                return matches[0] if len(matches) == 1 else None

            if source is None:
                return None
            expected_source = (self.jobs / jid / 'source').resolve(strict=True)
            source_info = source.lstat()
            if (_studio_reparse(source_info) or not stat.S_ISDIR(source_info.st_mode)
                    or source.resolve(strict=True) != expected_source):
                return None
            _studio_path(source, 'game/project.godot')

            helper_relative = 'eng/windows-game-ui.ps1'
            manifest = self.verified_snapshot_manifest(spec['snapshot_id'])
            helper_entry = (manifest or {}).get('files', {}).get(helper_relative)
            if not isinstance(helper_entry, dict) or not re.fullmatch('[a-f0-9]{64}', helper_entry.get('sha256', '')):
                return None
            source_helper = _studio_path(source, helper_relative)
            station_helper = _studio_path(self.repo, helper_relative)
            expected_helper_hash = helper_entry['sha256']
            if (digest(source_helper) != expected_helper_hash
                    or source_helper.read_text(encoding='utf-8-sig')
                    != station_helper.read_text(encoding='utf-8-sig')):
                return None

            pins = json.loads((self.repo / 'eng/toolchain.json').read_text(encoding='utf-8-sig'))
            version = pins['godot']['version'].split('.stable')[0]
            executable_directory = (self.repo / '.tools/godot/windows'
                                    / f'Godot_v{version}-stable_mono_win64')
            expected_console = (executable_directory
                                / f'Godot_v{version}-stable_mono_win64_console.exe').resolve(strict=True)
            gui_candidate = executable_directory / f'Godot_v{version}-stable_mono_win64.exe'
            expected_gui = gui_candidate.resolve(strict=True) if gui_candidate.is_file() else None
            expected_images = {
                os.path.normcase(os.path.normpath(str(path)))
                for path in (expected_console, expected_gui) if path is not None
            }
            descendants = _windows_descendants(runner.pid)
            gui_candidates = []
            console_candidates = []
            for pid, name in descendants.items():
                if name.casefold() not in {
                        expected_console.name.casefold(),
                        expected_gui.name.casefold() if expected_gui is not None else '',
                }:
                    continue
                image = _windows_process_image(pid)
                normalized_image = os.path.normcase(os.path.normpath(image)) if image else None
                if normalized_image not in expected_images:
                    continue
                if expected_gui is not None and normalized_image == os.path.normcase(os.path.normpath(str(expected_gui))):
                    gui_candidates.append(pid)
                elif normalized_image == os.path.normcase(os.path.normpath(str(expected_console))):
                    console_candidates.append(pid)
            if len(gui_candidates) == 1:
                return gui_candidates[0]
            if not gui_candidates and len(console_candidates) == 1:
                return console_candidates[0]
            return None
        except (OSError, ValueError, KeyError, TypeError):
            return None

    @staticmethod
    def _validate_ui_action(action):
        if not isinstance(action, dict) or not isinstance(action.get('action'), str):
            raise ValueError('UI action must be one bounded keys, move, or look request')
        kind = action['action']
        if kind == 'keys':
            keys = action.get('keys')
            if (set(action) != {'action', 'keys'} or not isinstance(keys, str)
                    or keys not in UI_KEY_CHORDS):
                raise ValueError('UI keys action is outside the fixed key allowlist')
            return kind
        if kind == 'move':
            key = action.get('key')
            if (set(action) != {'action', 'key', 'durationMs'}
                    or not isinstance(key, str) or key not in UI_MOVE_KEYS
                    or type(action.get('durationMs')) is not int
                    or not 1 <= action['durationMs'] <= 2000):
                raise ValueError('UI move action is outside the bounded movement allowlist')
            return kind
        if kind == 'look':
            dx = action.get('dx')
            dy = action.get('dy')
            if (set(action) != {'action', 'dx', 'dy'}
                    or type(dx) is not int or type(dy) is not int
                    or not -UI_LOOK_DELTA_LIMIT <= dx <= UI_LOOK_DELTA_LIMIT
                    or not -UI_LOOK_DELTA_LIMIT <= dy <= UI_LOOK_DELTA_LIMIT
                    or (dx == 0 and dy == 0)):
                raise ValueError('UI look action is outside the bounded relative-delta allowlist')
            return kind
        raise ValueError('UI action must be one bounded keys, move, or look request')

    @staticmethod
    def _validate_interactive_frame_result(report, jid, request_id, pid, expected_scene):
        if not isinstance(report, dict):
            raise ValueError('interactive frame result must be an object')
        if (report.get('kind') != 'interactive-game-viewport'
                or report.get('jobId') != jid or report.get('requestId') != request_id
                or type(report.get('pid')) is not int or report['pid'] != pid):
            raise ValueError('interactive frame identity does not match the active job')
        if report.get('status') != 'ok':
            raise ValueError('interactive frame was refused by the game')
        required = {'kind', 'jobId', 'requestId', 'pid', 'status', 'width', 'height',
                    'pngSha256', 'scenePath', 'cameraPath'}
        optional = {'cameraPosition', 'cameraRotationDegrees', 'interactionRayCollider',
                    'interactionId', 'interactionAvailable'}
        fields = set(report)
        if not required.issubset(fields) or fields - required not in (set(), optional):
            raise ValueError('interactive frame result fields are invalid')
        width, height = report.get('width'), report.get('height')
        if (type(width) is not int or type(height) is not int
                or not 1 <= width <= INTERACTIVE_FRAME_DIMENSION_LIMIT
                or not 1 <= height <= INTERACTIVE_FRAME_DIMENSION_LIMIT):
            raise ValueError('interactive frame dimensions are outside the bound')
        if (expected_scene not in APPROVED_PLAY_SCENES
                or report.get('scenePath') != expected_scene):
            raise ValueError('interactive frame scene does not match the active approved play scene')
        def valid_node_path(value):
            return (isinstance(value, str) and len(value) <= 512
                    and value.startswith('/root/') and '\\' not in value and ':' not in value
                    and not any(ord(character) < 32 for character in value)
                    and not any(part in ('', '.', '..') for part in value.split('/')[1:]))

        camera_path = report.get('cameraPath')
        if not valid_node_path(camera_path):
            raise ValueError('interactive frame camera path is invalid')
        if not isinstance(report.get('pngSha256'), str) or not re.fullmatch('[a-f0-9]{64}', report['pngSha256']):
            raise ValueError('interactive frame checksum is invalid')
        if fields - required:
            def valid_vec3(value):
                return (isinstance(value, list) and len(value) == 3
                        and all(type(component) in (int, float)
                                and abs(component) <= 1_000_000 and math.isfinite(component)
                                for component in value))

            camera_position = report['cameraPosition']
            camera_rotation = report['cameraRotationDegrees']
            if ((camera_position is None) != (camera_rotation is None)
                    or (camera_position is not None and not valid_vec3(camera_position))
                    or (camera_rotation is not None and not valid_vec3(camera_rotation))):
                raise ValueError('interactive frame camera metadata is invalid')
            ray_collider = report['interactionRayCollider']
            interaction_id = report['interactionId']
            interaction_available = report['interactionAvailable']
            if ray_collider is not None and not valid_node_path(ray_collider):
                raise ValueError('interactive frame interaction collider path is invalid')
            if (interaction_id is not None
                    and (not isinstance(interaction_id, str)
                         or not re.fullmatch(r'[A-Za-z0-9_.:/-]{1,256}', interaction_id))):
                raise ValueError('interactive frame interaction ID is invalid')
            if interaction_available is not None and type(interaction_available) is not bool:
                raise ValueError('interactive frame interaction availability is invalid')
            if ((interaction_id is None) != (interaction_available is None)
                    or (interaction_id is not None and ray_collider is None)):
                raise ValueError('interactive frame interaction metadata is inconsistent')
        return report

    def capture_current(self, jid, principal, action):
        if not re.fullmatch('[a-f0-9]{32}', jid):
            return 404, {'error': 'unknown or unavailable running play job'}
        if (not isinstance(action, dict) or action.get('action') != 'capture-current'
                or set(action) - {'action', 'requestId'}
                or ('requestId' in action
                    and (not isinstance(action['requestId'], str)
                         or not re.fullmatch('[a-f0-9]{32}', action['requestId'])))):
            return 400, {'error': 'capture-current accepts no additional fields'}
        requested_id = action.get('requestId')
        if sys.platform != 'win32':
            return 503, {'error': 'native game viewport capture is available only on the Windows station'}

        with self.lock:
            if self.busy != jid or jid not in self.job_owners:
                return 404, {'error': 'unknown or unavailable running play job'}
            if self.job_owners.get(jid) != principal:
                return 403, {'error': 'job owner required'}
            if (self.active_spec is None or self.active_spec.get('job_id') != jid
                    or not (self.active_spec.get('mode') == 'candidate-play'
                            or (self.active_spec.get('mode') == 'play'
                                and self.active_spec.get('scene') in APPROVED_PLAY_SCENES))):
                return 409, {'error': 'owned native play job is unavailable'}
            expected_scene = (PHOTO_WORLDS_GAME_SCENE if self.active_spec['mode'] == 'candidate-play'
                              else self.active_spec['scene'])
            pid = self._active_game_pid(jid)
            runner = self.active_runner
            if pid is None or runner is None:
                return 409, {'error': 'owned native game process is not ready for capture'}

            temporary = None
            temporary_created = False
            request_published = False
            try:
                frames = _studio_path(self.jobs, f'{jid}/artifacts/frames', kind='dir')
                expected_frames = (self.jobs / jid / 'artifacts' / 'frames').resolve(strict=True)
                if frames.resolve(strict=True) != expected_frames:
                    return 409, {'error': 'owned native frame directory is unavailable'}

                request_path = frames / 'interactive.request.json'
                processing_path = frames / 'interactive.request.processing.json'
                request_id = None
                result_path = None
                image_path = None
                pending_file = None
                publish_request = False
                for _ in range(3):
                    pending_paths = []
                    for name in (request_path.name, processing_path.name):
                        candidate_path = _studio_path(frames, name, missing=True)
                        if candidate_path is None:
                            continue
                        try:
                            candidate_info = candidate_path.lstat()
                        except FileNotFoundError:
                            continue
                        if (_studio_reparse(candidate_info) or not stat.S_ISREG(candidate_info.st_mode)
                                or candidate_info.st_size > INTERACTIVE_FRAME_REQUEST_LIMIT):
                            return 409, {'error': 'pending interactive viewport request is invalid'}
                        pending_paths.append(candidate_path)
                    if len(pending_paths) > 1:
                        return 409, {'error': 'interactive viewport request state is ambiguous'}
                    if not pending_paths:
                        break
                    pending_file = pending_paths[0]
                    try:
                        pending_path_info = pending_file.lstat()
                        with pending_file.open('rb') as stream:
                            pending_info = os.fstat(stream.fileno())
                            if (_studio_reparse(pending_info) or not stat.S_ISREG(pending_info.st_mode)
                                    or pending_info.st_size > INTERACTIVE_FRAME_REQUEST_LIMIT
                                    or not os.path.samestat(pending_path_info, pending_info)):
                                return 409, {'error': 'pending interactive viewport request is invalid'}
                            pending = _studio_json(stream.read(INTERACTIVE_FRAME_REQUEST_LIMIT + 1),
                                                   INTERACTIVE_FRAME_REQUEST_LIMIT,
                                                   'pending interactive viewport request')
                    except FileNotFoundError:
                        pending_file = None
                        continue
                    if (not isinstance(pending, dict) or set(pending) != {'version', 'jobId', 'requestId'}
                            or type(pending.get('version')) is not int or pending['version'] != 1
                            or pending.get('jobId') != jid
                            or not isinstance(pending.get('requestId'), str)
                            or not re.fullmatch('[a-f0-9]{32}', pending['requestId'])):
                        return 409, {'error': 'pending interactive viewport request does not match this job'}
                    request_id = pending['requestId']
                    if requested_id is not None and request_id != requested_id:
                        return 409, {'error': 'a different interactive viewport request is already pending',
                                     'jobId': jid, 'requestId': request_id}
                    request_published = True
                    break

                if request_id is None:
                    if requested_id is not None:
                        request_id = requested_id
                        result_path = frames / f'interactive-{request_id}.json'
                        image_path = frames / f'interactive-{request_id}.png'
                        temporary = frames / f'interactive.request-{request_id}.tmp'
                        result_exists = _studio_path(frames, result_path.name, missing=True) is not None
                        image_exists = _studio_path(frames, image_path.name, missing=True) is not None
                        temporary_exists = _studio_path(frames, temporary.name, missing=True) is not None
                        if result_exists != image_exists or (temporary_exists and not (result_exists and image_exists)):
                            return 409, {'error': 'interactive viewport request artifacts are incomplete',
                                         'jobId': jid, 'requestId': request_id}
                        # A completed request is replayed through the same validation below.
                        publish_request = not result_exists
                        if result_exists:
                            temporary = None
                            request_published = True
                    else:
                        for _ in range(4):
                            candidate = uuid.uuid4().hex
                            result_candidate = f'interactive-{candidate}.json'
                            image_candidate = f'interactive-{candidate}.png'
                            temporary_candidate = f'interactive.request-{candidate}.tmp'
                            if any(_studio_path(frames, name, missing=True) is not None
                                   for name in (result_candidate, image_candidate, temporary_candidate)):
                                continue
                            request_id = candidate
                            result_path = frames / result_candidate
                            image_path = frames / image_candidate
                            temporary = frames / temporary_candidate
                            break
                        if request_id is None or result_path is None or image_path is None or temporary is None:
                            return 409, {'error': 'cannot allocate a unique interactive viewport request'}
                        publish_request = True

                if publish_request:
                    payload = canonical({'version': 1, 'jobId': jid, 'requestId': request_id})
                    if len(payload) > INTERACTIVE_FRAME_REQUEST_LIMIT:
                        return 409, {'error': 'interactive viewport request exceeds its size bound'}
                    with temporary.open('xb') as stream:
                        temporary_created = True
                        stream.write(payload)
                        stream.flush()
                        os.fsync(stream.fileno())
                    try:
                        os.rename(temporary, request_path)
                    except OSError:
                        if temporary_created:
                            temporary.unlink(missing_ok=True)
                        return 409, {'error': 'an interactive viewport request is already pending'}
                    request_published = True

                if request_id is None:
                    return 409, {'error': 'interactive viewport request identity is unavailable'}
                if result_path is None:
                    result_path = frames / f'interactive-{request_id}.json'
                if image_path is None:
                    image_path = frames / f'interactive-{request_id}.png'

                deadline = time.monotonic() + INTERACTIVE_FRAME_TIMEOUT_SECONDS
                while True:
                    _studio_path(frames, request_path.name, missing=True)
                    _studio_path(frames, processing_path.name, missing=True)
                    result_file = _studio_path(frames, result_path.name, missing=True)
                    if result_file is not None:
                        break
                    if runner.poll() is not None:
                        return 409, {'error': 'owned native game exited before the viewport was captured'}
                    remaining = deadline - time.monotonic()
                    if remaining <= 0:
                        return 504, {'error': 'interactive viewport capture timed out; resume with the same requestId',
                                     'jobId': jid, 'requestId': request_id,
                                     'snapshotId': self.active_spec.get('snapshot_id'), 'nativePid': pid}
                    time.sleep(min(0.1, remaining))

                result_info = result_file.lstat()
                if (_studio_reparse(result_info) or not stat.S_ISREG(result_info.st_mode)
                        or result_info.st_size > INTERACTIVE_FRAME_RESULT_LIMIT):
                    return 409, {'error': 'interactive viewport result is invalid'}
                with result_file.open('rb') as stream:
                    opened_info = os.fstat(stream.fileno())
                    if (_studio_reparse(opened_info) or not stat.S_ISREG(opened_info.st_mode)
                            or opened_info.st_size != result_info.st_size
                            or not os.path.samestat(result_info, opened_info)):
                        return 409, {'error': 'interactive viewport result changed while reading'}
                    report = _studio_json(stream.read(INTERACTIVE_FRAME_RESULT_LIMIT + 1),
                                         INTERACTIVE_FRAME_RESULT_LIMIT, 'interactive viewport result')
                self._validate_interactive_frame_result(report, jid, request_id, pid, expected_scene)

                image_file = _studio_path(frames, image_path.name)
                image_info = image_file.lstat()
                if (_studio_reparse(image_info) or not stat.S_ISREG(image_info.st_mode)
                        or image_info.st_size <= 0 or image_info.st_size > INTERACTIVE_FRAME_PNG_LIMIT):
                    return 409, {'error': 'interactive viewport PNG is outside the size bound'}
                with image_file.open('rb') as stream:
                    opened_info = os.fstat(stream.fileno())
                    if (_studio_reparse(opened_info) or not stat.S_ISREG(opened_info.st_mode)
                            or opened_info.st_size != image_info.st_size
                            or not os.path.samestat(image_info, opened_info)):
                        return 409, {'error': 'interactive viewport PNG changed while reading'}
                    png = stream.read(INTERACTIVE_FRAME_PNG_LIMIT + 1)
                after_info = image_file.lstat()
                if (len(png) != image_info.st_size or len(png) > INTERACTIVE_FRAME_PNG_LIMIT
                        or not os.path.samestat(image_info, after_info)):
                    return 409, {'error': 'interactive viewport PNG changed while reading'}
                if (len(png) < 33 or png[:8] != b'\x89PNG\r\n\x1a\n'
                        or int.from_bytes(png[8:12], 'big') != 13 or png[12:16] != b'IHDR'
                        or not png.endswith(b'\x00\x00\x00\x00IEND\xaeB`\x82')):
                    return 409, {'error': 'interactive viewport PNG encoding is invalid'}
                png_width = int.from_bytes(png[16:20], 'big')
                png_height = int.from_bytes(png[20:24], 'big')
                png_hash = hashlib.sha256(png).hexdigest()
                if (png_width != report['width'] or png_height != report['height']
                        or png_hash != report['pngSha256']):
                    return 409, {'error': 'interactive viewport PNG does not match its receipt'}
                return 200, (png, {'pngSha256': png_hash, 'width': png_width, 'height': png_height,
                                   'requestId': request_id})
            except (OSError, ValueError, KeyError, TypeError):
                if temporary_created and temporary is not None and not request_published:
                    try:
                        temporary.unlink(missing_ok=True)
                    except OSError:
                        pass
                return 409, {'error': 'interactive viewport capture failed validation'}

    def _run_ui_helper(self, jid, pid, source, action):
        system_root = os.environ.get('SystemRoot')
        if not system_root:
            return 'ui_helper_launch_failed', None, None
        powershell = Path(system_root) / 'System32/WindowsPowerShell/v1.0/powershell.exe'
        helper = self.repo / 'eng/windows-game-ui.ps1'
        if not powershell.is_file() or not helper.is_file():
            return 'ui_helper_launch_failed', None, None

        duplicate_descriptor = None
        try:
            with tempfile.TemporaryDirectory(prefix='urman-game-ui-') as private_temp:
                private_root = Path(private_temp).resolve(strict=True)
                helper_temp = private_root / 'urman-codex-studio-ui'
                helper_temp.mkdir()
                root_info = private_root.lstat()
                helper_info = helper_temp.lstat()
                if (_studio_reparse(root_info) or not stat.S_ISDIR(root_info.st_mode)
                        or _studio_reparse(helper_info) or not stat.S_ISDIR(helper_info.st_mode)):
                    return 'ui_helper_launch_failed', None, None

                request_id = uuid.uuid4().hex
                request_path = helper_temp / f'request-{request_id}.json'
                result_path = helper_temp / f'result-{request_id}.json'
                image_path = helper_temp / f'image-{request_id}.png'
                request = {
                    'jobId': jid,
                    'pid': pid,
                    'result': str(result_path),
                    'image': str(image_path),
                    **action,
                }
                if self.active_spec is not None and self.active_spec.get('mode') == 'candidate-play':
                    record = self.candidate_for(self.active_spec['candidate_id'])
                    if (record is None or self.active_candidate is None or self.active_runner is None
                            or self.active_runner.poll() is not None
                            or self.active_candidate_pin is None
                            or record['owner'] != self.job_owners.get(jid)):
                        return 'ui_helper_launch_failed', None, None
                    pin = self.active_candidate_pin
                    pin.verify(_studio_path(self.active_candidate, 'windows/URMAN.exe'), record['manifest'])
                    duplicate_descriptor = os.dup(pin.stream.fileno())
                    duplicate_handle = pin.os_handle(duplicate_descriptor)
                    request.update(candidateRoot=str(self.active_candidate),
                                   runnerPid=self.active_runner.pid,
                                   executableSha256=record['manifest']['windows_exe_sha256'],
                                   executablePinHandle=str(duplicate_handle), executablePinIdentity=pin.identity)
                else:
                    request['sourceRoot'] = str(source)
                with request_path.open('xb') as stream:
                    stream.write(canonical(request))

                system_path = os.pathsep.join((str(Path(system_root) / 'System32'), system_root))
                module_path = str(Path(system_root) / 'System32/WindowsPowerShell/v1.0/Modules')
                helper_env = {
                    'SystemRoot': system_root,
                    'WINDIR': system_root,
                    'OS': 'Windows_NT',
                    'TEMP': str(private_root),
                    'TMP': str(private_root),
                    'PATH': system_path,
                    'PSModulePath': module_path,
                }
                command = [str(powershell), '-NoLogo', '-NoProfile', '-NonInteractive',
                           '-ExecutionPolicy', 'Bypass', '-File', str(helper),
                           '-RequestPath', str(request_path)]
                startup = None
                try:
                    if duplicate_descriptor is not None:
                        startup = subprocess.STARTUPINFO()
                        startup.lpAttributeList = {'handle_list': [duplicate_handle]}
                        os.set_handle_inheritable(duplicate_handle, True)
                    process = subprocess.Popen(
                        command, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                        stderr=subprocess.DEVNULL, cwd=str(private_root), env=helper_env,
                        startupinfo=startup, close_fds=True, creationflags=subprocess.CREATE_NO_WINDOW)
                finally:
                    if duplicate_descriptor is not None:
                        os.close(duplicate_descriptor)
                        duplicate_descriptor = None
                try:
                    exit_code = process.wait(timeout=UI_HELPER_TIMEOUT_SECONDS)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=2)
                    return 'ui_helper_timeout', None, process.returncode

                try:
                    result_info = result_path.lstat()
                    if (_studio_reparse(result_info) or not stat.S_ISREG(result_info.st_mode)
                            or result_info.st_size > 8 * 1024):
                        return 'ui_helper_invalid_result', None, exit_code
                    with result_path.open('rb') as stream:
                        report = _studio_json(stream.read(8 * 1024 + 1), 8 * 1024, 'UI helper result')
                except (OSError, ValueError):
                    return 'ui_helper_invalid_result', None, exit_code
                if exit_code == 0 and isinstance(report, dict) and report.get('ok') is True:
                    return None, None, exit_code
                if isinstance(report, dict) and report.get('ok') is False:
                    error = report.get('error')
                    if isinstance(error, str):
                        return UI_HELPER_FAILURE_CODES.get(error, 'ui_helper_refused'), report, exit_code
                    return 'ui_helper_refused', report, exit_code
                return 'ui_helper_invalid_result', report if isinstance(report, dict) else None, exit_code
        except (OSError, ValueError, subprocess.SubprocessError):
            return 'ui_helper_launch_failed', None, None
        finally:
            if duplicate_descriptor is not None:
                os.close(duplicate_descriptor)

    def _record_first_ui_refusal(self, jid, expected_pid, reason_code, report, exit_code):
        if jid in self.ui_refusal_attempted:
            return
        if (not re.fullmatch('[a-f0-9]{32}', jid)
                or reason_code not in UI_HELPER_REASON_CODES):
            return
        # The caller holds self.lock. Mark before disk I/O so a failed write is
        # never replaced by a later refusal from the same running job.
        self.ui_refusal_attempted.add(jid)

        record = {'schema_version': 1, 'job_id': jid, 'reason_code': reason_code}
        if type(exit_code) is int and 0 <= exit_code <= 0xFFFFFFFF:
            record['helper_exit_code'] = exit_code
        if isinstance(report, dict):
            helper_pid_matches_job = type(report.get('pid')) is int and report['pid'] == expected_pid
            if type(report.get('ok')) is bool:
                record['helper_ok'] = report['ok']
            relation = report.get('foregroundRelation')
            if (isinstance(relation, str)
                    and relation in {'owned', 'same_pid_other_window', 'other_or_none', 'unknown'}):
                record['foreground_relation'] = relation
            phase = report.get('focusPhase')
            if (isinstance(phase, str)
                    and phase in {'pre_activation', 'activation', 'post_activation', 'action', 'unknown'}):
                record['focus_phase'] = phase
            # Require the helper PID to match the already-verified job PID, but
            # never persist that PID, title, rectangle, image path, raw error,
            # or action fields. The narrow post-activation allowlist below may
            # retain only the numeric foreground PID and HWND.
            target_handle = report.get('targetHwnd')
            if (helper_pid_matches_job
                    and type(target_handle) is int and 0 < target_handle <= 0x7FFFFFFFFFFFFFFF):
                record['target_hwnd'] = target_handle

            if (helper_pid_matches_job
                    and reason_code == 'ui_focus_post_activation_lost'
                    and report.get('focusPhase') == 'post_activation'
                    and report.get('foregroundRelation') == 'other_or_none'):
                diagnostics = report.get('postActivationDiagnostics')
                if isinstance(diagnostics, dict):
                    target_is_window = diagnostics.get('target_is_window')
                    target_pid_query_succeeded = diagnostics.get('target_pid_query_succeeded')
                    target_pid_matches_job = diagnostics.get('target_pid_matches_job')
                    foreground_is_null = diagnostics.get('foreground_hwnd_is_null')
                    foreground_handle = diagnostics.get('foreground_hwnd')
                    foreground_pid = diagnostics.get('foreground_pid')
                    branches = diagnostics.get('activation_branches')
                    branch_names = (
                        'first_set_foreground_attempted', 'first_set_foreground_succeeded',
                        'first_foreground_check_owned', 'attach_input_attempted',
                        'attach_input_succeeded', 'detach_input_attempted',
                        'detach_input_succeeded', 'retry_set_foreground_attempted',
                        'retry_set_foreground_succeeded', 'retry_foreground_check_attempted',
                        'retry_foreground_check_owned', 'caption_activation_attempted',
                        'caption_activation_returned', 'caption_initial_foreground_check_attempted',
                        'caption_initial_foreground_check_owned',
                        'caption_post_position_foreground_check_attempted',
                        'caption_post_position_foreground_check_owned', 'caption_raise_attempted',
                        'caption_raise_succeeded', 'caption_click_attempted', 'caption_click_succeeded',
                    )
                    branches_valid = (isinstance(branches, dict)
                                      and all(type(branches.get(name)) is bool for name in branch_names))
                    target_valid = (type(target_is_window) is bool
                                    and type(target_pid_query_succeeded) is bool
                                    and type(target_pid_matches_job) is bool
                                    and (not target_pid_query_succeeded or target_is_window)
                                    and (not target_pid_matches_job or target_pid_query_succeeded))
                    if foreground_is_null is True:
                        foreground_valid = foreground_handle is None and foreground_pid is None
                    elif foreground_is_null is False:
                        foreground_valid = (
                            type(foreground_handle) is int
                            and 0 < foreground_handle <= 0x7FFFFFFFFFFFFFFF
                            and type(foreground_pid) is int
                            and 0 < foreground_pid <= 0xFFFFFFFF
                            and foreground_pid != expected_pid)
                    else:
                        foreground_valid = False
                    if target_valid and foreground_valid and branches_valid:
                        record['post_activation_diagnostics'] = {
                            'target_is_window': target_is_window,
                            'target_pid_query_succeeded': target_pid_query_succeeded,
                            'target_pid_matches_job': target_pid_matches_job,
                            'foreground_hwnd_is_null': foreground_is_null,
                            'foreground_hwnd': foreground_handle,
                            'foreground_pid': foreground_pid,
                            'activation_branches': {name: branches[name] for name in branch_names},
                        }

        payload = canonical(record)
        if len(payload) > 2048:
            raise ValueError('native UI refusal evidence exceeded its fixed bound')
        artifacts = _studio_path(self.jobs, f'{jid}/artifacts', kind='dir')
        expected_artifacts = (self.jobs / jid / 'artifacts').resolve(strict=True)
        if artifacts.resolve(strict=True) != expected_artifacts:
            raise ValueError('native UI refusal artifact root changed')
        _studio_path(artifacts, 'ui-first-refusal.json', missing=True)
        # A pre-existing regular file at this reserved name is untrusted; the
        # durable writer atomically replaces it with this allowlisted record.
        temporary = _studio_path(artifacts, 'ui-first-refusal.tmp', missing=True)
        if temporary is not None:
            raise ValueError('native UI refusal temporary path already exists')
        _studio_write_durable_json(artifacts / 'ui-first-refusal.json', record)
        saved = _studio_path(artifacts, 'ui-first-refusal.json')
        saved_info = saved.lstat()
        if _studio_reparse(saved_info) or not stat.S_ISREG(saved_info.st_mode) or saved_info.st_size > 2048:
            raise ValueError('native UI refusal evidence failed its bounded file check')

    def ui_action(self, jid, principal, action):
        if not re.fullmatch('[a-f0-9]{32}', jid):
            return 404, {'error': 'unknown or unavailable running play job'}
        try:
            self._validate_ui_action(action)
        except ValueError as error:
            return 400, {'error': str(error)}

        with self.lock:
            if self.busy != jid or jid not in self.job_owners:
                return 404, {'error': 'unknown or unavailable running play job'}
            if self.job_owners.get(jid) != principal:
                return 403, {'error': 'job owner required'}
            if (self.active_spec is None or self.active_spec.get('mode') not in {'play', 'candidate-play'}
                    or self.active_spec.get('job_id') != jid):
                return 409, {'error': 'owned native play job is unavailable'}
            pid = self._active_game_pid(jid)
            source = self.active_source
            if pid is None or (source is None and self.active_spec['mode'] != 'candidate-play'):
                return 409, {'error': 'owned native game window is not ready for input'}
            kind = action['action']
            reason_code, report, exit_code = self._run_ui_helper(jid, pid, source, action)
            if reason_code is not None:
                try:
                    self._record_first_ui_refusal(jid, pid, reason_code, report, exit_code)
                except Exception:
                    # Preserve the original refused-action response if evidence
                    # storage fails; do not expose paths or report contents.
                    print('native game UI refusal evidence write failed', file=sys.stderr)
                return 409, {'job_id': jid, 'action': kind, 'ok': False,
                             'error': 'native game input was refused or did not complete',
                             'reason_code': reason_code}
            return 200, {'job_id': jid, 'action': kind, 'ok': True}

    def _clear_active_job(self, jid):
        if (self.busy == jid and self.active_candidate_pin is not None
                and (self.active_candidate_recovery
                     or (self.active_runner is not None and self.active_runner.poll() is None))):
            # Keep the pin and reservation until operator recovery confirms termination.
            self.active_candidate_recovery = True
            self.active_candidate_helper_sha256 = None
            return
        if self.busy == jid:
            self.busy = None
        self.job_owners.pop(jid, None)
        self.ui_refusal_attempted.discard(jid)
        if self.active_spec is not None and self.active_spec.get('job_id') == jid:
            self.active_spec = None
            self.active_source = None
            self.active_runner = None
            self.active_candidate = None
            self.active_candidate_helper_sha256 = None
            if self.active_candidate_pin is not None:
                self.active_candidate_pin.close()
                self.active_candidate_pin = None

    def execute_candidate(self, path, spec, status):
        output = path / 'artifacts'
        candidate_root = path / 'candidate'
        native_play = spec['mode'] == 'candidate-play'
        receipt_schema = ('urman.windows_candidate_play_receipt.v1' if native_play
                          else 'urman.windows_candidate_boot_receipt.v1')
        started = time.monotonic()
        output.mkdir()
        receipt = {}
        guard_project_binding = None
        preserve_candidate = False
        try:
            record = self.candidate_for(spec['candidate_id'])
            owner = json.loads((path / 'owner.json').read_text(encoding='utf-8-sig')).get('principal')
            if record is None or record['owner'] != owner:
                raise ValueError('candidate ownership changed before execution')
            manifest = record['manifest']
            receipt_path = blob_path(self.blobs, manifest['receipt_sha256'])
            archive_path = blob_path(self.blobs, manifest['windows_zip_sha256'])
            for blob, key in ((receipt_path, 'receipt'), (archive_path, 'windows_zip')):
                if not blob.is_file() or blob.stat().st_size != manifest[key + '_bytes'] or digest(blob) != manifest[key + '_sha256']:
                    raise ValueError('candidate upload blob failed its saved hash/size check')
            if manifest['receipt_bytes'] > MAX_CANDIDATE_RECEIPT:
                raise ValueError('candidate receipt exceeds its size limit')
            candidate_receipt = json.loads(receipt_path.read_text(encoding='utf-8-sig'))
            validate_candidate_receipt(candidate_receipt, manifest)
            if native_play:
                project_path = self.repo / 'game/project.godot'
                project_sha = candidate_receipt['buildProvenance']['sourceAfter']['files'].get('game/project.godot')
                if isinstance(project_sha, str) and re.fullmatch('[a-f0-9]{64}', project_sha):
                    cached_project = blob_path(self.blobs, project_sha)
                    try:
                        cached_project.lstat()
                    except FileNotFoundError:
                        pass
                    else:
                        project_path = cached_project
                guard_project_binding = validate_candidate_guard_project(
                    candidate_receipt, project_path)
            unpack_candidate(archive_path, candidate_root, manifest)
            if native_play and _studio_path(candidate_root, 'windows/override.cfg', missing=True) is not None:
                raise ValueError('native candidate must not contain an external project override')
            if native_play:
                pin = _CandidateExecutablePin(_studio_path(candidate_root, 'windows/URMAN.exe'), manifest)
                with self.lock:
                    if self.busy != spec['job_id'] or self.active_spec is None or self.active_spec.get('job_id') != spec['job_id']:
                        pin.close()
                        raise ValueError('candidate ownership changed before pin publication')
                    self.active_candidate_pin = pin

            execution_names = ('eng/run-windows-check.ps1', 'eng/protected_run.py', 'eng/remote_common.py')
            if native_play:
                execution_names += ('eng/windows-game-ui.ps1',)
            execution_files = {name: {'station_sha256': digest(self.repo / name)} for name in execution_names}
            runner_env = None
            if native_play:
                frames = output / 'frames'
                frames.mkdir()
                runner_env = os.environ.copy()
                runner_env['URMAN_INTERACTIVE_FRAME_DIR'] = str(frames.resolve())
                runner_env['URMAN_INTERACTIVE_FRAME_JOB'] = spec['job_id']
            system_root = os.environ.get('SystemRoot', '')
            powershell = Path(system_root) / 'System32/WindowsPowerShell/v1.0/powershell.exe'
            if not powershell.is_file():
                raise ValueError('Windows PowerShell runner is unavailable')
            command = [str(powershell), '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
                       str(self.repo / 'eng/run-windows-check.ps1'), '-Root', str(self.repo),
                       '-ToolsRoot', str(self.repo), '-CandidateRoot', str(candidate_root),
                       '-Output', str(output), '-Mode', spec['mode'],
                       '-TimeoutSeconds', str(spec['timeout']), '-Python', self.config['python']]
            with (output / 'stdout.log').open('wb') as out, (output / 'stderr.log').open('wb') as err:
                process = subprocess.Popen(command, stdout=out, stderr=err,
                                           creationflags=subprocess.CREATE_NO_WINDOW, env=runner_env)
                if native_play:
                    with self.lock:
                        if self.busy == spec['job_id']:
                            self.active_candidate = candidate_root
                            self.active_candidate_helper_sha256 = execution_files['eng/windows-game-ui.ps1']['station_sha256']
                            self.active_runner = process
                try:
                    code = process.wait(timeout=spec['timeout'] + 120)
                except subprocess.TimeoutExpired:
                    if native_play:
                        with self.lock:
                            self.active_candidate_recovery = True
                            self.active_candidate_helper_sha256 = None
                    subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True)
                    process.wait(timeout=10)
                    raise RuntimeError('candidate job deadline exceeded; inspect the userdata recovery marker')
            receipt = json.loads((output / 'receipt.json').read_text(encoding='utf-8-sig'))
            receipt.update(schema=receipt_schema,
                           evidence_scope=('native-windows-package-technical' if native_play
                                           else 'headless-windows-package-boot'),
                           native_ui=('available-not-acceptance' if native_play else 'not-run'),
                           station=os.environ['COMPUTERNAME'], job_id=spec['job_id'],
                           candidate_id=spec['candidate_id'], candidate_manifest=manifest,
                           execution_files=execution_files, worker_code_sha256=self.worker_code_sha256,
                           runner_exit_code=code, worker_elapsed_seconds=round(time.monotonic() - started, 3))
            if native_play:
                receipt['human_playtest'] = 'pending'
                receipt['guard_project_binding'] = guard_project_binding
            write_json(output / 'receipt.json', receipt)
            if code != 0 or receipt.get('status') != 'PASS' or self.recovery():
                raise RuntimeError(receipt.get('error', f'candidate runner failed with exit {code}'))
            status.update(status='PASS', exit_code=0)
        except Exception as error:
            status.update(status='FAIL', exit_code=1, error=str(error)[:2048])
            try:
                current = json.loads((output / 'receipt.json').read_text(encoding='utf-8-sig')) if (output / 'receipt.json').is_file() else {}
                current.update(schema=receipt_schema, status='FAIL',
                               job_id=spec['job_id'], candidate_id=spec['candidate_id'],
                               candidate_manifest=(record.get('manifest') if 'record' in locals() and record else None),
                               error=str(error)[:2048], userdata_recovery_pending=self.recovery())
                if native_play:
                    current.update(evidence_scope='native-windows-package-technical',
                                   native_ui='available-not-acceptance', human_playtest='pending',
                                   guard_project_binding=guard_project_binding)
                write_json(output / 'receipt.json', current)
            except Exception as receipt_error:
                print('candidate receipt write failed:', receipt_error, file=sys.stderr)
            preserve_candidate = self.recovery()

        write_json(output / 'request.json', spec)
        # Drain in-flight UI/capture before removing the package or archiving frames.
        # Busy and ownership remain reserved until result cleanup has finished.
        with self.lock:
            if native_play and (self.active_candidate_recovery
                                or ('process' in locals() and process.poll() is None)):
                self.active_candidate_recovery = True
                self.active_candidate_helper_sha256 = None
                _write_result_unavailable(path, status,
                                          'candidate process tree termination unconfirmed; station reserved for recovery')
                return
            if self.active_spec is not None and self.active_spec.get('job_id') == spec['job_id']:
                self.active_spec = None
                self.active_source = None
                self.active_runner = None
                self.active_candidate = None
                self.active_candidate_helper_sha256 = None
            if self.active_candidate_pin is not None:
                self.active_candidate_pin.close()
                self.active_candidate_pin = None
        status['finished'] = time.time()
        if candidate_root.exists() and not preserve_candidate:
            shutil.rmtree(candidate_root, ignore_errors=True)
        try:
            status['result_sha256'] = _archive_result(path, output)
            status['result_unavailable'] = False
        except Exception as error:
            _write_result_unavailable(path, status, 'result archive failed: ' + str(error)[:1800])
            with self.lock:
                self._clear_active_job(spec['job_id'])
            return
        with self.lock:
            try:
                status['station_freed_bytes'] = self.reclaim(None)
                _studio_write_durable_json(path / 'status.json', status)
            except OSError as error:
                print('candidate station reclaim/status write failed:', error, file=sys.stderr)
                _studio_write_durable_json(path / 'status.json', status)
            finally:
                self._clear_active_job(spec['job_id'])

    def execute(self, path, spec, status):
        if spec.get('mode') in {'candidate-boot', 'candidate-play'}:
            try:
                self.execute_candidate(path, spec, status)
            except Exception as error:
                _write_result_unavailable(path, status, error)
                with self.lock:
                    self._clear_active_job(spec['job_id'])
            return
        output = path/'artifacts'
        source = path/'source'
        is_studio = spec.get('scene') == STUDIO_SCENE
        preserve_source = False
        started = time.monotonic()
        try:
            if is_studio:
                _studio_write_marker(path)
                preserve_source = True
            output.mkdir()
            try:
                source.mkdir()
                archive = self.snapshots / (spec['snapshot_id'] + '.zip')
                if archive.is_file():
                    manifest = unpack_snapshot(archive, source)
                else:
                    manifest = materialize_from_blobs(source, self.manifest_for(spec['snapshot_id']), self.blobs)
                verify_source(source, manifest)
                if spec['mode'] != 'export-release' and not (source/'game'/spec['scene'][6:]).is_file():
                    raise ValueError('scene does not exist in snapshot')
                for pin in ('global.json','eng/toolchain.json'):
                    if json.loads((source/pin).read_text(encoding='utf-8-sig')) != json.loads((self.repo/pin).read_text(encoding='utf-8-sig')):
                        raise ValueError('snapshot toolchain differs from station: '+pin)
                execution_files = {}
                execution_names = ('eng/run-windows-check.ps1','eng/protected_run.py','eng/remote_common.py')
                if spec['mode'] == 'export-release':
                    execution_names += ('eng/desktop_release_checks.py', 'eng/desktop_build_provenance.py',
                                        'eng/act1_candidate_identity.py', 'eng/godot_pck.py')
                if spec['mode'] == 'play':
                    execution_names += ('eng/windows-game-ui.ps1',)
                for name in execution_names:
                    # Git normalizes Windows/Mac line endings differently. Require
                    # identical source text, and record both actual byte hashes.
                    if (source/name).read_text(encoding='utf-8-sig') != (self.repo/name).read_text(encoding='utf-8-sig'):
                        raise ValueError('station execution infrastructure differs from snapshot: '+name+'; update station before retrying')
                    execution_files[name] = {'station_sha256':digest(self.repo/name),'snapshot_sha256':manifest['files'][name]['sha256'],'source_text_equal':True}
                # Station-local override is recorded separately; never overwrite a developer file.
                if spec['mode'] != 'export-release':
                    (source/'game/override.cfg').write_text('[filesystem]\nimport/blender/enabled=false\n')
                runner_env = None
                if spec['mode'] == 'play':
                    frames = output/'frames'
                    frames.mkdir()
                    runner_env = os.environ.copy()
                    runner_env['URMAN_INTERACTIVE_FRAME_DIR'] = str(frames.resolve())
                    runner_env['URMAN_INTERACTIVE_FRAME_JOB'] = spec['job_id']
                command = ['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',
                           str(self.repo/'eng/run-windows-check.ps1'), '-Root',str(source),
                           '-ToolsRoot',str(self.repo), '-Provenance',str(source/'snapshot-manifest.json'),
                           '-Output',str(output), '-Mode',('play' if spec['mode'] == 'studio' else spec['mode']),
                           '-TimeoutSeconds',str(spec['timeout']), '-Python',self.config['python']]
                if spec['mode'] != 'export-release':
                    command += ['-Scene', spec['scene']]
                if spec.get('headless'):
                    command += ['-Headless']
                if spec.get('points'):
                    command += ['-ViewPoints', spec['points']]
                if spec.get('phase'):
                    command += ['-AtmospherePhase', spec['phase']]
                if spec.get('fov'):
                    command += ['-ViewFov', spec['fov']]
                if spec.get('acceptance_profile') is not None:
                    command += ['-AcceptanceProfile', spec['acceptance_profile']]
                if spec.get('studio_restarts'):
                    command += ['-StudioRestarts', str(spec['studio_restarts'])]
                with (output/'stdout.log').open('wb') as out, (output/'stderr.log').open('wb') as err:
                    p = subprocess.Popen(command,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW,
                                         env=runner_env)
                    if spec['mode'] == 'play':
                        with self.lock:
                            if self.busy == spec['job_id']:
                                self.active_runner = p
                                self.active_source = source
                    # Runner bounds build/content/import/game separately. This outer emergency
                    # deadline preserves the guard's recovery marker instead of claiming restore.
                    outer_deadline = spec['timeout'] * 2 + 1050
                    if spec['mode'] == 'export-release':
                        # Five content/build steps; import; provenance start;
                        # export; package prepare; guarded bootstrap; verify.
                        step_budgets = (5 * 180, spec['timeout'] + 30, 180,
                                        spec['timeout'] + 30, 180, 60, 180)
                        outer_deadline = sum(step_budgets) + 120  # runner/guard cleanup and receipt margin
                    try:
                        code = p.wait(timeout=outer_deadline)
                    except subprocess.TimeoutExpired:
                        subprocess.run(['taskkill','/PID',str(p.pid),'/T','/F'],capture_output=True)
                        p.wait(timeout=10); raise RuntimeError('job deadline exceeded; inspect userdata recovery before next run')
                receipt = json.loads((output/'receipt.json').read_text(encoding='utf-8-sig'))
                receipt.update(station=os.environ['COMPUTERNAME'], job_id=spec['job_id'],
                               snapshot=manifest, snapshot_hashes_verified=True,
                               execution_files=execution_files, worker_code_sha256=self.worker_code_sha256,
                               desktop=desktop(), runner_exit_code=code, worker_elapsed_seconds=round(time.monotonic()-started,3))
                write_json(output/'receipt.json', receipt)
                if code != 0 or receipt.get('status') != 'PASS' or self.recovery():
                    raise RuntimeError(receipt.get('error', f'runner failed with exit {code}'))
                write_json(output/'receipt.json', receipt)
                status.update(status='PASS', exit_code=code)
            except Exception as error:
                status.update(status='FAIL', error=str(error)[:2048], exit_code=1)
                receipt_path = output/'receipt.json'
                receipt = json.loads(receipt_path.read_text(encoding='utf-8-sig')) if receipt_path.exists() else {}
                receipt.update(status='FAIL',error=str(error)[:2048],job_id=spec['job_id'],
                               snapshot_id=spec['snapshot_id'],userdata_recovery_pending=self.recovery())
                write_json(receipt_path,receipt)

            if is_studio:
                primary_error = (status.get('error') if status.get('status') == 'FAIL'
                                 and isinstance(status.get('error'), str) else None)
                try:
                    export_studio_session(source, output)
                    preserve_source = False
                except Exception as error:
                    preserve_source = True
                    export_error = str(error)[:1800]
                    status.update(status='FAIL', exit_code=1)
                    if primary_error:
                        status['evidence_export_error'] = export_error
                    else:
                        status['error'] = 'Studio evidence export failed: ' + export_error
                    receipt_path = output/'receipt.json'
                    receipt = json.loads(receipt_path.read_text(encoding='utf-8-sig')) if receipt_path.exists() else {}
                    receipt.update(status='FAIL', job_id=spec['job_id'],
                                   snapshot_id=spec['snapshot_id'], studio_evidence_preserved_on_station=True)
                    if primary_error:
                        receipt.update(error=primary_error, evidence_export_error=export_error)
                    else:
                        receipt['error'] = status['error']
                    write_json(receipt_path, receipt)

            write_json(output/'request.json',spec)
            # Drain any in-flight UI/capture request before taking the archive
            # snapshot. Keep busy and ownership reserved until final cleanup.
            with self.lock:
                if (self.active_spec is not None
                        and self.active_spec.get('job_id') == spec['job_id']):
                    self.active_spec = None
                    self.active_source = None
                    self.active_runner = None
            status['finished'] = time.time()
            try:
                status['result_sha256'] = _archive_result(path, output)
                status['result_unavailable'] = False
            except Exception as error:
                if is_studio:
                    preserve_source = True
                _write_result_unavailable(path, status, 'result archive failed: ' + str(error)[:1800])
                return

            with self.lock:
                try:
                    try:
                        status['station_freed_bytes'] = self.reclaim(spec['snapshot_id'])
                    except OSError as error:
                        print('station reclaim failed:', error, file=sys.stderr)
                    _studio_write_durable_json(path/'status.json', status)
                    if is_studio and not preserve_source:
                        try:
                            (path / STUDIO_PRESERVE_MARKER).unlink()
                        except FileNotFoundError:
                            pass
                        except OSError as error:
                            print('Studio source marker retained:', error, file=sys.stderr)
                finally:
                    self._clear_active_job(spec['job_id'])
        except Exception as error:
            if is_studio:
                preserve_source = True
                try:
                    _studio_write_marker(path)
                except OSError:
                    pass
            _write_result_unavailable(path, status, error)
        finally:
            with self.lock:
                self._clear_active_job(spec['job_id'])

class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass  # No headers, tokens, source paths or credentials in access logs.

    def respond(self, code, value):
        data = canonical(value); self.send_response(code)
        self.send_header('Content-Type','application/json'); self.send_header('Content-Length',str(len(data)))
        self.end_headers(); self.wfile.write(data)

    def respond_interactive_frame(self, png, metadata):
        self.send_response(200)
        self.send_header('Content-Type', 'image/png')
        self.send_header('Content-Length', str(len(png)))
        self.send_header('X-URMAN-Frame-SHA256', metadata['pngSha256'])
        self.send_header('X-URMAN-Frame-Request-Id', metadata['requestId'])
        self.send_header('Cache-Control', 'no-store')
        self.send_header('X-Content-Type-Options', 'nosniff')
        self.end_headers()
        self.wfile.write(png)

    def authenticated_principal(self):
        value = self.headers.get('Authorization','')
        matches = []
        for principal, token in self.server.station.config['tokens'].items():
            if isinstance(token, str) and hmac.compare_digest(value, 'Bearer ' + token):
                matches.append(principal)
        return matches[0] if len(matches) == 1 else None

    def authorized(self):
        value = self.headers.get('Authorization','')
        return any(hmac.compare_digest(value, 'Bearer '+token)
                   for token in self.server.station.config['tokens'].values())

    def do_GET(self):
        if not self.authorized():
            return self.respond(401, {'error':'authorization required'})
        station = self.server.station
        if self.path == '/health':
            return self.respond(200,station.health())
        if self.path == '/health/candidate':
            return self.respond(200, station.health(candidate_boot=True))
        if self.path == '/health/studio':
            return self.respond(200,station.studio_readiness())
        m = re.fullmatch(r'/jobs/([a-zA-Z0-9_-]{8,64})(/result)?',self.path)
        if not m or not (station.jobs/m[1]/'status.json').is_file():
            return self.respond(404,{'error':'unknown job'})
        request_path = station.jobs / m[1] / 'request.json'
        request = (json.loads(request_path.read_text(encoding='utf-8-sig'))
                   if request_path.is_file() else {})
        owner_path = station.jobs / m[1] / 'owner.json'
        if request.get('mode') in {'candidate-boot', 'candidate-play'} or owner_path.exists():
            principal = self.authenticated_principal()
            if principal is None:
                return self.respond(401, {'error': 'unambiguous candidate job principal required'})
            if (not owner_path.is_file()
                    or json.loads(owner_path.read_text(encoding='utf-8-sig')).get('principal') != principal):
                return self.respond(403, {'error': 'candidate job belongs to another principal'})
        status = json.loads((station.jobs/m[1]/'status.json').read_text(encoding='utf-8-sig'))
        if not m[2]:
            return self.respond(200,status)
        if status['status'] == 'RUNNING':
            return self.respond(409,{'error':'result not ready'})
        result = station.jobs/m[1]/'result.zip'
        try:
            result_info = result.lstat()
        except OSError:
            return self.respond(409, {'error':'result unavailable'})
        if (_studio_reparse(result_info) or not stat.S_ISREG(result_info.st_mode)
                or status.get('result_unavailable') or not isinstance(status.get('result_sha256'), str)):
            return self.respond(409, {'error':'result unavailable'})
        self.send_response(200); self.send_header('Content-Type','application/zip')
        self.send_header('Content-Length',str(result_info.st_size)); self.end_headers()
        with result.open('rb') as stream:
            shutil.copyfileobj(stream,self.wfile)

    def do_POST(self):
        if not self.authorized():
            return self.respond(401,{'error':'authorization required'})
        principal = self.authenticated_principal()
        self.connection.settimeout(300)
        station = self.server.station
        try:
            size = int(self.headers.get('Content-Length','0'))
            if size <= 0 or size > MAX_ARCHIVE:
                return self.respond(413,{'error':'invalid request size'})
            ui = re.fullmatch(r'/jobs/([a-f0-9]{32})/ui', self.path)
            if ui:
                if principal is None:
                    return self.respond(401, {'error':'unambiguous job principal required'})
                if size > UI_ACTION_LIMIT:
                    return self.respond(413, {'error':'UI action exceeds its size limit'})
                action = _studio_json(self.rfile.read(size), UI_ACTION_LIMIT, 'UI action')
                if isinstance(action, dict) and action.get('action') == 'capture-current':
                    code, value = station.capture_current(ui[1], principal, action)
                    if code == 200:
                        png, metadata = value
                        return self.respond_interactive_frame(png, metadata)
                    return self.respond(code, value)
                code, value = station.ui_action(ui[1], principal, action)
                return self.respond(code, value)
            if self.path == '/snapshots':
                with tempfile.TemporaryDirectory(dir=station.data) as temp:
                    temp = Path(temp); archive = temp/'upload.zip'
                    with archive.open('wb') as dest:
                        remaining = size
                        while remaining:
                            chunk = self.rfile.read(min(1024**2,remaining))
                            if not chunk:
                                raise ValueError('incomplete upload')
                            dest.write(chunk); remaining -= len(chunk)
                    src = temp/'verify'; src.mkdir()
                    manifest = unpack_snapshot(archive,src)
                    sid = manifest['snapshot_id']
                    target = station.snapshots/(sid+'.zip')
                    with station.lock:
                        if not target.exists():
                            if shutil.disk_usage(station.data).free < size + 10*1024**3:
                                raise ValueError('insufficient free station disk')
                            archive.replace(target)
                    return self.respond(200,{'snapshot_id':sid,'hashes_verified':True,'files':len(manifest['files'])})
            blob = re.fullmatch(r'/blobs/([a-f0-9]{64})', self.path)
            if blob:
                with station.lock:
                    if shutil.disk_usage(station.data).free < size + 10*1024**3:
                        raise ValueError('insufficient free station disk')
                    maximum = MAX_BLOB
                    if size > MAX_BLOB:
                        if principal is None:
                            return self.respond(401, {'error': 'unambiguous candidate owner required'})
                        for candidate_path in station.candidates.glob('*.json'):
                            record = station.candidate_for(candidate_path.stem)
                            if record is None or record['owner'] != principal:
                                continue
                            manifest = record['manifest']
                            if (manifest['windows_zip_sha256'] == blob[1]
                                    and manifest['windows_zip_bytes'] == size):
                                maximum = MAX_ARCHIVE
                                break
                        if maximum == MAX_BLOB:
                            return self.respond(413, {'error': 'oversized blob requires an exact owned Windows candidate ZIP plan'})
                    stored = store_blob(station.blobs, blob[1], self.rfile, size, maximum=maximum)
                return self.respond(200,{'sha256':blob[1],'stored':stored})
            if self.path == '/snapshots/plan':
                manifest = json.loads(self.rfile.read(size))
                sid = validate_manifest(manifest)
                if not station.seeded:
                    station.seed_from_latest_snapshot()
                with station.lock:
                    (station.manifests/(sid+'.json')).write_bytes(canonical(manifest))
                    station.prune_manifests()
                missing = sorted({meta['sha256'] for meta in manifest['files'].values()
                                  if not blob_path(station.blobs, meta['sha256']).is_file()})
                return self.respond(200,{'snapshot_id':sid,'known':not missing,
                                         'missing':missing,'files':len(manifest['files'])})
            if self.path == '/candidates/plan' and size <= 16384:
                if principal is None:
                    return self.respond(401, {'error': 'unambiguous candidate owner required'})
                code, value = station.plan_candidate(json.loads(self.rfile.read(size)), principal)
                return self.respond(code, value)
            if self.path == '/jobs' and size <= 16384:
                if principal is None:
                    return self.respond(401, {'error':'unambiguous job principal required'})
                code, value = station.submit(json.loads(self.rfile.read(size)), principal)
                return self.respond(code,value)
            return self.respond(404,{'error':'unknown operation'})
        except (ValueError, KeyError, OSError, zipfile.BadZipFile) as error:
            return self.respond(400,{'error':str(error)})

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--config',type=Path,required=True)
    args = parser.parse_args(); config=json.loads(args.config.read_text(encoding='utf-8-sig'))
    sys.stderr = (Path(config['data'])/'worker-error.log').open('a',encoding='utf-8',buffering=1)
    sys.stdout = sys.stderr
    station=Station(config)
    server=ThreadingHTTPServer(('127.0.0.1',8765),Handler); server.station=station
    server.serve_forever()

if __name__ == '__main__':
    main()
