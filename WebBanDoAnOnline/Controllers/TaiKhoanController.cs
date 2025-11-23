using BCrypt.Net;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class TaiKhoanController : Controller
    {
        // GET: Login
        public ActionResult Login()
        {
            // Nếu đã đăng nhập rồi thì kiểm tra vai trò để đá về đúng trang
            if (Session["TaiKhoan"] != null)
            {
                var user = Session["TaiKhoan"] as TaiKhoan;
                if (user.VaiTro == "Quản lý" || user.VaiTro == "Nhân viên")
                {
                    return RedirectToAction("Index", "QL_TrangChu");
                }
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // GET: Register
        public ActionResult Register()
        {
            return View();
        }

        // API XỬ LÝ ĐĂNG NHẬP
        [HttpPost]
        public string Login_act()
        {
            string acc = Request["txt_acc"];
            string pass = Request["txt_pass"];

            var result = new { ErrCode = 0, Msg = "", Url = "" };

            try
            {
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

                // 1. Tìm tài khoản
                var user = db.TaiKhoans.FirstOrDefault(u =>
                    (u.TenTK == acc || u.Email == acc || u.SoDienThoai == acc)
                    && (u.isDelete == 0 || u.isDelete == null)
                );

                if (user != null)
                {
                    // 2. Kiểm tra pass
                    bool isPassOk = false;

                    // Nếu pass trong DB chưa mã hóa (dữ liệu cũ), check thường
                    if (user.MatKhau == pass)
                    {
                        isPassOk = true;
                    }
                    else
                    {
                        // Nếu không khớp, thử check mã hóa BCrypt
                        try
                        {
                            if (BCrypt.Net.BCrypt.Verify(pass, user.MatKhau)) isPassOk = true;
                        }
                        catch { }
                    }

                    if (isPassOk)
                    {
                        if (user.TrangThai == "Bị khóa")
                        {
                            return JsonConvert.SerializeObject(new { ErrCode = 0, Msg = "Tài khoản đã bị khóa." });
                        }

                        // 3. Lưu Session
                        Session["TaiKhoan"] = user;
                        Session["HoTen"] = user.HoTen;

                        // 4. XỬ LÝ PHÂN QUYỀN (Logic bạn cần)
                        string redirectUrl = "/Home/Index"; // Mặc định là khách

                        if (user.VaiTro == "Quản lý" || user.VaiTro == "Nhân viên")
                        {
                            // Chuyển sang Area Admin
                            // Đảm bảo bạn đã tạo Area tên là "Admin"
                            redirectUrl = "/QL_TrangChu/Index";
                        }

                        result = new { ErrCode = 1, Msg = "Đăng nhập thành công", Url = redirectUrl };
                    }
                    else
                    {
                        result = new { ErrCode = 0, Msg = "Mật khẩu không chính xác", Url = "" };
                    }
                }
                else
                {
                    result = new { ErrCode = 0, Msg = "Tài khoản không tồn tại", Url = "" };
                }
            }
            catch (Exception ex)
            {
                result = new { ErrCode = 0, Msg = "Lỗi: " + ex.Message, Url = "" };
            }

            return JsonConvert.SerializeObject(result);
        }

        // ĐĂNG XUẤT
        public ActionResult LogOff()
        {
            Session["TaiKhoan"] = null;
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Index", "Home");
        }

        // API XỬ LÝ ĐĂNG KÝ
        [HttpPost]
        public string Register_act()
        {
            // 1. Lấy dữ liệu
            string hoTen = Request["txt_name"];
            string tenTK = Request["txt_user"];
            string sdt = Request["txt_phone"];
            string email = Request["txt_email"];
            string matKhau = Request["txt_pass"];
            string rePass = Request["txt_repass"];

            // 2. Validate cơ bản
            if (matKhau != rePass)
            {
                return JsonConvert.SerializeObject(new { ErrCode = 0, Msg = "Mật khẩu xác nhận không khớp." });
            }

            try
            {
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

                // 3. Check trùng Tên TK
                if (db.TaiKhoans.Any(x => x.TenTK == tenTK && (x.isDelete == 0 || x.isDelete == null)))
                {
                    return JsonConvert.SerializeObject(new { ErrCode = 0, Msg = "Tên tài khoản đã tồn tại." });
                }

                // 4. Check trùng SĐT (nếu cần thiết)
                if (db.TaiKhoans.Any(x => x.SoDienThoai == sdt && (x.isDelete == 0 || x.isDelete == null)))
                {
                    return JsonConvert.SerializeObject(new { ErrCode = 0, Msg = "Số điện thoại này đã được đăng ký." });
                }

                // 5. Mã hóa mật khẩu
                string hashPass = BCrypt.Net.BCrypt.HashPassword(matKhau);

                // 6. Tạo đối tượng User mới
                WebBanDoAnOnline.Models.TaiKhoan newUser = new WebBanDoAnOnline.Models.TaiKhoan
                {
                    HoTen = hoTen,
                    TenTK = tenTK,
                    SoDienThoai = sdt,
                    Email = email,
                    MatKhau = hashPass,

                    VaiTro = "Khách hàng",      // Mặc định là Khách
                    TrangThai = "Hoạt động",    // <--- QUAN TRỌNG: Set mặc định là Hoạt động

                    NgayGiaNhap = DateTime.Now,
                    Create_at = DateTime.Now,
                    isDelete = 0
                };

                db.TaiKhoans.InsertOnSubmit(newUser);
                db.SubmitChanges();

                // 7. Trả về thành công
                return JsonConvert.SerializeObject(new { ErrCode = 1, Msg = "Đăng ký thành công" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { ErrCode = 0, Msg = "Lỗi hệ thống: " + ex.Message });
            }
        }
    
    }
}