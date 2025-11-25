using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class QL_TrangChuController : Controller
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var user = Session["TaiKhoan"] as TaiKhoan;

            // 1. Chưa đăng nhập -> Về trang login
            if (user == null)
            {
                filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary(new { controller = "TaiKhoan", action = "Login", area = "" }));
                return;
            }

            // 2. Đã đăng nhập nhưng khác quản lý
            if (user.VaiTro != "Quản lý")
            {
                if (user.VaiTro == "Khách hàng")
                {    
                    filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary(new { controller = "Home", action = "Index", area = "" }));
                }
                else
                {
                    filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary(new { controller = "NV_TrangChu", action = "Index", area = "" }));
                }
                return;
            }

            // Nếu là Quản lý thì cho qua
            base.OnActionExecuting(filterContext);
        }

        // 2. LẤY DỮ LIỆU DASHBOARD 
        public string GetDashboardData()
        {
            try
            {
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                DateTime now = DateTime.Now;
                // Lấy ngày đầu tháng để tính toán dữ liệu tháng này
                DateTime startOfMonth = new DateTime(now.Year, now.Month, 1);

                // --- 1. THỐNG KÊ SỐ LIỆU CƠ BẢN ---

                // Ngân sách (Từ bảng NganSach)
                decimal nganSach = db.NganSaches
                    .Where(n => n.Thang == now.Month && n.Nam == now.Year)
                    .Sum(n => (decimal?)n.SoTienDuKien) ?? 0;

                // Chi tiêu thực tế (Từ bảng ThuChi, loại = 'Chi')
                decimal chiTieu = db.ThuChis
                    .Where(t => t.LoaiGiaoDich == "Chi" && t.NgayGiaoDich >= startOfMonth && (t.isDelete == 0 || t.isDelete == null))
                    .Sum(t => (decimal?)t.SoTien) ?? 0;

                // Doanh thu bán hàng (Từ bảng DonHang, trừ đơn hủy)
                decimal doanhThu = db.DonHangs
                    .Where(d => d.Create_at >= startOfMonth && d.TrangThai != "Đã hủy" && (d.isDelete == 0 || d.isDelete == null))
                    .Sum(d => (decimal?)d.TongTien) ?? 0;

                // Đơn hàng mới (Chờ xác nhận)
                int donMoi = db.DonHangs
                    .Count(d => d.TrangThai == "Chờ xác nhận" && (d.isDelete == 0 || d.isDelete == null));


                // --- 2. TOP 5 MÓN ĂN (Sắp xếp theo điểm đánh giá) ---
                var topMenu = db.SanPhams
                    .Where(p => (p.isDelete == 0 || p.isDelete == null))
                    .OrderByDescending(p => p.DiemDanhGia)
                    .Take(5)
                    .Select(p => new {
                        p.TenSP,
                        p.Gia,
                        p.TrangThai,
                        p.DiemDanhGia
                    }).ToList();


                // --- 3. DANH SÁCH NHÂN SỰ (Lấy Quản lý & Nhân viên) ---
                var staff = db.TaiKhoans
                    .Where(u => u.VaiTro != "Khách hàng" && (u.isDelete == 0 || u.isDelete == null))
                    .OrderByDescending(u => u.MaTK)
                    .Take(5)
                    .Select(u => new {
                        u.HoTen,
                        u.VaiTro
                    }).ToList();


                // --- 4. VOUCHER ĐANG CHẠY (Lấy 1 cái làm mẫu) ---
                var voucher = db.Vouchers
                    .Where(v => v.NgayKetThuc >= now && (v.isDelete == 0 || v.isDelete == null))
                    .OrderByDescending(v => v.NgayKetThuc)
                    .Select(v => new { v.TenVoucher, v.MoTaThem })
                    .FirstOrDefault();

                // Đóng gói JSON trả về
                var data = new
                {
                    Stats = new { NganSach = nganSach, ChiTieu = chiTieu, DoanhThu = doanhThu, DonMoi = donMoi },
                    Menu = topMenu,
                    Staff = staff,
                    Voucher = voucher
                };

                return JsonConvert.SerializeObject(data);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Error = ex.Message });
            }
        }
        public ActionResult Index()
        {
            return View();
        }
    }
}