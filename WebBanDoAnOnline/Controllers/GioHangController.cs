/*using System;
using System.Collections.Generic;
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

        // Hiển thị giỏ: yêu cầu đăng nhập
        [Authorize]
        public ActionResult Index()
        {
            var user = CurrentUser;
            if (user == null) return RedirectToAction("Login", "TaiKhoan");

            var model = (from g in db.GioHangs
                         join p in db.SanPhams on g.MaSP equals p.MaSP
                         where g.isDelete != 1 && p.isDelete != 1 && g.MaTK == user.MaTK
                         select new CartLineVM
                         {
                             MaSP = p.MaSP,
                             TenSP = p.TenSP,
                             Gia = p.Gia ?? 0m,
                             Anh = p.Anh,
                             SoLuong = g.SoLuong ?? 1,
                             GhiChu = g.Extend_Data
                         }).ToList();

            return View(model);
        }

        // Ajax: thêm vào giỏ. Nếu chưa đăng nhập -> trả về requireLogin=true
        [HttpPost]
        [AllowAnonymous]
        public ActionResult Add(int productId, int quantity = 1, string notes = "")
        {
            if (quantity < 1) quantity = 1;
            notes = (notes ?? string.Empty).Trim();

            var user = CurrentUser;
            if (user == null)
            {
                return Json(new { ok = false, requireLogin = true, message = "Vui lòng đăng nhập để thêm vào giỏ hàng." });
            }

            var sp = db.SanPhams.FirstOrDefault(x => x.MaSP == productId && x.isDelete != 1 && x.TrangThai == "Còn hàng");
            if (sp == null)
                return Json(new { ok = false, message = "Sản phẩm không tồn tại hoặc đã hết hàng." });

            // Gộp dòng theo (MaTK, MaSP, GhiChu)
            var line = db.GioHangs.FirstOrDefault(g =>
                g.MaTK == user.MaTK &&
                g.MaSP == productId &&
                (g.Extend_Data ?? "") == notes &&
                g.isDelete != 1);

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

            var totalQty = db.GioHangs.Where(g => g.MaTK == user.MaTK && g.isDelete != 1)
                                      .Select(g => (g.SoLuong ?? 0)).DefaultIfEmpty(0).Sum();

            var totalAmount = (from g in db.GioHangs
                               join p in db.SanPhams on g.MaSP equals p.MaSP
                               where g.MaTK == user.MaTK && g.isDelete != 1 && p.isDelete != 1
                               select (g.SoLuong ?? 0) * (p.Gia ?? 0m))
                               .DefaultIfEmpty(0m).Sum();

            return Json(new { ok = true, count = totalQty, total = totalAmount });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
*/

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

        [Authorize]
        public ActionResult Index()
        {
            var user = CurrentUser;
            if (user == null) return RedirectToAction("Login", "TaiKhoan");

            var model = (from g in db.GioHangs
                         join p in db.SanPhams on g.MaSP equals p.MaSP
                         where g.isDelete != 1 && p.isDelete != 1 && g.MaTK == user.MaTK
                         select new CartLineVM
                         {
                             MaSP = p.MaSP,
                             TenSP = p.TenSP,
                             Gia = p.Gia ?? 0m,
                             Anh = p.Anh,
                             SoLuong = g.SoLuong ?? 1,
                             GhiChu = g.Extend_Data
                         }).ToList();

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateInput(false)] // cho phép notes có ký tự đặc biệt
        public ActionResult Add(int productId, int quantity = 1, string notes = "")
        {
            if (quantity < 1) quantity = 1;
            notes = (notes ?? string.Empty).Trim();
            if (notes.Length > 1000) notes = notes.Substring(0, 1000); // an toàn độ dài

            var user = CurrentUser;
            if (user == null)
            {
                return Json(new { ok = false, requireLogin = true, message = "Vui lòng đăng nhập để thêm vào giỏ hàng." });
            }

            var sp = db.SanPhams.FirstOrDefault(x => x.MaSP == productId && x.isDelete != 1 && x.TrangThai == "Còn hàng");
            if (sp == null)
                return Json(new { ok = false, message = "Sản phẩm không tồn tại hoặc đã hết hàng." });

            // Tránh so sánh text ở SQL: lọc ứng viên rồi so sánh notes trong bộ nhớ
            var candidates = db.GioHangs
                .Where(g => g.MaTK == user.MaTK && g.MaSP == productId && g.isDelete != 1)
                .ToList();

            var line = candidates.FirstOrDefault(g => string.Equals(g.Extend_Data ?? string.Empty, notes, StringComparison.Ordinal));

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

            try
            {
                db.SubmitChanges();
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = "Không thể thêm vào giỏ hàng: " + ex.Message });
            }

            var totalQty = db.GioHangs.Where(g => g.MaTK == user.MaTK && g.isDelete != 1)
                                      .Select(g => (g.SoLuong ?? 0)).DefaultIfEmpty(0).Sum();

            var totalAmount = (from g in db.GioHangs
                               join p in db.SanPhams on g.MaSP equals p.MaSP
                               where g.MaTK == user.MaTK && g.isDelete != 1 && p.isDelete != 1
                               select (g.SoLuong ?? 0) * (p.Gia ?? 0m))
                               .DefaultIfEmpty(0m).Sum();

            return Json(new { ok = true, count = totalQty, total = totalAmount });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
