#!/usr/bin/env python3
"""Auditable offline speech previews. Never installs or approves station audio."""
import ast
import hashlib
import json
import math
from pathlib import Path
import re
import sys
import time
import wave

import numpy as np
import torch

REPO = Path(__file__).resolve().parents[2]
MODEL = Path(__file__).parent / 'silero-cis-base-d9355348e2781dc8fa25a135d1602c530afae24c'
# These keep the authored programme text. Russian + marks are pronunciation
# input, never claims that a native speaker has accepted the resulting audio.
PARTS = {
 'avyl-ident': [
  ('tat_albina', 'Хәерле көн, авылдашлар! Авыл FM эфирда. Исәнмесез, Кара-Урман!'),
  ('ru_albina', 'Р+ядом с в+ами — рай+онные н+овости, м+узыка и прив+еты из д+ома.')],
 'winter-weather': [
  ('ru_albina', 'За окн+ом з+имний день. К в+ечеру похолод+ает; на ук+атанных повор+отах ск+ользко. Вод+ителям — остор+ожнее у остан+овок и на подъ+ездах к двор+ам.'),
  ('tat_albina', 'Җылы киенегез, юлларда сак булыгыз!')],
 'school-bus': [
  ('ru_albina', 'Напомин+ание род+ителям: д+ети из небольш+их ав+ылов +ездят в сос+еднюю шк+олу авт+обусом. +Если ребёнок забол+ел и сег+одня не по+едет, предупред+ите сопровожда+ющего. Вод+ителю не н+ужно зря ждать у остан+овки.')],
 'family-salam': [
  ('tat_albina', 'Казаннан туган авылына сәлам юллыйлар. Әниләр, әтиләр, әбиләр, бабайлар — исән-сау булыгыз!'),
  ('ru_albina', 'А Ра+илю +апу поздравл+яют с днём рожд+ения д+ети и вн+уки. Тёплого д+ома вам и д+обрых сос+едей!')],
 'village-photos': [
  ('ru_albina', 'В дерев+енском кл+убе собир+ают п+одписи к ст+арым фотогр+афиям. +Если узн+али на сн+имке родн+ых или сос+едей, расскаж+ите, где и когд+а он был сд+елан. Оригин+алы берег+ите; для ст+енда дост+аточно к+опии.')],
 'nearby-villages': [
  ('tat_albina', 'Сәлам, Яңа Кырлай! Сәлам, Кушлавыч!'),
  ('ru_albina', 'Сл+ушателям из сос+едних ав+ылов — хор+ошего дня. +Если возвращ+аетесь в+ечером из рай+она, позвон+ите дом+ой зар+анее: пусть зн+ают, что вы в пут+и.')],
 'local-workshop': [
  ('ru_albina', 'Бытов+ой рем+онт р+ядом с д+омом: зат+очка инструм+ента, м+елкая св+арка, п+омощь с вор+отами и замк+ами. Объявл+ения мастер+ов ищ+ите на доск+е у магаз+ина. О цен+е и вр+емени догов+аривайтесь с м+астером зар+анее.')],
 'winter-congratulations': [
  ('tat_albina', 'Кадерле авылдашлар! Өйләрегез җылы, күңелләрегез тыныч булсын. Бер-берегезне онытмагыз, хәл белешеп торыгыз. Сәламнәрегезне көтеп калабыз!')],
 'lost-glove': [
  ('ru_albina', 'На л+авке у остан+овки нашл+и в+арежку. Кто потер+ял — спрос+ите в магаз+ине. М+аленькие в+ещи т+оже возвращ+аются дом+ой, +если сос+еди замеч+ают друг др+уга.')],
 'night-repeat': [
  ('ru_albina', 'Вы сл+ушаете Ав+ыл FM. Спас+ибо всем, кто передаёт прив+еты и д+елится дерев+енскими н+овостями.'),
  ('tat_albina', 'Хәерле юл!'),
  ('ru_albina', 'След+ите за дор+огой, берег+ите себ+я.')],
}

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def canonical(text):
    return re.sub(r'\s+', ' ', text.replace('+', '')).strip()

