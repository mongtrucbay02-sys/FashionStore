(() => {
  'use strict';

  const catalog = JSON.parse(document.getElementById('catalog-data').textContent);
  const products = catalog.products;
  const $ = id => document.getElementById(id);
  const product = id => products.find(item => item.id === id);
  const safe = value => String(value).replace(/[&<>"']/g, character => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  }[character]));
  const image = item => typeof item?.image === 'string' && /^\/images\/[a-zA-Z0-9/_-]+\.(png|jpe?g|webp)$/i.test(item.image) ? item.image : '';
  const label = item => `${item.name} · ${item.colorName}`;
  const money = amount => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
  const defaults = () => ({
    mode: 'set',
    shirt: product('loop-blazer')?.id || products.find(item => item.category === 'shirt')?.id || '',
    pants: product('loop-pant')?.id || products.find(item => item.category === 'pants')?.id || '',
    set: product('loop-set')?.id || products.find(item => item.category === 'set')?.id || ''
  });
  const items = look => (look.mode === 'set' ? [product(look.set)] : [product(look.shirt), product(look.pants)]).filter(Boolean);
  const valid = look => look && (look.mode === 'set'
    ? product(look.set)?.category === 'set'
    : look.mode === 'separates' && product(look.shirt)?.category === 'shirt' && product(look.pants)?.category === 'pants');
  const selection = look => ({ mode: look.mode, shirt: look.shirt, pants: look.pants, set: look.set });
  const storageKey = 'form-outfits-v2';
  let selected = defaults(), category = 'shirt', kind = 'all', color = 'all', saved = [], toastTimer;

  // Keep saved product selections from previous versions, without their AI photos.
  try {
    const stored = JSON.parse(localStorage.getItem(storageKey) || '[]');
    if (Array.isArray(stored)) saved = stored.filter(valid).slice(0, 12).map(selection);
  } catch { }

  function card(item) {
    const active = selected.mode === 'set'
      ? item.category === 'set' && selected.set === item.id
      : item.category !== 'set' && selected[item.category] === item.id;
    return `<button class="product-card ${active ? 'selected' : ''}" data-product="${safe(item.id)}" aria-pressed="${active}"><div class="product-image"><img src="${safe(image(item))}" alt="${safe(label(item))}" loading="lazy"/><span class="selection-mark">${active ? '✓' : '+'}</span></div><div class="product-meta"><strong>${safe(item.name)}</strong><span><i style="background:${safe(item.hex)}"></i>${safe(item.colorName)}</span><b>${money(item.price)}</b></div></button>`;
  }

  function filters() {
    const list = products.filter(item => item.category === category);
    const kinds = [...new Set(list.map(item => item.kind))];
    const names = { blazer: 'Blazer', coat: 'Mangto', shirt: 'Sơ mi', denim: 'Denim', tank: 'Áo sát nách', jacket: 'Jacket', formal: 'Quần âu', shorts: 'Quần short', set: 'Bộ phối sẵn' };
    $('kind-filter').innerHTML = '<option value="all">Tất cả kiểu dáng</option>' + kinds.map(value => `<option value="${safe(value)}">${safe(names[value] || value)}</option>`).join('');
    $('color-filters').innerHTML = '<button class="swatch all active" data-color="all" aria-label="Tất cả màu" aria-pressed="true">∗</button>' + [...new Set(list.map(item => item.color))].map(value => {
      const item = list.find(candidate => candidate.color === value);
      return `<button class="swatch" data-color="${safe(value)}" style="--swatch:${safe(item.hex)}" title="${safe(item.colorName)}" aria-label="${safe(item.colorName)}" aria-pressed="false"></button>`;
    }).join('');
  }

  function renderProducts() {
    const list = products.filter(item => item.category === category && (kind === 'all' || item.kind === kind) && (color === 'all' || item.color === color));
    $('product-count').textContent = `${list.length} món`;
    $('product-grid').innerHTML = list.length ? list.map(card).join('') : '<p class="empty-state">Chưa có sản phẩm với bộ lọc này. Hãy thử lựa chọn khác.</p>';
  }

  function renderPreview() {
    const first = selected.mode === 'set' ? product(selected.set) : product(selected.shirt);
    const pants = selected.mode === 'separates' ? product(selected.pants) : null;
    $('lookbook-image').hidden = !first;
    if (first) {
      $('lookbook-image').src = image(first);
      $('lookbook-image').alt = label(first);
    }
    $('preview-primary-label').textContent = first ? label(first) : 'Chọn trang phục để xem ảnh';
    $('outfit-preview').classList.toggle('separates', !!pants);
    $('pants-preview').hidden = !pants;
    if (pants) {
      $('outfit-pants-image').src = image(pants);
      $('outfit-pants-image').alt = label(pants);
      $('preview-pants-label').textContent = label(pants);
    }
    $('lookbook-description').textContent = selected.mode === 'set'
      ? 'Ảnh thật của bộ vest đã chọn. Giày và phụ kiện trong ảnh không tính kèm.'
      : 'Ảnh thật của áo và quần đã chọn, hiển thị riêng để đối chiếu. Giày và phụ kiện trong ảnh không tính kèm.';
  }

  function renderOutfit() {
    $('cart-mode').value = selected.mode;
    for (const type of ['shirt', 'pants', 'set']) {
      const active = selected.mode === 'set' ? type === 'set' : type !== 'set';
      $(`cart-${type}`).value = active ? selected[type] : '';
      const select = document.querySelector(`[name="${type}Size"]`), previous = select.value;
      select.replaceChildren(...(product(selected[type])?.sizes || []).map(size => new Option(size, size)));
      if ([...select.options].some(option => option.value === previous)) select.value = previous;
      select.disabled = !active;
      document.querySelector(`[data-size-for="${type}"]`).hidden = !active;
    }
    $('selected-summary').innerHTML = items(selected).map((item, index) => `<span><small>0${index + 1}</small>${safe(label(item))}</span>`).join('');
    $('total-price').textContent = money(items(selected).reduce((sum, item) => sum + item.price, 0));
    $('save-outfit').disabled = !valid(selected);
    document.querySelector('.shop-outfit-button').disabled = !valid(selected) || items(selected).some(item => !item.sizes.length || item.stock <= 0);
    renderPreview();
  }

  function renderSaved() {
    $('saved-count').textContent = saved.length;
    $('saved-grid').innerHTML = saved.length ? saved.map((look, index) => {
      const list = items(look);
      return `<article class="saved-card"><div class="saved-look-photo ${look.mode === 'separates' ? 'separates' : ''}">${list.map(item => `<img src="${safe(image(item))}" alt="${safe(label(item))}" loading="lazy"/>`).join('')}<span>Ảnh sản phẩm</span></div><strong>Look ${String(index + 1).padStart(2, '0')}</strong><p>${safe(list.map(label).join(' + '))}</p><div><button class="text-button" data-load="${index}">Mặc lại ↗</button><button class="text-button" data-delete="${index}">Xóa</button></div></article>`;
    }).join('') : '<p class="empty-state">Chọn áo và quần hoặc một bộ vest, rồi nhấn Lưu bộ đồ.</p>';
  }

  function render() { renderProducts(); renderOutfit(); }
  function toast(text) {
    clearTimeout(toastTimer);
    $('toast').textContent = text;
    $('toast').hidden = false;
    toastTimer = setTimeout(() => { $('toast').hidden = true; }, 3200);
  }
  function persist(value) {
    try {
      localStorage.setItem(storageKey, JSON.stringify(value));
      saved = value;
      renderSaved();
      return true;
    } catch { toast('Trình duyệt không cho phép lưu bộ đồ.'); return false; }
  }
  function switchCategory(next) {
    category = next;
    kind = color = 'all';
    document.querySelectorAll('[data-category]').forEach(button => {
      const active = button.dataset.category === next;
      button.setAttribute('aria-selected', active);
      button.tabIndex = active ? 0 : -1;
    });
    $('product-panel').setAttribute('aria-labelledby', `tab-${next}`);
    filters();
    renderProducts();
  }

  document.querySelector('.tabs').addEventListener('click', event => {
    const button = event.target.closest('[data-category]');
    if (button) switchCategory(button.dataset.category);
  });
  document.querySelector('.tabs').addEventListener('keydown', event => {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    const categories = ['shirt', 'pants', 'set'];
    const current = categories.indexOf(category);
    const index = event.key === 'Home' ? 0 : event.key === 'End' ? 2 : (current + (event.key === 'ArrowRight' ? 1 : -1) + 3) % 3;
    switchCategory(categories[index]);
    $(`tab-${categories[index]}`).focus();
  });
  $('kind-filter').addEventListener('change', event => { kind = event.target.value; renderProducts(); });
  $('color-filters').addEventListener('click', event => {
    const button = event.target.closest('[data-color]');
    if (!button) return;
    color = button.dataset.color;
    document.querySelectorAll('[data-color]').forEach(element => {
      const active = element.dataset.color === color;
      element.classList.toggle('active', active);
      element.setAttribute('aria-pressed', active);
    });
    renderProducts();
  });
  document.addEventListener('click', event => {
    const button = event.target.closest('[data-product]');
    if (!button) return;
    const item = product(button.dataset.product);
    if (!item) return;
    selected[item.category] = item.id;
    selected.mode = item.category === 'set' ? 'set' : 'separates';
    render();
  });
  $('reset').addEventListener('click', () => { selected = defaults(); switchCategory('set'); render(); toast('Đã khôi phục bộ mặc định.'); });
  $('save-outfit').addEventListener('click', () => {
    if (!valid(selected)) return;
    const same = look => look.mode === selected.mode && (selected.mode === 'set' ? look.set === selected.set : look.shirt === selected.shirt && look.pants === selected.pants);
    if (saved.some(same)) return toast('Bộ đồ này đã được lưu.');
    if (saved.length >= 12) return toast('Đã lưu 12 bộ. Xóa một bộ để lưu thêm.');
    if (persist([...saved, selection(selected)])) toast('Đã lưu bộ đồ.');
  });
  $('saved-grid').addEventListener('click', event => {
    const load = event.target.closest('[data-load]'), remove = event.target.closest('[data-delete]');
    if (load) {
      const look = saved[Number(load.dataset.load)];
      if (!look) return;
      selected = selection(look);
      switchCategory(look.mode === 'set' ? 'set' : 'shirt');
      render();
      $('studio').scrollIntoView({ behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
    }
    if (remove) persist(saved.filter((_, index) => index !== Number(remove.dataset.delete)));
  });

  filters();
  render();
  renderSaved();
})();
