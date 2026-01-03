using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class VoucherController : Controller
    {
        // GET Views
        public ActionResult QL_DanhSachVoucher() { return View(); }
        public ActionResult QL_ThemVoucher() { return View(); }
        public ActionResult QL_SuaVoucher(int id) { ViewBag.MaVoucher = id; return View(); }

        
        // API 1: LẤY DANH SÁCH VOUCHER
        
        public string Lay_DSVoucher()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var query = db.Vouchers.Where(v => v.isDelete == null || v.isDelete == 0);

            // Tìm kiếm
            string TimKiem = Request["TimKiem"];
            if (!string.IsNullOrEmpty(TimKiem))
            {
                string lower = TimKiem.ToLower();
                query = query.Where(v => v.TenVoucher.ToLower().Contains(lower) || v.MaCode.ToLower().Contains(lower));
            }

            // Phân trang
            int Trang = 1;
            if (!string.IsNullOrEmpty(Request["Trang"])) int.TryParse(Request["Trang"], out Trang);
            int TrangSize = 6;

            int totalItems = query.Count();
            int TongTrang = (int)Math.Ceiling((double)totalItems / TrangSize);
            if (TongTrang == 0) TongTrang = 1;

            // Query dữ liệu
            var listRaw = query.OrderByDescending(v => v.MaVoucher)
                               .Skip((Trang - 1) * TrangSize)
                               .Take(TrangSize)
                               .ToList();

            // Định dạng dữ liệu trả về
            var listResult = listRaw.Select(v => new
            {
                v.MaVoucher,
                v.TenVoucher,
                v.MaCode,
                v.LoaiGiam,
                v.GiaTri,
                v.DieuKienToiThieu,
                NgayBatDau = v.NgayBatDau.ToString("yyyy-MM-dd"),
                NgayKetThuc = v.NgayKetThuc.ToString("yyyy-MM-dd"),
                v.SoLuotConLai
            }).ToList();

            return JsonConvert.SerializeObject(new
            {
                TotalItems = totalItems,
                TongTrang = TongTrang,
                currentTrang = Trang,
                Vouchers = listResult
            });
        }


        // API 2: LẤY CHI TIẾT 1 VOUCHER

        public string LayTTVoucher()
        {
            string id_str = Request["id"];
            if (string.IsNullOrEmpty(id_str)) return "{}";
            int id = int.Parse(id_str);

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var v = db.Vouchers.SingleOrDefault(x => x.MaVoucher == id);

            if (v != null)
            {

                string loaiGiamView = (v.LoaiGiam == "Phần trăm") ? "PhanTram" : "SoTien";

                var result = new
                {
                    v.MaVoucher,
                    v.TenVoucher,
                    v.MaCode,
                    LoaiGiam = loaiGiamView,
                    v.GiaTri,
                    v.DieuKienToiThieu,
                    NgayBatDau = v.NgayBatDau.ToString("yyyy-MM-dd"),
                    NgayKetThuc = v.NgayKetThuc.ToString("yyyy-MM-dd"),
                    v.SoLuotDungToiDa,
                    v.MoTaThem
                };
                return JsonConvert.SerializeObject(result);
            }
            return "{}";
        }


        // API 3: THÊM MỚI VOUCHER

        public string ThemMoiVoucher()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            string maCode = Request["txt_MaCode"];

            // Check trùng
            if (db.Vouchers.Any(v => v.MaCode == maCode && v.isDelete != 1))
                return "Mã code này đã tồn tại.";

            try
            {
                // Lấy dữ liệu
                string tenVoucher = Request["txt_TenVoucher"];
                string loaiGiamRaw = Request["slc_LoaiGiam"]; 
                decimal giaTri = decimal.Parse(Request["txt_GiaTri"]);

                decimal? dieuKien = null;
                if (!string.IsNullOrEmpty(Request["txt_DieuKienToiThieu"]))
                    dieuKien = decimal.Parse(Request["txt_DieuKienToiThieu"]);

                int? soLuot = null;
                if (!string.IsNullOrEmpty(Request["txt_SoLuotDungToiDa"]))
                    soLuot = int.Parse(Request["txt_SoLuotDungToiDa"]);

                
                if (giaTri < 0) return "Giá trị giảm không được nhỏ hơn 0";

              
                if (loaiGiamRaw == "PhanTram" && giaTri > 100)
                {
                    return "Giảm theo phần trăm không được vượt quá 100%!";
                }

                if (dieuKien.HasValue && dieuKien < 0) return "Điều kiện tối thiểu không được nhỏ hơn 0";
                if (soLuot.HasValue && soLuot < 0) return "Số lượt dùng không được nhỏ hơn 0";

               
                string loaiGiamDB = (loaiGiamRaw == "PhanTram") ? "Phần trăm" : "Tiền";

                
                DateTime batDau = DateTime.ParseExact(Request["date_NgayBatDau"], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                DateTime ketThuc = DateTime.ParseExact(Request["date_NgayKetThuc"], "yyyy-MM-dd", CultureInfo.InvariantCulture);

                if (ketThuc < batDau) return "Ngày kết thúc không được nhỏ hơn ngày bắt đầu";

                Voucher vc = new Voucher
                {
                    TenVoucher = tenVoucher,
                    MaCode = maCode,
                    LoaiGiam = loaiGiamDB,
                    GiaTri = giaTri,
                    DieuKienToiThieu = dieuKien ?? 0,
                    NgayBatDau = batDau,
                    NgayKetThuc = ketThuc,
                    SoLuotDungToiDa = soLuot,
                    SoLuotDaSuDung = 0,
                    SoLuotConLai = soLuot,
                    MoTaThem = Request["txt_MoTaThem"],
                    Create_at = DateTime.Now,
                    isDelete = 0
                };

                db.Vouchers.InsertOnSubmit(vc);
                db.SubmitChanges();
                return "Thêm mới thành công!";
            }
            catch (Exception ex)
            {
                return "Lỗi: " + ex.Message;
            }
        }


        // API 4: CẬP NHẬT VOUCHER

        // API 4: CẬP NHẬT VOUCHER
        public string CapNhatVoucher()
        {
            string id_str = Request["txt_MaVoucher_hide"];
            if (string.IsNullOrEmpty(id_str)) return "Lỗi ID";
            int id = int.Parse(id_str);

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var vc = db.Vouchers.FirstOrDefault(v => v.MaVoucher == id);

            if (vc != null)
            {
                try
                {
                    // BƯỚC 1: LẤY DỮ LIỆU VÀ KHAI BÁO BIẾN TRƯỚC (QUAN TRỌNG)
                    // Phải khai báo ở đây để dùng được ở bên dưới
                    string loaiGiamRaw = Request["slc_LoaiGiam"];
                    decimal giaTri = decimal.Parse(Request["txt_GiaTri"]);

                    decimal? dieuKien = null;
                    if (!string.IsNullOrEmpty(Request["txt_DieuKienToiThieu"]))
                        dieuKien = decimal.Parse(Request["txt_DieuKienToiThieu"]);

                    // --- KHAI BÁO soLuotMoi Ở ĐÂY ---
                    int? soLuotMoi = null;
                    if (!string.IsNullOrEmpty(Request["txt_SoLuotDungToiDa"]))
                        soLuotMoi = int.Parse(Request["txt_SoLuotDungToiDa"]);
                    // --------------------------------

                    // BƯỚC 2: VALIDATE DỮ LIỆU
                    if (giaTri < 0) return "Giá trị giảm không được nhỏ hơn 0";

                    if (loaiGiamRaw == "PhanTram" && giaTri > 100)
                    {
                        return "Giảm theo phần trăm không được vượt quá 100%!";
                    }

                    if (dieuKien.HasValue && dieuKien < 0) return "Điều kiện tối thiểu không được nhỏ hơn 0";
                    if (soLuotMoi.HasValue && soLuotMoi < 0) return "Số lượt dùng không được nhỏ hơn 0";

                    // BƯỚC 3: CẬP NHẬT VÀO DATABASE
                    // Lúc này biến soLuotMoi đã có giá trị, không bị lỗi nữa

                    vc.TenVoucher = Request["txt_TenVoucher"];
                    vc.MaCode = Request["txt_MaCode"];
                    vc.LoaiGiam = (loaiGiamRaw == "PhanTram") ? "Phần trăm" : "Tiền";
                    vc.GiaTri = giaTri;
                    vc.DieuKienToiThieu = dieuKien ?? 0;

                    vc.NgayBatDau = DateTime.ParseExact(Request["date_NgayBatDau"], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    vc.NgayKetThuc = DateTime.ParseExact(Request["date_NgayKetThuc"], "yyyy-MM-dd", CultureInfo.InvariantCulture);

                    vc.MoTaThem = Request["txt_MoTaThem"];
                    vc.LastEdit_at = DateTime.Now;

                    // Xử lý cập nhật số lượt và tính lại lượt còn lại
                    vc.SoLuotDungToiDa = soLuotMoi;

                    if (soLuotMoi.HasValue)
                    {
                        // Tính toán lại: Lượt còn lại = Tổng mới - Đã dùng
                        int daDung = vc.SoLuotDaSuDung ?? 0;
                        int conLai = soLuotMoi.Value - daDung;

                        // Nếu chỉnh tổng lượt thấp hơn số đã dùng thì về 0 (tránh số âm)
                        vc.SoLuotConLai = conLai < 0 ? 0 : conLai;
                    }
                    else
                    {
                        // Nếu xóa giới hạn (để trống ô nhập) -> Set NULL
                        vc.SoLuotConLai = null;
                    }

                    db.SubmitChanges();
                    return "Cập nhật thành công!";
                }
                catch (Exception ex)
                {
                    return "Lỗi: " + ex.Message;
                }
            }
            return "Không tìm thấy voucher";
        }


        // API 5: XÓA VOUCHER

        // API 5: XÓA VOUCHER
        public string XoaVoucher()
        {
            // Lấy ID từ request
            string id_str = Request["id"];
            if (string.IsNullOrEmpty(id_str)) return "Lỗi: ID không hợp lệ";

            int id = int.Parse(id_str);
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

            // Tìm voucher
            var vc = db.Vouchers.FirstOrDefault(v => v.MaVoucher == id);

            if (vc != null)
            {
                // --- BỔ SUNG LOGIC KIỂM TRA ĐÃ SỬ DỤNG ---
                // Kiểm tra xem số lượt đã sử dụng có lớn hơn 0 hay không
                if (vc.SoLuotDaSuDung != null && vc.SoLuotDaSuDung > 0)
                {
                    return "Không thể xóa: Voucher này đã được sử dụng " + vc.SoLuotDaSuDung + " lần!";
                }
                // -----------------------------------------

                // Nếu chưa dùng thì mới cho xóa (Xóa mềm)
                vc.isDelete = 1;
                vc.Delete_at = DateTime.Now;
                db.SubmitChanges();
                return "Xóa thành công!";
            }

            return "Không tìm thấy voucher.";
        }
    }
}