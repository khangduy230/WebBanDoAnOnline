
using BCrypt.Net; 
using System;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using WebBanDoAnOnline.Models;

// Namespace phải khớp với project của bạn
namespace WebBanDoAnOnline.Controllers
{
    public class TaiKhoanController : Controller
    {
        private BanDoAnOnlineDataContext db;
        private const string ConnectionStringName = "BanDoAnOnlineConnectionString"; 

        public TaiKhoanController()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings[ConnectionStringName].ConnectionString;
                db = new BanDoAnOnlineDataContext(connectionString);
            }
            catch (Exception ex)
            {
                throw new Exception($"Không thể tìm thấy Connection String với tên '{ConnectionStringName}' trong Web.config.", ex);
            }
        }

        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(TaiKhoanViewModels.LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // ========================================================
            // === PHẦN LOGIN ĐÃ SỬA ĐỂ KIỂM TRA HASH ===
            // ========================================================

            // 1. Tìm người dùng bằng tên tài khoản (Email, SĐT, hoặc TenTK) TRƯỚC
            var user = db.TaiKhoans.FirstOrDefault(u =>
                (u.TenTK == model.TaiKhoan || u.Email == model.TaiKhoan || u.SoDienThoai == model.TaiKhoan)
                && u.isDelete != 1
            );

            bool isPasswordValid = false;

            // 2. Nếu tìm thấy user, mới kiểm tra mật khẩu
            if (user != null)
            {
                try
                {
                    // Dùng BCrypt.Verify để so sánh mật khẩu người dùng nhập (model.MatKhau)
                    // với mật khẩu đã hash trong CSDL (user.MatKhau)
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(model.MatKhau, user.MatKhau);
                }
                catch (Exception)
                {
                    // Xử lý lỗi nếu hash trong CSDL không hợp lệ (ví dụ: là mật khẩu cũ chưa hash)
                    isPasswordValid = false;
                }
            }

            // 3. Nếu mật khẩu hợp lệ (isPasswordValid == true)
            if (isPasswordValid) // Thay vì 'user != null'
            {
                Session["TaiKhoan"] = user;
                FormsAuthentication.SetAuthCookie(user.TenTK, false);

                if (Url.IsLocalUrl(returnUrl) && returnUrl.Length > 1 && returnUrl.StartsWith("/")
                    && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\"))
                {
                    return Redirect(returnUrl);
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                return View(model);
            }
        }

        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Register(TaiKhoanViewModels.RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra tồn tại
                if (db.TaiKhoans.Any(u => u.Email == model.Email && u.isDelete != 1))
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng.");
                }
                if (db.TaiKhoans.Any(u => u.SoDienThoai == model.SoDienThoai && u.isDelete != 1))
                {
                    ModelState.AddModelError("SoDienThoai", "Số điện thoại này đã được sử dụng.");
                }
                if (db.TaiKhoans.Any(u => u.TenTK == model.TenTK && u.isDelete != 1))
                {
                    ModelState.AddModelError("TenTK", "Tên tài khoản này đã tồn tại.");
                }

                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                // ========================================================
                // === PHẦN REGISTER ĐÃ SỬA ĐỂ TẠO HASH ===
                // ========================================================

                // 1. Hash mật khẩu trước khi lưu
                // Tham số "12" là "work factor" - độ phức tạp của hash. 12 là mức an toàn tốt.
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.MatKhau, workFactor: 12);

                // 2. Tạo TaiKhoan mới
                var newUser = new TaiKhoan
                {
                    HoTen = model.HoTen,
                    TenTK = model.TenTK,
                    SoDienThoai = model.SoDienThoai,
                    Email = model.Email,
                    MatKhau = hashedPassword, // <-- LƯU MẬT KHẨU ĐÃ HASH
                    VaiTro = "KhachHang",
                    Create_at = DateTime.Now,
                    LastEdit_at = DateTime.Now,
                    isDelete = 0
                };

                // 3. Lưu vào CSDL
                db.TaiKhoans.InsertOnSubmit(newUser);
                try
                {
                    db.SubmitChanges();
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Đã xảy ra lỗi khi đăng ký. " + ex.Message);
                    return View(model);
                }

                TempData["RegisterSuccess"] = "Đăng ký tài khoản thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }

            return View(model);
        }

        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            Session.Clear();
            Session.Abandon();
            FormsAuthentication.SignOut();
            return RedirectToAction("Index", "Home");
        }

        // Giải phóng DataContext
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