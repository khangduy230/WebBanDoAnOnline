using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;
using Newtonsoft.Json;

namespace WebBanDoAnOnline.Controllers
{
    public class ThanhToanController : Controller
    {
        // 1. Mở trang thanh toán (Chỉ trả về View rỗng)
        public ActionResult Index()
        {
            if (Session["TaiKhoan"] == null)
            {
                return RedirectToAction("Login", "TaiKhoan");
            }
            return View();
        }

        // 2. Trang báo thành công
        public ActionResult Success()
        {
            return View();
        }

        // =============================================
        // API 1: Lấy dữ liệu render trang thanh toán
        // =============================================
       
        // Trong ThanhToanController.cs

     
        public string LayThongTinThanhToan()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null) return "LOGIN_REQUIRED";

            // 1. Lấy danh sách địa chỉ (Giữ nguyên)
            var dsDiaChi = db.DiaChis
                                .Where(d => d.MaTK == user.MaTK && (d.isDelete == null || d.isDelete == 0))
                                .Select(d => new { d.MaDiaChi, d.TenNguoiNhan, d.SDTNhan, d.DiaChiCuThe, d.MacDinh })
                                .OrderByDescending(d => d.MacDinh).ToList();

            // 2. Lấy giỏ hàng & Tính tiền (Giữ nguyên logic cũ)
            var cartItems = (from g in db.GioHangs
                             join p in db.SanPhams on g.MaSP equals p.MaSP
                             where g.MaTK == user.MaTK && (g.isDelete == 0 || g.isDelete == null)
                             select new { g, p }).ToList();

            decimal tongTienHang = 0;
            var listProducts = new List<object>();
            DateTime now = DateTime.Now;

            foreach (var item in cartItems)
            {
                decimal giaGoc = item.p.Gia ?? 0;
                decimal giaKM = item.p.GiaKhuyenMai ?? 0;
                decimal giaBan = giaGoc;

                if (giaKM > 0 && giaKM < giaGoc &&
                    (!item.p.NgayBatDauKM.HasValue || item.p.NgayBatDauKM <= now) &&
                    (!item.p.NgayKetThucKM.HasValue || item.p.NgayKetThucKM >= now))
                {
                    giaBan = giaKM;
                }

                decimal thanhTien = giaBan * (item.g.SoLuong ?? 1);
                tongTienHang += thanhTien;

                listProducts.Add(new { TenSP = item.p.TenSP, SoLuong = item.g.SoLuong, DonGia = giaBan, ThanhTien = thanhTien });
            }

            decimal phiShip = (tongTienHang > 500000) ? 0 : 25000;

            // --- THÊM MỚI: LẤY DANH SÁCH VOUCHER ---
            var dsVoucher = db.Vouchers
                .Where(v => (v.isDelete == null || v.isDelete == 0) // Chưa xóa
                            && v.NgayBatDau <= now                  // Đã bắt đầu
                            && v.NgayKetThuc >= now                 // Chưa hết hạn
                            && (v.SoLuotConLai > 0 || v.SoLuotConLai == null)) // Còn lượt
                .Select(v => new
                {
                    v.MaCode,
                    v.TenVoucher,
                    v.GiaTri,
                    v.LoaiGiam,
                    v.DieuKienToiThieu,
                    v.MoTaThem,
                    HanSuDung = v.NgayKetThuc
                })
                .OrderBy(v => v.DieuKienToiThieu)
                .ToList();

            var result = new
            {
                DiaChi = dsDiaChi,
                SanPham = listProducts,
                TongTienHang = tongTienHang,
                PhiShip = phiShip,
                TongThanhToan = tongTienHang + phiShip,

                // Trả về danh sách voucher
                DanhSachVoucher = dsVoucher
            };

