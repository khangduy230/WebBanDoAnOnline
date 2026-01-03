using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;
using Newtonsoft.Json;

namespace WebBanDoAnOnline.Controllers
{
    public class KH_TrangChuController : Controller
    {
        // 1. Trang chủ
        public ActionResult Index()
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user != null)
            {
                if (user.VaiTro == "Nhân viên") return RedirectToAction("NV_DanhSachDonHang", "DonHang");
                if (user.VaiTro == "Quản lý") return RedirectToAction("Index", "QL_TrangChu");
            }
            return View();
        }

        // 2. API lấy dữ liệu sản phẩm (ĐÃ SỬA: Lấy Top đánh giá cao nhất)
        [HttpPost]
        public string LaySanPhamTrangChu()
        {
            try
            {
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

                // Kỹ thuật: Tính toán điểm trung bình ngay trong câu lệnh Select
                // để có thể OrderByDescending (Sắp xếp) theo điểm đó.
                var list = db.SanPhams
                             .Where(x => (x.isDelete == null || x.isDelete == 0) && x.TrangThai == "Còn hàng")
                             .Select(p => new
                             {
                                 p.MaSP,
                                 p.TenSP,
                                 p.Anh,
                                 p.Gia,
                                 // Tính điểm trung bình trực tiếp trong DB
                                 // (double?) để tránh lỗi nếu chưa có ai đánh giá (sẽ trả về null -> 0)
                                 DiemTB = db.DanhGias
                                            .Where(d => d.MaSP == p.MaSP && (d.isDelete == 0 || d.isDelete == null))
                                            .Average(d => (double?)d.SoSao) ?? 0
                             })
                             .OrderByDescending(p => p.DiemTB) // Sắp xếp giảm dần theo điểm sao
                             .ThenByDescending(p => p.MaSP)    // Nếu bằng điểm thì lấy cái mới hơn
                             .Take(8)                          // Lấy 8 sản phẩm đầu
                             .ToList();

                // Map lại dữ liệu để trả về JSON (Làm tròn số)
                var data = list.Select(x => new
                {
                    MaSP = x.MaSP,
                    TenSP = x.TenSP,
                    Anh = x.Anh,
                    GiaGoc = x.Gia ?? 0,
                    SoSao = Math.Round(x.DiemTB, 1) // Làm tròn 1 chữ số thập phân (VD: 4.7)
                });

                return JsonConvert.SerializeObject(data);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }

        // 3. Lấy thông tin Header (Giữ nguyên)
        public string LayThongTinHeader()
        {
            if (Session["TaiKhoan"] == null)
            {
                return JsonConvert.SerializeObject(new { isLogin = false, cartCount = 0 });
            }

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var user = Session["TaiKhoan"] as TaiKhoan;

            var cartCount = db.GioHangs
                              .Where(g => g.MaTK == user.MaTK && (g.isDelete == null || g.isDelete == 0))
                              .Sum(g => g.SoLuong) ?? 0;

            return JsonConvert.SerializeObject(new
            {
                isLogin = true,
                fullName = user.HoTen,
                avatar = !string.IsNullOrEmpty(user.AnhDaiDien) ? user.AnhDaiDien : "/img/default-avatar.png",
                cartCount = cartCount
            });
        }
    }
}