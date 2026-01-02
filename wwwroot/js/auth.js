// js/auth.js - FULL COMPAT + fixes 415 + fixes login redirect loop by fetching /api/auth/me
(function(){
  const PERMS = {
    PRODUCTS:1, CASHIER:2, CUSTOMERS:4, SUPPLIERS:8, EXPENSES:16,
    REPORTS:32, SETTINGS:64, BACKUP:128, EMPLOYEES:256, SHIFTS:512
  };

  function _lsGet(k){ try{ return localStorage.getItem(k); }catch(_){ return null; } }
  function _lsSet(k,v){ try{ localStorage.setItem(k,v); }catch(_){} }
  function _lsDel(k){ try{ localStorage.removeItem(k); }catch(_){} }

  function getToken(){
    return String(_lsGet('emp_token') || _lsGet('token') || _lsGet('EMP_TOKEN') || '').trim();
  }

  function getMeSync(){
    try{
      const s = _lsGet('emp_me');
      return s ? JSON.parse(s) : null;
    }catch(_){
      return null;
    }
  }

  function isAdmin(u){
    const name = String(u?.username || u?.userName || u?.UserName || '').toLowerCase();
    return name === 'admin';
  }

  function hasPerm(bit){
    const u = getMeSync();
    if(!u) return false;
    if(isAdmin(u)) return true;
    return (Number(u.permissions||u.Permissions||0) & bit) === bit;
  }

  async function apiFetch(url, options={}){
    options = options || {};
    options.headers = options.headers || {};

    // Auto auth header
    const t = getToken();
    if(t && !options.headers.Authorization && !options.headers.authorization){
      options.headers.Authorization = 'Bearer ' + t;
    }

    // Fix 415: if sending JSON string and Content-Type missing, add it
    const hasCT = !!(options.headers['Content-Type'] || options.headers['content-type']);
    if(!hasCT && typeof options.body === 'string'){
      const s = options.body.trim();
      if((s.startsWith('{') && s.endsWith('}')) || (s.startsWith('[') && s.endsWith(']'))){
        options.headers['Content-Type'] = 'application/json';
      }
    }

    // avoid caching in auth flows
    options.cache = options.cache || 'no-store';

    const res = await fetch(url, options);
    const ct = (res.headers.get('content-type')||'').toLowerCase();
    const body = ct.includes('application/json') ? await res.json() : await res.text();

    if(!res.ok){
      const msg = (typeof body === 'string' && body) ? body : (body?.detail || body?.title || ('HTTP ' + res.status));
      const err = new Error(msg);
      err.status = res.status;
      err.body = body;
      throw err;
    }

    return body;
  }

  // âœ… crucial fix: load me from server when missing
  async function getMe(){
    const cached = getMeSync();
    if(cached) return cached;

    const t = getToken();
    if(!t) throw new Error('NO_TOKEN');

    // Try standard endpoint
    const me = await apiFetch('/api/auth/me', { method:'GET' });
    try{ _lsSet('emp_me', JSON.stringify(me || {})); }catch(_){ }
    return me;
  }

  async function requireAuth({permissionBit=0, redirect='/login.html'} = {}){
    const t = getToken();
    if(!t){ location.href = redirect; return null; }

    let u = getMeSync();
    if(!u){
      try{ u = await getMe(); }
      catch(_){
        // token invalid or endpoint failed -> clear and go login
        try{ _lsDel('emp_token'); _lsDel('token'); _lsDel('EMP_TOKEN'); _lsDel('emp_me'); }catch(__){}
        location.href = redirect;
        return null;
      }
    }

    if(permissionBit && !hasPerm(permissionBit)){
      alert('ًںڑ« ظ„ط§ طھظ…ظ„ظƒ طµظ„ط§ط­ظٹط© ط§ظ„ط¯ط®ظˆظ„ ظ„ظ‡ط°ظ‡ ط§ظ„ط´ط§ط´ط©');
      location.href = '/index.html';
      return null;
    }

    return u;
  }

  function logout(){
    _lsDel('emp_token');
    _lsDel('token');
    _lsDel('EMP_TOKEN');
    _lsDel('emp_me');
    _lsDel('emp_remember');
    location.href = '/login.html';
  }

  function setToken(v){
    v = String(v||'').trim();
    if(v) _lsSet('emp_token', v);
    else _lsDel('emp_token');
    // if token changes, refresh me next time
    _lsDel('emp_me');
  }

  // Expose (backward compatible names)
  window.NaderPerms = PERMS;
  window.NaderAuth = window.NaderAuth || {};
  window.NaderAuth.getToken = getToken;
  window.NaderAuth.token = getToken;
  window.NaderAuth.getMe = getMe;       // async
  window.NaderAuth.me = getMeSync;      // sync cached only (for quick UI)
  window.NaderAuth.getMeSync = getMeSync;
  window.NaderAuth.hasPerm = hasPerm;
  window.NaderAuth.apiFetch = apiFetch;
  window.NaderAuth.requireAuth = requireAuth;
  window.NaderAuth.logout = logout;
  window.NaderAuth.setToken = setToken;
})();

