using System;
using System.Linq;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    [Authorize]
    public class ThanhToanController : Controller
    {
        private readonly BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext(
            System.Configuration.ConfigurationManager.ConnectionStrings["BanDoAnOnlineConnectionString"].ConnectionString
        );

        private TaiKhoan CurrentUser => Session["TaiKhoan"] as TaiKhoan;

        private static decimal GiaApDung(SanPham p, DateTime? now = null)
        {
            var n = now ?? DateTime.Now;
            var goc = p.Gia ?? 0m;
            var km = p.GiaKhuyenMai ?? 0m;
            var trongKhung =
                (!p.NgayBatDauKM.HasValue || p.NgayBatDauKM.Value <= n) &&
                (!p.NgayKetThucKM.HasValue || p.NgayKetThucKM.Value >= n);
            return (goc > 0 && km > 0 && km < goc && trongKhung) ? km : goc;
        }

        private static bool DangGiamGia(SanPham p, DateTime? now = null)
        {
            var n = now ?? DateTime.Now;
            var goc = p.Gia ?? 0m;
            var km = p.GiaKhuyenMai ?? 0m;
            var trongKhung =
                (!p.NgayBatDauKM.HasValue || p.NgayBatDauKM.Value <= n) &&
                (!p.NgayKetThucKM.HasValue || p.NgayKetThucKM.Value >= n);
            return goc > 0 && km > 0 && km < goc && trongKhung;
        }

        // GET: /ThanhToan
        public ActionResult Index()
        {
            var user = CurrentUser;
            if (user == null) return RedirectToAction("Login", "TaiKhoan");

            var rows = (from g in db.GioHangs
                        join p in db.SanPhams on g.MaSP equals p.MaSP
                        where g.isDelete != 1 && p.isDelete != 1 && g.MaTK == user.MaTK
                        select new { g, p }).ToList();

            if (!rows.Any()) return RedirectToAction("Index", "GioHang");

            var vm = new TrangThanhToanVM
            {
                DanhSach = rows.Select(x => new DongThanhToanVM
                {
                    MaSP = x.p.MaSP,
                    TenSP = x.p.TenSP,
                    Anh = x.p.Anh,
                    DonGia = GiaApDung(x.p),
                    GiaGoc = x.p.Gia ?? 0m,
                    OnSale = DangGiamGia(x.p),
                    SoLuong = x.g.SoLuong ?? 1,
                    GhiChu = x.g.Extend_Data
                }).ToList()
            };

            vm.TongTienHang = vm.DanhSach.Sum(i => i.ThanhTien);
            vm.PhiGiaoHang = 25000m;
            vm.PhuPhi = 3000m;
            vm.GiamGia = 0m;
            vm.TongThanhToan = vm.TongTienHang + vm.PhiGiaoHang + vm.PhuPhi - vm.GiamGia;

            // ĐỊA CHỈ: an toàn khi rỗng
            var dsDiaChi = db.DiaChis
                             .Where(d => d.MaTK == user.MaTK && d.isDelete != 1)
                             .OrderByDescending(d => d.MacDinh == true)
                             .ToList();

            vm.DanhSachDiaChi = dsDiaChi;
            if (dsDiaChi.Any())
            {
                vm.MaDiaChiDaChon = dsDiaChi.FirstOrDefault(d => d.MacDinh == true)?.MaDiaChi
                                    ?? dsDiaChi.First().MaDiaChi;
            }
            else
            {
                vm.MaDiaChiDaChon = null; // chưa có địa chỉ
            }

            ViewBag.Title = "Thanh toán";
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApplyVoucher(string code, decimal subtotal)
        {
            try
            {
                code = (code ?? string.Empty).Trim().ToUpperInvariant();
                if (subtotal <= 0) return Json(new { ok = true, discount = 0 });

                decimal discount = 0m;
                if (!string.IsNullOrWhiteSpace(code))
                {
                    if (string.Equals(code, "GIAM30", StringComparison.OrdinalIgnoreCase))
                    {
                        if (subtotal >= 100000m) discount = 30000m;
                    }
                    else
                    {
                        var vc = db.Vouchers.FirstOrDefault(v => v.isDelete != 1 && v.TenVoucher.ToUpper() == code);
                        if (vc != null)
                        {
                            if (string.Equals(vc.LoaiGiam, "AMOUNT", StringComparison.OrdinalIgnoreCase))
                                discount = Math.Min(vc.GiaTri, subtotal);
                            else if (string.Equals(vc.LoaiGiam, "PERCENT", StringComparison.OrdinalIgnoreCase))
                                discount = Math.Min(Math.Round(subtotal * vc.GiaTri / 100m, 0), subtotal);
                        }
                    }
                }
                return Json(new { ok = true, discount });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = "Áp dụng voucher thất bại.", detail = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PlaceOrder(int addressId, string payMethod, string note, string voucherCode)
        {
            try
            {
                var user = CurrentUser;
                if (user == null) return Json(new { ok = false, requireLogin = true });

                var diaChi = db.DiaChis.FirstOrDefault(d => d.MaDiaChi == addressId && d.MaTK == user.MaTK && d.isDelete != 1);
                if (diaChi == null) return Json(new { ok = false, message = "Địa chỉ không hợp lệ." });

                var rows = (from g in db.GioHangs
                            join p in db.SanPhams on g.MaSP equals p.MaSP
                            where g.isDelete != 1 && p.isDelete != 1 && g.MaTK == user.MaTK
                            select new { g, p }).ToList();

                if (!rows.Any()) return Json(new { ok = false, message = "Giỏ hàng trống." });

                var items = rows.Select(x => new
                {
                    x.p.MaSP,
                    Price = GiaApDung(x.p),
                    Qty = (x.g.SoLuong ?? 1),
                    Notes = x.g.Extend_Data
                }).ToList();

                var subtotal = items.Sum(i => i.Price * i.Qty);
                var shipping = 25000m;
                var service = 3000m;

                decimal discount = 0m;
                int? maVoucher = null;
                var code = (voucherCode ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(code))
                {
                    if (string.Equals(code, "GIAM30", StringComparison.OrdinalIgnoreCase) && subtotal >= 100000m)
                        discount = 30000m;
                    else
                    {
                        var vc = db.Vouchers.FirstOrDefault(v => v.isDelete != 1 && v.TenVoucher.ToUpper() == code.ToUpper());
                        if (vc != null)
                        {
                            if (string.Equals(vc.LoaiGiam, "AMOUNT", StringComparison.OrdinalIgnoreCase))
                                discount = Math.Min(vc.GiaTri, subtotal);
                            else if (string.Equals(vc.LoaiGiam, "PERCENT", StringComparison.OrdinalIgnoreCase))
                                discount = Math.Min(Math.Round(subtotal * vc.GiaTri / 100m, 0), subtotal);

                            maVoucher = vc.MaVoucher;
                            if (vc.SoLuotConLai.HasValue && vc.SoLuotConLai.Value <= 0)
                                return Json(new { ok = false, message = "Voucher đã hết lượt sử dụng." });
                        }
                    }
                }

                var grand = subtotal + shipping + service - discount;
                if (grand <= 0) grand = 0;

                var dh = new DonHang
                {
                    MaTK = user.MaTK,
                    MaDiaChi = diaChi.MaDiaChi,
                    MaVoucher = maVoucher,
                    TongTien = grand,
                    PhuongThucTT = (payMethod == "BANK" ? "Ngân hàng" : "Tiền mặt"),
                    TrangThai = "Chờ xác nhận",
                    Create_at = DateTime.Now,
                    isDelete = 0,
                    Extend_Data = note
                };
                db.DonHangs.InsertOnSubmit(dh);
                db.SubmitChanges();

                foreach (var it in items)
                {
                    db.ChiTietDonHangs.InsertOnSubmit(new ChiTietDonHang
                    {
                        MaDH = dh.MaDH,
                        MaSP = it.MaSP,
                        SoLuong = it.Qty,
                        DonGia = it.Price,
                        Extend_Data = it.Notes,
                        Create_at = DateTime.Now,
                        isDelete = 0
                    });
                }

                db.LichSuTrangThais.InsertOnSubmit(new LichSuTrangThai
                {
                    MaDH = dh.MaDH,
                    TrangThai = "Đặt hàng",
                    ThoiGian = DateTime.Now,
                    Create_at = DateTime.Now,
                    isDelete = 0
                });

                if (maVoucher.HasValue)
                {
                    var vc = db.Vouchers.FirstOrDefault(v => v.MaVoucher == maVoucher.Value);
                    if (vc != null && vc.SoLuotConLai.HasValue && vc.SoLuotConLai.Value > 0)
                        vc.SoLuotConLai = vc.SoLuotConLai.Value - 1;
                }

                foreach (var r in rows) { r.g.isDelete = 1; r.g.LastEdit_at = DateTime.Now; }
                db.SubmitChanges();

                Session["CartCount"] = 0;

                return Json(new { ok = true, orderId = dh.MaDH, redirect = Url.Action("Success", "ThanhToan", new { id = dh.MaDH }) });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = "Đặt hàng thất bại.", detail = ex.Message });
            }
        }

        public ActionResult Success(int id)
        {
            ViewBag.Title = "Đặt hàng thành công";
            ViewBag.OrderId = id;
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}