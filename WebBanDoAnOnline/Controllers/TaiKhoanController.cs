using BCrypt.Net;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using WebBanDoAnOnline.Models; // Đảm bảo namespace đúng với project của bạn

namespace WebBanDoAnOnline.Controllers
{
    public class TaiKhoanController : Controller
    {
        BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

        // 1. GET: Đăng nhập
        public ActionResult DangNhap()
        {
            if (Session["TaiKhoan"] != null)
                return RedirectToAction("Index", "KH_TrangChu");
            return View();
        }

        // 2. GET: Đăng ký
        public ActionResult DangKy()
        {
            return View();
        }

        // 3. GET: Quên mật khẩu - Bước 1: Nhập tài khoản & Câu hỏi
        // Tên file View tương ứng: QMK_TaiKhoanVaCauHoi.cshtml
        public ActionResult QMK_TaiKhoanVaCauHoi()
        {
            return View();
        }

        // ---------------- API & POST ACTIONS ----------------

        // XỬ LÝ ĐĂNG NHẬP (AJAX)
        [HttpPost]
        public JsonResult XuLyDangNhap(string txt_acc, string txt_pass)
        {
            if (string.IsNullOrEmpty(txt_acc) || string.IsNullOrEmpty(txt_pass))
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin." });

            try
            {
                var user = db.TaiKhoans.FirstOrDefault(u =>
                    (u.TenTK == txt_acc || u.Email == txt_acc || u.SoDienThoai == txt_acc)
                    && (u.isDelete == 0 || u.isDelete == null));

                if (user != null)
                {
                    bool isPassOk = false;
                    // Check pass thường hoặc hash
                    if (user.MatKhau == txt_pass) isPassOk = true;
                    else
                    {
                        try { if (BCrypt.Net.BCrypt.Verify(txt_pass, user.MatKhau)) isPassOk = true; } catch { }
                    }

                    if (isPassOk)
                    {
                        if (user.TrangThai == "Bị khóa")
                            return Json(new { success = false, message = "Tài khoản đã bị khóa." });

                        Session["TaiKhoan"] = user;
                        Session["HoTen"] = user.HoTen;
                        if (string.IsNullOrEmpty(user.CauHoiBaoMat) || string.IsNullOrEmpty(user.CauTraLoiBaoMat))
                        {
                            return Json(new
                            {
                                success = true,
                                url = "/CaiDat/HoSo", // Chuyển hướng sang trang hồ sơ
                                isMissingInfo = true  // Cờ đánh dấu thiếu thông tin để hiện thông báo bên View
                            });
                        }

                        string url = (user.VaiTro == "Quản lý" || user.VaiTro == "Nhân viên")
                                     ? "/QL_TrangChu/Index" : "/KH_TrangChu/Index";

                        return Json(new { success = true, url = url });
                    }
                }
                return Json(new { success = false, message = "Tài khoản hoặc mật khẩu không đúng." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // XỬ LÝ ĐĂNG KÝ (AJAX) - Đã thêm tham số CauHoi & CauTraLoi
        [HttpPost]
        public JsonResult XuLiDangKy(string txt_name, string txt_user, string txt_phone, string txt_email,
                                     string txt_pass, string txt_repass,
                                     string CauHoiBaoMat, string CauTraLoiBaoMat)
        {
            if (string.IsNullOrEmpty(txt_name) || string.IsNullOrEmpty(txt_user) || string.IsNullOrEmpty(txt_pass))
                return Json(new { success = false, message = "Thiếu thông tin bắt buộc." });

            if (txt_pass != txt_repass)
                return Json(new { success = false, message = "Mật khẩu xác nhận không khớp." });

            // Kiểm tra tồn tại
            if (db.TaiKhoans.Any(x => x.TenTK == txt_user && x.isDelete != 1))
                return Json(new { success = false, message = "Tên tài khoản đã tồn tại." });

            if (db.TaiKhoans.Any(x => x.SoDienThoai == txt_phone && x.isDelete != 1))
                return Json(new { success = false, message = "Số điện thoại đã tồn tại." });

            try
            {
                string hashPass = BCrypt.Net.BCrypt.HashPassword(txt_pass);

                TaiKhoan newUser = new TaiKhoan
                {
                    HoTen = txt_name,
                    TenTK = txt_user,
                    SoDienThoai = txt_phone,
                    Email = txt_email,
                    MatKhau = hashPass,
                    // Lưu câu hỏi bảo mật
                    CauHoiBaoMat = CauHoiBaoMat,
                    CauTraLoiBaoMat = CauTraLoiBaoMat,

                    VaiTro = "Khách hàng",
                    TrangThai = "Hoạt động",
                    NgayGiaNhap = DateTime.Now,
                    Create_at = DateTime.Now,
                    isDelete = 0
                };

                db.TaiKhoans.InsertOnSubmit(newUser);
                db.SubmitChanges();

                return Json(new { success = true, message = "Đăng ký thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // XỬ LÝ QUÊN MẬT KHẨU BƯỚC 1: Kiểm tra câu hỏi (Form POST thường)
        [HttpPost]
        public ActionResult XuLyXacThucCauHoi(string TenTK, string CauHoiBaoMat, string CauTraLoiBaoMat)
        {
            var user = db.TaiKhoans.FirstOrDefault(x => x.TenTK == TenTK && (x.isDelete == 0 || x.isDelete == null));

            if (user == null)
            {
                ViewBag.Error = "Tài khoản không tồn tại!";
                return View("QMK_TaiKhoanVaCauHoi");
            }

            // So sánh câu hỏi và câu trả lời (Cần chính xác từng ký tự hoặc dùng ToLower() để so sánh lỏng hơn)
            if (user.CauHoiBaoMat != CauHoiBaoMat ||
                user.CauTraLoiBaoMat.Trim().ToLower() != CauTraLoiBaoMat.Trim().ToLower())
            {
                ViewBag.Error = "Câu hỏi hoặc câu trả lời bảo mật không chính xác!";
                return View("QMK_TaiKhoanVaCauHoi");
            }

            // Nếu đúng, chuyển sang View đặt lại mật khẩu và truyền tên TK qua
            ViewBag.TenTK = user.TenTK;
            // Lưu ý: View "DatLaiMatKhau" là tên file .cshtml View 3 của bạn
            return View("DatLaiMatKhau");
        }

        // XỬ LÝ QUÊN MẬT KHẨU BƯỚC 2: Đổi mật khẩu mới (Form POST thường)
        [HttpPost]
        public ActionResult XuLyDoiMatKhauMoi(string TenTK, string MatKhauMoi, string XacNhanMatKhau)
        {
            if (MatKhauMoi != XacNhanMatKhau)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp!";
                ViewBag.TenTK = TenTK; // Giữ lại tên TK để user nhập lại
                return View("DatLaiMatKhau");
            }

            var user = db.TaiKhoans.FirstOrDefault(x => x.TenTK == TenTK);
            if (user != null)
            {
                user.MatKhau = BCrypt.Net.BCrypt.HashPassword(MatKhauMoi);
                db.SubmitChanges();

                // Đổi xong quay về đăng nhập
                return RedirectToAction("DangNhap");
            }

            ViewBag.Error = "Đã xảy ra lỗi, không tìm thấy tài khoản.";
            return View("DatLaiMatKhau");
        }

        public ActionResult DangXuat()
        {
            Session.Clear();
            return RedirectToAction("Index", "KH_TrangChu");
        }
    }
}