"""Fail closed on local data / credentials in shipped files. Never print a secret."""
import argparse, re, json
from pathlib import Path

blocked_names={'state.json','reading-settings.json','reading.db','reading.db-wal','reading.db-shm','placement.json','startup-error.log'}
secrets=re.compile(rb'(?:(?<![A-Za-z0-9_])sk-(?:[a-fA-F0-9]{32}(?![A-Za-z0-9])|[A-Za-z0-9_-]{40,})|Bearer\s+eyJ[A-Za-z0-9_.-]{25,})')
protected=re.compile(rb'"Protected(?:Key|ApsKey|SpringerKey|GuardianKey)"\s*:\s*"([^"\r\n]+)"')
def audit(root):
    problems=[];files=0
    for p in root.rglob('*'):
        if not p.is_file():continue
        rel=p.relative_to(root);files+=1
        if p.name.lower() in blocked_names or p.suffix.lower() in {'.db','.log','.pfx','.pem'} or any(x in {'.git','.codex','.tools'} for x in rel.parts):
            problems.append((str(rel),'private data or credential file'));continue
        # Scan code, documents and built managed assemblies as well as config files.
        if p.suffix.lower() not in {'.cs','.csproj','.py','.ps1','.md','.json','.txt','.xml','.config','.exe','.dll','.nsi','.svg','.iss','.yml','.yaml','.env','.toml','.props','.targets'}:continue
        with p.open('rb') as f:
            tail=b''
            while chunk:=f.read(1024*1024):
                data=tail+chunk
                flat=data.replace(b'\x00',b'')
                if secrets.search(flat) or protected.search(flat):problems.append((str(rel),'credential pattern'));break
                tail=data[-4096:]
    return {'filesChecked':files,'problems':problems}
if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('root',type=Path);ap.add_argument('--out',type=Path);args=ap.parse_args()
    if not args.root.is_dir():raise SystemExit('Audit directory does not exist')
    result=audit(args.root)
    if args.out:args.out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Credential audit:',result['filesChecked'],'files;',len(result['problems']),'problems')
    for path,reason in result['problems']:print(path,reason)
    raise SystemExit(bool(result['problems']))
