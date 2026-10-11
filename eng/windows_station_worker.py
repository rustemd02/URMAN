"""Private, single-job URMAN worker. Start in an interactive user session."""
import argparse
import ctypes
import hashlib
import hmac
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
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
import zipfile

from remote_common import (MAX_ARCHIVE, MAX_RESULT_BYTES, MAX_RESULT_FILES,
                           STUDIO_AUTHORING_FILES, canonical, digest, unpack_snapshot,
                           validate_manifest, verify_source)

# Incremental snapshots: the station keeps one blob per distinct file content, so a
# new build sends only the files that actually changed instead of the whole tree.
MAX_BLOB = 512 * 1024**2
KEEP_MANIFESTS = 50
KEEP_ARCHIVES = 1        # the newest archive is what an empty blob store is seeded from
BLOB_GRACE = 24 * 3600   # content a client may still be uploading is not collected
STUDIO_SCENE = 'res://studio/studio_main.tscn'
STUDIO_OWNER = 'urman.world:act1/kit/house-old-pc-babai-approach'
STUDIO_PRESERVE_MARKER = '.preserve-source'
STUDIO_MAX_REQUESTS = 32
STUDIO_MAX_TRANSACTIONS = 32
STUDIO_MAX_REQUEST = 256 * 1024
STUDIO_MAX_PROPOSAL = 32 * 1024
STUDIO_MAX_PREVIEW_FAILURE = 4 * 1024
STUDIO_MAX_JOURNAL = 64 * 1024
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


