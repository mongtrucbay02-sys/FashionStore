(() => {
  'use strict';
  document.querySelectorAll('.qr-payment').forEach(panel => {
    const statusUrl = panel.dataset.paymentStatusUrl;
    if (statusUrl) {
      async function checkPayment() {
        try {
          const response = await fetch(statusUrl, { cache: 'no-store', redirect: 'manual' });
          if (response.status === 404 || response.status === 401 || response.status === 403 ||
              response.type === 'opaqueredirect') return;

          if (response.ok) {
            const status = await response.json();
            if (status.paid || status.cancelled) {
              window.location.reload();
              return;
            }
          }
        } catch {
          // Retry after temporary connection errors.
        }
        window.setTimeout(checkPayment, 5000);
      }
      window.setTimeout(checkPayment, 1000);
    }
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
