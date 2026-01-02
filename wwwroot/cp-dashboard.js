(async function(){
  if(!CP.guard()) return;

  const who = document.getElementById('who');
  const hint = document.getElementById('hint');
  const apiState = document.getElementById('apiState');
  const dbName = document.getElementById('dbName');
  const envPill = document.getElementById('envPill');
  const lastSync = document.getElementById('lastSync');

  who.textContent = CP.user() || 'Admin';
  hint.textContent = 'جلسة لوحة التحكم فعّالة';

  // روابط (نجهزها لاحقًا بصفحات فعلية)
  document.getElementById('goTenants').addEventListener('click', e=>{ e.preventDefault(); location.href='/controlpanel-tenants.html'; });
CP.showAlert('صفحة المستأجرين بنسويها بعد الخطوة الجاية ✅'); });
  document.getElementById('goAdmins').addEventListener('click', e=>{ e.preventDefault(); CP.showAlert('صفحة المدراء بنسويها بعد الخطوة الجاية ✅'); });
  document.getElementById('goEmployees').addEventListener('click', e=>{ e.preventDefault(); CP.showAlert('صفحة الموظفين والصلاحيات بنسويها بعد الخطوة الجاية ✅'); });
  document.getElementById('goBackups').addEventListener('click', e=>{ e.preventDefault(); CP.showAlert('صفحة النسخ الاحتياطي بنسويها بعد الخطوة الجاية ✅'); });

  document.getElementById('logout').addEventListener('click', ()=>{
    CP.clear();
    location.href = '/controlpanel-login.html';
  });

  document.getElementById('refresh').addEventListener('click', ()=>loadAll());

  function setKpi(id, val){
    const el = document.getElementById(id);
    if(el) el.textContent = (val === null || val === undefined) ? '-' : String(val);
  }

  function addActivity(type, desc){
    const body = document.getElementById('activityBody');
    if(!body) return;
    if(body.dataset.empty !== '0'){
      body.innerHTML = '';
      body.dataset.empty = '0';
    }
    const tr = document.createElement('tr');
    const now = new Date();
    tr.innerHTML = `<td>${type}</td><td>${desc}</td><td>${now.toLocaleString('ar-SA')}</td>`;
    body.prepend(tr);
    // قصّ القائمة
    while(body.children.length > 6) body.removeChild(body.lastChild);
  }

  async function pingApi(){
    // نجرّب عدة endpoints (حسب اختلاف مشروعك)
    const urls = CP.guessApi();
    for(const u of urls){
      try{
        await CP.apiFetch(u, { method:'GET' });
        apiState.textContent = 'API: OK';
        addActivity('System', 'Ping OK: ' + u);
        return true;
      }catch(_){}
    }
    apiState.textContent = 'API: ?';
    return false;
  }

  async function loadKpis(){
    // نحاول نجيب أرقام من endpoints موجودة عندك غالبًا
    // المنتجات
    try{
      const products = await CP.apiFetch('/api/products', { method:'GET' });
      setKpi('kpiProducts', Array.isArray(products) ? products.length : (products?.items?.length ?? '-'));
      addActivity('Products', 'تم جلب المنتجات');
    }catch(e){
      setKpi('kpiProducts','-');
      addActivity('Products', 'تعذر جلب المنتجات');
    }

    // العملاء
    try{
      const customers = await CP.apiFetch('/api/customers/full', { method:'GET' });
      setKpi('kpiCustomers', Array.isArray(customers) ? customers.length : (customers?.items?.length ?? '-'));
      addActivity('Customers', 'تم جلب العملاء');
    }catch(e){
      setKpi('kpiCustomers','-');
      addActivity('Customers', 'تعذر جلب العملاء');
    }

    // فواتير اليوم (لو endpoint مختلف بنعدل)
    try{
      const today = new Date();
      const yyyy = today.getFullYear();
      const mm = String(today.getMonth()+1).padStart(2,'0');
      const dd = String(today.getDate()).padStart(2,'0');
      const date = `${yyyy}-${mm}-${dd}`;

      // محاولة: report endpoint
      const rep = await CP.apiFetch('/api/cashier/invoices/report?dateFrom='+date+'&dateTo='+date, { method:'GET' });
      // بعض التقارير ترجع {rows:[], totals:{}}
      const count = Array.isArray(rep) ? rep.length : (rep?.rows?.length ?? rep?.items?.length ?? '-');
      setKpi('kpiInvoices', count);
      addActivity('Invoices', 'تم جلب تقرير فواتير اليوم');
    }catch(e){
      setKpi('kpiInvoices','-');
      addActivity('Invoices', 'تعذر جلب فواتير اليوم');
    }
  }

  async function loadAll(){
    CP.hideAlert();
    envPill.textContent = location.hostname.includes('127.0.0.1') || location.hostname.includes('localhost') ? 'LOCAL' : 'HOST';
    dbName.textContent = 'DB: naderposdb';

    const ok = await pingApi();
    if(!ok){
      CP.showAlert('API ما رد على /ping (مو مشكلة كبيرة). إذا كل شي يشتغل تجاهله 😄', 'err');
    }

    await loadKpis();
    lastSync.textContent = 'آخر تحديث: ' + new Date().toLocaleString('ar-SA');
  }

  await loadAll();
})();

