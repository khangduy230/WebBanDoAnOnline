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
        // 1. Mở trang thanh toán
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

        
        // API 1: LẤY DỮ LIỆU THANH TOÁN 
        
        public string LayThongTinThanhToan()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var user = Session["TaiKhoan"] as TaiKhoan;

            if (user == null) return "LOGIN_REQUIRED";

            DateTime now = DateTime.Now;

            // 1. Lấy danh sách địa chỉ
            var dsDiaChi = db.DiaChis
                             .Where(d => d.MaTK == user.MaTK && (d.isDelete == null || d.isDelete == 0))
                             .Select(d => new {
                                 d.MaDiaChi,
                                 d.TenNguoiNhan,
                                 d.SDTNhan,
                                 d.DiaChiCuThe,
                                 d.MacDinh
                             })
                             .OrderByDescending(d => d.MacDinh)
                             .ToList();

            // 2. Xử lý LỌC SẢN PHẨM THEO SESSION
           
            string selectedIdsStr = Session["CheckoutItems"] as string;
            List<int> selectedIds = new List<int>();

            if (!string.IsNullOrEmpty(selectedIdsStr))
            {
                selectedIds = selectedIdsStr.Split(',').Select(int.Parse).ToList();
            }

            // Query giỏ hàng cơ bản
            var cartQuery = from g in db.GioHangs
                            join p in db.SanPhams on g.MaSP equals p.MaSP
                            where g.MaTK == user.MaTK
                                  && (g.isDelete == 0 || g.isDelete == null)
                            select new { g, p };

            // Nếu có danh sách chọn thì lọc, nếu không thì lấy hết (fallback)
            if (selectedIds.Count > 0)
            {
                cartQuery = cartQuery.Where(x => selectedIds.Contains(x.p.MaSP));
            }

            var cartItems = cartQuery.ToList();

            // 3. Tính toán tiền
            decimal tongTienHang = 0;
            var listProducts = new List<object>();

            foreach (var item in cartItems)
            {
                //  giá khuyến mãi
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

                listProducts.Add(new
                {
                    TenSP = item.p.TenSP,
                    SoLuong = item.g.SoLuong,
                    DonGia = giaBan,
                    ThanhTien = thanhTien
                });
            }

            // 4. Lấy Voucher khả dụng
            var dsVoucher = db.Vouchers
                .Where(v => (v.isDelete == 0 || v.isDelete == null)
                            && v.NgayBatDau <= now && v.NgayKetThuc >= now
                            && (v.SoLuotConLai > 0 || v.SoLuotConLai == null))
                .Select(v => new
                {
                    v.MaCode,
                    v.TenVoucher,
                    v.LoaiGiam,
                    v.GiaTri,
                    v.DieuKienToiThieu,
                    v.MoTaThem
                })
                .OrderBy(v => v.DieuKienToiThieu)
                .ToList();

            // 5. Phí vận chuyển
            decimal phiShip = (tongTienHang > 500000) ? 0 : 25000;

            var result = new
            {
                DiaChi = dsDiaChi,
                SanPham = listProducts,
                Vouchers = dsVoucher,
                TongTienHang = tongTienHang,
                PhiShip = phiShip,
                TongThanhToan = tongTienHang + phiShip
            };

            return JsonConvert.SerializeObject(result);
        }

        
        // API 2: KIỂM TRA VOUCHER
        
        public string KiemTraVoucher()
        {
            string code = Request["code"];
            string total_str = Request["total"];

            if (string.IsNullOrEmpty(code)) return JsonConvert.SerializeObject(new { ok = false, msg = "Chưa nhập mã" });

            decimal subtotal = decimal.Parse(total_str);
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            DateTime now = DateTime.Now;

            var vc = db.Vouchers.FirstOrDefault(v => v.MaCode == code
                                                  && v.NgayBatDau <= now
                                                  && v.NgayKetThuc >= now
                                                  && (v.SoLuotConLai > 0 || v.SoLuotConLai == null)
                                                  && (v.isDelete == 0 || v.isDelete == null));

            if (vc != null)
            {
                if (vc.DieuKienToiThieu > 0 && subtotal < vc.DieuKienToiThieu)
                {
                    return JsonConvert.SerializeObject(new { ok = false, msg = "Đơn hàng chưa đủ điều kiện tối thiểu." });
                }

                decimal discount = 0;
                if (vc.LoaiGiam == "Tiền") discount = vc.GiaTri;
                else discount = (subtotal * vc.GiaTri) / 100; // Phần trăm

                if (discount > subtotal) discount = subtotal;

                return JsonConvert.SerializeObject(new { ok = true, discount = discount, msg = "Áp dụng thành công!" });
            }

            return JsonConvert.SerializeObject(new { ok = false, msg = "Mã không hợp lệ hoặc đã hết hạn." });
        }

        
        // API 3: ĐẶT HÀNG (CÓ LỌC SẢN PHẨM ĐÃ CHỌN)
        
        public string DatHang()
        {
            try
            {
                var user = Session["TaiKhoan"] as TaiKhoan;
                if (user == null) return "LOGIN_REQUIRED";

                string addressId_str = Request["addressId"];
                string payMethod = Request["payMethod"];
                string note = Request["note"];
                string voucherCode = Request["voucherCode"];

                if (string.IsNullOrEmpty(addressId_str)) return "Vui lòng chọn địa chỉ.";
                int addressId = int.Parse(addressId_str);

                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                DateTime now = DateTime.Now;

                // --- LẤY LẠI DANH SÁCH SẢN PHẨM CẦN MUA 
                string selectedIdsStr = Session["CheckoutItems"] as string;
                List<int> selectedIds = new List<int>();
                if (!string.IsNullOrEmpty(selectedIdsStr))
                {
                    selectedIds = selectedIdsStr.Split(',').Select(int.Parse).ToList();
                }

                // Query những món có trong giỏ của User
                var cartQuery = db.GioHangs.Where(g => g.MaTK == user.MaTK && (g.isDelete == 0 || g.isDelete == null));

                // Lọc đúng những món đã chọn
                if (selectedIds.Count > 0)
                {
                    cartQuery = cartQuery.Where(g => selectedIds.Contains(g.MaSP ?? 0));
                }

                var cartItems = cartQuery.ToList();
                if (!cartItems.Any()) return "Không tìm thấy sản phẩm nào để thanh toán.";

                // --- TÍNH TOÁN LẠI TIỀN 
                decimal subTotal = 0;
                foreach (var item in cartItems)
                {
                    var sp = db.SanPhams.Single(p => p.MaSP == item.MaSP);
                    decimal gia = sp.Gia ?? 0;
                    if (sp.GiaKhuyenMai > 0 && sp.GiaKhuyenMai < gia &&
                        (!sp.NgayBatDauKM.HasValue || sp.NgayBatDauKM <= now) &&
                        (!sp.NgayKetThucKM.HasValue || sp.NgayKetThucKM >= now))
                    {
                        gia = sp.GiaKhuyenMai ?? 0;
                    }
                    subTotal += gia * (item.SoLuong ?? 1);
                }

                decimal shipping = (subTotal > 500000) ? 0 : 25000;
                decimal discount = 0;
                int? maVoucher = null;

                if (!string.IsNullOrEmpty(voucherCode))
                {
                    var vc = db.Vouchers.FirstOrDefault(v => v.MaCode == voucherCode);
                    if (vc != null)
                    {
                        // Check lại điều kiện lần cuối
                        if (vc.DieuKienToiThieu == 0 || subTotal >= vc.DieuKienToiThieu)
                        {
                            if (vc.LoaiGiam == "Tiền") discount = vc.GiaTri;
                            else discount = (subTotal * vc.GiaTri) / 100;

                            if (discount > subTotal) discount = subTotal;
                            maVoucher = vc.MaVoucher;

                            // Trừ lượt
                            if (vc.SoLuotConLai > 0) vc.SoLuotConLai -= 1;
                            vc.SoLuotDaSuDung = (vc.SoLuotDaSuDung ?? 0) + 1;
                        }
                    }
                }

                // --- LƯU ĐƠN HÀNG ---
                DonHang dh = new DonHang();
                dh.MaTK = user.MaTK;
                dh.MaDiaChi = addressId;
                dh.MaVoucher = maVoucher;
                dh.TongTienSanPham = subTotal;
                dh.TienGiamGia = discount;
                dh.TongTien = subTotal + shipping - discount;

               
                dh.PhuongThucTT = (payMethod == "BANK") ? "Online" : "COD";

                dh.TrangThai = "Chờ xác nhận";
                dh.GhiChu = note;
                dh.Create_at = DateTime.Now;
                dh.isDelete = 0;

                db.DonHangs.InsertOnSubmit(dh);
                db.SubmitChanges();

                // --- LƯU CHI TIẾT ---
                foreach (var item in cartItems)
                {
                    var sp = db.SanPhams.Single(p => p.MaSP == item.MaSP);
                    decimal giaFinal = sp.Gia ?? 0;
                    if (sp.GiaKhuyenMai > 0 && sp.GiaKhuyenMai < giaFinal) giaFinal = sp.GiaKhuyenMai ?? 0;

                    ChiTietDonHang ct = new ChiTietDonHang();
                    ct.MaDH = dh.MaDH;
                    ct.MaSP = item.MaSP;
                    ct.TenSP = sp.TenSP;
                    ct.SoLuong = item.SoLuong;
                    ct.DonGia = giaFinal;
                    ct.ThanhTien = giaFinal * (ct.SoLuong ?? 1);
                    ct.GhiChu = item.GhiChu;
                    ct.Create_at = DateTime.Now;

                    db.ChiTietDonHangs.InsertOnSubmit(ct);

                    
                    db.GioHangs.DeleteOnSubmit(item);
                }

                // Lưu lịch sử
                LichSuTrangThai ls = new LichSuTrangThai();
                ls.MaDH = dh.MaDH;
                ls.TrangThaiMoi = "Chờ xác nhận";
                ls.ThoiGian = DateTime.Now;
                ls.GhiChu = "Khởi tạo đơn hàng";
                db.LichSuTrangThais.InsertOnSubmit(ls);

                db.SubmitChanges();

                // Reset Session chọn món
                Session["CheckoutItems"] = null;

                return "SUCCESS";
            }
            catch (Exception ex)
            {
                return "Lỗi: " + ex.Message;
            }
        }
    }
}