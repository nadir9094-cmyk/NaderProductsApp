/* Nader Auth Guard (V1) */
(function(){
  const API = (p)=> (p.startsWith("http")?p:(location.origin + p));

  function getToken(){ return localStorage.getItem("emp_token") || ""; }
  function setToken(t){ if(t) localStorage.setItem("emp_token", t); else localStorage.removeItem("emp_token"); }

  async function apiFetch(path, opt={}){
    const headers = Object.assign({ "Content-Type":"application/json" }, opt.headers||{});
    const tk = getToken();
    if(tk) headers["Authorization"] = "Bearer " + tk;
    const res = await fetch(API(path), Object.assign(opt,{headers}));
    const txt = await res.text();
    let data=null; try{ data = txt ? JSON.parse(txt) : null; }catch{ data = txt; }
    if(!res.ok){
      const msg = (typeof data==="string" ? data : (data?.detail || data?.title || "ERROR"));
      const err = new Error(msg);
      err.status = res.status;
      throw err;
    }
    return data;
  }

  // صلاحيات (نفس bits اللي عندنا في السيرفر)
  const PERM = {
    Products: 0,
    CustomersDeferred: 1,
    Suppliers: 2,
    Expenses: 3,
    Cashier: 4,
    SalesInvoices: 5,
    Employees: 6,
    Settings: 7,
    Backup: 8,
    SubscriptionPlan: 9
  };

  function hasPerm(permMask, bitIndex){
    try{
      const m = BigInt(permMask||0);
      const b = 1n << BigInt(bitIndex);
      return (m & b) !== 0n;
    }catch{
      // fallback numeric
      return ((Number(permMask||0) >>> 0) & (1 << bitIndex)) !== 0;
    }
  }

  async function getMe(){
    return await apiFetch("/api/auth/me");
  }

  async function requireAuth(opts={}){
    const { permissionBit=null, redirect="/login.html" } = opts;
    const tk = getToken();
    if(!tk){
      location.href = redirect;
      return null;
    }

    try{
      const me = await getMe();
      // حفظ بيانات بسيطة للعرض
      localStorage.setItem("emp_me", JSON.stringify(me));

      if(permissionBit !== null){
        const ok = hasPerm(me.permissions, permissionBit);
        if(!ok){
          // منع الدخول بلطف
          document.body.innerHTML = `
            <div style="font-family:system-ui;max-width:720px;margin:60px auto;padding:20px">
              <h2 style="margin:0 0 10px 0">🚫 لا تملك صلاحية دخول هذه الصفحة</h2>
              <p style="color:#555;margin:0 0 18px 0">تواصل مع مدير النظام لتفعيل الصلاحية.</p>
              <button id="goHome" style="padding:10px 14px;border:1px solid #ddd;border-radius:10px;background:#fff;cursor:pointer">العودة للرئيسية</button>
              <button id="logout" style="padding:10px 14px;border:1px solid #dc2626;border-radius:10px;background:#dc2626;color:#fff;cursor:pointer;margin-right:8px">تسجيل خروج</button>
            </div>`;
          document.getElementById("goHome").onclick=()=>location.href="/index.html";
          document.getElementById("logout").onclick=async()=>{
            try{ await apiFetch("/api/auth/logout",{method:"POST"}); }catch{}
            setToken("");
            location.href = redirect;
          };
          return null;
        }
      }

      return me;
    }catch(e){
      // توكن منتهي/غير صحيح
      setToken("");
      location.href = redirect;
      return null;
    }
  }

  async function logout(){
    try{ await apiFetch("/api/auth/logout",{method:"POST"}); }catch{}
    setToken("");
  }

  // export
  window.NaderAuth = { apiFetch, getToken, setToken, getMe, requireAuth, logout, PERM };
})();
