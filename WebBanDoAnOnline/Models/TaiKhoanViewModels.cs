using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace WebBanDoAnOnline.Models
{
    public class TaiKhoanViewModels
    {
        public class LoginViewModel
        {
            [Required(ErrorMessage = "Vui lòng nhập tài khoản")]
            [Display(Name = "Tài khoản")]
            public string TaiKhoan { get; set; } // dùng cho email/sđt/TenTK

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu")]
            public string MatKhau { get; set; } 

        }
        public class RegisterViewModel
        {
            [Required(ErrorMessage = "Vui lòng nhập họ tên")]
            [Display(Name = "Họ và tên")]
            public string HoTen { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập tên tài khoản")]
            [Display(Name = "Tên tài khoản (dùng để đăng nhập)")]
            [StringLength(15, ErrorMessage = "Tên tài khoản không quá 15 ký tự")]
            public string TenTK { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
            [Display(Name = "Số điện thoại")]
            public string SoDienThoai { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập email")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
            [StringLength(100, ErrorMessage = "{0} phải có ít nhất {2} ký tự.", MinimumLength = 6)] // Đã cập nhật
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu")]
            public string MatKhau { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            [Compare("MatKhau", ErrorMessage = "Mật khẩu và mật khẩu xác nhận không khớp.")]
            public string ConfirmMatKhau { get; set; }
        }
    }
}