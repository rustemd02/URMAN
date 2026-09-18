# Post-hoc preserved copy of the exact heredoc executed via Core exec_command.
# Saved after completion on 2026-09-17; this file did not exist at execution time.
# Historical replay source only: exclusive evidence directories already exist.
python3 - <<'PY'
import pathlib,subprocess,os,json,time,datetime,hashlib
root=pathlib.Path.cwd();evidence=root/'docs/production/act1_takeover_evidence_2026-09-16';candidate=root/'build/native_20260917_release01_candidate';output=evidence/'release01-performance-vehicles-01';frames=evidence/'release01-performance-vehicles-01-frames';stem='release01-vehicles-01'
assert not output.exists() and not frames.exists() and not (evidence/(stem+'.log')).exists() and not (evidence/(stem+'.json')).exists()
frames.mkdir();environment=os.environ.copy();environment['URMAN_PERF_CAPTURE_DIR']=str(frames)
command=['sh','eng/run-act1-vehicle-performance.sh','--candidate',str(candidate),'--output',str(output)]
result={'started':datetime.datetime.now(datetime.timezone.utc).isoformat(),'args':command,'scope':'Native moving Release measurements and separate completed viewport images; no listening/human acceptance.'};started=time.monotonic()
with (evidence/(stem+'.log')).open('x') as log:run=subprocess.run(command,env=environment,stdout=log,stderr=subprocess.STDOUT)
result['exitCode']=run.returncode;result['seconds']=round(time.monotonic()-started,3);result['samples']=[]
receipt_path=output/'receipt.json'
if receipt_path.is_file():
 receipt=json.loads(receipt_path.read_text());result['innerStatus']=receipt['status'];result['receiptSha256']=hashlib.sha256(receipt_path.read_bytes()).hexdigest()
 for item in receipt['samples']:
  reasons=[];metrics=item.get('metrics',{})
  for key,value in {'preset':'medium','scale':'0.90','msaa':'Msaa2X','vsync':'Disabled','window':'1920x1080'}.items():
   if metrics.get(key)!=value:reasons.append(key+' actual '+str(metrics.get(key))+' expected '+value)
  text=(output/item['log']).read_text();prefix='act1-performance-frame: ';records=[json.loads(line[len(prefix):]) for line in text.splitlines() if line.startswith(prefix)];capture=records[0] if len(records)==1 else None
  if capture is None or capture.get('status')!='CAPTURED' or capture.get('sample')!=item['sample'] or capture.get('mode')!='gameplay':reasons.append('Missing completed gameplay viewport image')
  else:
   p=pathlib.Path(capture['path'])
   if p.parent!=frames or not p.is_file() or hashlib.sha256(p.read_bytes()).hexdigest()!=capture['sha256']:reasons.append('Viewport image identity differs')
  result['samples'].append({'sample':item['sample'],'performanceStatus':item['status'],'profileAndImageStatus':'PASS' if not reasons else 'FAIL','reasons':reasons,'capture':capture})
else:result['error']='Vehicle performance receipt missing'
result['status']='PASS' if run.returncode==0 and result.get('innerStatus')=='PASS' and len(result['samples'])==3 and all(item['profileAndImageStatus']=='PASS' for item in result['samples']) else 'FAIL'
result['finished']=datetime.datetime.now(datetime.timezone.utc).isoformat()
with (evidence/(stem+'.json')).open('x') as stream:json.dump(result,stream,ensure_ascii=False,indent=2);stream.write('\n')
print(json.dumps(result,ensure_ascii=False,indent=2),flush=True)
raise SystemExit(0 if result['status']=='PASS' else (run.returncode or 1))
PY
