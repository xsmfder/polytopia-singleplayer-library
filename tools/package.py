"""Build a source-bound release package, excluding all runtime/test/user artifacts."""
from pathlib import Path
import argparse,hashlib,json,zipfile
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write_zip(path,files):
 with zipfile.ZipFile(path,'x',zipfile.ZIP_DEFLATED) as z:
  for name,data in sorted(files.items()):
   info=zipfile.ZipInfo(name,(2026,10,27,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o100644<<16;z.writestr(info,data)
 path.with_name(path.name+'.sha256').write_text(sha(path)+'  '+path.name+'\n',encoding='utf-8')
def main():
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--build',required=True,type=Path);ap.add_argument('--out',required=True,type=Path);args=ap.parse_args()
 build=json.loads(args.build.read_text(encoding='utf-8-sig'));manifest=json.loads((ROOT/'manifest.json').read_text(encoding='utf-8-sig'))
 if not build['ok'] or build['symbols']!=['POLYLIB_RELEASE']:raise ValueError('A successful release build with the test harness excluded is required')
 actual={str(p.relative_to(ROOT)).replace('\\','/'):sha(p) for p in (ROOT/'src').glob('*.cs')}
 if actual!={x['path']:x['sha256'] for x in build['sources']}:raise ValueError('Source drift since compilation')
 assets={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'assets').glob('*.png')}
 if assets!={x['path']:x['sha256'] for x in build.get('assets',[])} or len(assets)!=2:raise ValueError('Icon asset drift since compilation')
 dll=Path(build['candidate'])
 if sha(dll)!=build['dllSha256']:raise ValueError('Binary differs from build')
 for r in build['references']:
  if sha(Path(r['path']))!=r['sha256']:raise ValueError('Compile reference changed')
 if manifest['id']!='single_player_library' or manifest['dependencies'][0]['min']!='2.17.3.16375':raise ValueError('Unexpected manifest')
 out=args.out.resolve();out.mkdir(parents=True,exist_ok=True)
 pkg=out/('single_player_library-'+manifest['version']+'.polymod')
 write_zip(pkg,{'manifest.json':(ROOT/'manifest.json').read_bytes(),'SinglePlayerLibrary.dll':dll.read_bytes(),'LICENSE':(ROOT/'LICENSE').read_bytes()})
 source={}
 for name in ['README.md','README.en.md','CHANGELOG.md','CONTRIBUTING.md','THIRD_PARTY_NOTICES.md','VERIFICATION.md','LICENSE','manifest.json','.gitignore','.gitattributes','.editorconfig']:
  source[name]=(ROOT/name).read_bytes()
 for directory in ['docs','.github']:
  for doc in (ROOT/directory).rglob('*.md'):
   source[doc.relative_to(ROOT).as_posix()]=doc.read_bytes()
 for directory in ['src','tests','tools']:
  for p in (ROOT/directory).glob('*'):
   if p.is_file() and p.suffix in ('.cs','.py','.ps1'):
    source[p.relative_to(ROOT).as_posix()]=p.read_bytes()
 for name in ['refresh.png','folder.png','provenance.json','sources/refresh-blue.png','sources/folder-blue.png']:
  source['assets/'+name]=(ROOT/'assets'/name).read_bytes()
 bundle=out/('single_player_library-'+manifest['version']+'-source.zip');write_zip(bundle,source)
 record={'ok':True,'version':manifest['version'],'package':str(pkg),'sha256':sha(pkg),'dllSha256':sha(dll),'assets':assets,
   'build':str(args.build.resolve()),'sources':actual,'sourceBundle':str(bundle),'sourceBundleSha256':sha(bundle),'testHarnessIncluded':False,'published':False}
 (out/'package.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(record,ensure_ascii=False))
if __name__=='__main__':main()
