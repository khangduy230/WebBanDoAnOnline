using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;
using Newtonsoft.Json;

namespace WebBanDoAnOnline.Controllers
{
    public class HomeController : Controller
    {
        // 1. Trang chủ
        public ActionResult Index()
        {
            return View();
        }

        // 2. API lấy dữ liệu sản phẩm cho trang chủ
        [HttpPost]
        public string GetHomeDataJson()
        {
            try
            {
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

                // Lấy 8 sản phẩm mới nhất, chưa xóa, còn hàng
                // Sắp xếp theo ngày tạo (mới nhất lên đầu) hoặc điểm đánh giá
                var list = db.SanPhams
                             .Where(x => (x.isDelete == null || x.isDelete == 0) && x.TrangThai == "Còn hàng")
                             .OrderByDescending(x => x.Create_at)
                             .Take(8)
                             .ToList();

                // Map dữ liệu sang object ẩn danh
                var data = list.Select(x => new
                {
                    MaSP = x.MaSP,
                    TenSP = x.TenSP,
                    Anh = x.Anh, // Cột trong DB là Anh
                    GiaGoc = x.Gia ?? 0,
                    GiaKM = x.GiaKhuyenMai ?? 0,

                    // DB không có LuotXem, ta lấy SoLuotDanhGia làm số liệu hiển thị thay thế
                    LuotXem = x.SoLuotDanhGia ?? 0,

                    // Logic kiểm tra giảm giá
                    DangGiamGia = (x.GiaKhuyenMai > 0 && x.GiaKhuyenMai < x.Gia)
                });

                return JsonConvert.SerializeObject(data);
            }
            catch (Exception)
            {
                return "[]";
            }
        }

        // 3. API lấy thông tin Header (Login, Cart count...)
        [HttpPost]
        public string GetHeaderInfo()
        {
            // Kiểm tra session
            if (Session["TaiKhoan"] == null)
            {
                return JsonConvert.SerializeObject(new { isLogin = false, cartCount = 0, notifCount = 0 });
            }

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var user = Session["TaiKhoan"] as TaiKhoan;

            // Tính tổng số lượng trong giỏ hàng (chưa xóa)
            var cartCount = db.GioHangs
                              .Where(g => g.MaTK == user.MaTK && (g.isDelete == null || g.isDelete == 0))
                              .Sum(g => g.SoLuong) ?? 0;

            // Tính thông báo chưa đọc
            var notifCount = db.ThongBaos
                               .Count(t => t.MaTK == user.MaTK && t.IsRead == false);

            return JsonConvert.SerializeObject(new
            {
                isLogin = true,
                fullName = user.HoTen,
                avatar = !string.IsNullOrEmpty(user.AnhDaiDien) ? user.AnhDaiDien : "https://i.imgur.com/4Ym2k5N.png",
                cartCount = cartCount,
                notifCount = notifCount
            });
        }
    }
}