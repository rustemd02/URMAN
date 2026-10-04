#!/usr/bin/env python3
"""Small, source-backed village one-shots. No voice synthesis or ambient bed.

Downloads public CC0 previews after checking the original page's license,
keeps only short mono PCM clips, and records source/output hashes. Rebuilding
requires macOS afconvert; running the game does not. Listening remains a human check.
"""
import array
import hashlib
import json
import math
from pathlib import Path
import random
import re
import subprocess
import tempfile
import urllib.request
import wave
from generate_act1_footsteps import normalize_pcm_wav

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'game/assets/audio/act1/village_life'
RATE = 24000
SOURCES = [
    ('dog', 'qubodup', 211607, 5),
    ('plates', 'lolamadeus', 144185, 5),
    ('cutlery', 'OwlStorm', 209014, .95),
    ('kettle', 'Rudmer_Rotteveel', 699580, 7),
    ('hens', 'Breviceps', 456803, 3),
    ('tea', 'edschaefer', 411438, 5),
    ('tap', 'qubodup', 210429, 5),
    ('cat', 'TRNGLE', 362652, 1.16),
    ('shovel', 'XiiiSamples', 382270, 6),
]


def write(name, samples):
    peak = max(abs(v) for v in samples)
    rms = math.sqrt(sum(v*v for v in samples) / len(samples))
    if peak == 0: raise ValueError('silent clip')
    gain = min(.13 / max(.001, rms), .65 / peak)
    # Very short fades avoid a cut at a nonzero sample.
    fade = min(480, len(samples)//4)
    data = array.array('h', (round(v*gain*32767*min(1,i/fade,(len(samples)-1-i)/fade)) for i,v in enumerate(samples)))
    path = OUT / (name+'.wav')
    with wave.open(str(path),'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(data.tobytes())
    return dict(file='res://assets/audio/act1/village_life/'+path.name,
                seconds=len(samples)/RATE, pcmBytes=len(data)*2,
                sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    receipts=[]
    with tempfile.TemporaryDirectory(prefix='urman-life-audio-') as temporary:
        temp=Path(temporary)
        for name,author,ident,seconds in SOURCES:
            page=f'https://freesound.org/people/{author}/sounds/{ident}/'
            html=urllib.request.urlopen(page,timeout=40).read()
            text=html.decode()
            if 'creativecommons.org/publicdomain/zero/1.0' not in text:
                raise ValueError('CC0 not confirmed: '+page)
            preview=re.search(r'https://cdn\.freesound\.org/previews/[^"<>\s]+-hq\.mp3',text).group(0)
            compressed=urllib.request.urlopen(preview,timeout=40).read()
            source=temp/(name+'.mp3'); source.write_bytes(compressed)
            decoded=temp/(name+'.wav')
            stage=temp/(name+'.stage.wav')
            subprocess.run(['afconvert','-f','WAVE','-d',f'LEI16@{RATE}','-c','1',str(source),str(stage)],check=True)
            normalize_pcm_wav(stage,decoded,sample_rate=RATE)
            with wave.open(str(decoded),'rb') as w: pcm=array.array('h',w.readframes(w.getnframes()))
            count=min(len(pcm),round(seconds*RATE))
            # Choose an uninterrupted energetic section; never collage animal calls.
            energy=[sum(v*v for v in pcm[i:i+RATE]) for i in range(0,len(pcm),RATE)]
            span=max(1,round(seconds))
            start=max(range(max(1,len(energy)-span)),key=lambda i:sum(energy[i:i+span]))*RATE
            start=min(start,len(pcm)-count)
            receipt=write(name,[v/32768 for v in pcm[start:start+count]])
            receipt.update(id=name,source=page,author=author,license='CC0-1.0',
                sourcePreviewUrl=preview,sourcePreviewSha256=hashlib.sha256(compressed).hexdigest(),
                licensePageSha256=hashlib.sha256(html).hexdigest(),startSeconds=start/RATE,
                preparation='public HQ preview; mono PCM16 24 kHz; contiguous slice; level and 20 ms fades')
            receipts.append(receipt)
            print(name,receipt['seconds'],flush=True)
        # A short derivative of the already licensed local violin recording.
        source=ROOT/'game/assets/audio/act1/radio/music-one.wav'
        decoded=temp/'music.wav'
        stage=temp/'music.stage.wav'
        subprocess.run(['afconvert','-f','WAVE','-d',f'LEI16@{RATE}','-c','1',str(source),str(stage)],check=True)
        normalize_pcm_wav(stage,decoded,sample_rate=RATE)
        with wave.open(str(decoded),'rb') as w: pcm=array.array('h',w.readframes(w.getnframes()))
        receipt=write('music',[v/32768 for v in pcm[40*RATE:52*RATE]]); receipt.update(id='music',
            source='https://commons.wikimedia.org/wiki/File:Aisa_Hakimcan_skripka.flac',
            author='Aisa Hakimcan; see original radio credits',license='CC-BY-SA-4.0',
            credits='res://assets/audio/act1/radio/credits-488afc2836463293404a9bacc88a1076cfd17793dabd8ea6273b343238f21bd5.json',
            sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
            preparation='12 second excerpt starting at 40 seconds; mono24k; gain/fades; derivative under CC-BY-SA-4.0')
        receipts.append(receipt)
        # Project-original short mechanical Foley, explicitly not field recordings.
        rng=random.Random(4104)
        for name,duration,frequency in [('clock',4,1800),('sewing',4,170),('broom',3,70)]:
            samples=[]
            for i in range(round(duration*RATE)):
                t=i/RATE
                if name=='clock':
                    tick=t%.5
                    v=math.exp(-tick*130)*(math.sin(t*frequency*math.tau)+rng.uniform(-.2,.2))
                elif name=='sewing':
                    tick=t%.085
                    v=.6*math.exp(-tick*65)*(math.sin(t*frequency*math.tau)+rng.uniform(-.3,.3))*min(1,t*4,(duration-t)*4)
                else:
                    v=rng.uniform(-1,1)*max(0,math.sin(t*math.tau*1.2))**3
                samples.append(v*.2)
            receipt=write(name,samples); receipt.update(id=name,license='Project-original',
                preparation='deterministic mechanical Foley; generated by tools/audio/prepare_village_life.py; not a recording')
            receipts.append(receipt)
    (OUT/'credits.json').write_text(json.dumps(dict(schemaVersion=1,clips=receipts,
        listening='external/not-run'),ensure_ascii=False,indent=2)+'\n')
    print('total PCM bytes',sum(r['pcmBytes'] for r in receipts),flush=True)


if __name__=='__main__': main()
