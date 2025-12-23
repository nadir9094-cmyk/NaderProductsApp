$ErrorActionPreference="Stop"

$root = "C:\sami"
$www  = Join-Path $root "wwwroot"
$null = New-Item -ItemType Directory -Force -Path $www
$null = New-Item -ItemType Directory -Force -Path (Join-Path $www "backups")

# backup.html (إنشاء/تحديث مضمون)
$backupHtmlPath = Join-Path $www "backup.html"
$backupHtml = @"
<!doctype html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>النسخ الاحتياطي</title>
  <style>
    body{font-family:Tahoma,Arial;background:#f5f6fa;margin:0;padding:24px}
    .card{max-width:920px;margin:auto;background:#fff;border-radius:14px;padding:18px;box-shadow:0 10px 30px rgba(0,0,0,.08)}
    button{border:0;border-radius:10px;padding:10px 14px;cursor:pointer}
    .primary{background:#2d98da;color:#fff}
    a{color:#2d98da;text-decoration:none}
    .muted{color:#556;font-size:14px}
    table{width:100%;border-collapse:collapse;margin-top:12px}
    th,td{padding:10px;border-bottom:1px solid #eef1f5;text-align:right;font-size:14px}
  </style>
</head>
<body>
  <div class="card">
    <h2>💾 النسخ الاحتياطي للقاعدة</h2>
    <div class="muted" id="status">جاهز.</div>
    <p>
      <button class="primary" onclick="runNow()">📦 إنشاء نسخة الآن</button>
      <button onclick="refresh()">🔄 تحديث</button>
    </p>
    <p class="muted">روابط API: <a href="/api/backup/list">/api/backup/list</a></p>
    <table>
      <thead><tr><th>الملف</th><th>الحجم</th><th>التاريخ</th><th>تحميل</th></tr></thead>
      <tbody id="rows"></tbody>
    </table>
  </div>
<script>
function fmtBytes(b){ if(b==null) return ''; const u=['B','KB','MB','GB']; let i=0; while(b>=1024&&i<u.length-1){b/=1024;i++;} return (i===0?b:b.toFixed(1))+' '+u[i]; }
async function api(url,opt){ const r=await fetch(url,opt); if(!r.ok) throw new Error(await r.text()); return r; }
async function refresh(){
  const st=document.getElementById('status'); st.textContent='⏳ جاري جلب النسخ...';
  try{
    const j=await (await api('/api/backup/list')).json();
    const tbody=document.getElementById('rows'); tbody.innerHTML='';
    (j.files||[]).forEach(f=>{
      const tr=document.createElement('tr');
      tr.innerHTML = `<td>${f.name}</td><td>${fmtBytes(f.size)}</td><td>${f.modifiedLocal}</td><td><a href="/api/backup/download/${encodeURIComponent(f.name)}">تحميل</a></td>`;
      tbody.appendChild(tr);
    });
    st.textContent='✅ تم. عدد النسخ: '+((j.files||[]).length);
  }catch(e){ st.textContent='❌ '+e.message; }
}
async function runNow(){
  const st=document.getElementById('status'); st.textContent='⏳ جاري إنشاء النسخة...';
  try{
    const j=await (await api('/api/backup/run',{method:'POST'})).json();
    st.textContent='✅ تم إنشاء: '+j.file;
    await refresh();
  }catch(e){ st.textContent='❌ '+e.message; }
}
refresh();
</script>
</body>
</html>
"@
Set-Content -Encoding UTF8 -Path $backupHtmlPath -Value $backupHtml

# appsettings.json: نكتب BackupSettings مباشرة كسطر JSON (مضمون 100%)
$cfgPath = Join-Path $root "appsettings.json"
if(-not (Test-Path $cfgPath)){ throw "appsettings.json غير موجود في C:\sami" }

$cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
$cfg | Add-Member -NotePropertyName BackupSettings -NotePropertyValue ([pscustomobject]@{
  Enabled=$true
  TimeLocal="02:00"
  RetainDays=14
  UseDocker=$true
  DockerContainerName="naderpg"
  DatabaseName="naderposdb"
  DbUser="postgres"
  OutputDir="wwwroot/backups"
}) -Force
($cfg | ConvertTo-Json -Depth 50) | Set-Content -Encoding UTF8 -Path $cfgPath

Write-Host "✅ تم ضبط backup.html + BackupSettings فقط (بدون لمس Program.cs)"
Write-Host "الخطوة التالية: بنركّب API داخل Program.cs بس بطريقة استبدال ملف كامل (مضمونة)"
