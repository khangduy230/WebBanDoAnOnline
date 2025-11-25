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
        
        public ActionResult LayNguoiDung() { return View(); }
        public ActionResult ThemNguoiDung() { return View(); }
        public ActionResult SuaNguoiDung(int id) { ViewBag.MaTK = id; return View(); }

        // 1. LẤY DANH SÁCH 
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
            // Phân trang
            int page = 1;
            if (!string.IsNullOrEmpty(Request["page"])) int.TryParse(Request["page"], out page);
            int pageSize = 6;
            
            int totalItems = query.Count();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            if (totalPages == 0) totalPages = 1;

            // Lấy dữ liệu trang hiện tại

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

        // 2. LẤY CHI TIẾT 
        [HttpPost]
        public string LayTTNguoiDung()
        {
            string id_str = Request["id"];
            if (string.IsNullOrEmpty(id_str)) return "{}";
            int id = int.Parse(id_str);

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var user = db.TaiKhoans.FirstOrDefault(x => x.MaTK == id);

            if (user == null) return "{}";

            
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

        // 3. THÊM MỚI 
        
        public string Insert()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

            
            string tenTK = Request["txt_TenTK"];
            string email = Request["txt_Email"];
            string sdt = Request["txt_SoDienThoai"];
            string matKhau = Request["txt_MatKhau"];
            string hoTen = Request["txt_HoTen"];

           
            string vaiTroKey = Request["slc_VaiTro"];     
            string trangThaiKey = Request["slc_TrangThai"]; 

            
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

                
                switch (vaiTroKey)
                {
                    case "QuanLy": tk.VaiTro = "Quản lý"; break;   
                    case "NhanVien": tk.VaiTro = "Nhân viên"; break; 
                    default: tk.VaiTro = "Khách hàng"; break;      
                }

                tk.TrangThai = (trangThaiKey == "BiKhoa") ? "Bị khóa" : "Hoạt động";
                

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

        // 4. CẬP NHẬT 
        [HttpPost]
        public string Update()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            string id_str = Request["txt_MaTK_hide"]; 
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

        // 5. XÓA

        // AJAX: Đảo trạng thái khóa/mở khóa
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