def main():
    destination = Path(sys.argv[1]).resolve()
    if destination.exists():
        raise SystemExit('Refusing to overwrite a preview run: ' + str(destination))
    if REPO / 'game' in destination.parents:
        raise SystemExit('Preview preparation must stay outside runtime game files.')
    manifest = json.loads((MODEL / 'download-manifest.json').read_text())
    for record in manifest['entries']:
        if digest(REPO / record['path']) != record['sha256']:
            raise SystemExit('Pinned model/evidence digest mismatch: ' + record['path'])
    notebook = json.loads((MODEL / 'v5_cis_base_jit.ipynb').read_text())
    constants = {}
    for cell in notebook['cells']:
        if cell['cell_type'] != 'code':
            continue
        for node in ast.parse(''.join(cell.get('source', []))).body:
            if isinstance(node, ast.Assign) and len(node.targets) == 1 and isinstance(node.targets[0], ast.Name):
                if node.targets[0].id in ('symbols', 'speakers'):
                    constants[node.targets[0].id] = ast.literal_eval(node.value)
    symbol_ids = {symbol: index for index, symbol in enumerate(constants['symbols'])}
    speakers = constants['speakers']
    programme = json.loads((REPO / 'game/content/vehicles/avyl_radio.v1.json').read_text())
    segments = {item['id']: item for item in programme['segments']}
    for identity, parts in PARTS.items():
        if canonical(' '.join(text for _, text in parts)) != segments[identity]['transcript']:
            raise SystemExit('Pronunciation input differs from current authored text: ' + identity)
    torch.set_num_threads(2)
    torch.manual_seed(20260916)
    model = torch.jit.load(str(MODEL / 'v5_cis_base_nostress.jit'), map_location='cpu').eval()
    destination.mkdir(parents=True)
    records = []
    for identity, parts in PARTS.items():
        started = time.monotonic()
        samples = []
        chunks = []
        for voice, text in parts:
            # Spell the Latin programme acronym in the pronunciation input;
            # preserve the exact written source transcript above and below.
            prepared = text.replace('FM', 'эф эм').lower()
            unsupported = sorted(set(prepared) - set(symbol_ids))
            if unsupported:
                raise ValueError('Unsupported speech characters: ' + repr(unsupported))
            tokens = torch.tensor([[2] + [symbol_ids[s] for s in prepared] + [1]], dtype=torch.long)
            with torch.inference_mode():
                generated = model(tokens, torch.tensor([speakers[voice]], dtype=torch.long))
            audio = generated.detach().cpu().numpy().reshape(-1).astype(np.float32)
            if audio.size == 0 or not np.isfinite(audio).all():
                raise ValueError('Model returned empty or non-finite audio.')
            if samples:
                samples.append(np.zeros(5760, dtype=np.float32))
            samples.append(audio)
            chunks.append({'modelSpeaker': voice, 'modelSpeakerId': speakers[voice], 'pronunciationText': prepared,
                           'samples': int(audio.size), 'sampleRate': 48000})
        audio = np.concatenate(samples)
        # Preserve internal pauses. Trim only near-silent edges and retain 50ms.
        nonquiet = np.flatnonzero(np.abs(audio) > 0.002)
        if nonquiet.size == 0:
            raise ValueError('Model returned silent audio.')
        audio = audio[max(0, int(nonquiet[0]) - 2400):min(audio.size, int(nonquiet[-1]) + 2401)]
        gain = min(1.0, 10 ** (-3 / 20) / float(np.max(np.abs(audio))))
        audio *= gain
        pcm = np.clip(np.rint(audio * 32767), -32768, 32767).astype('<i2')
        target = destination / (identity + '.wav')
        with wave.open(str(target), 'wb') as output:
            output.setnchannels(1); output.setsampwidth(2); output.setframerate(48000); output.writeframes(pcm.tobytes())
        record = {'segmentId': identity, 'path': str(target.relative_to(REPO)), 'sha256': digest(target),
                  'origin': 'synthetic-preview', 'status': 'not-human-recording; listening-and-native-Tatar-review-pending',
                  'transcript': segments[identity]['transcript'], 'language': segments[identity]['language'],
                  'durationSeconds': audio.size / 48000, 'peakDbfs': 20 * math.log10(float(np.max(np.abs(audio)))),
                  'rmsDbfs': 20 * math.log10(float(np.sqrt(np.mean(audio.astype(np.float64) ** 2)))),
                  'processing': {'edgeSilenceTrimThreshold': 0.002, 'edgePadSeconds': 0.05,
                                 'partGapSeconds': 0.12, 'normalizationGain': gain, 'format': 'PCM16 mono 48000Hz'},
                  'generationSeconds': time.monotonic() - started, 'chunks': chunks}
        records.append(record)
        (destination / 'preview-manifest.json').write_text(json.dumps({
            'origin': 'synthetic-preview', 'recordedActor': False, 'acceptedAsFinalRecording': False,
            'model': manifest, 'torchVersion': torch.__version__, 'seed': 20260916,
            'generatorSha256': digest(Path(__file__)), 'runtimeInstallation': 'not-installed',
            'finalMaratAndRinatVoices': 'not-produced; original actor recordings remain required',
            'segments': records}, ensure_ascii=False, indent=2) + '\n')
        print(json.dumps({key: record[key] for key in ('segmentId', 'durationSeconds', 'peakDbfs', 'rmsDbfs', 'generationSeconds', 'sha256')}, ensure_ascii=False), flush=True)

if __name__ == '__main__':
    main()
