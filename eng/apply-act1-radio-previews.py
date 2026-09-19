#!/usr/bin/env python3
"""Install declared synthetic radio previews without accepting human recordings.

Dry-run is the default. --install-preview explicitly selects the reversible
preview delivery. Never builds/imports Godot or edits the asset registry.
"""
from __future__ import annotations

import argparse
import ast
import importlib.util
import json
import math
from pathlib import Path
import re
import sys

COMMIT = 'd9355348e2781dc8fa25a135d1602c530afae24c'
WEIGHT_SHA = 'd7d361caf78b8480bcd65a0c367af665a2bf6f06c8507306e3781dc7c6ce781b'
LICENSE_SHA = 'a27fc071e03323fecda8a6ba416109bf36811801fa0c95e4371cf9105848718d'
README_SHA = '940148cd83182ffcc27a036d2bb6ce2004e2d5cf3884c96dd59e0cf1df993e57'
NOTEBOOK_SHA = '7e4acc21ef18286da21a3a3f87ee5a06ae3c9e1643f29d9f3b3109294cf413cb'
ORIGIN = 'synthetic-preview'
RIGHTS = 'licensed-synthetic-preview'
ACCEPTANCE = 'preview-pending-listening-and-language-review'
REPEAT_RIGHTS = 'licensed-recording-repeat'
EXPECTED = {'avyl-ident', 'winter-weather', 'school-bus', 'family-salam', 'village-photos',
            'nearby-villages', 'local-workshop', 'winter-congratulations', 'lost-glove', 'night-repeat'}
MODEL_FILES = {'README.md': README_SHA, 'LICENSE_CIS': LICENSE_SHA,
               'v5_cis_base_jit.ipynb': NOTEBOOK_SHA, 'v5_cis_base_nostress.jit': WEIGHT_SHA}

def shared(project):
    spec = importlib.util.spec_from_file_location('urman_radio_recording_delivery', project / 'eng/apply-act1-radio-recordings.py')
    if spec is None or spec.loader is None:
        raise ValueError('The existing recording-delivery utilities are missing.')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

