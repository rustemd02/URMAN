# Post-hoc preserved copy of the exact heredoc executed via Core exec_command.
# Saved after completion on 2026-09-17; this file did not exist at execution time.
# Historical replay source only: exclusive evidence directories already exist.
python3 - <<'PY'
import pathlib,subprocess,os,sys,json,time,datetime,hashlib,signal,fcntl,runpy
root=pathlib.Path.cwd();candidate=root/'build/native_20260917_release01_candidate';evidence=root/'docs/production/act1_takeover_evidence_2026-09-16';output=evidence/'release01-performance-base-01'
output.mkdir(exist_ok=False)
frames=output/'frames';frames.mkdir()
samples=['village_day@arrival','house_old_pc@entry','fap_clinic@waiting_room','kara_urman_night@village_path','village_day@school_classroom','village_day@council_hall','village_day@mosque_hall']
def identity(label):
 p=output/(label+'.json')
 with (output/(label+'.log')).open('x') as log:r=subprocess.run([sys.executable,'eng/act1_candidate_identity.py','--candidate',str(candidate),'--output',str(p)],stdout=log,stderr=subprocess.STDOUT)
 assert r.returncode==0, 'Strict candidate identity failed: '+label
 data=json.loads(p.read_text())
 assert data['comparison']['status']=='matches-recorded-inputs' and data['candidate']['buildProvenance']['producer']['mode']=='release'
 return data
contract=runpy.run_path(str(root/'eng/protected_run.py'),run_name='urman_guard_contract');userdata=contract['default_userdata']().absolute();lock=userdata.parent/('.'+userdata.name+'.protected-run.lock')
guard=output/'guard-clean.py';guard.write_text("import os,sys\nos.execv(sys.executable,[sys.executable,"+repr(str(root/'eng/protected_run.py'))+",'--clean',*sys.argv[1:]])\n");guard.chmod(0o700)
env=os.environ.copy();env.update(URMAN_ACT1_PACKAGE_BINARY=str(candidate/'launch/URMAN.app/Contents/MacOS/URMAN'),URMAN_ACT1_PACKAGE_ZIP='',URMAN_ACT1_HEADLESS='0',URMAN_GUARD=str(guard),URMAN_PERF_CAPTURE_DIR=str(frames))
receipt={'started':datetime.datetime.now(datetime.timezone.utc).isoformat(),'candidate':str(candidate),'status':'INVALID','samples':[],'criteria':{'warmupSeconds':12,'measurementSeconds':60,'window':'1920x1080','preset':'medium','scale':'0.90','msaa':'Msaa2X','vsync':'Disabled','minimumAverageFps':58,'maximumP95Ms':18,'maximumP99Ms':25,'maximumLongFrameFraction':.005,'maximumFrameMs':100,'minimumFocusFraction':.95,'maximumObscuredFrames':0},'limits':['Stationary native Release performance; no human traversal/duration/audio acceptance.','Full precision runtime status is authoritative; printed numeric values are rounded.','Settings fields and engine monitors are final observations; engine monitors are not interval averages.']}
child=None;current_log=None;pending=False;interrupted=False
def stop_group():
 if child is not None:
  try:os.killpg(child.pid,signal.SIGTERM)
  except ProcessLookupError:pass
def restored():
 if current_log is None or 'userdata guard: original files and permissions restored and verified' not in current_log.read_text():return False
 try:
  with lock.open('r') as stream:
   fcntl.flock(stream,fcntl.LOCK_EX|fcntl.LOCK_NB)
   try:return not stream.read().strip()
   finally:fcntl.flock(stream,fcntl.LOCK_UN)
 except (FileNotFoundError,BlockingIOError):return False
def await_restore():
 deadline=time.monotonic()+20
 while time.monotonic()<deadline:
  if (child is None or child.poll() is not None) and restored():return True
  time.sleep(.05)
 return False
def interrupt(signum,frame):
 global interrupted
 interrupted=True;stop_group()
