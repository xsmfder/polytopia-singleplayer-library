import sys,json,time,os,uuid
from pathlib import Path
BASE=Path(__file__).resolve().parents[1]
root=BASE/'evidence'/sys.argv[1]
def request(op,**args):
 q={'id':uuid.uuid4().hex,'op':op,**args};tmp=root/'request.next';tmp.write_text(json.dumps(q),encoding='utf-8')
 for n in range(100):
  try:os.replace(tmp,root/'request.json');break
  except PermissionError:time.sleep(.01)
 else:raise RuntimeError('publish timeout')
 deadline=time.monotonic()+20
 while time.monotonic()<deadline:
  try:
   r=json.loads((root/'reply.json').read_text(encoding='utf-8-sig'))
   if r['id']==q['id']:
    (root/('reply-'+q['id']+'.json')).write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
    if r.get('error'):raise RuntimeError(r['error'])
    return r
  except (OSError,ValueError):pass
  time.sleep(.05)
 raise TimeoutError(op)
def show(r):print(json.dumps({k:r.get(k) for k in ('screen','pending','info','lastError')},ensure_ascii=False),flush=True)
if __name__=='__main__':
 cmd=sys.argv[2]
 if cmd=='create':
  for op,kw in [('new_game',{}),('mode',{'mode':sys.argv[3] if len(sys.argv)>3 else 'Perfection'}),('tribe',{}),('confirm_tribe',{}),('start',{})]:
   show(request(op,**kw));time.sleep(.5)
  time.sleep(1);show(request('status'))
 elif cmd=='finish':
  for n in range(300):
   r=request('status');s=r.get('info')
   if not s:show(r);raise RuntimeError('No session')
   if r.get('pending') or s['state']=='Ended':show(r);break
   if s.get('popup') and 'Save Replay' not in s['popup']:
    show(request('dismiss_popup'));time.sleep(.2)
   elif s['ready'] and not s['actions'] and not s.get('paused') and s['stack']==s['command'] and s['player']==1:
    show(request('end_turn'));time.sleep(.3)
   else:time.sleep(.15)
  else:raise TimeoutError('finish')
 else:show(request(cmd,**(json.loads(sys.argv[3]) if len(sys.argv)>3 else {})))
