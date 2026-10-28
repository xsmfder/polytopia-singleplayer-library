"""Guarded local installation; default is a read-only plan."""
from pathlib import Path
import argparse,json,hashlib,zipfile,subprocess,ctypes,os,time,shutil

def closed():
 from ctypes import wintypes as w
 r=subprocess.run(['powershell','-NoProfile','-Command','Get-Process Polytopia -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id'],capture_output=True,text=True)
 k=ctypes.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[w.DWORD,w.BOOL,w.DWORD];k.OpenProcess.restype=w.HANDLE;k.WaitForSingleObject.argtypes=[w.HANDLE,w.DWORD];k.WaitForSingleObject.restype=w.DWORD;k.CloseHandle.argtypes=[w.HANDLE]
 for raw in r.stdout.split():
  if not raw.isdigit():continue
  h=k.OpenProcess(0x100000,False,int(raw))
  if not h:
   if ctypes.get_last_error()!=87:return False
   continue
  state=k.WaitForSingleObject(h,0);k.CloseHandle(h)
  if state!=0:return False
 return True

def main():
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('operation',choices=['install','disable','enable']);ap.add_argument('--game-dir',required=True,type=Path);ap.add_argument('--package',type=Path);ap.add_argument('--apply',action='store_true');args=ap.parse_args()
 game=args.game_dir.resolve()
 if (game/'version.txt').read_text(encoding='utf-8-sig').strip()!='2.17.3.16375':raise ValueError('Unsupported game version')
 target=game/'Mods/single_player_library.polymod';disabled=target.with_suffix('.polymod.disabled')
 if args.operation=='install':
  if not args.package:raise ValueError('--package required')
  with zipfile.ZipFile(args.package) as z:
   if set(z.namelist())!={'manifest.json','SinglePlayerLibrary.dll','LICENSE'} or z.testzip():raise ValueError('Unexpected package content')
   m=json.loads(z.read('manifest.json'))
   if m['id']!='single_player_library' or not m['client']:raise ValueError('Incorrect package')
  digest=hashlib.sha256(args.package.read_bytes()).hexdigest();sidecar=args.package.with_name(args.package.name+'.sha256')
  if sidecar.read_text().split()[0].lower()!=digest:raise ValueError('Package checksum mismatch')
  if disabled.exists():raise ValueError('Disabled copy exists; enable or archive it before replacing')
 record={'operation':args.operation,'game':str(game),'target':str(target),'apply':args.apply}
 print(json.dumps(record,ensure_ascii=False,indent=2))
 if not args.apply:return
 if not closed():raise RuntimeError('Close the game first; no process was stopped')
 if args.operation=='install':
  backup=Path(__file__).resolve().parents[1]/'evidence'/('install-'+str(time.time_ns()));backup.mkdir(parents=True)
  if target.exists():shutil.copy2(target,backup/'installed-before.polymod')
  target.parent.mkdir(parents=True,exist_ok=True);temp=target.with_suffix('.polymod.installing');shutil.copyfile(args.package,temp);os.replace(temp,target)
  record.update(sha256=hashlib.sha256(target.read_bytes()).hexdigest(),backup=str(backup))
  (backup/'receipt.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
 elif args.operation=='disable':
  if disabled.exists():raise ValueError('Disabled copy already exists')
  target.rename(disabled)
 else:
  if target.exists():raise ValueError('Active copy already exists')
  disabled.rename(target)
 print('Done; user saves and replays were preserved.')
if __name__=='__main__':main()
