
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class BaoCaoController : Controller
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null)
            {
                filterContext.Result = new RedirectToRouteResult(
                    new System.Web.Routing.RouteValueDictionary(new { controller = "TaiKhoan", action = "DangNhap", area = "" }));
                return;
            }
            if (user.VaiTro != "Quản lý")
            {
                if (user.VaiTro == "Khách hàng")
                    filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary(new { controller = "KH_TrangChu", action = "Index", area = "" }));
                else
                    filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary(new { controller = "DonHang", action = "NV_DanhSachDonHang", area = "" }));
                return;
            }
            base.OnActionExecuting(filterContext);
        }

        public ActionResult QL_BaoCao()
        {
            ViewBag.Title = "Báo cáo";
            return View();
        }

        [HttpPost]
        public string Lay_BaoCao()
        {
            try
            {
                var db = new BanDoAnOnlineDataContext();

                // Đọc tham số yyyy-MM-dd (input type="date")
                string tuNgayStr = Request["tuNgay"];
                string denNgayStr = Request["denNgay"];
                DateTime today = DateTime.Today;
                DateTime tuNgay = new DateTime(today.Year, today.Month, 1);
                DateTime denNgay = today.AddDays(1).AddTicks(-1);

                DateTime tmp;
                if (DateTime.TryParseExact(tuNgayStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out tmp))
                    tuNgay = tmp.Date;
                if (DateTime.TryParseExact(denNgayStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out tmp))
                    denNgay = tmp.Date.AddDays(1).AddTicks(-1);

                // Bộ lọc đơn hàng hợp lệ
                var donHangs = db.DonHangs.Where(d =>
                    d.Create_at >= tuNgay && d.Create_at <= denNgay &&
                    (d.isDelete == 0 || d.isDelete == null));

                // Doanh thu: loại trừ đơn hủy
                var donHopLeDoanhThu = donHangs.Where(d => d.TrangThai != "Đã hủy");
                decimal doanhThu = donHopLeDoanhThu.Sum(d => (decimal?)d.TongTien) ?? 0;

                int tongDon = donHangs.Count();
                int donDaHuy = donHangs.Count(d => d.TrangThai == "Đã hủy");
                int donDaGiao = donHangs.Count(d => d.TrangThai == "Đã giao" || d.TrangThai == "Đã nhận hàng");

                // Top sản phẩm (dựa trên ChiTietDonHang + DonHang, bỏ đơn hủy)
                var topSanPham = db.ChiTietDonHangs
                    .Where(ct => (ct.isDelete == 0 || ct.isDelete == null))
                    .Join(db.DonHangs, ct => ct.MaDH, dh => dh.MaDH, (ct, dh) => new { ct, dh })
                    .Where(x => x.dh.Create_at >= tuNgay && x.dh.Create_at <= denNgay &&
                                (x.dh.isDelete == 0 || x.dh.isDelete == null) &&
                                x.dh.TrangThai != "Đã hủy")
                    .GroupBy(x => new { x.ct.MaSP, x.ct.TenSP })
                    .Select(g => new
                    {
                        TenSP = g.Key.TenSP,
                        SoLuong = g.Sum(z => z.ct.SoLuong ?? 0),
                        DoanhThu = g.Sum(z => z.ct.ThanhTien)
                    })
                    .OrderByDescending(x => x.SoLuong)
                    .Take(5)
                    .ToList();

                // Doanh thu theo Danh mục (ct -> sp -> dm), bỏ bản ghi không có MaSP
                var doanhThuTheoDanhMuc = db.ChiTietDonHangs
                    .Where(ct => (ct.isDelete == 0 || ct.isDelete == null) && ct.MaSP != null)
                    .Join(db.DonHangs, ct => ct.MaDH, dh => dh.MaDH, (ct, dh) => new { ct, dh })
                    .Where(x => x.dh.Create_at >= tuNgay && x.dh.Create_at <= denNgay &&
                                (x.dh.isDelete == 0 || x.dh.isDelete == null) &&
                                x.dh.TrangThai != "Đã hủy")
                    .Join(db.SanPhams, x => x.ct.MaSP, sp => sp.MaSP, (x, sp) => new { x.ct, x.dh, sp })
                    .Join(db.DanhMucs, y => y.sp.MaDM, dm => dm.MaDM, (y, dm) => new { y.ct, y.dh, y.sp, dm })
                    .GroupBy(z => z.dm.TenDM)
                    .Select(g => new
                    {
                        TenDM = g.Key,
                        DoanhThu = g.Sum(z => z.ct.ThanhTien)
                    })
                    .OrderByDescending(x => x.DoanhThu)
                    .ToList();

                // Voucher: số đơn dùng và doanh thu của đơn đó
                var voucherHieuQua = db.DonHangs
                    .Where(d => d.Create_at >= tuNgay && d.Create_at <= denNgay &&
                                (d.isDelete == 0 || d.isDelete == null) &&
                                d.TrangThai != "Đã hủy" &&
                                d.MaVoucher != null)
                    .Join(db.Vouchers, dh => dh.MaVoucher, v => v.MaVoucher, (dh, v) => new { dh, v })
                    .GroupBy(x => new { x.v.MaVoucher, x.v.TenVoucher })
                    .Select(g => new
                    {
                        TenVoucher = g.Key.TenVoucher,
                        SoDon = g.Count(),
                        DoanhThu = g.Sum(x => (decimal?)x.dh.TongTien) ?? 0
                    })
                    .OrderByDescending(x => x.SoDon)
                    .ToList();

                var kq = new
                {
                    KhoangThoiGian = new { Tu = tuNgay.ToString("dd/MM/yyyy"), Den = denNgay.ToString("dd/MM/yyyy") },
                    TongQuan = new
                    {
                        TongDon = tongDon,
                        DaGiao = donDaGiao,
                        DaHuy = donDaHuy,
                        DoanhThu = doanhThu
                    },
                    TopSanPham = topSanPham,
                    DoanhThuTheoDanhMuc = doanhThuTheoDanhMuc,
                    Voucher = voucherHieuQua
                };

                return JsonConvert.SerializeObject(new { success = true, data = kq });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }
    }
}