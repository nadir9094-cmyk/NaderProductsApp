const LOGIN_URL = "/api/cp/login";
(function(){
  const $ = (id)=>document.getElementById(id);
  const alertBox = $('alert');
  const u = $('u');
  const p = $('p');
  const btn = $('loginBtn');
  const toggle = $('toggle');

  function showAlert(msg, kind){
    alertBox.hidden = false;
    alertBox.className = 'cp-alert ' + (kind||'');
    alertBox.textContent = msg;
  }
  function hideAlert(){
    alertBox.hidden = true;
    alertBox.textContent = '';
    alertBox.className = 'cp-alert';
  }

  toggle?.addEventListener('click', ()=>{
    p.type = (p.type === 'Password') ? 'text' : 'Password';
  });

  async function apiFetch(url, opt){
    const res = await fetch(url, {
      headers: { 'Content-Type':'application/json' },
      ...opt
    });
    const text = await res.text();
    let data = null;
    try{ data = text ? JSON.parse(text) : null; } catch { data = text; }
    if(!res.ok) throw new Error((data && data.detail) || (data && data.message) || text || ('HTTP '+res.status));
    return data;
  }

  btn?.addEventListener('click', async ()=>{
    hideAlert();
    const Username = (u.value||'').trim();
    const Password = (p.value||'').trim();
    if(!Username || !Password){
      showAlert('أدخل اسم المستخدم وكلمة المرور.', 'err');
      return;
    }
    btn.disabled = true;
    btn.textContent = 'جارٍ التحقق...';

    try{
      // جرّب أكثر من مسار لأن أسماء APIs عندك كانت تتغير خلال التطوير
      const payload = { Username, Password };
      const candidates = [
        '/api/cp/login',
        '/api/cp/login',
        '/api/cp/login',
        '/api/cp/login'
      ];

      let ok = null, lastErr = null;
      for(const url of candidates){
        try{
          ok = await apiFetch(url, { method:'POST', body: JSON.stringify(payload) });
          if(ok) break;
        }catch(e){ lastErr = e; }
      }

      if(!ok){
        throw lastErr || new Error('تعذر تسجيل الدخول: تحقق من مسار API');
      }

      // خزّن التوكن إذا رجّعه الـ API
      const token = ok.token || ok.accessToken || ok.jwt || null;
      if(token){
        localStorage.setItem('cp_token', token);
      }
      localStorage.setItem('cp_user', Username);

      showAlert('تم تسجيل الدخول بنجاح ✅', 'ok');

      // تحويل للوحة (نعملها بعدين)
      setTimeout(()=>{ window.location.href = '/controlpanel.html'; }, 650);

    }catch(e){
      showAlert('فشل تسجيل الدخول: ' + e.message, 'err');
    }finally{
      btn.disabled = false;
      btn.textContent = 'دخول';
    }
  });

})();




