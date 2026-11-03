(() => {
  'use strict';
  const widgets=[...document.querySelectorAll('[data-size-advisor]')];if(!widgets.length)return;
  const guidePromise=fetch('/data/size-guide.json',{credentials:'same-origin'}).then(r=>{if(!r.ok)throw new Error();return r.json();});
  widgets.forEach(widget=>{
    const $=selector=>widget.querySelector(selector),height=$('[data-height]'),weight=$('[data-weight]'),fit=$('[data-fit]'),result=$('[data-size-result]'),apply=$('[data-apply-size]'),kind=widget.dataset.sizeKind;
    let choice=null;
    const selects=()=>[...document.querySelectorAll(widget.dataset.sizeTarget)].filter(el=>!el.disabled&&!el.closest('[hidden]'));
    function sizeFor(target){
      if(!choice||choice.error)return '';
      const type=kind==='outfit'?(target.name==='shirtSize'?'shirt':target.name==='pantsSize'?'pants':'set'):kind;
      return type==='set'?(choice.shirt?.size&&choice.shirt.size===choice.pants?.size?choice.shirt.size:''):choice[type]?.size||'';
    }
    function availability(){
      const targets=selects(),supported=targets.filter(el=>sizeFor(el)&&[...el.options].some(o=>o.value===sizeFor(el)&&!o.disabled));
      apply.disabled=!choice||!!choice.error||!targets.length||supported.length!==targets.length;
      $('[data-size-availability]').textContent=!choice||choice.error?'':!targets.length?'Chọn sản phẩm để áp dụng size gợi ý.':targets.some(el=>(kind==='set'||el.name==='setSize')&&!sizeFor(el))?'Bộ vest dùng một size chung nhưng cỡ áo và quần dự kiến khác nhau hoặc chưa đủ thông tin. Hãy đối chiếu số đo bộ vest; bạn có thể chọn mua áo và quần riêng.':supported.length!==targets.length?'Cỡ gợi ý chưa có ở một số món đang chọn hoặc nằm ngoài bảng. Hệ thống không tự đổi sang cỡ khác.':'Có thể áp dụng cỡ gợi ý cho sản phẩm đang chọn. Bạn vẫn có thể điều chỉnh từng món.';
    }
    function invalidate(){choice=null;result.hidden=true;apply.disabled=true;}
    [height,weight,fit].forEach(input=>input.addEventListener('input',invalidate));
    $('[data-recommend]').addEventListener('click',async()=>{
      result.hidden=false;
      try{
        const guide=await guidePromise;choice=window.FashionSizeRules.recommend(height.valueAsNumber,weight.valueAsNumber,fit.value,guide);
        const types=kind==='shirt'||kind==='pants'?[kind]:['shirt','pants'],names={shirt:'Áo',pants:'Quần'};
        const valid=types.filter(type=>choice[type]?.size),labels=valid.map(type=>`${names[type]} ${choice[type].size}`);
        $('[data-size-badge]').textContent=types.length===1?(choice[types[0]]?.size||'—'):valid.map(type=>`${names[type]} ${choice[type].size}`).join(' / ')||'—';
        $('[data-size-title]').textContent=labels.length?labels.join(' · '):'Cần kiểm tra lại size';
        const errors=types.map(type=>choice[type]?.error).filter(Boolean);
        $('[data-size-reason]').textContent=choice.error||[choice.reason,...errors].filter(Boolean).join(' ');
        result.classList.toggle('has-error',!!choice.error||errors.length>0);availability();
      }catch{choice=null;$('[data-size-badge]').textContent='—';$('[data-size-title]').textContent='Chưa tải được bảng size';$('[data-size-reason]').textContent='Tải lại trang rồi thử lại.';availability();}
    });
    apply.addEventListener('click',()=>{availability();if(apply.disabled)return;const applied=[];for(const select of selects()){select.value=sizeFor(select);applied.push(select.name==='shirtSize'?'Áo '+select.value:select.name==='pantsSize'?'Quần '+select.value:select.value);select.dispatchEvent(new Event('change',{bubbles:true}));}$('[data-size-availability]').textContent='Đã chọn '+applied.join(' · ')+'. Bạn có thể điều chỉnh trước khi thêm giỏ.';});
    const root=document.querySelector('#studio')||document.querySelector(widget.dataset.sizeTarget)?.closest('form');
    if(root)new MutationObserver(()=>{if(choice)availability();}).observe(root,{subtree:true,childList:true,attributes:true,attributeFilter:['disabled','hidden']});
    guidePromise.then(guide=>{
      $('[data-guide-label]').textContent=guide.label;$('[data-size-boundary]').textContent=guide.boundaryNote;
      for(const type of ['shirt','pants']){
        const chart=guide.charts[type],heading=document.createElement('h3');heading.textContent=chart.label;const table=document.createElement('table'),head=document.createElement('thead'),body=document.createElement('tbody'),tr=document.createElement('tr');
        for(const value of ['Size','Chiều cao','Cân nặng']){const th=document.createElement('th');th.textContent=value;tr.append(th);}head.append(tr);
        for(const row of chart.rows){const tr=document.createElement('tr');for(const value of [row.size,row.heightLabel,row.weightLabel]){const td=document.createElement('td');td.textContent=value;tr.append(td);}body.append(tr);}
        table.append(head,body);$('[data-size-tables]').append(heading,table);
      }
    }).catch(()=>{$('[data-guide-label]').textContent='Chưa tải được bảng size.';});
  });
})();
