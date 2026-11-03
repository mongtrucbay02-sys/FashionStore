(function(root) {
  'use strict';
  function recommend(height, weight, fit, guide) {
    if (!Number.isFinite(height) || !Number.isFinite(weight) || height < 130 || height > 220 || weight < 30 || weight > 180 || !['regular','loose'].includes(fit))
      return { error: 'Nhập chiều cao từ 130–220 cm và cân nặng từ 30–180 kg hợp lệ.' };
    function one(kind) {
      const chart = guide.charts[kind], rows = chart.rows;
      const hi = rows.findIndex(r => r.heightExclusive ? height < r.maxHeight : height <= r.maxHeight);
      const wi = rows.findIndex(r => r.weightExclusive ? weight < r.maxWeight : weight <= r.maxWeight);
      if (hi < 0 || wi < 0) return { error: `${chart.label}: thông số ngoài bảng, cần đối chiếu số đo sản phẩm.` };
      const base = Math.max(hi, wi), index = base + (fit === 'loose' ? 1 : 0);
      if (index >= rows.length) return { error: `${chart.label}: cỡ cơ bản ${rows[base].size}, chưa có cỡ lớn hơn trong bảng để mặc rộng.` };
      return { size: rows[index].size, baseSize: rows[base].size };
    }
    return { shirt:one('shirt'), pants:one('pants'), reason:`Đối chiếu ${height} cm và ${weight} kg theo hai bảng riêng, ưu tiên cỡ lớn hơn khi chiều cao và cân nặng khác cỡ.` + (fit === 'loose' ? ' Tăng thêm một cỡ theo lựa chọn mặc rộng.' : '') };
  }
  const api = { recommend }; if (typeof module === 'object' && module.exports) module.exports = api; else root.FashionSizeRules = api;
})(typeof window !== 'undefined' ? window : globalThis);
