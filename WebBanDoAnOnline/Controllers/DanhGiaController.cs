using Newtonsoft.Json;
using System;
using System.Linq;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class DanhGiaController : Controller
    {
        private BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();


        // GET: Khách hàng viết đánh giá
        public ActionResult KH_DanhGiaSanPham(int maDH, int maSP)
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null)
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            // Lấy thông tin sản phẩm trong đơn hàng để hiển thị
            var chiTiet = db.ChiTietDonHangs.FirstOrDefault(ct => ct.MaDH == maDH && ct.MaSP == maSP);
            if (chiTiet == null)
            {
                return RedirectToAction("DonMua", "CaiDat");
            }

            ViewBag.MaDH = maDH;
            ViewBag.MaSP = maSP;
            ViewBag.TenSP = chiTiet.TenSP;

            // Lấy ảnh từ bảng SanPham
            var sp = db.SanPhams.FirstOrDefault(s => s.MaSP == maSP);
            ViewBag.AnhSP = (sp != null && !string.IsNullOrEmpty(sp.Anh)) ? sp.Anh : "/Content/img/no-image.jpg";

            return View();
        }

        // GET: View Quản lý (Dành cho Quản lý & Nhân viên)
        public ActionResult QL_QuanLyDanhGia()
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null || !new[] { "Quản lý"}.Contains(user.VaiTro))
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }
            // Truyền vai trò xuống View để ẩn/hiện nút Xóa
            ViewBag.VaiTro = user.VaiTro;
            return View();
        }
        public ActionResult NV_QuanLyDanhGia()
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null || !new[] {  "Nhân viên" }.Contains(user.VaiTro))
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }
            // Truyền vai trò xuống View để ẩn/hiện nút Xóa
            ViewBag.VaiTro = user.VaiTro;
            return View();
        }

        // API 1: Lấy danh sách (Có tìm kiếm + Phân trang)
        [HttpPost]
        public string LayDanhSachDanhGia()
        {
            try
            {
                string tuKhoa = Request["tuKhoa"];
                string trangThaiAn = Request["trangThaiAn"]; // "da_an" hoặc "chua_an"

                int page = 1;
                int.TryParse(Request["page"], out page);
                int pageSize = 10;

                var query = db.DanhGias.AsQueryable();

                // 1. Lọc ẩn/hiện
                if (trangThaiAn == "da_an")
                    query = query.Where(d => d.isDelete == 1);
                else
                    query = query.Where(d => d.isDelete == 0 || d.isDelete == null);

                // 2. Tìm kiếm (Tên khách, Tên SP, Nội dung)
                if (!string.IsNullOrEmpty(tuKhoa))
                {
                    var lower = tuKhoa.ToLower();
                    query = query.Where(d =>
                        ((d.TaiKhoan.HoTen ?? "").ToLower().Contains(lower)) ||
                        ((d.SanPham.TenSP ?? "").ToLower().Contains(lower)) ||
                        ((d.BinhLuan ?? "").ToLower().Contains(lower)));
                }

                // 3. Phân trang
                int totalItems = query.Count();
                int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
                if (totalPages < 1) totalPages = 1;
                if (page < 1) page = 1;
                if (page > totalPages) page = totalPages;

                var data = query
                    .OrderByDescending(d => d.Create_at)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(d => new
                    {
                        MaDG = d.MaDG,
                        TenKhachHang = d.TaiKhoan.HoTen,
                        Avatar = d.TaiKhoan.AnhDaiDien,
                        TenSanPham = d.SanPham.TenSP,
                        AnhSanPham = d.SanPham.Anh,
                        SoSao = d.SoSao,
                        BinhLuan = d.BinhLuan,
                        PhanHoi = d.PhanHoi, // Cột trả lời trong DB
                        ThoiGian = d.Create_at,
                        DaAn = (d.isDelete == 1)
                    })
                    .ToList() // Chuyển về bộ nhớ để format ngày tháng bên dưới
                    .Select(d => new {
                        d.MaDG,
                        d.TenKhachHang,
                        d.Avatar,
                        d.TenSanPham,
                        d.AnhSanPham,
                        d.SoSao,
                        d.BinhLuan,
                        d.PhanHoi,
                        d.DaAn,
                        ThoiGian = d.ThoiGian.HasValue ? d.ThoiGian.Value.ToString("dd/MM/yyyy HH:mm") : ""
                    });

                return JsonConvert.SerializeObject(new { success = true, data = data, totalPages = totalPages, currentPage = page });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }

        // API 2: Ẩn/Hiện đánh giá
        [HttpPost]
        public string ToggleAnDanhGia()
        {
            try
            {
                int maDG = int.Parse(Request["maDG"]);
                var dg = db.DanhGias.FirstOrDefault(d => d.MaDG == maDG);
                if (dg != null)
                {
                    // Đảo trạng thái 0 <-> 1
                    dg.isDelete = (dg.isDelete == 1) ? (byte)0 : (byte)1;
                    db.SubmitChanges();
                    return "OK";
                }
                return "Không tìm thấy đánh giá";
            }
            catch (Exception ex) { return "Lỗi: " + ex.Message; }
        }

        // API 3: Xóa vĩnh viễn (Chỉ Quản lý)
        [HttpPost]
        public string XoaDanhGia()
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null || user.VaiTro != "Quản lý") return "Bạn không có quyền xóa!";

            try
            {
                int maDG = int.Parse(Request["maDG"]);
                var dg = db.DanhGias.FirstOrDefault(d => d.MaDG == maDG);
                if (dg != null)
                {
                    db.DanhGias.DeleteOnSubmit(dg);
                    db.SubmitChanges();
                    return "OK";
                }
                return "Không tìm thấy đánh giá";
            }
            catch (Exception ex) { return "Lỗi: " + ex.Message; }
        }

        // API 4: Trả lời đánh giá
        [HttpPost]
        public string TraLoiDanhGia()
        {
            try
            {
                int maDG = int.Parse(Request["maDG"]);
                string noiDung = Request["noiDung"];

                var dg = db.DanhGias.FirstOrDefault(d => d.MaDG == maDG);
                if (dg != null)
                {
                    dg.PhanHoi = noiDung; // Cập nhật cột phản hồi
                    db.SubmitChanges();
                    return "OK";
                }
                return "Không tìm thấy đánh giá";
            }
            catch (Exception ex) { return "Lỗi: " + ex.Message; }
        }
    }
}