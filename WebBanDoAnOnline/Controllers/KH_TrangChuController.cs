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

        // 2. API lấy dữ liệu sản phẩm (ĐÃ SỬA: Tính điểm sao)
        [HttpPost] // Thêm HttpPost cho chuẩn bảo mật
        public string LaySanPhamTrangChu()
        {
            try
            {
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

                // Bước 1: Lấy danh sách sản phẩm thô ra trước
                var list = db.SanPhams
                             .Where(x => (x.isDelete == null || x.isDelete == 0) && x.TrangThai == "Còn hàng")
                             .OrderByDescending(x => x.Create_at)
                             .Take(8)
                             .ToList(); // Quan trọng: .ToList() để ngắt kết nối DB, xử lý tính toán ở RAM

                // Bước 2: Duyệt qua từng sản phẩm để tính điểm trung bình
                var data = list.Select(x =>
                {
                    // Truy vấn bảng DanhGia để lấy các đánh giá của sản phẩm này
                    var listDG = db.DanhGias.Where(d => d.MaSP == x.MaSP && (d.isDelete == 0 || d.isDelete == null));

                    double diemTB = 0;
                    if (listDG.Any())
                    {
                        // Tính trung bình cộng cột SoSao
                        diemTB = listDG.Average(d => (double)d.SoSao);
                    }

                    return new
                    {
                        MaSP = x.MaSP,
                        TenSP = x.TenSP,
                        Anh = x.Anh,
                        GiaGoc = x.Gia ?? 0,
                        // Trả về thuộc tính SoSao cho View sử dụng
                        SoSao = Math.Round(diemTB, 1)
                    };
                });

                return JsonConvert.SerializeObject(data);
            }
            catch (Exception ex)
            {
                // Nên log lỗi ra để debug nếu cần
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