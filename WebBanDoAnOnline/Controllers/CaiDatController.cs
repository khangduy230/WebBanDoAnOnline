using Newtonsoft.Json;
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
                return RedirectToAction("DangNhap", "TaiKhoan");
            }
            return View();
        }

        // POST: CaiDat/GetProfile (CaiDat/LayHoSo)
        
        public string LayHoSo()
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
                    Avatar = user.AnhDaiDien ?? "/img/default-avatar.png"
                };

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }

        // POST: CaiDat/SaveProfile (CaiDat/LuuHoSo)
        [HttpPost]
        public string LuuHoSo()
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
                        string fileName = Path.GetFileNameWithoutExtension(file.FileName);    // Tên file không có đuôi
                        string extension = Path.GetExtension(file.FileName);                  // Đuôi file (.jpg, .png, ...)
                        fileName = fileName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;  // Thêm timestamp để tránh trùng tên

                        string uploadPath = Server.MapPath("~/img/");  // Thư mục lưu ảnh đại diện
                        if (!Directory.Exists(uploadPath))
                        {
                            Directory.CreateDirectory(uploadPath);
                        }

                        string filePath = Path.Combine(uploadPath, fileName);  // Đường dẫn đầy đủ
                        file.SaveAs(filePath);

                        user.AnhDaiDien = "/img/" + fileName;
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
                return RedirectToAction("DangNhap", "TaiKhoan");
            }
            return View();
        }

        public ActionResult DiaChi()
        {
            if (Session["TaiKhoan"] == null)
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }
            return View();
        }

        public ActionResult DonMua()
        {
            if (Session["TaiKhoan"] == null)
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }
            return View();
        }

        // POST: API lấy danh sách đơn hàng theo trạng thái
        // LayDonMua
        public string LayDonMua()
        {
            try
            {
                if (Session["TaiKhoan"] == null) return JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
                var sessionUser = Session["TaiKhoan"] as TaiKhoan;
                string statusFilter = Request.Form["status"];

                // Tối ưu query
                var loadOptions = new DataLoadOptions();
                loadOptions.LoadWith<DonHang>(d => d.ChiTietDonHangs);
                loadOptions.LoadWith<ChiTietDonHang>(ct => ct.SanPham);
                db.LoadOptions = loadOptions;

                // Query cơ bản
                var query = db.DonHangs.Where(d => d.MaTK == sessionUser.MaTK && (d.isDelete == null || d.isDelete == 0));

                // Lọc theo trạng thái
                if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "all")
                {
                    switch (statusFilter)
                    {
                        case "pending": query = query.Where(d => d.TrangThai == "Chờ xác nhận"); break;
                        case "shipping": query = query.Where(d => d.TrangThai == "Đã xác nhận" || d.TrangThai == "Đang giao"); break;
                        case "completed": query = query.Where(d => d.TrangThai == "Đã nhận hàng"); break;
                        case "cancelled": query = query.Where(d => d.TrangThai == "Đã hủy"); break;
                    }
                }

                var list = query.OrderByDescending(d => d.Create_at).ToList();

                // Lấy danh sách các mã đơn hàng để tối ưu truy vấn
                var listMaDH = list.Select(x => x.MaDH).ToList();

                // Lấy tất cả đánh giá của các đơn hàng này vào bộ nhớ (để tránh query lặp trong vòng lặp)
                var listDanhGia = db.DanhGias.Where(dg => listMaDH.Contains(dg.MaDH ?? 0) && (dg.isDelete == 0 || dg.isDelete == null)).ToList();

                var data = list.Select(d => new
                {
                    MaDH = d.MaDH,
                    MaVanDon = "DH" + d.MaDH.ToString("D6"),
                    TrangThai = d.TrangThai,
                    TongTien = d.TongTien,
                    NgayTao = d.Create_at.HasValue ? d.Create_at.Value.ToString("dd/MM/yyyy HH:mm") : "",
                    DiaChiGiaoHang = "Giao đến địa chỉ mặc định", // Hoặc query bảng DiaChi nếu cần

                    SanPhams = d.ChiTietDonHangs.Select(ct => new {
                        MaSP = ct.MaSP,
                        TenSP = ct.TenSP,
                        SoLuong = ct.SoLuong,
                        DonGia = ct.DonGia,
                        Anh = (ct.SanPham != null && !string.IsNullOrEmpty(ct.SanPham.Anh))
                                ? ct.SanPham.Anh.Replace("~", "")
                                : "/img/no-image.jpg",

                        // --- QUAN TRỌNG: Kiểm tra xem sản phẩm này trong đơn này đã đánh giá chưa ---
                        DaDanhGia = listDanhGia.Any(dg => dg.MaDH == d.MaDH && dg.MaSP == ct.MaSP)
                    }).ToList()
                });

                return JsonConvert.SerializeObject(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }



        // --- API 3: GỬI ĐÁNH GIÁ (Mới thêm) ---
        // --- API 3: GỬI ĐÁNH GIÁ (Đã sửa lỗi và thêm tính điểm TB) ---

        [HttpPost]
        public string GuiDanhGia()
        {
            try
            {
                if (Session["TaiKhoan"] == null)
                    return JsonConvert.SerializeObject(new { success = false, message = "Vui lòng đăng nhập." });

                var user = Session["TaiKhoan"] as TaiKhoan;

                int maDH = 0, maSP = 0, soSao = 5;
                int.TryParse(Request.Form["MaDH"], out maDH);
                int.TryParse(Request.Form["MaSP"], out maSP);
                int.TryParse(Request.Form["SoSao"], out soSao);
                string binhLuan = Request.Form["BinhLuan"];

                using (var context = new BanDoAnOnlineDataContext()) // Khởi tạo context mới để tránh conflict
                {
                    // 1. Check đơn hàng hợp lệ
                    var dh = context.DonHangs.FirstOrDefault(d => d.MaDH == maDH && d.MaTK == user.MaTK && d.TrangThai == "Đã nhận hàng");
                    if (dh == null) return JsonConvert.SerializeObject(new { success = false, message = "Đơn hàng không hợp lệ hoặc chưa hoàn thành." });

                    // 2. Check đã đánh giá chưa
                    var check = context.DanhGias.FirstOrDefault(x => x.MaDH == maDH && x.MaSP == maSP);
                    if (check != null) return JsonConvert.SerializeObject(new { success = false, message = "Bạn đã đánh giá sản phẩm này rồi." });

                    // 3. Insert đánh giá
                    var dg = new DanhGia
                    {
                        MaTK = user.MaTK,
                        MaSP = maSP,
                        MaDH = maDH,
                        SoSao = soSao,
                        BinhLuan = binhLuan,
                        Create_at = DateTime.Now,
                        isDelete = 0
                    };
                    context.DanhGias.InsertOnSubmit(dg);
                    context.SubmitChanges(); // Lưu đánh giá trước

                    // 4. Tính điểm TB (Quan trọng: Tính lại toàn bộ đánh giá của SP đó)
                    var sp = context.SanPhams.FirstOrDefault(s => s.MaSP == maSP);
                    if (sp != null)
                    {
                        // Lấy list sao, bỏ qua các đánh giá bị xóa
                        var ratings = context.DanhGias
                            .Where(r => r.MaSP == maSP && (r.isDelete == 0 || r.isDelete == null))
                            .Select(r => r.SoSao)
                            .ToList();

                        if (ratings.Any())
                        {
                            sp.SoLuotDanhGia = ratings.Count;
                            sp.DiemDanhGia = ratings.Average(r => (double)r);
                        }
                        context.SubmitChanges();
                    }
                }

                return JsonConvert.SerializeObject(new { success = true, message = "Cảm ơn bạn đã đánh giá!" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        public ActionResult KhuyenMai()
        {
            if (Session["TaiKhoan"] == null)
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }
            return View();
        }

        public ActionResult XoaTaiKhoan()
        {
            if (Session["TaiKhoan"] == null)
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }
            return View();
        }

        // Dispose pattern để giải phóng tài nguyên
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

        // POST: CaiDat/ChangePassword (Đổi mật khẩu)
        [HttpPost]
        public string DoiMatKhau()
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

                string currentPassword = Request.Form["currentPassword"];
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

        // GET: Lấy danh sách địa chỉ (LayDiaChi)
        [HttpPost]
        public string LayDiaChi()
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
                    SDTNhan = sdt,              
                    DiaChiCuThe = diaChi,        
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

                
                if (!string.IsNullOrEmpty(Request.Form["TenNguoiNhan"]))
                {
                    diaChi.TenNguoiNhan = Request.Form["TenNguoiNhan"];
                }
                if (!string.IsNullOrEmpty(Request.Form["SDT"]))
                {
                    diaChi.SDTNhan = Request.Form["SDT"];  
                }
                if (!string.IsNullOrEmpty(Request.Form["DiaChi"]))
                {
                    diaChi.DiaChiCuThe = Request.Form["DiaChi"];  
                }

                var macDinh = Request.Form["MacDinh"] == "true";

                
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

        // GET: Lấy danh sách voucher của user (LayDSVoucher)
        [HttpPost]
        public string LayDSVoucher()
        {
            try
            {
                if (Session["TaiKhoan"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
                }

                var sessionUser = Session["TaiKhoan"] as TaiKhoan;
                var now = DateTime.Now;
                string searchTerm = Request.Form["searchTerm"]?.ToLower().Trim() ?? "";

                var vouchersQuery = db.Vouchers
                    .Where(v => (v.isDelete == null || v.isDelete == 0));

                // Logic tìm kiếm nâng cao
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    bool isNumeric = decimal.TryParse(searchTerm, out decimal numericValue);

                    vouchersQuery = vouchersQuery.Where(v =>
                        // 1. Tìm kiếm theo tên hoặc mã code
                        v.MaCode.ToLower().Contains(searchTerm) ||
                        v.TenVoucher.ToLower().Contains(searchTerm) ||

                        // 2. Nếu từ khóa là số, tìm theo giá trị
                        (isNumeric && v.GiaTri == numericValue)
                    );
                }

                var vouchers = vouchersQuery
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

        //  Lưu voucher (Kiểm tra mã có tồn tại không)
        
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
        //  Xác nhận xóa tài khoản 
        
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

                // 4.  XỬ LÝ THÔNG BÁO - KIỂM TRA isDelete TỒN TẠI
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

                    //  XỬ LÝ CHI TIẾT ĐỢN HÀNG
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

                    //  XỬ LÝ LỊCH SỬ TRẠNG THÁI
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

                // 6. Cuối cùng - Soft delete tài khoản
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
        //  Hủy đơn hàng (Chỉ áp dụng cho đơn 'Chờ xác nhận')
        
        public string HuyDonHang()
        {
            try
            {
                // 1. Kiểm tra đăng nhập
                if (Session["TaiKhoan"] == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Vui lòng đăng nhập lại." });
                }

                var sessionUser = Session["TaiKhoan"] as TaiKhoan;

                // 2. Lấy ID đơn hàng từ request
                int maDH;
                if (!int.TryParse(Request.Form["id"], out maDH))
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Dữ liệu không hợp lệ." });
                }

                // 3. Tìm đơn hàng 
                var dh = db.DonHangs.FirstOrDefault(d => d.MaDH == maDH && d.MaTK == sessionUser.MaTK);

                if (dh == null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                // 4. Kiểm tra trạng thái hiện tại
                if (dh.TrangThai != "Chờ xác nhận")
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Đơn hàng đã được xử lý, không thể hủy. Vui lòng liên hệ cửa hàng." });
                }

                // 5. Cập nhật trạng thái
                string trangThaiCu = dh.TrangThai;
                dh.TrangThai = "Đã hủy";
                dh.Update_at = DateTime.Now;

                // 6. Ghi lại lịch sử thay đổi trạng thái
                LichSuTrangThai lichSu = new LichSuTrangThai
                {
                    MaDH = dh.MaDH,
                    TrangThaiCu = trangThaiCu,
                    TrangThaiMoi = "Đã hủy",
                    ThoiGian = DateTime.Now,
                    GhiChu = "Khách hàng chủ động hủy đơn",
                    Create_at = DateTime.Now
                };
                db.LichSuTrangThais.InsertOnSubmit(lichSu);

                // 7. Lưu thay đổi
                db.SubmitChanges();

                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = true, message = "Đã hủy đơn hàng thành công!" });
            }
            catch (Exception ex)
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }

}