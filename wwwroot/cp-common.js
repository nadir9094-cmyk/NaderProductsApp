const PING_URL = "/api/cp/ping";
(function(){
  function qs(k){ return new URLSearchParams(location.search).get(k); }

  window.CP = {
    token: () => localStorage.getItem('cp_token') || '',
    user: () => localStorage.getItem('cp_user') || '',
    setToken: (t)=> localStorage.setItem('cp_token', t || ''),
    setUser:  (u)=> localStorage.setItem('cp_user', u || ''),
    clear: ()=>{ localStorage.removeItem('cp_token'); localStorage.removeItem('cp_user'); },
    showAlert: (msg, kind)=>{
      const el = document.getElementById('alert');
      if(!el) return;
      el.hidden = false;
      el.className = 'cp-alert ' + (kind||'');
      el.textContent = msg;
    },
    hideAlert: ()=>{
      const el = document.getElementById('alert');
      if(!el) return;
      el.hidden = true;
      el.textContent = '';
      el.className = 'cp-alert';
    },
    apiFetch: async (url, opt)=>{
      const token = CP.token();
      const headers = { 'Content-Type':'application/json', ...(opt && opt.headers ? opt.headers : {}) };
      if(token) headers['Authorization'] = 'Bearer ' + token;

      const res = await fetch(url, { ...opt, headers });
      const text = await res.text();
      let data = null;
      try { data = text ? JSON.parse(text) : null; } catch { data = text; }

      if(!res.ok){
        const msg = (data && data.detail) || (data && data.message) || text || ('HTTP ' + res.status);
        throw new Error(msg);
      }
      return data;
    },
    guard: ()=>{
      // حماية بسيطة: لازم توكن
      const t = CP.token();
      if(!t){
        location.href = '/controlpanel-login.html?next=' + encodeURIComponent(location.pathname);
        return false;
      }
      return true;
    },
    guessApi: ()=>{
      // مسارات محتملة لاختبار API بدون ما نخرب شيء
      return [
        '/api/cp/ping',
        '/api/cp/ping',
        '/api/cp/ping'
      ];
    }
  };

  // دعم next= بعد تسجيل الدخول
  window.CP_NEXT = qs('next');
})();


