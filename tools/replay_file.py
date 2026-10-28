"""Inspect a .polysave/.polyreplay, or restore a selected save to the vanilla slot.
Default restore is a read-only plan. --apply requires a closed game and keeps backups.
"""
from pathlib import Path
import argparse,hashlib,json,struct,os,shutil,time,ctypes,subprocess
MAGIC=b'POLYRPL1';VERSION='2.17.3.16375';MAX=64*1024*1024

def read(path):
 p=Path(path)
 if p.stat().st_size>MAX+65584:raise ValueError('File too large')
 b=p.read_bytes()
 if len(b)<50 or b[:8]!=MAGIC or hashlib.sha256(b[:-32]).digest()!=b[-32:]:raise ValueError('Invalid file format or checksum')
 ml,pl=struct.unpack('<ii',b[8:16])
 if not 0<ml<=65536 or not 0<pl<=MAX or 48+ml+pl!=len(b):raise ValueError('Invalid lengths')
 m=json.loads(b[16:16+ml]);import uuid;uuid.UUID(m['Id'])
 if m['Format']!=1 or m['GameVersion']!=VERSION or m['Mode'] not in ('Domination','Perfection','Infinity'):raise ValueError('Unsupported version or game mode')
 return m,b[16+ml:-32]
def alive():
 from ctypes import wintypes as w
 k=ctypes.WinDLL('kernel32',use_last_error=True)
 k.OpenProcess.argtypes=[w.DWORD,w.BOOL,w.DWORD];k.OpenProcess.restype=w.HANDLE
 k.WaitForSingleObject.argtypes=[w.HANDLE,w.DWORD];k.WaitForSingleObject.restype=w.DWORD;k.CloseHandle.argtypes=[w.HANDLE]
 r=subprocess.run(['powershell','-NoProfile','-Command','Get-Process Polytopia -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id'],capture_output=True,text=True)
 for raw in r.stdout.split():
  if not raw.isdigit():continue
  h=k.OpenProcess(0x100000,False,int(raw))
  if not h:
   if ctypes.get_last_error()!=87:return True
   continue
  state=k.WaitForSingleObject(h,0);k.CloseHandle(h)
  if state!=0:return True
 return False

def main():
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('file',type=Path);ap.add_argument('--restore-profile',type=Path);ap.add_argument('--apply',action='store_true');args=ap.parse_args()
 m,payload=read(args.file)
 if not args.restore_profile:print(json.dumps(m,ensure_ascii=False,indent=2));return
 if args.file.suffix!='.polysave' or m['Finished']:raise ValueError('Select an unfinished .polysave file')
 if m.get('NativeEncoding')!='local-client':raise ValueError('This development save uses replay-wrapper encoding; open and save it once in the current mod before vanilla restoration')
 root=args.restore_profile.resolve();native=(root/'Data/Singleplayer').resolve()
 if not native.is_relative_to(root) or not root.is_dir():raise ValueError('Invalid profile path')
 existing=list(native.glob('*.state'));target=native/(m['Id']+'.state')
 print(json.dumps({'operation':'restore','profile':str(root),'backupFiles':[str(p) for p in existing],'target':str(target),'apply':args.apply},ensure_ascii=False,indent=2))
 if not args.apply:return
 if alive():raise RuntimeError('Close the game before restoring')
 backup=(root/'SinglePlayerLibrary/Backups'/('vanilla-restore-'+str(time.time_ns()))).resolve()
 if not backup.is_relative_to(root):raise ValueError('Backup path escapes profile')
 backup.mkdir(parents=True)
 native.mkdir(parents=True,exist_ok=True)
 # All resolved move targets are within the explicitly chosen profile.
 for p in existing:
  if not p.resolve().is_relative_to(native):raise ValueError('Save path escapes native directory')
  shutil.copy2(p,backup/p.name)
  if hashlib.sha256(p.read_bytes()).digest()!=hashlib.sha256((backup/p.name).read_bytes()).digest():raise IOError('Backup checksum mismatch')
 temp=native/(m['Id']+'.restore-tmp');temp.write_bytes(payload)
 for p in existing:p.replace(backup/(p.name+'.original'))
 os.replace(temp,target)
 (backup/'restore.json').write_text(json.dumps({'selected':str(args.file.resolve()),'target':str(target),'sha256':hashlib.sha256(payload).hexdigest()},indent=2),encoding='utf-8')
 print('Restored; original native files retained in',backup)
if __name__=='__main__':main()
