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

        // GET: DanhGia/QL_QuanLyDanhGia
        public ActionResult QL_QuanLyDanhGia()
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null || user.VaiTro != "Quản lý")
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            return View();
        }

        // GET: DanhGia/NV_QuanLyDanhGia
        public ActionResult NV_QuanLyDanhGia()
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null || !new[] { "Quản lý", "Nhân viên" }.Contains(user.VaiTro))
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            return View();
        }

        // API: Lấy danh sách đánh giá
        [HttpPost]
        public string LayDanhSachDanhGia()
        {
            try
            {
                string tuKhoa = Request["tuKhoa"];
                string trangThaiAn = Request["trangThaiAn"];

                var query = db.DanhGias.AsQueryable();

                // Trạng thái ẩn/hiện
                if (trangThaiAn == "da_an")
                    query = query.Where(d => d.isDelete == 1);
                else
                    query = query.Where(d => d.isDelete == 0 || d.isDelete == null);

                // Lọc từ khóa (coalesce để tránh null)
                if (!string.IsNullOrEmpty(tuKhoa))
                {
                    var lower = tuKhoa.ToLower();
                    query = query.Where(d =>
                        ((d.TaiKhoan.HoTen ?? "").ToLower().Contains(lower)) ||
                        ((d.SanPham.TenSP ?? "").ToLower().Contains(lower)) ||
                        ((d.BinhLuan ?? "").ToLower().Contains(lower)));
                }

                // Chuyển sang LINQ to Objects rồi mới format ngày
                var data = query
                    .OrderByDescending(d => d.Create_at)
                    .AsEnumerable()
                    .Select(d => new
                    {
                        MaDG = d.MaDG,
                        TenKhachHang = d.TaiKhoan?.HoTen,
                        Avatar = d.TaiKhoan?.AnhDaiDien,
                        TenSanPham = d.SanPham?.TenSP,
                        AnhSanPham = d.SanPham?.Anh,
                        SoSao = d.SoSao,
                        BinhLuan = d.BinhLuan,
                        ThoiGian = d.Create_at.HasValue ? d.Create_at.Value.ToString("dd/MM/yyyy") : "",
                        DaAn = (d.isDelete ?? 0) == 1
                    })
                    .ToList();

                return JsonConvert.SerializeObject(new { success = true, data });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }

        // API: Ẩn/Hiện một đánh giá
        [HttpPost]
        public string ToggleAnDanhGia()
        {
            try
            {
                var user = Session["TaiKhoan"] as TaiKhoan;
                if (user == null || !new[] { "Quản lý", "Nhân viên" }.Contains(user.VaiTro))
                {
                    return JsonConvert.SerializeObject(new { success = false, message = "Không có quyền truy cập." });
                }

                int maDG = int.Parse(Request["maDG"]);
                var danhGia = db.DanhGias.FirstOrDefault(d => d.MaDG == maDG);
                if (danhGia == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, message = "Không tìm thấy đánh giá." });
                }

                // isDelete: TinyInt -> byte/byte?
                // Trường hợp nullable (byte?)
                if (danhGia.isDelete is byte?)
                {
                    var current = danhGia.isDelete.HasValue ? danhGia.isDelete.Value : (byte)0;
                    danhGia.isDelete = (current == 1) ? (byte)0 : (byte)1;
                }
                else
                {
                    // Trường hợp không nullable (byte)
                    danhGia.isDelete = (danhGia.isDelete == 1) ? (byte)0 : (byte)1;
                }

                danhGia.Update_at = DateTime.Now;
                db.SubmitChanges();

                string message = (danhGia.isDelete == 1) ? "Ẩn đánh giá thành công." : "Hiện đánh giá thành công.";
                return JsonConvert.SerializeObject(new { success = true, message = message });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // API: Xóa vĩnh viễn đánh giá (Chỉ quản lý)
        [HttpPost]
        public string XoaDanhGia()
        {
            try
            {
                var user = Session["TaiKhoan"] as TaiKhoan;
                if (user == null || user.VaiTro != "Quản lý")
                {
                    return JsonConvert.SerializeObject(new { success = false, message = "Chỉ quản lý mới có quyền xóa." });
                }

                int maDG = int.Parse(Request["maDG"]);
                var danhGia = db.DanhGias.FirstOrDefault(d => d.MaDG == maDG);

                if (danhGia == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, message = "Không tìm thấy đánh giá." });
                }

                db.DanhGias.DeleteOnSubmit(danhGia);
                db.SubmitChanges();

                return JsonConvert.SerializeObject(new { success = true, message = "Xóa đánh giá thành công." });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // API: Trả lời đánh giá (Chỉ quản lý)
        [HttpPost]
        public string TraLoiDanhGia()
        {
            // VÔ HIỆU HÓA: Vì DB không hỗ trợ chức năng này
            return JsonConvert.SerializeObject(new { success = false, message = "Chức năng này chưa được hỗ trợ do cấu trúc cơ sở dữ liệu." });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

    }
}