def prepare_delivery(delivery: Path, project: Path):
    project, delivery = project.resolve(), delivery.resolve()
    api = shared(project)
    manifest_bytes = (delivery / 'preview-manifest.json').read_bytes()
    manifest = api.strict_json(manifest_bytes)
    if manifest.get('origin') != ORIGIN or manifest.get('recordedActor') is not False \
            or manifest.get('acceptedAsFinalRecording') is not False \
            or manifest.get('runtimeInstallation') != 'not-installed':
        raise ValueError('Preview provenance must explicitly reject actor/final recording claims.')
    if not isinstance(manifest.get('torchVersion'), str) or manifest.get('seed') != 20260916:
        raise ValueError('Preview generation version and seed are required.')
    station_bytes = (project / api.STATION).read_bytes()
    station = api.strict_json(station_bytes)
    if station.get('schemaVersion') != 1 or station.get('stationId') != 'avyl-fm':
        raise ValueError('Unsupported authored station.')
    segments = {row['id']: row for row in station['segments']}
    if len(segments) != len(station['segments']):
        raise ValueError('Duplicate authored segment IDs.')
    model = manifest['model']
    if model.get('repositoryCommit') != COMMIT or model.get('modelLicense') != 'MIT':
        raise ValueError('Only the reviewed MIT CIS Base model revision is supported.')
    retained = {'preview-manifest.json': manifest_bytes}
    model_entries = {}

    def blob(relative, expected):
        relative = Path(api.nonempty(relative, 'source path'))
        path = project / relative
        if relative.is_absolute() or '..' in relative.parts or path.is_symlink() \
                or not path.resolve().is_relative_to(project) or not path.is_file():
            raise ValueError('Source escapes the existing project or is absent: ' + str(relative))
        data = path.read_bytes()
        if len(data) > 160 * 1024 * 1024 or api.digest(data) != api.sha(expected, str(relative)):
            raise ValueError('Source bytes differ from their pinned digest: ' + str(relative))
        return data

    for entry in model['entries']:
        name = Path(entry['path']).name
        if name in model_entries:
            raise ValueError('Duplicate model evidence filename.')
        model_entries[name] = entry
        expected_url = ('https://models.silero.ai/models/tts/ru/v5_cis_base_nostress.jit' if name.endswith('.jit')
                        else 'https://raw.githubusercontent.com/snakers4/silero-models/' + COMMIT + '/' + name)
        if entry.get('sourceUrl') != expected_url:
            raise ValueError('Model source URL differs from its pinned primary source.')
        if name in MODEL_FILES and entry.get('sha256') != MODEL_FILES[name]:
            raise ValueError('Model/evidence does not match the reviewed source revision.')
        data = blob(entry['path'], entry['sha256'])
        if len(data) != entry['bytes']:
            raise ValueError('Model/evidence declared byte count differs.')
        retained['model/' + name] = data
    if not set(MODEL_FILES) <= set(model_entries):
        raise ValueError('Weights, actual MIT grant, README exception and reference notebook are required.')
    generator_path = '.tools/radio-preview/prepare_avyl_preview.py'
    retained['generator.py'] = blob(generator_path, manifest['generatorSha256'])
    constants = {}
    notebook = api.strict_json(retained['model/v5_cis_base_jit.ipynb'])
    for cell in notebook['cells']:
        if cell['cell_type'] != 'code':
            continue
        for node in ast.parse(''.join(cell.get('source', []))).body:
            if isinstance(node, ast.Assign) and len(node.targets) == 1 and isinstance(node.targets[0], ast.Name) \
                    and node.targets[0].id in ('symbols', 'speakers'):
                constants[node.targets[0].id] = ast.literal_eval(node.value)
    records = manifest['segments']
    ids = [row.get('segmentId') for row in records]
    if len(ids) != len(set(ids)) or set(ids) != EXPECTED:
        raise ValueError('This preview delivery must contain exactly the ten authored spoken segments.')
    delivery_id = api.digest(manifest_bytes)
    archive = api.SOURCES / ('synthetic-preview-' + delivery_id)
    outputs, reports = {}, []
    for record in records:
        identity = record['segmentId']
        segment = segments[identity]
        if segment['kind'] == 'music' or record.get('origin') != ORIGIN \
                or record.get('status') != 'not-human-recording; listening-and-native-Tatar-review-pending':
            raise ValueError('Only explicitly declared spoken synthetic previews are accepted.')
        if segment.get('rightsStatus') == 'approved':
            raise ValueError('A synthetic preview cannot overwrite an accepted recording: ' + identity)
        if record.get('transcript') != segment['transcript'] or record.get('language') != segment['language']:
            raise ValueError('The preview transcript/language differs from the current authored programme.')
        chunks = record.get('chunks')
        if not isinstance(chunks, list) or not chunks:
            raise ValueError('Exact pronunciation text, voice IDs and generated chunks are required.')
        for chunk in chunks:
            voice = chunk.get('modelSpeaker')
            if voice not in ('tat_albina', 'ru_albina') or chunk.get('modelSpeakerId') != constants['speakers'][voice]:
                raise ValueError('Voice identity differs from the pinned reference model.')
            text = api.nonempty(chunk.get('pronunciationText'), 'pronunciation text')
            if text != text.lower() or set(text) - set(constants['symbols']):
                raise ValueError('Unknown or unsupported synthesis input character.')
            if chunk.get('sampleRate') != 48000 or not isinstance(chunk.get('samples'), int) or chunk['samples'] <= 0:
                raise ValueError('A synthesis chunk needs its actual sample count.')
        normalize = lambda text: re.sub(r'\s+', ' ', text.lower().replace('+', '').replace('fm', 'эф эм')).strip()
        if normalize(' '.join(chunk['pronunciationText'] for chunk in chunks)) != normalize(segment['transcript']):
            raise ValueError('Pronunciation inputs change the authored spoken text.')
        processing = record.get('processing')
        if not isinstance(processing, dict) or processing.get('format') != 'PCM16 mono 48000Hz' \
                or not isinstance(processing.get('normalizationGain'), (int, float)) \
                or not 0 < processing['normalizationGain'] <= 1:
            raise ValueError('The actual preview processing must be declared.')
        wav = blob(record['path'], record['sha256'])
        audio = api.inspect_wav(wav, 'speech')
        duration = record.get('durationSeconds')
        if isinstance(duration, bool) or not isinstance(duration, (int, float)) or not math.isfinite(duration) \
                or abs(duration - audio['durationSeconds']) > 1 / audio['sampleRate']:
            raise ValueError('Declared preview duration differs from actual PCM frames.')
        retained['wav/' + identity + '.wav'] = wav
        target = api.RUNTIME / 'previews' / (identity + '.wav')
        outputs[target.as_posix()] = wav
        segment.update(streamPath='res://assets/audio/act1/radio/previews/' + identity + '.wav',
                       durationSeconds=audio['durationSeconds'], sha256=api.digest(wav), rightsStatus=RIGHTS,
                       origin=ORIGIN, acceptance=ACCEPTANCE, modelLicense='MIT',
                       provenance=(archive / 'provenance.json').as_posix() + '#' + identity)
        reports.append({'segmentId': identity, 'origin': ORIGIN, 'acceptance': ACCEPTANCE, 'audio': audio,
                        'sha256': api.digest(wav), 'transcript': record['transcript'], 'language': record['language'],
                        'voicesAndPronunciation': chunks, 'processing': processing})
    original=segments.get('music-one');repeat=segments.get('music-two')
    if not original or original.get('kind')!='music' or original.get('rightsStatus')!='approved' or not repeat:
        raise ValueError('The repeat requires the existing approved music-one recording.')
    if repeat.get('rightsStatus') not in ('pending-recording',REPEAT_RIGHTS):
        raise ValueError('An existing independent music-two delivery must not be replaced by a repeat.')
    repeat.update(title='Из архива · повтор',transcript='Повтор той же архивной скрипичной записи Айсы Хакимджана. Не отдельная композиция или новая запись.',
                  language=original['language'],streamPath=original['streamPath'],durationSeconds=original['durationSeconds'],
                  sha256=original['sha256'],provenance=original['provenance'],rightsStatus=REPEAT_RIGHTS,
                  origin='recorded-music-repeat',acceptance='reuses-existing-approved-recording',repeatsSegmentId=original['id'])
    recorded = [row['id'] for row in station['segments'] if row.get('rightsStatus') == 'approved']
    for identity in recorded:
        row = segments[identity]
        if row.get('streamPath') != 'res://assets/audio/act1/radio/' + identity + '.wav':
            raise ValueError('Existing recording has an unexpected runtime path.')
        data = blob((api.RUNTIME / (identity + '.wav')).as_posix(), row.get('sha256'))
        audio = api.inspect_wav(data, row['kind'])
        if abs(audio['durationSeconds'] - row['durationSeconds']) > 1 / audio['sampleRate']:
            raise ValueError('Existing recording has an incorrect duration.')
        source, separator, source_id = row.get('provenance', '').partition('#')
        source_path = project / source
        if separator != '#' or source_id != identity or not source_path.is_file() \
                or not source_path.resolve().is_relative_to(project / api.SOURCES):
            raise ValueError('Existing recording provenance is unavailable.')
    pending = [row['id'] for row in station['segments'] if row.get('rightsStatus') not in ('approved', RIGHTS, REPEAT_RIGHTS)]
    provenance = {'schemaVersion': 1, 'stationId': station['stationId'], 'deliveryId': delivery_id,
                  'origin': ORIGIN, 'recordedActor': False, 'acceptedAsFinalRecording': False,
                  'acceptance': ACCEPTANCE, 'model': model,
                  'credit': 'Silero Team, CIS Base MIT; authored URMAN radio scripts; synthetic Albina voice IDs.',
                  'licenseScope': 'MIT model/software; no claim of original human performance or accepted pronunciation.',
                  'retainedModelFiles': {name: 'model/' + name for name in model_entries},
                  'licenseEvidence': 'model/LICENSE_CIS', 'previewManifest': 'preview-manifest.json', 'segments': reports}
    retained['provenance.json'] = api.encoded(provenance)
    for name, data in retained.items():
        outputs[(archive / name).as_posix()] = data
    outputs[(api.RUNTIME / 'previews' / ('credits-' + delivery_id + '.json')).as_posix()] = api.encoded(provenance)
    station['audioAcceptance'] = (f'{len(recorded)} recorded segment(s), 1 explicitly declared archive repeat, {len(reports)} synthetic previews, '
                                f'{len(pending)} pending; listening, native-language and final production acceptance not implied')
    outputs[api.STATION.as_posix()] = api.encoded(station)
    return {'project': project, 'archive': archive, 'outputs': outputs, 'stationBefore': station_bytes,
            'report': {'deliveryId': delivery_id, 'syntheticPreviews': reports, 'recordedSegments': recorded,
                       'repeatedSegments':['music-two'],'repeatSource':'music-one',
                       'programmeDurationSeconds':sum(row['durationSeconds'] for row in station['segments']),
                       'pendingSegments': pending, 'acceptance': ACCEPTANCE,
                       'runtimeFiles': [name for name in outputs if name.startswith('game/')],
                       'followup': 'Update existing registry; serial import/build; native timeline/seek/save checks; listening and native Tatar review.'}}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('delivery', type=Path)
    parser.add_argument('--project', required=True, type=Path)
    parser.add_argument('--install-preview', action='store_true')
    args = parser.parse_args()
    try:
        plan = prepare_delivery(args.delivery, args.project)
        if args.install_preview:
            player = (args.project / 'game/scripts/VehicleRadioPlayer.cs').read_text()
            if RIGHTS not in player or ACCEPTANCE not in player:
                raise ValueError('The existing radio player does not yet support the explicit synthetic-preview contract.')
            shared(args.project.resolve()).apply_delivery(plan)
        print(json.dumps({'mode': 'installed-preview' if args.install_preview else 'dry-run', **plan['report']}, ensure_ascii=False, indent=2))
        return 0
    except (OSError, ValueError, KeyError, TypeError) as error:
        print('synthetic radio preview rejected: ' + str(error), file=sys.stderr)
        return 1

if __name__ == '__main__':
    raise SystemExit(main())
