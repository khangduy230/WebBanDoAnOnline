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
        
        public ActionResult QL_DanhSachNguoiDung() { return View(); }
        public ActionResult QL_ThemNguoiDung() { return View(); }
        public ActionResult QL_SuaNguoiDung(int id) { ViewBag.MaTK = id; return View(); }

        // 1. LẤY DANH SÁCH 
        [HttpPost]
        public string Lay_DSNguoiDung()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

            // Tìm theo tên, email, sđt, tài khoản
            string TimKiem = Request["TimKiem"];
            var query = db.TaiKhoans.Where(x => x.isDelete != 1);

            if (!string.IsNullOrEmpty(TimKiem))
            {
                string lower = TimKiem.ToLower();
                query = query.Where(x => x.HoTen.ToLower().Contains(lower)
                                      || x.TenTK.ToLower().Contains(lower)
                                      || x.Email.ToLower().Contains(lower)
                                      || x.SoDienThoai.Contains(lower));
            }
            // Phân trang
            int Trang = 1;
            if (!string.IsNullOrEmpty(Request["Trang"])) int.TryParse(Request["Trang"], out Trang);
            int TrangSize = 6;
            
            int totalItems = query.Count();
            int TongTrang = (int)Math.Ceiling((double)totalItems / TrangSize);
            if (TongTrang == 0) TongTrang = 1;

            // Lấy dữ liệu trang hiện tại

            var data = query.OrderByDescending(x => x.MaTK)
                            .Skip((Trang - 1) * TrangSize)
                            .Take(TrangSize)
                            .Select(x => new {
                                x.MaTK,
                                x.HoTen,
                                x.TenTK,
                                x.Email,
                                x.SoDienThoai,
                                x.VaiTro,
                                x.TrangThai
                            }).ToList();

            return JsonConvert.SerializeObject(new { TongTrang = TongTrang, currentTrang = Trang, NguoiDungs = data });
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
        public ActionResult ChiTietTaiKhoan(string id)
        {
            ViewBag.UserId = id;
            return View("~/Views/NguoiDung/ChiTietTaiKhoan.cshtml");
        }

        // API: Lấy chi tiết 1 người dùng theo MaTK

        [HttpPost]
        public string QL_LayChiTietNguoiDung()
        {
            try
            {
                string id_str = Request["id"];
                if (string.IsNullOrEmpty(id_str)) return JsonConvert.SerializeObject(new { error = "invalid_id" });
                int id = int.Parse(id_str);

                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                var tk = db.TaiKhoans.FirstOrDefault(x => x.MaTK == id);
                if (tk == null) return JsonConvert.SerializeObject(new { error = "not_found" });

                // --- 1. XỬ LÝ AVATAR (FIX LỖI ẢNH) ---
                // Nếu đường dẫn chứa dấu ~, thay thế bằng rỗng để trình duyệt hiểu
                string avatarPath = "/img/no-image.jpg";
                if (!string.IsNullOrEmpty(tk.AnhDaiDien))
                {
                    avatarPath = tk.AnhDaiDien.Replace("~", "");
                }

                // --- 2. XỬ LÝ ĐƠN HÀNG ---
                // Đếm tổng đơn (Chỉ đếm đơn chưa bị xóa mềm)
                int tongDon = db.DonHangs.Count(d => d.MaTK == id && (d.isDelete == 0 || d.isDelete == null));

                // Lấy đơn mới nhất
                var rawDonHang = db.DonHangs
                    .Where(d => d.MaTK == id && (d.isDelete == 0 || d.isDelete == null))
                    .OrderByDescending(d => d.Create_at)
                    .Select(d => new { d.MaDH, d.Create_at, d.TrangThai, d.TongTien })
                    .FirstOrDefault();

                object donGanNhat = null;
                if (rawDonHang != null)
                {
                    donGanNhat = new
                    {
                        rawDonHang.MaDH,
                        time = rawDonHang.Create_at.HasValue ? rawDonHang.Create_at.Value.ToString("HH:mm - dd/MM/yyyy") : "",
                        rawDonHang.TrangThai,
                        rawDonHang.TongTien
                    };
                }

                // --- 3. NGÀY CẬP NHẬT ---
                string capNhatGanNhat = "Chưa cập nhật";
                if (tk.LastEdit_at.HasValue)
                    capNhatGanNhat = tk.LastEdit_at.Value.ToString("HH:mm - dd/MM/yyyy");
                else if (tk.Update_at.HasValue)
                    capNhatGanNhat = tk.Update_at.Value.ToString("HH:mm - dd/MM/yyyy");

                // --- 4. TRẢ VỀ KẾT QUẢ ---
                var result = new
                {
                    id = tk.MaTK,
                    hoTen = tk.HoTen,
                    email = tk.Email,
                    sdt = tk.SoDienThoai,
                    vaiTro = tk.VaiTro,
                    trangThai = tk.TrangThai,

                    avatar = avatarPath, // Trả về đường dẫn đã xử lý

                    createAt = tk.Create_at.HasValue ? tk.Create_at.Value.ToString("HH:mm - dd/MM/yyyy") : "",
                    updateAt = capNhatGanNhat,
                    tongDon = tongDon,
                    donGanNhat = donGanNhat
                };

                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { error = "server_error", message = ex.Message });
            }
        }

        // 3. THÊM MỚI 

        public string ThemMoiNguoiDung()
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
        public string CapNhatNguoiDung()
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
        public string DatTrangThai()
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