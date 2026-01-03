(function(){
  const pages = [
    { path:'/index',    file:'/index.html',    title:'الرئيسية' },
    { path:'/products', file:'/products.html', title:'المنتجات' },
    { path:'/cashier',  file:'/cashier.html',  title:'الكاشير' },
    { path:'/customers',file:'/customers.html',title:'العملاء/المؤجل' },
    { path:'/invoices', file:'/invoices.html', title:'الفواتير' },
    { path:'/reports',  file:'/reports.html',  title:'التقارير' }
  ];

  window.mountNav = function(){
    const host = document.querySelector('[data-nav]');
    if(!host) return;

    const nav = document.createElement('nav');
    nav.setAttribute('data-app-nav','');
    const wrap = document.createElement('div');
    wrap.className = 'wrap';

    const here = (location.pathname || '/').toLowerCase();
    const hereNoHtml = here.endsWith('.html') ? here.slice(0,-5) : here;

    pages.forEach(p=>{
      const a = document.createElement('a');
      a.href = p.path; // نخليها بدون .html (السيرفر بيرجعها)
      a.textContent = p.title;
      const pLower = p.path.toLowerCase();
      if(hereNoHtml === pLower || here === p.file.toLowerCase()) a.classList.add('active');
      wrap.appendChild(a);
    });

    nav.appendChild(wrap);
    host.replaceWith(nav);
  };

  // لو فتح المستخدم /products.html نخليه “نظيف” على /products (اختياري)
  window.addEventListener('DOMContentLoaded', ()=>{
    try{
      if(location.pathname.toLowerCase().endsWith('.html')){
        const clean = location.pathname.slice(0,-5);
        // استثناء index.html نخليه / أو /index حسب رغبتك
        if(clean.toLowerCase() === '/index'){
          history.replaceState({},'', '/');
        } else {
          history.replaceState({},'', clean);
        }
      }
    }catch{}
  });
})();
