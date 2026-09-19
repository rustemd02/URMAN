#!/usr/bin/env python3
"""Validate the real preview package plus rejection cases; never installs it."""
import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('delivery',type=Path)
    parser.add_argument('--project',type=Path,default=Path(__file__).resolve().parents[1])
    args=parser.parse_args();project=args.project.resolve();delivery=args.delivery.resolve()
    spec=importlib.util.spec_from_file_location('radio_preview_delivery',project/'eng/apply-act1-radio-previews.py')
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    station=project/'game/content/vehicles/avyl_radio.v1.json'
    station_before=station.read_bytes();manifest=json.loads((delivery/'preview-manifest.json').read_text())
    before={item['path']:hashlib.sha256((project/item['path']).read_bytes()).hexdigest() for item in manifest['segments']}
    checks=[]
    plan=module.prepare_delivery(delivery,project)
    assert plan['report']['recordedSegments']==['music-one'] and plan['report']['pendingSegments']==[]
    assert plan['report']['repeatedSegments']==['music-two'] and plan['report']['repeatSource']=='music-one'
    projected=json.loads(plan['outputs']['game/content/vehicles/avyl_radio.v1.json'])
    source=json.loads(station_before)
    assert [r['id'] for r in projected['segments']]==[r['id'] for r in source['segments']]
    original=next(r for r in projected['segments'] if r['id']=='music-one')
    repeat=next(r for r in projected['segments'] if r['id']=='music-two')
    assert original==next(r for r in source['segments'] if r['id']=='music-one')
    assert repeat['rightsStatus']==module.REPEAT_RIGHTS and repeat['repeatsSegmentId']==original['id']
    assert all(repeat[key]==original[key] for key in ('streamPath','sha256','provenance','durationSeconds','language'))
    assert all(r['rightsStatus']==module.RIGHTS and r['origin']==module.ORIGIN and r['acceptance']==module.ACCEPTANCE
               for r in projected['segments'] if r['id'] in module.EXPECTED)
    assert any(name.endswith('/model/v5_cis_base_nostress.jit') and len(data)==91695221 for name,data in plan['outputs'].items())
    assert not any('marat' in name.lower() or 'rinat' in name.lower() for name in plan['outputs'])
    checks.append('real ten-WAV plan preserves IDs and original music; explicit repeat reuses its exact bytes/provenance; full source model stays outside runtime')
    del plan
    cases=[
        ('false original-actor claim',lambda m:m.update(recordedActor=True)),
        ('false final acceptance',lambda m:m.update(acceptedAsFinalRecording=True)),
        ('unreviewed model licence',lambda m:m['model'].update(modelLicense='CC-BY-NC-4.0')),
        ('changed voice ID',lambda m:m['segments'][0]['chunks'][0].update(modelSpeakerId=21)),
        ('changed authored transcript',lambda m:m['segments'][0].update(transcript='Другая реплика.')),
        ('changed spoken words',lambda m:m['segments'][0]['chunks'][0].update(pronunciationText='другая реплика.')),
        ('changed WAV digest',lambda m:m['segments'][0].update(sha256='0'*64)),
        ('incorrect duration',lambda m:m['segments'][0].update(durationSeconds=1.0)),
        ('duplicate/missing segment',lambda m:m['segments'].__setitem__(1,copy.deepcopy(m['segments'][0]))),
    ]
    with tempfile.TemporaryDirectory(prefix='urman-radio-preview-check-') as temporary:
        root=Path(temporary)
        for name,mutate in cases:
            altered=copy.deepcopy(manifest);mutate(altered)
            (root/'preview-manifest.json').write_text(json.dumps(altered,ensure_ascii=False))
            try:module.prepare_delivery(root,project)
            except (ValueError,KeyError,TypeError):checks.append('rejects '+name)
            else:raise AssertionError('accepted '+name)
    assert station.read_bytes()==station_before
    assert before=={path:hashlib.sha256((project/path).read_bytes()).hexdigest() for path in before}
    checks.append('dry-run and rejected deliveries preserve station and all original preview WAV bytes')
    print(json.dumps({'exitCode':0,'scope':'real package byte/provenance validation and rejection; no runtime installation/listening claim','checks':checks},ensure_ascii=False,indent=2))

if __name__=='__main__':main()
