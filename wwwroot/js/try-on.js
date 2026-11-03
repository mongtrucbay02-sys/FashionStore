(() => {
  'use strict';
  const $=id=>document.getElementById(id),page=$('tryon');if(!page)return;
  const catalog=JSON.parse($('tryon-catalog-data').textContent),maxBytes=5*1024*1024;
  const mask=document.createElement('canvas'),maskContext=mask.getContext('2d'),paint=$('tryon-paint'),paintContext=paint.getContext('2d');
  let source='catalog',original=null,originalUrl='',personBlob=null,workingWidth=0,workingHeight=0,garments=[],garmentUrls=[],resultUrl='',busy=false,preparing=false,revision=0,stroke=null,undo=[],abort=null;
  function message(text,error=false){$('tryon-status').textContent=text;$('tryon-status').classList.toggle('error',error);}
  const painted=()=>document.querySelector('[name="regionMode"]:checked').value==='paint';
  const product=id=>catalog.find(p=>p.id===id);
  function currentProducts(){if($('tryon-mode').value==='set')return [product($('tryon-set').value)].filter(Boolean);const scope=$('tryon-scope').value;return [scope!=='bottom'?product($('tryon-shirt').value):null,scope!=='top'?product($('tryon-pants').value):null].filter(Boolean);}
  function clearResult(){if(resultUrl)URL.revokeObjectURL(resultUrl);resultUrl='';$('tryon-result').hidden=true;$('tryon-after').removeAttribute('src');$('tryon-before').removeAttribute('src');$('tryon-download').removeAttribute('href');}
  function dirty(){clearResult();controls();}
  function controls(){
    const expected=$('tryon-mode').value==='set'||$('tryon-scope').value!=='outfit'?1:2;
    const complete=source==='catalog'?currentProducts().length===expected:source==='upload'?garments.length>0:$('tryon-description').value.trim().length>0;
    $('tryon-generate').disabled=busy||preparing||!personBlob||!complete||page.dataset.configured!=='true'||page.dataset.authenticated!=='true';
    page.querySelectorAll('input,select,textarea,[data-source]').forEach(el=>el.disabled=busy||preparing);
    $('tryon-cancel').hidden=!busy;paint.classList.toggle('is-disabled',busy||!painted());
    $('tryon-undo').disabled=busy||undo.length===0;$('tryon-eraser').disabled=$('tryon-clear-mask').disabled=busy||!original;
  }
  function drawOverlay(){paintContext.clearRect(0,0,paint.width,paint.height);if(painted()){paintContext.drawImage(mask,0,0);paintContext.globalCompositeOperation='source-in';paintContext.fillStyle='rgba(120,172,71,.55)';paintContext.fillRect(0,0,paint.width,paint.height);paintContext.globalCompositeOperation='source-over';}}
  function sourceFields(){
    const set=$('tryon-mode').value==='set',scope=$('tryon-scope').value;
    $('tryon-catalog').hidden=source!=='catalog';$('tryon-upload').hidden=source!=='upload';
    $('tryon-shirt-field').hidden=set||scope==='bottom';$('tryon-pants-field').hidden=set||scope==='top';$('tryon-set-field').hidden=!set;
    if(set&&scope!=='outfit'){$('tryon-mode').value='separates';return sourceFields();}
    $('tryon-catalog-preview').replaceChildren();
    for(const p of currentProducts()){
      if(!/^\/images\/[a-zA-Z0-9/_-]+\.(png|jpe?g|webp)$/i.test(p.image))continue;
      const figure=document.createElement('figure'),image=document.createElement('img'),caption=document.createElement('figcaption');image.src=p.image;image.alt=p.name;caption.textContent=`${p.name} · ${p.colorName}`;figure.append(image,caption);$('tryon-catalog-preview').append(figure);
    }
    $('tryon-description-label').firstChild.textContent=source==='description'?'Mô tả đồ nam muốn thử (bắt buộc)':'Mô tả thêm (không bắt buộc)';
    dirty();
  }
  async function validImage(file){
    if(!file||!file.size||file.size>maxBytes)throw new Error('Mỗi ảnh cần nhỏ hơn hoặc bằng 5 MB.');
    const b=new Uint8Array(await file.slice(0,33).arrayBuffer());
    const png=b.length>=33&&[137,80,78,71,13,10,26,10].every((v,i)=>b[i]===v),jpg=b[0]===255&&b[1]===216&&b[2]===255,webp=String.fromCharCode(...b.slice(0,4))==='RIFF'&&String.fromCharCode(...b.slice(8,12))==='WEBP';
    if(!png&&!jpg&&!webp)throw new Error('Chỉ nhận ảnh JPG, PNG hoặc WEBP hợp lệ.');
    const typed=new Blob([file],{type:png?'image/png':jpg?'image/jpeg':'image/webp'}),url=URL.createObjectURL(typed);
    try{const image=await load(url);if(image.naturalWidth*image.naturalHeight>20000000||image.naturalWidth<128||image.naturalHeight<128)throw new Error('Ảnh cần từ 128 px mỗi cạnh, tối đa 20 megapixel.');return {file:typed,url,image};}catch(e){URL.revokeObjectURL(url);throw e;}
  }
  function load(url){return new Promise((resolve,reject)=>{const image=new Image();image.onload=()=>resolve(image);image.onerror=()=>reject(new Error('Không đọc được ảnh. Hãy chọn file khác.'));image.src=url;});}
  function blob(canvas,type='image/png',quality){return new Promise((resolve,reject)=>canvas.toBlob(value=>value?resolve(value):reject(new Error('Không chuẩn bị được ảnh.')),type,quality));}
  $('tryon-person').addEventListener('change',async e=>{
    const file=e.target.files?.[0],version=++revision;preparing=true;personBlob=null;original=null;clearResult();controls();
    if(originalUrl)URL.revokeObjectURL(originalUrl);originalUrl='';$('tryon-person-preview').hidden=true;paint.hidden=true;$('tryon-person-empty').hidden=false;
    try{
      if(!file)return;const result=await validImage(file);if(version!==revision){URL.revokeObjectURL(result.url);return;}
      const ratio=result.image.naturalWidth/result.image.naturalHeight;if(ratio<1/3||ratio>3){URL.revokeObjectURL(result.url);throw new Error('Chọn ảnh có tỷ lệ từ 1:3 đến 3:1, thấy rõ vùng trang phục.');}
      const scale=Math.min(1,1536/Math.max(result.image.naturalWidth,result.image.naturalHeight)),canvas=document.createElement('canvas');canvas.width=Math.round(result.image.naturalWidth*scale);canvas.height=Math.round(result.image.naturalHeight*scale);canvas.getContext('2d').drawImage(result.image,0,0,canvas.width,canvas.height);
      let normalized=await blob(canvas);if(normalized.size>maxBytes)normalized=await blob(canvas,'image/jpeg',.95);
      if(normalized.size>maxBytes){URL.revokeObjectURL(result.url);throw new Error('Ảnh sau chuẩn hóa quá lớn. Chọn ảnh nhỏ hơn.');}
      if(version!==revision){URL.revokeObjectURL(result.url);return;}
      original=result.image;originalUrl=result.url;personBlob=normalized;workingWidth=canvas.width;workingHeight=canvas.height;
      mask.width=paint.width=workingWidth;mask.height=paint.height=workingHeight;undo=[];drawOverlay();
      $('tryon-person-stage').style.aspectRatio=`${original.naturalWidth}/${original.naturalHeight}`;$('tryon-person-preview').src=originalUrl;$('tryon-person-preview').hidden=false;$('tryon-person-empty').hidden=true;paint.hidden=false;
      message(`Đã chuẩn bị ảnh ${original.naturalWidth} × ${original.naturalHeight}. Ảnh gửi AI tối đa cạnh 1.536 px; kết quả tải về giữ kích thước khung ảnh gốc.`);
    }catch(e){message(e.message,true);}finally{if(version===revision){preparing=false;controls();}}
  });
  $('tryon-garments').addEventListener('change',async e=>{
    preparing=true;controls();garments=[];garmentUrls.forEach(URL.revokeObjectURL);garmentUrls=[];$('tryon-upload-preview').replaceChildren();dirty();
    try{
      const files=[...e.target.files];if(files.length>2)throw new Error('Chọn tối đa hai ảnh trang phục.');
      for(const file of files){const item=await validImage(file);garments.push(item.file);garmentUrls.push(item.url);const figure=document.createElement('figure'),image=document.createElement('img'),caption=document.createElement('figcaption');image.src=item.url;image.alt='Ảnh trang phục tham chiếu';caption.textContent=file.name;figure.append(image,caption);$('tryon-upload-preview').append(figure);}message(`Đã chọn ${garments.length} ảnh trang phục tham chiếu.`);
    }catch(e){garments=[];garmentUrls.forEach(URL.revokeObjectURL);garmentUrls=[];$('tryon-upload-preview').replaceChildren();$('tryon-garments').value='';message(e.message,true);}finally{preparing=false;controls();}
  });
  page.querySelectorAll('[data-source]').forEach(button=>button.addEventListener('click',()=>{source=button.dataset.source;page.querySelectorAll('[data-source]').forEach(b=>b.setAttribute('aria-pressed',String(b===button)));sourceFields();}));
  for(const id of ['tryon-mode','tryon-shirt','tryon-pants','tryon-set','tryon-scope'])$(id).addEventListener('change',sourceFields);
  $('tryon-description').addEventListener('input',dirty);
  page.querySelectorAll('[name="regionMode"]').forEach(radio=>radio.addEventListener('change',()=>{$('tryon-brush-controls').hidden=!painted();drawOverlay();dirty();}));
  function point(e){const r=paint.getBoundingClientRect();return{x:(e.clientX-r.left)/r.width*workingWidth,y:(e.clientY-r.top)/r.height*workingHeight};}
  function line(from,to){maskContext.globalCompositeOperation=$('tryon-eraser').getAttribute('aria-pressed')==='true'?'destination-out':'source-over';maskContext.strokeStyle='white';maskContext.fillStyle='white';maskContext.lineWidth=Number($('tryon-brush').value)/100*Math.max(workingWidth,workingHeight);maskContext.lineCap='round';maskContext.lineJoin='round';maskContext.beginPath();maskContext.moveTo(from.x,from.y);maskContext.lineTo(to.x,to.y);maskContext.stroke();maskContext.beginPath();maskContext.arc(to.x,to.y,maskContext.lineWidth/2,0,Math.PI*2);maskContext.fill();maskContext.globalCompositeOperation='source-over';drawOverlay();}
  paint.addEventListener('pointerdown',e=>{if(busy||!original||!painted()||e.button!==0)return;e.preventDefault();undo.push(maskContext.getImageData(0,0,workingWidth,workingHeight));if(undo.length>5)undo.shift();stroke={pointer:e.pointerId,last:point(e)};paint.setPointerCapture(e.pointerId);line(stroke.last,stroke.last);dirty();});
  paint.addEventListener('pointermove',e=>{if(!stroke||stroke.pointer!==e.pointerId)return;const next=point(e);line(stroke.last,next);stroke.last=next;});
  const finish=e=>{if(stroke?.pointer===e.pointerId){stroke=null;controls();}};paint.addEventListener('pointerup',finish);paint.addEventListener('pointercancel',finish);
  $('tryon-eraser').addEventListener('click',()=>{$('tryon-eraser').setAttribute('aria-pressed',String($('tryon-eraser').getAttribute('aria-pressed')!=='true'));});
  $('tryon-undo').addEventListener('click',()=>{const previous=undo.pop();if(previous){maskContext.putImageData(previous,0,0);drawOverlay();dirty();}});
  $('tryon-clear-mask').addEventListener('click',()=>{if(!original)return;undo.push(maskContext.getImageData(0,0,workingWidth,workingHeight));if(undo.length>5)undo.shift();maskContext.clearRect(0,0,workingWidth,workingHeight);drawOverlay();dirty();});
  function hasPaint(){const data=maskContext.getImageData(0,0,workingWidth,workingHeight).data;for(let i=3;i<data.length;i+=4)if(data[i])return true;return false;}
  $('tryon-cancel').addEventListener('click',()=>abort?.abort());
  $('tryon-generate').addEventListener('click',async()=>{
    if(busy||$('tryon-generate').disabled)return;if(painted()&&!hasPaint()){message('Hãy tô vùng quần áo cần thay hoặc chọn AI tự xác định.',true);return;}
    const target=original,mode=painted(),allowed=document.createElement('canvas');allowed.width=workingWidth;allowed.height=workingHeight;allowed.getContext('2d').drawImage(mask,0,0);
    clearResult();busy=true;controls();$('tryon-generate').setAttribute('aria-busy','true');abort=new AbortController();const start=Date.now(),timeout=setTimeout(()=>abort.abort(),260000),elapsed=$('tryon-elapsed');elapsed.hidden=false;
    const tick=setInterval(()=>{elapsed.textContent=`Đã chờ ${Math.floor((Date.now()-start)/1000)} giây`;},1000);message('Cloudflare đang thay trang phục trên ảnh của bạn. Có thể mất vài phút…');
    try{
      const form=new FormData();form.append('__RequestVerificationToken',page.querySelector('[name="__RequestVerificationToken"]').value);form.append('person',personBlob,personBlob.type==='image/png'?'person.png':'person.jpg');
      Object.entries({source,scope:$('tryon-scope').value,mode:$('tryon-mode').value,shirtId:$('tryon-shirt').value,pantsId:$('tryon-pants').value,setId:$('tryon-set').value,description:$('tryon-description').value}).forEach(([k,v])=>form.append(k,v));
      if(source==='upload')garments.forEach((file,i)=>form.append('garments',file,`garment-${i}.${file.type==='image/png'?'png':file.type==='image/webp'?'webp':'jpg'}`));
      if(mode){const apiMask=document.createElement('canvas');apiMask.width=workingWidth;apiMask.height=workingHeight;const ctx=apiMask.getContext('2d');ctx.fillStyle='white';ctx.fillRect(0,0,workingWidth,workingHeight);ctx.globalCompositeOperation='destination-out';ctx.drawImage(allowed,0,0);form.append('mask',await blob(apiMask),'mask.png');}
      const response=await fetch(page.dataset.url,{method:'POST',body:form,credentials:'same-origin',signal:abort.signal});
      if(response.redirected)throw new Error('Phiên đăng nhập đã hết. Đăng nhập lại rồi chọn ảnh.');
      if(!response.ok){const error=response.headers.get('content-type')?.includes('application/json')?await response.json():null;throw new Error(error?.message||(response.status===413?'Tổng ảnh tải lên quá lớn.':'Không xử lý được yêu cầu. Tải lại trang và thử lại.'));}
      if(!response.headers.get('content-type')?.includes('image/png'))throw new Error('Máy chủ chưa trả ảnh PNG hợp lệ.');
      const rawUrl=URL.createObjectURL(await response.blob());let edited;
      try{edited=await load(rawUrl);}finally{URL.revokeObjectURL(rawUrl);}
      const output=document.createElement('canvas');output.width=target.naturalWidth;output.height=target.naturalHeight;const ctx=output.getContext('2d');ctx.drawImage(edited,0,0,output.width,output.height);
      if(mode){ctx.globalCompositeOperation='destination-in';ctx.drawImage(allowed,0,0,output.width,output.height);ctx.globalCompositeOperation='destination-over';ctx.drawImage(target,0,0,output.width,output.height);ctx.globalCompositeOperation='source-over';}
      resultUrl=URL.createObjectURL(await blob(output));$('tryon-before').src=originalUrl;$('tryon-after').src=resultUrl;$('tryon-download').href=resultUrl;$('tryon-result-caption').textContent=mode?'AI thay trong vùng tô · phần còn lại từ ảnh gốc':'Thử đồ AI · tự xác định vùng trang phục';$('tryon-result').hidden=false;
      message('Đã có kết quả thử đồ. So sánh trước/sau và tải PNG để giữ ảnh.');$('tryon-result').scrollIntoView({behavior:matchMedia('(prefers-reduced-motion: reduce)').matches?'auto':'smooth'});
    }catch(e){message(e.name==='AbortError'?'Đã dừng chờ hoặc hết thời gian. Không tự gửi lại yêu cầu; kiểm tra kết quả/hạn mức trước khi thử tiếp.':e.message||'Không kết nối được máy chủ.',true);}finally{clearInterval(tick);clearTimeout(timeout);busy=false;abort=null;elapsed.hidden=true;$('tryon-generate').removeAttribute('aria-busy');controls();}
  });
  const params=new URLSearchParams(location.search);for(const [key,id]of[['shirtId','tryon-shirt'],['pantsId','tryon-pants'],['setId','tryon-set'],['mode','tryon-mode']]){const value=params.get(key);if([...$(id).options].some(o=>o.value===value))$(id).value=value;}
  sourceFields();
})();