def _studio_hash(path, limit):
    path = Path(path)
    size = path.stat().st_size
    if size > limit:
        raise ValueError('Studio authored target exceeds its size limit')
    h = hashlib.sha256()
    total = 0
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024**2), b''):
            total += len(chunk)
            if total > limit:
                raise ValueError('Studio authored target grew beyond its size limit')
            h.update(chunk)
    if total != size:
        raise ValueError('Studio authored target changed while being read')
    return size, h.hexdigest()


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
    """Export only bounded raw pilot request and transaction evidence."""
    source = Path(source)
    output = Path(output)
    payloads = []
    request_ids = {}
    proposal_ids = set()
    requests_root = _studio_path(source, '.urman-studio/ai-jobs', kind='dir', missing=True)
    if requests_root is not None:
        request_dirs = _studio_children(requests_root, STUDIO_MAX_REQUESTS, 'Studio request directories')
        for directory in request_dirs:
            info = directory.lstat()
            if _studio_reparse(info) or not stat.S_ISDIR(info.st_mode) or not re.fullmatch('[a-f0-9]{32}', directory.name):
                raise ValueError('Studio request directory is not a canonical request ID')
            request_id = directory.name
            contents = _studio_children(directory, 3, 'Studio request directory')
            names = {item.name for item in contents}
            if names not in ({'request.json'}, {'request.json', 'proposal.json'},
                             {'request.json', 'proposal.json', 'preview-failed.json'}):
                raise ValueError('Studio request directory has missing or unexpected files: ' + request_id)
            if any(_studio_reparse(item.lstat()) or not stat.S_ISREG(item.lstat().st_mode) for item in contents):
                raise ValueError('Studio request evidence contains a link or non-file: ' + request_id)
            _, request_bytes = _studio_read(source, f'.urman-studio/ai-jobs/{request_id}/request.json',
                                            STUDIO_MAX_REQUEST, 'Studio request')
            request = _studio_json(request_bytes, STUDIO_MAX_REQUEST, 'Studio request')
            if (not isinstance(request, dict) or request.get('kind') != 'urman.studio.authoring-request'
                    or request.get('schemaVersion') != 2 or request.get('requestId') != request_id):
                raise ValueError('Studio request identity is invalid: ' + request_id)
            request_ids[request_id] = request
            payloads.append((f'.urman-studio/ai-jobs/{request_id}/request.json', request_bytes, None))
            proposal_bytes = None
            if 'proposal.json' in names:
                _, proposal_bytes = _studio_read(source, f'.urman-studio/ai-jobs/{request_id}/proposal.json',
                                                 STUDIO_MAX_PROPOSAL, 'Studio proposal')
                proposal = _studio_json(proposal_bytes, STUDIO_MAX_PROPOSAL, 'Studio proposal')
                if (not isinstance(proposal, dict) or proposal.get('requestId') != request_id
                        or not isinstance(request.get('project'), dict)
                        or proposal.get('snapshotHash') != request['project'].get('snapshotHash')):
                    raise ValueError('Studio request and proposal identity do not match: ' + request_id)
                proposal_ids.add(request_id)
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
            _, journal_bytes = _studio_read(source, journal_relative, STUDIO_MAX_JOURNAL, 'Studio transaction journal')
            journal = _studio_json(journal_bytes, STUDIO_MAX_JOURNAL, 'Studio transaction journal')
            required_journal_keys = {'version', 'id', 'owner', 'status', 'rollbackSide', 'targetSide', 'side', 'files'}
            optional_journal_keys = {'accepted', 'acceptedOrder', 'commandLabel'}
            if (not isinstance(journal, dict)
                    or not required_journal_keys.issubset(journal)
                    or set(journal) - required_journal_keys - optional_journal_keys):
                raise ValueError('Studio transaction journal has missing or unexpected fields: ' + transaction_id)
            if type(journal['version']) is not int or journal['version'] != 1 or journal['id'] != transaction_id:
                raise ValueError('Studio transaction journal version or ID is invalid: ' + transaction_id)
            if journal['owner'] != STUDIO_OWNER:
                raise ValueError('Unexpected Studio transaction owner; source must be preserved: ' + transaction_id)
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
            if not isinstance(journal['files'], list) or len(journal['files']) not in (3, 4):
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
                        if image_size > STUDIO_MAX_BINARY:
                            raise ValueError('Studio transaction image exceeds 128 MiB: ' + transaction_id)
                        payloads.append((image_relative, image_path, file_hash))

                match = re.fullmatch(r'game/assets/models/studio/hero-house-([a-f0-9]{32})\.glb', relative)
                if match:
                    if request_id is not None:
                        raise ValueError('Studio transaction has multiple request-scoped models: ' + transaction_id)
                    request_id = match.group(1)
                elif relative != 'game/content/studio/hero_house.recipe.json' and not re.fullmatch(
                        r'game/assets/models/studio/hero-house-[a-f0-9]{32}\.glb\.manifest\.json', relative) and not re.fullmatch(
                        r'assets/source/blender/studio/hero-house-[a-f0-9]{32}\.blend', relative):
                    raise ValueError('Studio transaction contains a target outside the pilot allowlist: ' + transaction_id)

            if request_id is None or request_id not in request_ids or request_id not in proposal_ids:
                raise ValueError('Studio transaction has no matching exported request: ' + transaction_id)
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
            contents = _studio_children(directory, 9, 'Studio transaction directory')
            if {item.name for item in contents} != expected_contents:
                raise ValueError('Studio transaction directory has missing or unexpected files: ' + transaction_id)
            if any(_studio_reparse(item.lstat()) or not stat.S_ISREG(item.lstat().st_mode) for item in contents):
                raise ValueError('Studio transaction evidence contains a link or non-file: ' + transaction_id)
            payloads.append((journal_relative, journal_bytes, None))

    current_targets = {}
    inspected_bytes = 0
    for request_id in sorted(request_ids):
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
                entry = {'path': relative, 'present': False, 'bytes': 0, 'sha256': None}
            else:
                size, sha = _studio_hash(path, STUDIO_MAX_BINARY)
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
    total_bytes = 0
    seen_payload_paths = set()
    for relative, source_or_bytes, expected_hash in payloads:
        if relative in seen_payload_paths:
            raise ValueError('Studio evidence contains a duplicate exported path')
        seen_payload_paths.add(relative)
        if isinstance(source_or_bytes, bytes):
            size = len(source_or_bytes)
        else:
            size = _studio_path(source, relative).lstat().st_size
        if size > STUDIO_MAX_BINARY:
            raise ValueError('Studio binary evidence exceeds 128 MiB')
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
        with destination.open('xb') as stream:
            if isinstance(source_or_bytes, bytes):
                if copied_bytes + len(source_or_bytes) > STUDIO_MAX_EXPORT:
                    raise ValueError('Studio evidence grew beyond the 512 MiB export budget')
                stream.write(source_or_bytes)
                hasher.update(source_or_bytes)
                written = len(source_or_bytes)
            else:
                source_path = _studio_path(source, relative)
                source_info = source_path.lstat()
                source_size = source_info.st_size
                if source_size > STUDIO_MAX_BINARY:
                    raise ValueError('Studio binary evidence exceeds 128 MiB')
                with source_path.open('rb') as incoming:
                    for chunk in iter(lambda: incoming.read(1024**2), b''):
                        next_written = written + len(chunk)
                        if next_written > STUDIO_MAX_BINARY:
                            raise ValueError('Studio evidence file grew beyond 128 MiB')
                        if copied_bytes + next_written > STUDIO_MAX_EXPORT:
                            raise ValueError('Studio evidence grew beyond the 512 MiB export budget')
                        stream.write(chunk)
                        hasher.update(chunk)
                        written = next_written
        if written != source_size:
            raise ValueError('Studio evidence changed while being exported: ' + relative)
        if expected_hash is not None and hasher.hexdigest() != expected_hash:
            raise ValueError('Studio transaction image hash mismatch: ' + relative)
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
    with temp_path.open('rb') as stream:
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

