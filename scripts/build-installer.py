"""Build a self-contained per-user installer from audited release files (NSIS 3.12)."""
from pathlib import Path
import argparse, subprocess, importlib.util, json, hashlib

root=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('audit_release',root/'scripts/audit-release.py')
audit_module=importlib.util.module_from_spec(spec);spec.loader.exec_module(audit_module)
ap=argparse.ArgumentParser();ap.add_argument('--version',default='0.5.6');ap.add_argument('--compiler',type=Path,default=root/'.tools/nsis-3.12/makensis.exe');ap.add_argument('--qa',action='store_true');args=ap.parse_args()
release=root/f'artifacts/QuietDesk-v{args.version}-win-x64'
if not (release/'QuietDesk.exe').is_file():raise SystemExit('Publish and package release first.')
if not args.compiler.is_file():raise SystemExit('NSIS 3.12 portable compiler required; see docs/INSTALLER.md.')
audit=audit_module.audit(release)
if audit['problems']:raise SystemExit('Release contains local data or possible credentials; run audit-release.py for paths.')
work=root/'artifacts'/('installer-qa' if args.qa else 'installer');work.mkdir(parents=True,exist_ok=True)
install=[];uninstall=[];manifest=[];dirs=set()
for p in sorted(release.rglob('*')):
    if not p.is_file():continue
    rel=p.relative_to(release)
    if any(c in str(rel) for c in ('$','"','\n','\r')):raise SystemExit('Unsupported installer file name')
    directory=str(rel.parent).replace('/','\\')
    install.extend([f'SetOutPath "$INSTDIR\\{directory}"',f'File "{p}"'])
    uninstall.append(f'Delete "$INSTDIR\\{rel}"')
    for d in rel.parents:
        if str(d)!='.':dirs.add(str(d))
    manifest.append({'path':str(rel).replace('\\','/'),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
for d in sorted(dirs,key=lambda x:len(Path(x).parts),reverse=True):uninstall.append(f'RMDir "$INSTDIR\\{d}"')
for name,lines in [('install.nsh',install),('uninstall.nsh',uninstall)]: (work/name).write_text('\n'.join(lines),encoding='utf-8-sig')
(work/'payload-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(work/'credential-audit.json').write_text(json.dumps(audit,indent=2),encoding='utf-8')
output=root/f'artifacts/QuietDesk-{args.version}-Setup{ "-QA" if args.qa else ""}.exe'
command=[str(args.compiler.resolve()),'/V2',f'/DVERSION={args.version}',f'/DRELEASE={release}',f'/DOUTPUT={output}',f'/DINSTALL_MANIFEST={work / "install.nsh"}',f'/DUNINSTALL_MANIFEST={work / "uninstall.nsh"}']
if args.qa:command+=['/DQA','/DPRODUCT_KEY=QuietDesk-Installer-QA']
command.append(str(root/'scripts/installer.nsi'))
subprocess.run(command,check=True,cwd=root,creationflags=subprocess.CREATE_NO_WINDOW|subprocess.BELOW_NORMAL_PRIORITY_CLASS)
print('Installer:',output)
print('SHA256:',hashlib.sha256(output.read_bytes()).hexdigest())
