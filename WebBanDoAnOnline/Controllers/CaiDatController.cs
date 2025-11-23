using System;
using System.Data.Linq;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class CaiDatController : Controller
    {
        private BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

        // GET: CaiDat
        public ActionResult Index()
        {
            return RedirectToAction("HoSo");
        }

        // GET: CaiDat/HoSo
        public ActionResult HoSo()
        {
            if (Session["TaiKhoan"] == null)
            {
                return RedirectToAction("Login", "TaiKhoan");
            }
            return View();
        }

        // POST: CaiDat/GetProfile
        [HttpPost]
        public string GetProfile()
        {
            try
            {
                if (Session["TaiKhoan"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
                }

                var sessionUser = Session["TaiKhoan"] as TaiKhoan;
                if (sessionUser == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Không tìm thấy người dùng" });
                }

                var user = db.TaiKhoans.FirstOrDefault(u => u.MaTK == sessionUser.MaTK);
                if (user == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Không tìm thấy tài khoản trong hệ thống" });
                }

                var data = new
                {
                    HoTen = user.HoTen,
                    Email = user.Email,
                    SoDienThoai = user.SoDienThoai,
                    Avatar = user.AnhDaiDien ?? "https://i.imgur.com/4Ym2k5N.png"
                };

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }

        // POST: CaiDat/SaveProfile
        [HttpPost]
        public string SaveProfile()
        {
            try
            {
                if (Session["TaiKhoan"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
                }

                var sessionUser = Session["TaiKhoan"] as TaiKhoan;
                if (sessionUser == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Không tìm thấy người dùng" });
                }

                var user = db.TaiKhoans.FirstOrDefault(u => u.MaTK == sessionUser.MaTK);
                if (user == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Không tìm thấy tài khoản trong hệ thống" });
                }

                user.HoTen = Request.Form["HoTen"];
                user.Email = Request.Form["Email"];
                user.SoDienThoai = Request.Form["SoDienThoai"];

                if (Request.Files.Count > 0)
                {
                    var file = Request.Files[0];
                    if (file != null && file.ContentLength > 0)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file.FileName);
                        string extension = Path.GetExtension(file.FileName);
                        fileName = fileName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;

                        string uploadPath = Server.MapPath("~/Uploads/Avatars/");
                        if (!Directory.Exists(uploadPath))
                        {
                            Directory.CreateDirectory(uploadPath);
                        }

                        string filePath = Path.Combine(uploadPath, fileName);
                        file.SaveAs(filePath);

                        user.AnhDaiDien = "/Uploads/Avatars/" + fileName;
                    }
                }

                db.SubmitChanges();
                Session["TaiKhoan"] = user;

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, message = "Cập nhật thành công" });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }

        public ActionResult BaoMat()
        {
            if (Session["TaiKhoan"] == null)
            {
                return RedirectToAction("Login", "TaiKhoan");
            }
            return View();
        }

        public ActionResult DiaChi()
        {
            if (Session["TaiKhoan"] == null)
            {
                return RedirectToAction("Login", "TaiKhoan");
            }
            return View();
        }

        public ActionResult DonMua()
        {
            if (Session["TaiKhoan"] == null)
            {
                return RedirectToAction("Login", "TaiKhoan");
            }
            return View();
        }

        // POST: API lấy danh sách đơn hàng theo trạng thái
        [HttpPost]
        public string GetOrders()
        {
            try
            {
                if (Session["TaiKhoan"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
                }

                var sessionUser = Session["TaiKhoan"] as TaiKhoan;
                string statusFilter = Request.Form["status"];

                // ✅ SỬ DỤNG DataLoadOptions để eager load dữ liệu liên quan
                var loadOptions = new DataLoadOptions();
                loadOptions.LoadWith<DonHang>(d => d.DiaChi);
                loadOptions.LoadWith<DonHang>(d => d.ChiTietDonHangs);
                loadOptions.LoadWith<ChiTietDonHang>(ct => ct.SanPham);
                db.LoadOptions = loadOptions;

                // Lấy danh sách đơn hàng của user
                var query = db.DonHangs.Where(d => d.MaTK == sessionUser.MaTK && (d.isDelete == null || d.isDelete == 0));

                // Lọc theo trạng thái
                if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "all")
                {
                    switch (statusFilter)
                    {
                        case "pending":
                            query = query.Where(d => d.TrangThai == "Đơn hàng đang được chuẩn bị");
                            break;
                        case "shipping":
                            query = query.Where(d => d.TrangThai == "Shipper nhận hàng thành công");
                            break;
                        case "delivering":
                            query = query.Where(d => d.TrangThai == "Đang giao hàng");
                            break;
                        case "completed":
                            query = query.Where(d => d.TrangThai == "Nhận hàng");
                            break;
                    }
                }

                // Sắp xếp theo ngày tạo mới nhất và ToList() ngay để load hết dữ liệu
                var orders = query.OrderByDescending(d => d.Create_at).ToList();

                // ✅ Map sang object JSON - Xử lý sau khi đã ToList()
                var data = orders.Select(d =>
                {
                    // ✅ LẤY THÔNG TIN ĐỊA CHỈ - SỬA TÊN PROPERTIES CHO ĐÚNG VỚI MODEL
                    var diaChiText = "Chưa có địa chỉ";
                    if (d.DiaChi != null)
                    {
                        // Giả sử model DiaChi có các property: TenNguoiNhan, SDT, DiaChi
                        // Bạn cần kiểm tra chính xác tên trong file BanDoAnOnline.designer.cs
                        var tenNguoiNhan = d.DiaChi.TenNguoiNhan ?? "";
                        var sdt = d.DiaChi.SDT ?? "";
                        var diaChi = d.DiaChi.DiaChi1 ?? ""; // Thường có suffix "1" nếu tên trùng với class
                        diaChiText = string.Format("{0} - {1}\n{2}", tenNguoiNhan, sdt, diaChi);
                    }

                    // Lấy danh sách sản phẩm
                    var sanPhams = d.ChiTietDonHangs
                        .Where(ct => ct.isDelete == null || ct.isDelete == 0)
                        .Select(ct =>
                        {
                            var tenSP = "N/A";
                            var anh = "/Content/Images/default-food.jpg";

                            if (ct.SanPham != null)
                            {
                                tenSP = ct.SanPham.TenSP ?? "N/A";
                                if (!string.IsNullOrEmpty(ct.SanPham.Anh))
                                {
                                    anh = ct.SanPham.Anh;
                                }
                            }

                            var soLuong = 0;
                            var donGia = 0m;

                            if (ct.SoLuong.HasValue)
                            {
                                soLuong = ct.SoLuong.Value;
                            }

                            donGia = ct.DonGia; // No need to check for .HasValue or use .Value, since DonGia is of type decimal (not nullable)

                            return new
                            {
                                TenSP = tenSP,
                                SoLuong = soLuong,
                                DonGia = donGia,
                                Anh = anh
                            };
                        })
                        .ToList();

                    return new
                    {
                        MaDH = d.MaDH,
                        MaVanDon = "AJX" + d.MaDH.ToString("D5") + "M",
                        TrangThai = d.TrangThai ?? "Chưa xác nhận",
                        TongTien = d.TongTien, // ✅ TongTien là decimal, không cần .HasValue
                        NgayTao = d.Create_at.HasValue ? d.Create_at.Value.ToString("dd/MM/yyyy HH:mm") : "",
                        DiaChiGiaoHang = diaChiText,
                        SanPhams = sanPhams
                    };
                }).ToList();

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }

        public ActionResult KhuyenMai()
        {
            if (Session["TaiKhoan"] == null)
            {
                return RedirectToAction("Login", "TaiKhoan");
            }
            return View();
        }

        public ActionResult XoaTaiKhoan()
        {
            if (Session["TaiKhoan"] == null)
            {
                return RedirectToAction("Login", "TaiKhoan");
            }
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (db != null)
                {
                    db.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        // POST: CaiDat/ChangePassword
        [HttpPost]
        public string ChangePassword()
        {
            try
            {
                if (Session["TaiKhoan"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
                }

                var sessionUser = Session["TaiKhoan"] as TaiKhoan;
                if (sessionUser == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Không tìm thấy người dùng" });
                }

                var user = db.TaiKhoans.FirstOrDefault(u => u.MaTK == sessionUser.MaTK);
                if (user == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Không tìm thấy tài khoản trong hệ thống" });
                }

                string currentPassword = Request.Form["CurrentPassword"];
                string newPassword = Request.Form["NewPassword"];
                string confirmPassword = Request.Form["ConfirmPassword"];

                bool isPasswordCorrect = false;

                if (user.MatKhau == currentPassword)
                {
                    isPasswordCorrect = true;
                }
                else
                {
                    try
                    {
                        if (BCrypt.Net.BCrypt.Verify(currentPassword, user.MatKhau))
                        {
                            isPasswordCorrect = true;
                        }
                    }
                    catch { }
                }

                if (!isPasswordCorrect)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Mật khẩu hiện tại không đúng" });
                }

                if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Mật khẩu mới phải có ít nhất 6 ký tự" });
                }

                if (newPassword != confirmPassword)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Mật khẩu xác nhận không khớp" });
                }

                if (currentPassword == newPassword)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Mật khẩu mới không được giống mật khẩu hiện tại" });
                }

                user.MatKhau = BCrypt.Net.BCrypt.HashPassword(newPassword);
                db.SubmitChanges();

                Session["TaiKhoan"] = user;

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, message = "Đổi mật khẩu thành công" });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // GET: Lấy danh sách địa chỉ
        [HttpPost]
        public string GetDiaChi()
        {
            try
            {
                if (Session["TaiKhoan"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
                }

                var sessionUser = Session["TaiKhoan"] as TaiKhoan;

                var danhSachDiaChi = db.DiaChis
                    .Where(d => d.MaTK == sessionUser.MaTK && (d.isDelete == null || d.isDelete == 0))
                    .OrderByDescending(d => d.MacDinh)
                    .ThenByDescending(d => d.Create_at)
                    .Select(d => new
                    {
                        MaDC = d.MaDiaChi,
                        TenNguoiNhan = d.TenNguoiNhan,
                        SDT = d.SDTNhan,
                        DiaChi = d.DiaChiCuThe,
                        MacDinh = d.MacDinh ?? false
                    })
                    .ToList();

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, data = danhSachDiaChi });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }

        // POST: Thêm địa chỉ mới
        [HttpPost]
        public string ThemDiaChi()
        {
            try
            {
                if (Session["TaiKhoan"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
                }

                var sessionUser = Session["TaiKhoan"] as TaiKhoan;

                var tenNguoiNhan = Request.Form["TenNguoiNhan"];
                var sdt = Request.Form["SDT"];
                var diaChi = Request.Form["DiaChi"];
                var macDinh = Request.Form["MacDinh"] == "true";

                // Nếu đặt làm mặc định, bỏ mặc định các địa chỉ khác
                if (macDinh)
                {
                    var diaChiCu = db.DiaChis.Where(d => d.MaTK == sessionUser.MaTK && d.MacDinh == true);
                    foreach (var dc in diaChiCu)
                    {
                        dc.MacDinh = false;
                    }
                }

                var diaChiMoi = new DiaChi
                {
                    MaTK = sessionUser.MaTK,
                    TenNguoiNhan = tenNguoiNhan,
                    SDTNhan = sdt,              // ✅ Sửa từ SDT thành SDTNhan
                    DiaChiCuThe = diaChi,        // ✅ Sửa từ DiaChi1 thành DiaChiCuThe
                    MacDinh = macDinh,
                    Create_at = DateTime.Now,
                    isDelete = 0
                };

                db.DiaChis.InsertOnSubmit(diaChiMoi);
                db.SubmitChanges();

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, message = "Thêm địa chỉ thành công" });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }

        // POST: Sửa địa chỉ
        [HttpPost]
        public string SuaDiaChi()
        {
            try
            {
                if (Session["TaiKhoan"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
                }

                var sessionUser = Session["TaiKhoan"] as TaiKhoan;
                var maDC = int.Parse(Request.Form["MaDC"]);

                var diaChi = db.DiaChis.FirstOrDefault(d => d.MaDiaChi == maDC && d.MaTK == sessionUser.MaTK);

                if (diaChi == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Không tìm thấy địa chỉ" });
                }

                // ✅ Chỉ cập nhật khi có dữ liệu mới
                if (!string.IsNullOrEmpty(Request.Form["TenNguoiNhan"]))
                {
                    diaChi.TenNguoiNhan = Request.Form["TenNguoiNhan"];
                }
                if (!string.IsNullOrEmpty(Request.Form["SDT"]))
                {
                    diaChi.SDTNhan = Request.Form["SDT"];  // ✅ Sửa từ SDT thành SDTNhan
                }
                if (!string.IsNullOrEmpty(Request.Form["DiaChi"]))
                {
                    diaChi.DiaChiCuThe = Request.Form["DiaChi"];  // ✅ Sửa từ DiaChi1 thành DiaChiCuThe
                }

                var macDinh = Request.Form["MacDinh"] == "true";

                // Nếu đặt làm mặc định, bỏ mặc định các địa chỉ khác
                if (macDinh && (!diaChi.MacDinh.HasValue || !diaChi.MacDinh.Value))
                {
                    var diaChiCu = db.DiaChis.Where(d => d.MaTK == sessionUser.MaTK && d.MacDinh == true && d.MaDiaChi != maDC);
                    foreach (var dc in diaChiCu)
                    {
                        dc.MacDinh = false;
                    }
                }

                diaChi.MacDinh = macDinh;
                diaChi.Update_at = DateTime.Now;

                db.SubmitChanges();

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, message = "Cập nhật địa chỉ thành công" });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }

        // POST: Xóa địa chỉ
        [HttpPost]
        public string XoaDiaChi()
        {
            try
            {
                if (Session["TaiKhoan"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
                }

                var sessionUser = Session["TaiKhoan"] as TaiKhoan;
                var maDC = int.Parse(Request.Form["MaDC"]);

                var diaChi = db.DiaChis.FirstOrDefault(d => d.MaDiaChi == maDC && d.MaTK == sessionUser.MaTK);

                if (diaChi == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Không tìm thấy địa chỉ" });
                }

                diaChi.isDelete = 1;
                diaChi.Update_at = DateTime.Now;
                db.SubmitChanges();

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, message = "Xóa địa chỉ thành công" });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }

        // GET: Lấy danh sách voucher của user
        [HttpPost]
        public string GetVoucherList()
        {
            try
            {
                if (Session["TaiKhoan"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
                }

                var sessionUser = Session["TaiKhoan"] as TaiKhoan;
                var now = DateTime.Now;

                // Lấy tất cả voucher còn hạn hoặc đã hết hạn (chưa xóa)
                var vouchers = db.Vouchers
                    .Where(v => (v.isDelete == null || v.isDelete == 0))
                    .OrderByDescending(v => v.NgayBatDau)
                    .ToList();

                var data = vouchers.Select(v =>
                {
                    string trangThai = "ConHan";
                    if (now < v.NgayBatDau || now > v.NgayKetThuc)
                    {
                        trangThai = "HetHan";
                    }
                    else if (v.SoLuotConLai <= 0)
                    {
                        trangThai = "HetLuot";
                    }

                    // Định dạng giá trị giảm
                    string giaTriText = v.LoaiGiam == "Percent"
                        ? "Giảm " + v.GiaTri + "%"
                        : "Giảm " + v.GiaTri.ToString("N0") + "đ";

                    // Điều kiện tối thiểu
                    string dieuKienText = v.DieuKienToiThieu.HasValue
                        ? " (Đơn tối thiểu " + v.DieuKienToiThieu.Value.ToString("N0") + "đ)"
                        : "";

                    return new
                    {
                        MaVoucher = v.MaVoucher,
                        TenVoucher = v.TenVoucher,
                        MaCode = v.MaCode,
                        ThoiGian = v.NgayBatDau.ToString("dd/MM/yyyy") + " - " + v.NgayKetThuc.ToString("dd/MM/yyyy"),
                        GiaTriText = giaTriText + dieuKienText,
                        TrangThai = trangThai,
                        SoLuotConLai = v.SoLuotConLai ?? 0
                    };
                }).ToList();

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }

        // POST: Lưu voucher (Kiểm tra mã có tồn tại không)
        [HttpPost]
        public string LuuVoucher()
        {
            try
            {
                if (Session["TaiKhoan"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
                }

                var maCode = Request.Form["maCode"];

                if (string.IsNullOrEmpty(maCode))
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Vui lòng nhập mã voucher" });
                }

                // Tìm voucher theo mã code
                var voucher = db.Vouchers.FirstOrDefault(v =>
                    v.MaCode == maCode &&
                    (v.isDelete == null || v.isDelete == 0));

                if (voucher == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Mã voucher không tồn tại" });
                }

                var now = DateTime.Now;

                // Kiểm tra thời gian hiệu lực
                if (now < voucher.NgayBatDau)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Voucher chưa đến thời gian sử dụng" });
                }

                if (now > voucher.NgayKetThuc)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Voucher đã hết hạn" });
                }

                // Kiểm tra số lượt sử dụng
                if (voucher.SoLuotConLai <= 0)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Voucher đã hết lượt sử dụng" });
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, message = "Lưu mã voucher thành công! Bạn có thể sử dụng khi đặt hàng." });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }
        // POST: Xác nhận xóa tài khoản (Soft Delete)
        [HttpPost]
        public string XacNhanXoaTaiKhoan()
        {
            try
            {
                if (Session["TaiKhoan"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
                }

                var sessionUser = Session["TaiKhoan"] as TaiKhoan;
                var password = Request.Form["password"];

                if (string.IsNullOrEmpty(password))
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Vui lòng nhập mật khẩu" });
                }

                // Lấy thông tin user từ database
                var user = db.TaiKhoans.FirstOrDefault(u => u.MaTK == sessionUser.MaTK && (u.isDelete == null || u.isDelete == 0));
                if (user == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Không tìm thấy tài khoản hoặc tài khoản đã bị vô hiệu hóa" });
                }

                // Xác thực mật khẩu
                bool isPasswordCorrect = false;
                if (user.MatKhau == password)
                {
                    isPasswordCorrect = true;
                }
                else
                {
                    try
                    {
                        if (BCrypt.Net.BCrypt.Verify(password, user.MatKhau))
                        {
                            isPasswordCorrect = true;
                        }
                    }
                    catch { }
                }

                if (!isPasswordCorrect)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Mật khẩu không đúng" });
                }

                // ✅ BẮT ĐẦU XÓA MỀM (SOFT DELETE)
                var maTK = user.MaTK;
                var now = DateTime.Now;

                // 1. Soft delete giỏ hàng
                var gioHangs = db.GioHangs.Where(g => g.MaTK == maTK && (g.isDelete == null || g.isDelete == 0)).ToList();
                foreach (var gh in gioHangs)
                {
                    gh.isDelete = 1;
                    if (gh.GetType().GetProperty("Update_at") != null)
                    {
                        gh.Update_at = now;
                    }
                }

                // 2. Soft delete địa chỉ
                var diaChis = db.DiaChis.Where(d => d.MaTK == maTK && (d.isDelete == null || d.isDelete == 0)).ToList();
                foreach (var dc in diaChis)
                {
                    dc.isDelete = 1;
                    if (dc.GetType().GetProperty("Update_at") != null)
                    {
                        dc.Update_at = now;
                    }
                }

                // 3. Soft delete đánh giá
                var danhGias = db.DanhGias.Where(dg => dg.MaTK == maTK && (dg.isDelete == null || dg.isDelete == 0)).ToList();
                foreach (var dg in danhGias)
                {
                    dg.isDelete = 1;
                    if (dg.GetType().GetProperty("Update_at") != null)
                    {
                        dg.Update_at = now;
                    }
                }

                // 4. ✅ XỬ LÝ THÔNG BÁO - KIỂM TRA isDelete TỒN TẠI
                var thongBaos = db.ThongBaos.Where(tb => tb.MaTK == maTK).ToList();
                foreach (var tb in thongBaos)
                {
                    // Kiểm tra nếu có thuộc tính isDelete
                    var isDeleteProp = tb.GetType().GetProperty("isDelete");
                    if (isDeleteProp != null)
                    {
                        isDeleteProp.SetValue(tb, (byte)1);
                    }
                    
                    // Kiểm tra nếu có thuộc tính Update_at
                    var updateAtProp = tb.GetType().GetProperty("Update_at");
                    if (updateAtProp != null)
                    {
                        updateAtProp.SetValue(tb, now);
                    }
                }

                // 5. Soft delete đơn hàng và dữ liệu liên quan
                var donHangs = db.DonHangs.Where(dh => dh.MaTK == maTK && (dh.isDelete == null || dh.isDelete == 0)).ToList();
                foreach (var dh in donHangs)
                {
                    dh.isDelete = 1;
                    if (dh.GetType().GetProperty("Update_at") != null)
                    {
                        dh.Update_at = now;
                    }

                    // ✅ XỬ LÝ CHI TIẾT ĐỢN HÀNG
                    var chiTiets = db.ChiTietDonHangs.Where(ct => ct.MaDH == dh.MaDH).ToList();
                    foreach (var ct in chiTiets)
                    {
                        var isDeleteProp = ct.GetType().GetProperty("isDelete");
                        if (isDeleteProp != null)
                        {
                            isDeleteProp.SetValue(ct, (byte)1);
                        }
                        
                        var updateAtProp = ct.GetType().GetProperty("Update_at");
                        if (updateAtProp != null)
                        {
                            updateAtProp.SetValue(ct, now);
                        }
                    }

                    // ✅ XỬ LÝ LỊCH SỬ TRẠNG THÁI
                    var lichSu = db.LichSuTrangThais.Where(ls => ls.MaDH == dh.MaDH).ToList();
                    foreach (var ls in lichSu)
                    {
                        var isDeleteProp = ls.GetType().GetProperty("isDelete");
                        if (isDeleteProp != null)
                        {
                            isDeleteProp.SetValue(ls, (byte)1);
                        }
                        
                        var updateAtProp = ls.GetType().GetProperty("Update_at");
                        if (updateAtProp != null)
                        {
                            updateAtProp.SetValue(ls, now);
                        }
                    }
                }

                // 6. ✅ Cuối cùng - Soft delete tài khoản
                user.isDelete = 1;
                user.Delete_at = now;

                // Lưu tất cả thay đổi vào database
                db.SubmitChanges();

                // Xóa session và đăng xuất
                Session.Clear();
                Session.Abandon();

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { 
                    success = true, 
                    message = "Tài khoản của bạn đã bị vô hiệu hóa.\n\nEmail và số điện thoại của bạn sẽ không thể sử dụng để đăng ký lại." 
                });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }

}