def store_blob(blobs, sha, stream, size):
    """Store one content-addressed file. The path is its hash, so no name can escape."""
    if not re.fullmatch('[a-f0-9]{64}', sha) or not 0 < size <= MAX_BLOB:
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
    session = ctypes.c_ulong()
    ctypes.windll.kernel32.ProcessIdToSessionId(os.getpid(), ctypes.byref(session))
    user32 = ctypes.windll.user32
    user32.OpenInputDesktop.restype = ctypes.c_void_p
    handle = user32.OpenInputDesktop(0, False, 1)
    unlocked = bool(handle)
    if handle:
        user32.CloseDesktop.argtypes = [ctypes.c_void_p]; user32.CloseDesktop(handle)
    return {'session_id': session.value, 'interactive': session.value != 0, 'input_desktop': unlocked}

class Station:
    def __init__(self, config):
        self.config = config
        self.repo = Path(config['repository']).resolve()
        self.worker_code_sha256 = digest(Path(__file__))
        self.data = Path(config['data']).resolve(); self.data.mkdir(parents=True, exist_ok=True)
        self.lock = threading.Lock(); self.busy = None
        self.snapshots = self.data / 'snapshots'; self.snapshots.mkdir(exist_ok=True)
        self.jobs = self.data / 'runs'; self.jobs.mkdir(exist_ok=True)
        self.blobs = self.data / 'blobs'; self.blobs.mkdir(exist_ok=True)
        self.manifests = self.data / 'manifests'; self.manifests.mkdir(exist_ok=True)
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
        marker = Path(os.environ['APPDATA']) / 'Godot/app_userdata/.URMAN.protected-run.lock'
        if not marker.exists():
            return False
        with marker.open('rb') as stream:
            stream.seek(1)
            return bool(stream.read().strip())

    def health(self):
        pins = json.loads((self.repo / 'eng/toolchain.json').read_text(encoding='utf-8-sig'))
        v = pins['godot']['version'].split('.stable')[0]
        godot = self.repo / f'.tools/godot/windows/Godot_v{v}-stable_mono_win64/Godot_v{v}-stable_mono_win64_console.exe'
        dotnet = self.repo / '.tools/dotnet/dotnet.exe'
        tools = (godot.is_file() and dotnet.is_file() and Path(self.config['python']).is_file()
                 and all((self.repo/'eng'/name).is_file() for name in
                         ('run-windows-check.ps1','protected_run.py','remote_common.py')))
        d = desktop(); pending = self.recovery()
        pipeline_locked = False
        try:
            with (self.repo/'.tools/windows-station.lock').open('ab'):
                pass
        except OSError:
            pipeline_locked = True
        # Restart with an orphan runner is blocked even if the source lock is gone.
        orphan = subprocess.run(['tasklist','/FI','IMAGENAME eq '+godot.name,'/FO','CSV','/NH'],capture_output=True,text=True).stdout
        main_name = godot.name.replace('_console.exe', '.exe')
        orphan += subprocess.run(['tasklist','/FI','IMAGENAME eq '+main_name,'/FO','CSV','/NH'],capture_output=True,text=True).stdout
        orphaned = (godot.name.lower() in orphan.lower() or main_name.lower() in orphan.lower() or pipeline_locked) and not self.busy
        return {'ready': bool(tools and d['interactive'] and d['input_desktop'] and not pending and not orphaned),
                'host': os.environ['COMPUTERNAME'], 'desktop': d, 'tools': tools,
                'userdata_recovery_pending': pending, 'orphan_game_process': bool(orphaned),
                'busy_job': self.busy, 'godot': pins['godot']['version'],
                'sdk': json.loads((self.repo/'global.json').read_text(encoding='utf-8-sig'))['sdk']['version'],
                'blob_store': self.blob_usage(), 'disk': self.disk(),
                'gpu_acceptance': self.config.get('gpu_acceptance', 'not-run')}

    def submit(self, spec):
        if set(spec) - {'job_id','snapshot_id','mode','scene','timeout','headless','points','phase','fov'}:
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
        if mode not in ('smoke', 'capture', 'studio'):
            raise ValueError('unsupported mode')
        timeout_max = 1200 if mode == 'studio' else 300
        if type(spec.get('timeout')) is not int or not 1 <= spec['timeout'] <= timeout_max:
            raise ValueError(f'timeout must be 1..{timeout_max} seconds')
        if type(spec.get('headless')) is not bool:
            raise ValueError('headless must be boolean')
        scene = spec.get('scene', '')
        if not re.fullmatch(r'res://[\w/.-]+\.tscn', scene) or '..' in scene:
            raise ValueError('invalid scene')
        if scene == STUDIO_SCENE and mode != 'studio':
            raise ValueError('Studio scene is only available in fixed Studio mode')
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
        if mode == 'smoke' and not scene.startswith('res://tests/'):
            raise ValueError('smoke requires an existing test scene')
        if mode == 'capture' and (scene != 'res://scenes/act1_demo.tscn' or spec['headless']):
            raise ValueError('capture requires native act1_demo')
        points = spec.get('points', '')
        number = r'-?\d+(?:\.\d+)?'
        vector = rf'(?:[A-Za-z][A-Za-z0-9_]*@)?{number},{number},{number}'
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
            write_json(path/'status.json', status); self.busy = jid
            threading.Thread(target=self.execute, args=(path,spec,status), daemon=False).start()
            return 202, status

    def execute(self, path, spec, status):
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
                if not (source/'game'/spec['scene'][6:]).is_file():
                    raise ValueError('scene does not exist in snapshot')
                for pin in ('global.json','eng/toolchain.json'):
                    if json.loads((source/pin).read_text(encoding='utf-8-sig')) != json.loads((self.repo/pin).read_text(encoding='utf-8-sig')):
                        raise ValueError('snapshot toolchain differs from station: '+pin)
                execution_files = {}
                for name in ('eng/run-windows-check.ps1','eng/protected_run.py','eng/remote_common.py'):
                    # Git normalizes Windows/Mac line endings differently. Require
                    # identical source text, and record both actual byte hashes.
                    if (source/name).read_text(encoding='utf-8-sig') != (self.repo/name).read_text(encoding='utf-8-sig'):
                        raise ValueError('station execution infrastructure differs from snapshot: '+name+'; update station before retrying')
                    execution_files[name] = {'station_sha256':digest(self.repo/name),'snapshot_sha256':manifest['files'][name]['sha256'],'source_text_equal':True}
                # Station-local override is recorded separately; never overwrite a developer file.
                (source/'game/override.cfg').write_text('[filesystem]\nimport/blender/enabled=false\n')
                command = ['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',
                           str(self.repo/'eng/run-windows-check.ps1'), '-Root',str(source),
                           '-ToolsRoot',str(self.repo), '-Provenance',str(source/'snapshot-manifest.json'),
                           '-Output',str(output), '-Mode',('play' if spec['mode'] == 'studio' else spec['mode']), '-Scene',spec['scene'],
                           '-TimeoutSeconds',str(spec['timeout']), '-Python',self.config['python']]
                if spec['headless']:
                    command += ['-Headless']
                if spec.get('points'):
                    command += ['-ViewPoints', spec['points']]
                if spec.get('phase'):
                    command += ['-AtmospherePhase', spec['phase']]
                if spec.get('fov'):
                    command += ['-ViewFov', spec['fov']]
                with (output/'stdout.log').open('wb') as out, (output/'stderr.log').open('wb') as err:
                    p = subprocess.Popen(command,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
                    # Runner bounds build/content/import/game separately. This outer emergency
                    # deadline preserves the guard's recovery marker instead of claiming restore.
                    try:
                        code = p.wait(timeout=spec['timeout']*2+1050)
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
                try:
                    export_studio_session(source, output)
                    preserve_source = False
                except Exception as error:
                    preserve_source = True
                    status.update(status='FAIL', error='Studio evidence export failed: ' + str(error)[:1800], exit_code=1)
                    receipt_path = output/'receipt.json'
                    receipt = json.loads(receipt_path.read_text(encoding='utf-8-sig')) if receipt_path.exists() else {}
                    receipt.update(status='FAIL', error=status['error'], job_id=spec['job_id'],
                                   snapshot_id=spec['snapshot_id'], studio_evidence_preserved_on_station=True)
                    write_json(receipt_path, receipt)

            write_json(output/'request.json',spec)
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
                    self.busy = None
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
                self.busy = None

class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass  # No headers, tokens, source paths or credentials in access logs.

    def respond(self, code, value):
        data = canonical(value); self.send_response(code)
        self.send_header('Content-Type','application/json'); self.send_header('Content-Length',str(len(data)))
        self.end_headers(); self.wfile.write(data)

    def authorized(self):
        value = self.headers.get('Authorization','')
        return any(hmac.compare_digest(value, 'Bearer '+token) for token in self.server.station.config['tokens'].values())

    def do_GET(self):
        if not self.authorized():
            return self.respond(401, {'error':'authorization required'})
        station = self.server.station
        if self.path == '/health':
            return self.respond(200,station.health())
        m = re.fullmatch(r'/jobs/([a-zA-Z0-9_-]{8,64})(/result)?',self.path)
        if not m or not (station.jobs/m[1]/'status.json').is_file():
            return self.respond(404,{'error':'unknown job'})
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
        self.connection.settimeout(300)
        station = self.server.station
        try:
            size = int(self.headers.get('Content-Length','0'))
            if size <= 0 or size > MAX_ARCHIVE:
                return self.respond(413,{'error':'invalid request size'})
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
                if shutil.disk_usage(station.data).free < size + 10*1024**3:
                    raise ValueError('insufficient free station disk')
                with station.lock:
                    stored = store_blob(station.blobs, blob[1], self.rfile, size)
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
            if self.path == '/jobs' and size <= 16384:
                code, value = station.submit(json.loads(self.rfile.read(size)))
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
