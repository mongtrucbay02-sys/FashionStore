(() => {
  'use strict';
  document.querySelectorAll('.qr-payment').forEach(panel => {
    const image = panel.querySelector('[data-qr-image]');
    function showError() {
      image.hidden = true;
      panel.querySelector('[data-qr-error]').hidden = false;
    }
    if (image) {
      image.addEventListener('error', showError);
      if (image.complete && !image.naturalWidth) showError();
    }
    panel.querySelectorAll('[data-copy]').forEach(button => button.addEventListener('click', async () => {
      const result = panel.querySelector('[data-copy-result]');
      try {
        await navigator.clipboard.writeText(button.dataset.copy);
        result.textContent = 'Đã sao chép.';
      } catch {
        result.textContent = 'Chưa sao chép được. Hãy chọn và sao chép thông tin trực tiếp.';
      }
    }));
  });
})();
