import subprocess
from pathlib import Path
p=Path(r'D:/Unity Projects/Escape');out=p/'Reports/PASS19-player';out.mkdir(exist_ok=True)
si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=0
for kind,flag,fps in [('bugs','-escapeBugfixAudit',30),('bugs','-escapeBugfixAudit',60),('bugs','-escapeBugfixAudit',144),('rescue','-escapeRescueAudit',60),('zone','-escapeZoneAudit',60)]:
 report=out/f'{kind}-{fps}.txt';log=p/f'Logs/pass19-{kind}-{fps}.log'
 proc=subprocess.Popen([str(p/'Builds/Windows-PASS19/Escape-PASS19.exe'),'-screen-fullscreen','0','-screen-width','640','-screen-height','360','-diag-job-temp-memory-leak-validation',flag,str(report),'-auditFps',str(fps),'-logFile',str(log)],cwd=p)
 try:code=proc.wait(timeout=50)
 except subprocess.TimeoutExpired:proc.terminate();proc.wait(timeout=10);raise SystemExit('Own audit timeout')
 print(kind,fps,'exit',code,report.read_text() if report.exists() else 'No report',flush=True)
 assert code==0,(kind,fps)
 assert report.read_text().startswith('PASS:')
 text=log.read_text('utf8',errors='replace')
 assert not any(t in text for t in ['ThreadsafeLinearAllocator','JobTempAlloc','NullReferenceException','error CS','curves must all','MissingReferenceException']),log
print('All five PASS19 player checks and diagnostic memory logs clean',flush=True)
