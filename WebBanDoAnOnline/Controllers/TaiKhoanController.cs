using BCrypt.Net;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class TaiKhoanController : Controller
    {
        // GET: DangNhap
        public ActionResult Login()
        {
            if (Session["TaiKhoan"] != null)
            {
                var user = Session["TaiKhoan"] as TaiKhoan;
                if (user.VaiTro == "Quản lý") return RedirectToAction("Index", "QL_TrangChu");
                if (user.VaiTro == "Nhân viên") return RedirectToAction("Index", "NV_TrangChu");
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // GET: DangKy
        public ActionResult Register()
        {
            return View();
        }

        // --- API LOGIN 
        
        public JsonResult Login_act(string txt_acc, string txt_pass)
        {
            if (string.IsNullOrEmpty(txt_acc) || string.IsNullOrEmpty(txt_pass))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ tài khoản và mật khẩu." });
            }

            try
            {
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                var user = db.TaiKhoans.FirstOrDefault(u =>
                    (u.TenTK == txt_acc || u.Email == txt_acc || u.SoDienThoai == txt_acc)
                    && (u.isDelete == 0 || u.isDelete == null)
                );

                if (user != null)
                {
                    bool isPassOk = false;
                    if (user.MatKhau == txt_pass) isPassOk = true;
                    else
                    {
                        try { if (BCrypt.Net.BCrypt.Verify(txt_pass, user.MatKhau)) isPassOk = true; } catch { }
                    }

                    if (isPassOk)
                    {
                        if (user.TrangThai == "Bị khóa")
                        {
                            return Json(new { success = false, message = "Tài khoản của bạn đã bị khóa." });
                        }

                        Session["TaiKhoan"] = user;
                        Session["HoTen"] = user.HoTen;

                        string redirectUrl = "/Home/Index";
                        if (user.VaiTro == "Quản lý" || user.VaiTro == "Nhân viên") redirectUrl = "/QL_TrangChu/Index";

                        
                        return Json(new { success = true, message = "Đăng nhập thành công", url = redirectUrl });
                    }
                }

                return Json(new { success = false, message = "Tài khoản hoặc mật khẩu không chính xác." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // API REGISTER 
        
        public JsonResult Register_act(string txt_name, string txt_user, string txt_phone, string txt_email, string txt_pass, string txt_repass)
        {
            if (string.IsNullOrEmpty(txt_name) || string.IsNullOrEmpty(txt_user) || string.IsNullOrEmpty(txt_pass))
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin." });

            if (txt_pass != txt_repass)
                return Json(new { success = false, message = "Mật khẩu xác nhận không khớp." });

            if (!string.IsNullOrEmpty(txt_email) && !Regex.IsMatch(txt_email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return Json(new { success = false, message = "Email không đúng định dạng." });

            if (!string.IsNullOrEmpty(txt_phone) && !Regex.IsMatch(txt_phone, @"^\d+$"))
                return Json(new { success = false, message = "Số điện thoại chỉ được chứa ký tự số." });

            try
            {
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

                if (db.TaiKhoans.Any(x => x.TenTK == txt_user && (x.isDelete == 0 || x.isDelete == null)))
                    return Json(new { success = false, message = "Tên tài khoản này đã tồn tại." });

                if (db.TaiKhoans.Any(x => x.SoDienThoai == txt_phone && (x.isDelete == 0 || x.isDelete == null)))
                    return Json(new { success = false, message = "Số điện thoại này đã được đăng ký." });

                string hashPass = BCrypt.Net.BCrypt.HashPassword(txt_pass);
                WebBanDoAnOnline.Models.TaiKhoan newUser = new WebBanDoAnOnline.Models.TaiKhoan
                {
                    HoTen = txt_name,
                    TenTK = txt_user,
                    SoDienThoai = txt_phone,
                    Email = txt_email,
                    MatKhau = hashPass,
                    VaiTro = "Khách hàng",
                    TrangThai = "Hoạt động",
                    NgayGiaNhap = DateTime.Now,
                    Create_at = DateTime.Now,
                    isDelete = 0
                };

                db.TaiKhoans.InsertOnSubmit(newUser);
                db.SubmitChanges();

               
                return Json(new { success = true, message = "Đăng ký thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        public ActionResult LogOff()
        {
            Session["TaiKhoan"] = null;
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Index", "Home");
        }
    }
}