            return JsonConvert.SerializeObject(result);
        }

        // =============================================
        // API 2: Kiểm tra Voucher
        // =============================================
        [HttpPost]
        public string KiemTraVoucher()
        {
            string code = Request["code"];
            string total_str = Request["total"]; // Tổng tiền hàng hiện tại

            if (string.IsNullOrEmpty(code)) return JsonConvert.SerializeObject(new { ok = false, msg = "Chưa nhập mã" });

            decimal subtotal = decimal.Parse(total_str);
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

            // Tìm voucher còn hạn, còn lượt dùng
            DateTime now = DateTime.Now;
            var vc = db.Vouchers.FirstOrDefault(v => v.MaCode == code
                                                  && v.NgayBatDau <= now
                                                  && v.NgayKetThuc >= now
                                                  && (v.SoLuotConLai > 0 || v.SoLuotConLai == null)
                                                  && (v.isDelete == 0 || v.isDelete == null));

            if (vc != null)
            {
                // Kiểm tra điều kiện tối thiểu
                if (vc.DieuKienToiThieu > 0 && subtotal < vc.DieuKienToiThieu)
                {
                    return JsonConvert.SerializeObject(new { ok = false, msg = "Đơn hàng chưa đủ " + vc.DieuKienToiThieu?.ToString("#,0") + "đ để dùng mã này." });
                }

                decimal discount = 0;
                if (vc.LoaiGiam == "Tiền")
                {
                    discount = vc.GiaTri;
                }
                else // Phần trăm
                {
                    discount = (subtotal * vc.GiaTri) / 100;
                }

                // Đảm bảo không giảm quá tổng tiền
                if (discount > subtotal) discount = subtotal;

                return JsonConvert.SerializeObject(new { ok = true, discount = discount, msg = "Áp dụng thành công!" });
            }

            return JsonConvert.SerializeObject(new { ok = false, msg = "Mã giảm giá không tồn tại hoặc đã hết hạn." });
        }

        // =============================================
        // API 3: Đặt hàng (PlaceOrder)
        // =============================================
        [HttpPost]
        public string DatHang()
        {
            try
            {
                var user = Session["TaiKhoan"] as TaiKhoan;
                if (user == null) return "LOGIN_REQUIRED";

                // 1. Nhận tham số từ FormData
                string addressId_str = Request["addressId"];
                string payMethod = Request["payMethod"];
                string note = Request["note"];
                string voucherCode = Request["voucherCode"];

                if (string.IsNullOrEmpty(addressId_str)) return "Vui lòng chọn địa chỉ giao hàng.";
                int addressId = int.Parse(addressId_str);

                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

                // 2. Lấy lại giỏ hàng để tính toán lần cuối (Backend phải tự tính lại tiền)
                var cartItems = db.GioHangs.Where(g => g.MaTK == user.MaTK && (g.isDelete == 0 || g.isDelete == null)).ToList();
                if (!cartItems.Any()) return "Giỏ hàng trống, không thể thanh toán.";

                decimal subTotal = 0;
                DateTime now = DateTime.Now;

                foreach (var item in cartItems)
                {
                    var sp = db.SanPhams.Single(p => p.MaSP == item.MaSP);
                    decimal gia = sp.Gia ?? 0;

                    // Check giá KM
                    if (sp.GiaKhuyenMai > 0 && sp.GiaKhuyenMai < gia &&
                        (!sp.NgayBatDauKM.HasValue || sp.NgayBatDauKM <= now) &&
                        (!sp.NgayKetThucKM.HasValue || sp.NgayKetThucKM >= now))
                    {
                        gia = sp.GiaKhuyenMai ?? 0;
                    }
                    subTotal += gia * (item.SoLuong ?? 1);
                }

                // 3. Tính phí & Voucher
                decimal shipping = (subTotal > 500000) ? 0 : 25000;
                decimal discount = 0;
                int? maVoucher = null;

                if (!string.IsNullOrEmpty(voucherCode))
                {
                    var vc = db.Vouchers.FirstOrDefault(v => v.MaCode == voucherCode);
                    if (vc != null)
                    {
                        if (vc.LoaiGiam == "Tiền") discount = vc.GiaTri;
                        else discount = (subTotal * vc.GiaTri) / 100;

                        if (discount > subTotal) discount = subTotal;
                        maVoucher = vc.MaVoucher;

                        // Trừ lượt dùng voucher
                        if (vc.SoLuotConLai > 0) vc.SoLuotConLai -= 1;
                        vc.SoLuotDaSuDung = (vc.SoLuotDaSuDung ?? 0) + 1;
                    }
                }

                // 4. Tạo Đơn hàng
                DonHang dh = new DonHang();
                dh.MaTK = user.MaTK;
                dh.MaDiaChi = addressId;
                dh.MaVoucher = maVoucher;
                dh.TongTienSanPham = subTotal;
                dh.TienGiamGia = discount;
                dh.TongTien = subTotal + shipping - discount;
                dh.PhuongThucTT = (payMethod == "BANK") ? "Chuyển khoản" : "Tiền mặt (COD)";
                dh.TrangThai = "Chờ xác nhận";
                dh.GhiChu = note;
                dh.Create_at = DateTime.Now;
                dh.isDelete = 0;

                db.DonHangs.InsertOnSubmit(dh);
                db.SubmitChanges(); // Lấy MaDH

                // 5. Lưu Chi tiết đơn hàng
                foreach (var item in cartItems)
                {
                    var sp = db.SanPhams.Single(p => p.MaSP == item.MaSP);
                    ChiTietDonHang ct = new ChiTietDonHang();
                    ct.MaDH = dh.MaDH;
                    ct.MaSP = item.MaSP;
                    ct.TenSP = sp.TenSP;
                    ct.SoLuong = item.SoLuong;

                    // Lấy giá tại thời điểm mua
                    decimal giaFinal = sp.Gia ?? 0;
                    if (sp.GiaKhuyenMai > 0 && sp.GiaKhuyenMai < giaFinal) giaFinal = sp.GiaKhuyenMai ?? 0;

                    ct.DonGia = giaFinal;
                    ct.ThanhTien = giaFinal * (ct.SoLuong ?? 1);
                    ct.GhiChu = item.GhiChu;
                    ct.Create_at = DateTime.Now;

                    db.ChiTietDonHangs.InsertOnSubmit(ct);

                    // Xóa giỏ hàng (Xóa mềm hoặc cứng tùy bạn, ở đây dùng xóa cứng cho gọn DB giỏ)
                    // Hoặc xóa mềm: item.isDelete = 1;
                    db.GioHangs.DeleteOnSubmit(item);
                }

                // 6. Lưu lịch sử trạng thái
                LichSuTrangThai ls = new LichSuTrangThai();
                ls.MaDH = dh.MaDH;
                ls.TrangThaiMoi = "Chờ xác nhận";
                ls.ThoiGian = DateTime.Now;
                ls.GhiChu = "Khách hàng đặt đơn mới";
                db.LichSuTrangThais.InsertOnSubmit(ls);

                db.SubmitChanges();

                return "SUCCESS";
            }
            catch (Exception ex)
            {
                return "Lỗi: " + ex.Message;
            }
        }

    }
}