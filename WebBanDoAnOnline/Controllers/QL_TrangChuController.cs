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
                filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary(new { controller = "TaiKhoan", action = "DangNhap", area = "" }));
                return;
            }

            // 2. Đã đăng nhập nhưng khác quản lý
            if (user.VaiTro != "Quản lý")
            {
                if (user.VaiTro == "Khách hàng")
                {
                    filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary(new { controller = "KH_TrangChu", action = "Index", area = "" }));
                }
                else
                {
                    filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary(new { controller = "DonHang", action = "NV_DanhSachDonHang", area = "" }));
                }
                return;
            }

            // Nếu là Quản lý thì cho qua
            base.OnActionExecuting(filterContext);
        }

        // 2. LẤY DỮ LIỆU DASHBOARD 
        public string LayDuLieuDashBoard()
        {
            try
            {
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                DateTime now = DateTime.Now;
                // Lấy ngày đầu tháng để tính toán dữ liệu tháng này
                DateTime startOfMonth = new DateTime(now.Year, now.Month, 1);
                DateTime startOfNextMonth = startOfMonth.AddMonths(1);

                // --- 1. THỐNG KÊ SỐ LIỆU CƠ BẢN ---



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
                .Select(p => new
                {
                    p.TenSP,
                    p.Gia,
                    p.TrangThai,
                    // Tính điểm trung bình ngay tại đây. 
                    // (double?) ép kiểu để tránh lỗi khi chưa có đánh giá nào (trả về null -> 0)
                    DiemTrungBinh = db.DanhGias
                        .Where(d => d.MaSP == p.MaSP && (d.isDelete == 0 || d.isDelete == null))
                        .Average(d => (double?)d.SoSao) ?? 0
                })
                .OrderByDescending(p => p.DiemTrungBinh) // Sắp xếp theo điểm vừa tính
                .Take(5)
                .ToList() // Thực thi query lấy dữ liệu về
                .Select(p => new {
                    p.TenSP,
                    p.Gia,
                    p.TrangThai,
                    DiemDanhGia = Math.Round(p.DiemTrungBinh, 1) // Làm tròn số (VD: 4.666 -> 4.7)
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

                // --- 5. DỮ LIỆU BIỂU ĐỒ (Doanh thu & Số đơn theo ngày trong tháng) ---
                var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
                var labels = new List<string>();
                var revenueData = new List<decimal>();
                var orderCountData = new List<int>();

                // Lấy danh sách đơn hàng trong tháng (để xử lý trong bộ nhớ cho nhanh)
                var ordersInMonth = db.DonHangs
                    .Where(d => d.Create_at >= startOfMonth && d.Create_at < startOfNextMonth
                                && d.TrangThai != "Đã hủy"
                                && (d.isDelete == 0 || d.isDelete == null))
                    .Select(d => new { d.Create_at, d.TongTien })
                    .ToList();

                // Duyệt qua từng ngày trong tháng để tổng hợp dữ liệu
                for (int i = 1; i <= daysInMonth; i++)
                {
                    labels.Add(i.ToString()); // Nhãn ngày (1, 2, 3...)

                    // Lọc các đơn hàng của ngày thứ i
                    var ordersOfDay = ordersInMonth.Where(d => d.Create_at.Value.Day == i).ToList();

                    revenueData.Add(ordersOfDay.Sum(d => (decimal?)d.TongTien) ?? 0);
                    orderCountData.Add(ordersOfDay.Count);
                }

                // Thống kê phụ cho biểu đồ (Tổng đơn, Đã giao, Đã hủy)
                var totalOrdersMonth = db.DonHangs.Count(d => d.Create_at >= startOfMonth && d.Create_at < startOfNextMonth && (d.isDelete == 0 || d.isDelete == null));
                var deliveredOrders = db.DonHangs.Count(d => d.Create_at >= startOfMonth && d.Create_at < startOfNextMonth && d.TrangThai == "Đã giao" && (d.isDelete == 0 || d.isDelete == null));
                var cancelledOrders = db.DonHangs.Count(d => d.Create_at >= startOfMonth && d.Create_at < startOfNextMonth && d.TrangThai == "Đã hủy" && (d.isDelete == 0 || d.isDelete == null));


                // Đóng gói JSON trả về
                var data = new
                {
                    Stats = new
                    {
                        DoanhThu = doanhThu,
                        DonMoi = donMoi,
                        DonThang = new { Tong = totalOrdersMonth, DaGiao = deliveredOrders, DaHuy = cancelledOrders }
                    },
                    Menu = topMenu,
                    Staff = staff,
                    Voucher = voucher,
                    Chart = new
                    {
                        Labels = labels,
                        DoanhThuTheoNgay = revenueData,
                        SoDonHangTheoNgay = orderCountData
                    }
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