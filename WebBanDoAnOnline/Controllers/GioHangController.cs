using System;
using System.Linq;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class GioHangController : Controller
    {
        private readonly BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext(
            System.Configuration.ConfigurationManager.ConnectionStrings["BanDoAnOnlineConnectionString"].ConnectionString
        );

        private TaiKhoan CurrentUser => Session["TaiKhoan"] as TaiKhoan;

        // Helper: Tính giá áp dụng theo KM và khung thời gian
        private static decimal GetEffectivePrice(SanPham p, DateTime? now = null)
        {
            var n = now ?? DateTime.Now;
            var giaGoc = p.Gia ?? 0m;
            var km = p.GiaKhuyenMai ?? 0m;

            var trongKhung =
                (!p.NgayBatDauKM.HasValue || p.NgayBatDauKM.Value <= n) &&
                (!p.NgayKetThucKM.HasValue || p.NgayKetThucKM.Value >= n);

            if (giaGoc > 0 && km > 0 && km < giaGoc && trongKhung)
                return km;

            return giaGoc;
        }

        private static bool IsOnSale(SanPham p, DateTime? now = null)
        {
            var n = now ?? DateTime.Now;
            var giaGoc = p.Gia ?? 0m;
            var km = p.GiaKhuyenMai ?? 0m;

            var trongKhung =
                (!p.NgayBatDauKM.HasValue || p.NgayBatDauKM.Value <= n) &&
                (!p.NgayKetThucKM.HasValue || p.NgayKetThucKM.Value >= n);

            return giaGoc > 0 && km > 0 && km < giaGoc && trongKhung;
        }

        [Authorize]
        public ActionResult Index()
        {
            var user = CurrentUser;
            if (user == null) return RedirectToAction("Login", "TaiKhoan");

            var rows = (from g in db.GioHangs
                        join p in db.SanPhams on g.MaSP equals p.MaSP
                        where g.isDelete != 1 && p.isDelete != 1 && g.MaTK == user.MaTK
                        select new { g, p }).ToList();

            var model = rows.Select(x =>
            {
                var giaGoc = x.p.Gia ?? 0m;
                var giaApDung = GetEffectivePrice(x.p);
                return new CartLineVM
                {
                    MaSP = x.p.MaSP,
                    TenSP = x.p.TenSP,
                    Gia = giaApDung,           // tương thích cũ
                    GiaGoc = giaGoc,
                    GiaApDung = giaApDung,
                    OnSale = giaApDung < giaGoc,
                    Anh = x.p.Anh,
                    SoLuong = x.g.SoLuong ?? 1,
                    GhiChu = x.g.Extend_Data
                };
            }).ToList();

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateInput(false)]
        public ActionResult Add(int productId, int quantity = 1, string notes = "")
        {
            try
            {
                if (quantity < 1) quantity = 1;
                notes = (notes ?? string.Empty).Trim();
                if (notes.Length > 1000) notes = notes.Substring(0, 1000);

                var user = CurrentUser;
                if (user == null)
                {
                    return Json(new { ok = false, requireLogin = true, message = "Vui lòng đăng nhập để thêm vào giỏ hàng." });
                }

                var sp = db.SanPhams.FirstOrDefault(x => x.MaSP == productId && x.isDelete != 1 && x.TrangThai == "Còn hàng");
                if (sp == null)
                    return Json(new { ok = false, message = "Sản phẩm không tồn tại hoặc đã hết hàng." });

                var candidates = db.GioHangs
                    .Where(g => g.MaTK == user.MaTK && g.MaSP == productId && g.isDelete != 1)
                    .ToList();

                var line = candidates.FirstOrDefault(g =>
                    string.Equals(g.Extend_Data ?? string.Empty, notes, StringComparison.Ordinal));

                if (line == null)
                {
                    db.GioHangs.InsertOnSubmit(new GioHang
                    {
                        MaTK = user.MaTK,
                        MaSP = productId,
                        SoLuong = quantity,
                        Extend_Data = notes,
                        Create_at = DateTime.Now,
                        isDelete = 0
                    });
                }
                else
                {
                    line.SoLuong = (line.SoLuong ?? 0) + quantity;
                    line.LastEdit_at = DateTime.Now;
                }

                db.SubmitChanges();

                var lines = (from g in db.GioHangs
                             join p in db.SanPhams on g.MaSP equals p.MaSP
                             where g.MaTK == user.MaTK && g.isDelete != 1 && p.isDelete != 1
                             select new { Qty = (g.SoLuong ?? 0), P = p }).ToList();

                var totalQty = lines.Sum(x => x.Qty);
                var totalAmount = lines.Sum(x => x.Qty * GetEffectivePrice(x.P));

                Session["CartCount"] = totalQty;

                return Json(new { ok = true, count = totalQty, total = totalAmount });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = "Không thể thêm vào giỏ hàng.", detail = ex.Message });
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateInput(false)]
        public ActionResult UpdateQuantity(int productId, string notes, int quantity)
        {
            try
            {
                if (quantity < 1) quantity = 1;

                var user = CurrentUser;
                if (user == null)
                {
                    return Json(new { ok = false, requireLogin = true, message = "Phiên đăng nhập đã hết hạn." });
                }

                notes = (notes ?? string.Empty).Trim();
                var candidates = db.GioHangs
                    .Where(g => g.MaTK == user.MaTK && g.MaSP == productId && g.isDelete != 1)
                    .ToList();

                var line = candidates.FirstOrDefault(g =>
                    string.Equals(g.Extend_Data ?? string.Empty, notes, StringComparison.Ordinal));

                if (line == null)
                    return Json(new { ok = false, message = "Không tìm thấy dòng giỏ hàng." });

                line.SoLuong = quantity;
                line.LastEdit_at = DateTime.Now;

                db.SubmitChanges();

                var rows = (from g in db.GioHangs
                            join p in db.SanPhams on g.MaSP equals p.MaSP
                            where g.MaTK == user.MaTK && g.isDelete != 1 && p.isDelete != 1
                            select new { ProductId = p.MaSP, Qty = (g.SoLuong ?? 0), P = p }).ToList();

                var totalQty = rows.Sum(x => x.Qty);
                var totalAmount = rows.Sum(x => x.Qty * GetEffectivePrice(x.P));
                var unitPrice = GetEffectivePrice(rows.FirstOrDefault(x => x.ProductId == productId)?.P);
                var lineTotal = unitPrice * quantity;

                Session["CartCount"] = totalQty;

                return Json(new { ok = true, count = totalQty, total = totalAmount, unit = unitPrice, lineTotal = lineTotal });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = "Cập nhật số lượng thất bại.", detail = ex.Message });
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateInput(false)]
        public ActionResult Remove(int productId, string notes)
        {
            try
            {
                var user = CurrentUser;
                if (user == null)
                {
                    return Json(new { ok = false, requireLogin = true, message = "Phiên đăng nhập đã hết hạn." });
                }

                notes = (notes ?? string.Empty).Trim();
                var candidates = db.GioHangs
                    .Where(g => g.MaTK == user.MaTK && g.MaSP == productId && g.isDelete != 1)
                    .ToList();

                var line = candidates.FirstOrDefault(g =>
                    string.Equals(g.Extend_Data ?? string.Empty, notes, StringComparison.Ordinal));

                if (line == null)
                    return Json(new { ok = false, message = "Không tìm thấy dòng giỏ hàng để xóa." });

                line.isDelete = 1;
                line.LastEdit_at = DateTime.Now;
                db.SubmitChanges();

                var lines = (from g in db.GioHangs
                             join p in db.SanPhams on g.MaSP equals p.MaSP
                             where g.MaTK == user.MaTK && g.isDelete != 1 && p.isDelete != 1
                             select new { Qty = (g.SoLuong ?? 0), P = p }).ToList();

                var totalQty = lines.Sum(x => x.Qty);
                var totalAmount = lines.Sum(x => x.Qty * GetEffectivePrice(x.P));

                Session["CartCount"] = totalQty;

                return Json(new { ok = true, count = totalQty, total = totalAmount });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = "Xóa khỏi giỏ hàng thất bại.", detail = ex.Message });
            }
        }

        [HttpGet]
        public JsonResult CartCount()
        {
            int count = 0;

            var user = CurrentUser;
            if (user != null)
            {
                count = (from g in db.GioHangs
                         join p in db.SanPhams on g.MaSP equals p.MaSP
                         where g.MaTK == user.MaTK && g.isDelete != 1 && p.isDelete != 1
                         select (g.SoLuong ?? 0)).Sum();
            }
            else if (Session["CartCount"] is int c)
            {
                count = c;
            }

            return Json(new { ok = true, count }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}