for sig in (signal.SIGINT,signal.SIGTERM,signal.SIGHUP):signal.signal(sig,interrupt)
exit_code=2
try:
 before=identity('candidate-before')
 for index,sample in enumerate(samples,1):
  if interrupted:raise RuntimeError('Interrupted before next sample')
  label=f'{index:02d}-'+sample.replace('@','-');current_log=output/(label+'.log')
  args=['sh','eng/benchmark-act1-demo-package.sh','--resolution','1920x1080','--urman-perf-probe-mode=gameplay','--urman-perf-sample='+sample,'--urman-perf-warmup-seconds=12','--urman-perf-duration-seconds=60','--urman-perf-request-focus','--urman-perf-no-vsync']
  started=time.monotonic();timestamp=datetime.datetime.now(datetime.timezone.utc).isoformat();print('release-base-performance: starting '+sample,flush=True)
  with current_log.open('x') as log:
   child=subprocess.Popen(args,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True);pending=True
   try:code=child.wait(timeout=240)
   except subprocess.TimeoutExpired:
    stop_group();pending=not await_restore();raise RuntimeError(sample+' timed out; guard restored='+str(not pending))
   pending=not await_restore()
   if pending:raise RuntimeError(sample+' guard restoration unconfirmed')
  lines=current_log.read_text().splitlines();prefix='act1-demo-package-performance: ';records=[line for line in lines if line.startswith(prefix)]
  tokens=dict(item.split('=',1) for item in records[0][len(prefix):].split() if '=' in item) if len(records)==1 else {}
  lp='act1-facility-performance-location: ';locations=[json.loads(line[len(lp):]) for line in lines if line.startswith(lp)]
  def number(key):return float(tokens[key].removesuffix('ms'))
  reasons=[]
  expected={'status':'PASS','mode':'gameplay','sample':sample,'window_fps_valid':'true','window':'1920x1080','preset':'medium','scale':'0.90','msaa':'Msaa2X','vsync':'Disabled'}
  for key,value in expected.items():
   if tokens.get(key)!=value:reasons.append(key+' expected '+value+'; actual '+str(tokens.get(key)))
  try:
   count=int(tokens['sample_count']);focus=int(tokens['focused_sample_count'])/count
   tests={'warmup':number('warmup_seconds')>=12,'duration':number('sample_seconds')>=60,'fps':number('fps')>=58,'p95':number('p95')<=18,'p99':number('p99')<=25,'max':number('max')<=100,'longFrames':number('long_frame_fraction')<=.005,'focus':focus>=.95,'overlay':int(tokens['obscured_sample_count'])==0}
   reasons.extend(k for k,v in tests.items() if not v)
  except (KeyError,ValueError,ZeroDivisionError) as error:reasons.append('Incomplete numeric metrics: '+str(error));focus=None
  location=locations[0] if len(locations)==1 else None
  cp='act1-performance-frame: ';captures=[json.loads(line[len(cp):]) for line in lines if line.startswith(cp)]
  capture=captures[0] if len(captures)==1 else None
  if capture is None or capture.get('status')!='CAPTURED' or capture.get('sample')!=sample or capture.get('mode')!='gameplay':reasons.append('Completed gameplay viewport capture unavailable')
  else:
   image_path=pathlib.Path(capture['path'])
   if image_path.parent!=frames or not image_path.is_file() or hashlib.sha256(image_path.read_bytes()).hexdigest()!=capture['sha256']:reasons.append('Viewport PNG identity differs')
  if index>=5:
   if location is None:reasons.append('Expected exactly one location receipt')
   else:
    required=location.get('setupValid') is True and location.get('locationValid') is True and not location.get('failure') and location.get('poseWrites')==1 and location.get('stablePhysicsFrames',0)>=6 and location.get('actualFloor')==location.get('expectedFloor') and bool(location.get('actualFloor')) and location.get('warmupSeconds')==12 and location.get('measurementSeconds')==60 and location.get('questWrites')==0 and location.get('traversalProof') is False
    if not required:reasons.append('Facility location validation')
  else:
   if not any(line.startswith('zone-loaded: '+sample+' ') for line in lines):reasons.append('Missing requested ordinary zone load')
  after=identity(label+'-identity-after');unchanged=before['candidate']==after['candidate']
  if not unchanged:reasons.append('Candidate changed')
  if code!=0:reasons.append('Process exit '+str(code))
  if interrupted:reasons.append('Interrupted')
  row={'sample':sample,'started':timestamp,'seconds':round(time.monotonic()-started,3),'exitCode':code,'args':args,'log':current_log.name,'logSha256':hashlib.sha256(current_log.read_bytes()).hexdigest(),'status':'PASS' if not reasons else 'FAIL','reasons':reasons,'guardRestored':not pending,'candidateUnchanged':unchanged,'rawMetricLines':records,'metrics':tokens,'exactFocusFraction':focus,'location':location,'capture':capture,'stalls':[l for l in lines if l.startswith('act1-perf-stall:')]}
  receipt['samples'].append(row);print('release-base-performance: '+sample+' '+row['status']+' '+json.dumps(reasons),flush=True)
  child=None
  if not unchanged:raise RuntimeError('Candidate identity changed; no subsequent sample')
 receipt['status']='PASS' if not interrupted and len(receipt['samples'])==7 and all(r['status']=='PASS' for r in receipt['samples']) else 'FAIL';exit_code=0 if receipt['status']=='PASS' else 1
except Exception as error:receipt['error']=str(error);print(str(error),flush=True)
finally:
 if pending:
  stop_group();pending=not await_restore()
  receipt['guardRestorationConfirmed']=not pending
  if pending:receipt['status']='INVALID';exit_code=2
 receipt['finished']=datetime.datetime.now(datetime.timezone.utc).isoformat()
 with (output/'receipt.json').open('x') as stream:json.dump(receipt,stream,ensure_ascii=False,indent=2);stream.write('\n')
 print(json.dumps({'status':receipt['status'],'sampleCount':len(receipt['samples']),'receipt':str(output/'receipt.json')}),flush=True)
raise SystemExit(exit_code)
PY
