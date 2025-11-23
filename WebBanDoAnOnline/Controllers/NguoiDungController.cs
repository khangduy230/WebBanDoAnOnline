using BCrypt.Net;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class NguoiDungController : Controller
    {
        // ... (Các hàm View: LayNguoiDung, ThemNguoiDung, SuaNguoiDung giữ nguyên)
        public ActionResult LayNguoiDung() { return View(); }
        public ActionResult ThemNguoiDung() { return View(); }
        public ActionResult SuaNguoiDung(int id) { ViewBag.MaTK = id; return View(); }

        // 1. LẤY DANH SÁCH (API)
        [HttpPost]
        public string Lay_DSNguoiDung()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

            // Tìm theo tên, email, sđt, tài khoản
            string searchTerm = Request["searchTerm"];
            var query = db.TaiKhoans.Where(x => x.isDelete != 1);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                string lower = searchTerm.ToLower();
                query = query.Where(x => x.HoTen.ToLower().Contains(lower)
                                      || x.TenTK.ToLower().Contains(lower)
                                      || x.Email.ToLower().Contains(lower)
                                      || x.SoDienThoai.Contains(lower));
            }

            int page = 1;
            if (!string.IsNullOrEmpty(Request["page"])) int.TryParse(Request["page"], out page);
            int pageSize = 6;

            int totalItems = query.Count();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            if (totalPages == 0) totalPages = 1;

            var data = query.OrderByDescending(x => x.MaTK)
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .Select(x => new {
                                x.MaTK,
                                x.HoTen,
                                x.TenTK,
                                x.Email,
                                x.SoDienThoai,
                                x.VaiTro,
                                x.TrangThai
                            }).ToList();

            return JsonConvert.SerializeObject(new { TotalPages = totalPages, CurrentPage = page, NguoiDungs = data });
        }

        // 2. LẤY CHI TIẾT (API)
        [HttpPost]
        public string LayTTNguoiDung()
        {
            string id_str = Request["id"];
            if (string.IsNullOrEmpty(id_str)) return "{}";
            int id = int.Parse(id_str);

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var user = db.TaiKhoans.FirstOrDefault(x => x.MaTK == id);

            if (user == null) return "{}";

            // Trả về JSON object
            return JsonConvert.SerializeObject(new
            {
                user.MaTK,
                user.HoTen,
                user.TenTK,
                user.Email,
                user.SoDienThoai,
                user.VaiTro,
                user.TrangThai
            });
        }

        // 3. THÊM MỚI (API)
        [HttpPost]
        public string Insert()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

            // Lấy dữ liệu (Tên input phải khớp với View bên dưới)
            string tenTK = Request["txt_TenTK"];
            string email = Request["txt_Email"];
            string sdt = Request["txt_SoDienThoai"];
            string matKhau = Request["txt_MatKhau"];
            string hoTen = Request["txt_HoTen"];

            // Lấy value từ Select
            string vaiTroKey = Request["slc_VaiTro"];     // "QuanLy", "NhanVien", "KhachHang"
            string trangThaiKey = Request["slc_TrangThai"]; // "HoatDong", "BiKhoa"

            // Check trùng DB
            if (db.TaiKhoans.Any(x => x.TenTK == tenTK && x.isDelete != 1)) return "Tên tài khoản đã tồn tại.";
            if (db.TaiKhoans.Any(x => x.Email == email && x.isDelete != 1)) return "Email đã tồn tại.";
            if (db.TaiKhoans.Any(x => x.SoDienThoai == sdt && x.isDelete != 1)) return "SĐT đã tồn tại.";

            try
            {
                TaiKhoan tk = new TaiKhoan();
                tk.HoTen = hoTen;
                tk.TenTK = tenTK;
                tk.Email = email;
                tk.SoDienThoai = sdt;
                tk.MatKhau = BCrypt.Net.BCrypt.HashPassword(matKhau);

                // --- QUAN TRỌNG: MAP DỮ LIỆU KHỚP VỚI DATABASE CHECK CONSTRAINT ---
                switch (vaiTroKey)
                {
                    case "QuanLy": tk.VaiTro = "Quản lý"; break;   // DB nhận "Quản lý"
                    case "NhanVien": tk.VaiTro = "Nhân viên"; break; // DB nhận "Nhân viên"
                    default: tk.VaiTro = "Khách hàng"; break;      // DB nhận "Khách hàng"
                }

                tk.TrangThai = (trangThaiKey == "BiKhoa") ? "Bị khóa" : "Hoạt động";
                // ------------------------------------------------------------------

                tk.NgayGiaNhap = DateTime.Now;
                tk.Create_at = DateTime.Now;
                tk.isDelete = 0;

                db.TaiKhoans.InsertOnSubmit(tk);
                db.SubmitChanges();
                return "Thêm mới thành công";
            }
            catch (Exception ex)
            {
                return "Lỗi: " + ex.Message;
            }
        }

        // 4. CẬP NHẬT (API)
        [HttpPost]
        public string Update()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            string id_str = Request["txt_MaTK_hide"]; // Lấy ID ẩn
            if (string.IsNullOrEmpty(id_str)) return "Lỗi ID";
            int id = int.Parse(id_str);

            var tk = db.TaiKhoans.FirstOrDefault(x => x.MaTK == id);
            if (tk == null) return "Không tìm thấy tài khoản.";

            try
            {
                tk.HoTen = Request["txt_HoTen"];
                tk.Email = Request["txt_Email"];
                tk.SoDienThoai = Request["txt_SoDienThoai"];

                string vaiTroKey = Request["slc_VaiTro"];
                string trangThaiKey = Request["slc_TrangThai"];
                string newPass = Request["txt_MatKhau"];

                // Map lại Role cho đúng DB
                switch (vaiTroKey)
                {
                    case "QuanLy": tk.VaiTro = "Quản lý"; break;
                    case "NhanVien": tk.VaiTro = "Nhân viên"; break;
                    default: tk.VaiTro = "Khách hàng"; break;
                }

                tk.TrangThai = (trangThaiKey == "BiKhoa") ? "Bị khóa" : "Hoạt động";

                // Nếu có đổi mật khẩu
                if (!string.IsNullOrEmpty(newPass))
                {
                    tk.MatKhau = BCrypt.Net.BCrypt.HashPassword(newPass);
                }

                tk.LastEdit_at = DateTime.Now;
                db.SubmitChanges();
                return "Cập nhật thành công";
            }
            catch (Exception ex)
            {
                return "Lỗi: " + ex.Message;
            }
        }

        // 5. XÓA (API)
        [HttpPost]
        public string ToggleStatus()
        {
            string id_str = Request["id"];
            if (string.IsNullOrEmpty(id_str)) return "Lỗi ID";

            int id = int.Parse(id_str);
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

            var tk = db.TaiKhoans.FirstOrDefault(x => x.MaTK == id);

            if (tk != null)
            {
                // Logic đảo trạng thái
                if (tk.TrangThai == "Hoạt động")
                {
                    tk.TrangThai = "Bị khóa";
                    db.SubmitChanges();
                    return "Đã KHÓA tài khoản thành công.";
                }
                else
                {
                    tk.TrangThai = "Hoạt động";
                    db.SubmitChanges();
                    return "Đã MỞ KHÓA tài khoản thành công.";
                }
            }
            return "Không tìm thấy tài khoản.";
        }
    }
}