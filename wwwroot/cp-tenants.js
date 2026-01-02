(async function(){
  const elTb = document.getElementById('tb');
  const elCnt = document.getElementById('cnt');
  const elMsg = document.getElementById('msg');
  const elQ = document.getElementById('q');
  const btnReload = document.getElementById('btnReload');

  function fmtMoney(x){
    const n = Number(x ?? 0);
    return n.toLocaleString('ar-SA', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }
  function daysLeft(expiresUtc){
    if(!expiresUtc) return { text:'—', days:null };
    const d = new Date(expiresUtc);
    const now = new Date();
    const diff = Math.ceil((d - now) / (1000*60*60*24));
    return { text: diff >= 0 ? (diff + ' يوم') : ('منتهي منذ ' + Math.abs(diff) + ' يوم'), days: diff };
  }
  function badge(status){
    const s = (status||'').trim();
    if(s.includes('مدفوع') && !s.includes('جزئ')) return `<span class="badge b-ok">${s}</span>`;
    if(s.includes('جزئ')) return `<span class="badge b-warn">${s}</span>`;
    return `<span class="badge b-bad">${s||'—'}</span>`;
  }

  async function load(){
    elMsg.textContent = '...جاري التحميل';
    let list = [];
    try{
      // expects /api/cp/tenants/list
      list = await CP.apiFetch('/api/cp/tenants/list', { method:'GET' });
      if(!Array.isArray(list)) list = list.items || list.data || [];
    }catch(e){
      elMsg.textContent = 'فشل تحميل المستأجرين (شوف Console)';
      console.error(e);
      return;
    }

    const q = (elQ.value||'').trim().toLowerCase();
    if(q){
      list = list.filter(x=>{
        const blob = [
          x.tenantCode, x.ownerName, x.storeName, x.vatNumber, x.planName, x.username
        ].filter(Boolean).join(' ').toLowerCase();
        return blob.includes(q);
      });
    }

    elCnt.textContent = String(list.length);

    elTb.innerHTML = list.map((x,i)=>{
      const exp = x.expiresAtUtc || x.expiresAt || x.expireAtUtc || x.expireAt;
      const left = daysLeft(exp);
      const due  = x.amountDue ?? x.amount ?? 0;
      const paid = x.amountPaid ?? 0;
      const st   = x.paymentStatus || ((paid >= due && due>0) ? 'مدفوع' : (paid>0 ? 'مدفوع جزئياً' : 'غير مدفوع'));

      return `
        <tr>
          <td>${i+1}</td>
          <td><b>${x.tenantCode ?? x.code ?? '—'}</b></td>
          <td>${x.ownerName ?? x.owner ?? '—'}</td>
          <td>${x.storeName ?? x.shopName ?? '—'}</td>
          <td>${x.vatNumber ?? x.vat ?? '—'}</td>
          <td>${x.planName ?? x.plan ?? '—'}</td>
          <td>${exp ? new Date(exp).toLocaleDateString('ar-SA') : '—'}</td>
          <td>${left.text}</td>
          <td>${fmtMoney(due)}</td>
          <td>${fmtMoney(paid)}</td>
          <td>${badge(st)}</td>
          <td>
            <div class="row-actions">
              <button onclick="window.CP_TENANTS.renew(${x.id})">تجديد</button>
              <button onclick="window.CP_TENANTS.cancel(${x.id})">إلغاء</button>
            </div>
          </td>
        </tr>`;
    }).join('');

    elMsg.textContent = '';
  }

  window.CP_TENANTS = {
    async renew(id){
      const months = Number(prompt('كم شهر تمديد؟', '1') || '1');
      try{
        await CP.apiFetch(`/api/cp/tenants/${id}/renew`, { method:'PUT', body: JSON.stringify({ months }) });
        CP.showAlert('تم إرسال طلب التجديد ✅');
        load();
      }catch(e){
        console.error(e);
        CP.showAlert('فشل التجديد (قد يكون الـ API يتوقع شكل Body مختلف).', 'err');
      }
    },
    async cancel(id){
      if(!confirm('متأكد تلغي اشتراك هذا المستأجر؟')) return;
      try{
        await CP.apiFetch(`/api/cp/tenants/${id}/cancel`, { method:'PUT', body: '{}' });
        CP.showAlert('تم إرسال طلب الإلغاء ✅');
        load();
      }catch(e){
        console.error(e);
        CP.showAlert('فشل الإلغاء.', 'err');
      }
    }
  };

  btnReload.addEventListener('click', load);
  elQ.addEventListener('input', ()=>{ clearTimeout(window.__qT); window.__qT=setTimeout(load, 250); });

  await load();
})();