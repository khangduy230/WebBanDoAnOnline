using Newtonsoft.Json;
using System;
using System.Linq;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class DanhGiaController : Controller
    {
        private BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

        [HttpGet]
        public ActionResult KH_DanhGiaSanPham(int? maDH, int? maSP)
        {
            // 1. Kiểm tra tham số URL
            if (maDH == null || maSP == null)
            {
                TempData["Err"] = "Đường dẫn không hợp lệ (thiếu mã đơn hàng hoặc sản phẩm).";
                return RedirectToAction("DonMua", "CaiDat");
            }

            // 2. Kiểm tra đăng nhập
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null || user.VaiTro != "Khách hàng")
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            // 3. Lấy dữ liệu từ DB
            var ct = db.ChiTietDonHangs.FirstOrDefault(x => x.MaDH == maDH && x.MaSP == maSP);
            var dh = db.DonHangs.FirstOrDefault(d => d.MaDH == maDH && d.MaTK == user.MaTK);

            // 4. Kiểm tra tồn tại
            if (dh == null || ct == null)
            {
                TempData["Err"] = "Không tìm thấy thông tin sản phẩm trong đơn hàng của bạn.";
                return RedirectToAction("DonMua", "CaiDat");
            }

            // 5. Kiểm tra trạng thái (chỉ cho đánh giá khi đã nhận hàng)
            if (dh.TrangThai != "Đã nhận hàng")
            {
                TempData["Err"] = "Đơn hàng chưa hoàn thành, bạn chưa thể đánh giá.";
                return RedirectToAction("DonMua", "CaiDat");
            }

            // 6. Đổ dữ liệu ra ViewBag (Dùng object rõ ràng, không dynamic để tránh lỗi View)
            string tenSP = ct.TenSP;
            if (string.IsNullOrEmpty(tenSP) && ct.SanPham != null) tenSP = ct.SanPham.TenSP;

            string anhSP = "";
            if (ct.SanPham != null) anhSP = ct.SanPham.Anh;

            ViewBag.KHDG = new
            {
                MaDH = dh.MaDH,
                MaSP = ct.MaSP,
                TenSP = tenSP ?? "Sản phẩm",
                AnhSP = anhSP ?? "",
                SoLuong = ct.SoLuong ?? 1,
                ThanhTien = (ct.DonGia ?? 0) * (ct.SoLuong ?? 1),
                TrangThai = dh.TrangThai ?? ""
            };

            return View();
        }

        // POST: Xử lý lưu đánh giá
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LuuDanhGiaTrang(int MaDH, int MaSP, int SoSao, string BinhLuan)
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null) return RedirectToAction("DangNhap", "TaiKhoan");

            try
            {
                // Kiểm tra lại đơn hàng
                var dh = db.DonHangs.FirstOrDefault(d => d.MaDH == MaDH && d.MaTK == user.MaTK);
                if (dh == null || dh.TrangThai != "Đã nhận hàng")
                {
                    TempData["Err"] = "Đơn hàng không hợp lệ.";
                    return RedirectToAction("DonMua", "CaiDat");
                }

                // Kiểm tra đã đánh giá chưa
                var daDanhGia = db.DanhGias.Any(x => x.MaDH == MaDH && x.MaSP == MaSP && (x.isDelete == 0 || x.isDelete == null));
                if (daDanhGia)
                {
                    TempData["Err"] = "Bạn đã đánh giá sản phẩm này rồi.";
                    return RedirectToAction("DonMua", "CaiDat");
                }

                // Lưu đánh giá
                var dg = new DanhGia();
                dg.MaTK = user.MaTK;
                dg.MaDH = MaDH;
                dg.MaSP = MaSP;
                dg.SoSao = (byte)SoSao;
                dg.BinhLuan = BinhLuan ?? "";
                dg.Create_at = DateTime.Now;
                dg.isDelete = 0;

                db.DanhGias.InsertOnSubmit(dg);
                db.SubmitChanges();

                // Cập nhật điểm trung bình cho sản phẩm
                var sp = db.SanPhams.FirstOrDefault(s => s.MaSP == MaSP);
                if (sp != null)
                {
                    var listDG = db.DanhGias.Where(x => x.MaSP == MaSP && (x.isDelete == 0 || x.isDelete == null)).ToList();
                    if (listDG.Count > 0)
                    {
                        sp.SoLuotDanhGia = listDG.Count;
                        sp.DiemDanhGia = listDG.Average(x => (double)x.SoSao);
                        db.SubmitChanges();
                    }
                }

                TempData["Ok"] = "Cảm ơn bạn đã đánh giá!";
                return RedirectToAction("DonMua", "CaiDat");
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Lỗi: " + ex.Message;
                return RedirectToAction("DonMua", "CaiDat");
            }
        }

        // GET: View Quản lý (Dành cho Quản lý & Nhân viên)
        public ActionResult NV_QuanLyDanhGia()
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null || !new[] { "Quản lý", "Nhân viên" }.Contains(user.VaiTro))
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }
            // Truyền vai trò xuống View để ẩn/hiện nút Xóa
            ViewBag.VaiTro = user.VaiTro;
            return View();
        }

        // API 1: Lấy danh sách (Có tìm kiếm + Phân trang)
        [HttpPost]
        public string LayDanhSachDanhGia()
        {
            try
            {
                string tuKhoa = Request["tuKhoa"];
                string trangThaiAn = Request["trangThaiAn"]; // "da_an" hoặc "chua_an"

                int page = 1;
                int.TryParse(Request["page"], out page);
                int pageSize = 10;

                var query = db.DanhGias.AsQueryable();

                // 1. Lọc ẩn/hiện
                if (trangThaiAn == "da_an")
                    query = query.Where(d => d.isDelete == 1);
                else
                    query = query.Where(d => d.isDelete == 0 || d.isDelete == null);

                // 2. Tìm kiếm (Tên khách, Tên SP, Nội dung)
                if (!string.IsNullOrEmpty(tuKhoa))
                {
                    var lower = tuKhoa.ToLower();
                    query = query.Where(d =>
                        ((d.TaiKhoan.HoTen ?? "").ToLower().Contains(lower)) ||
                        ((d.SanPham.TenSP ?? "").ToLower().Contains(lower)) ||
                        ((d.BinhLuan ?? "").ToLower().Contains(lower)));
                }

                // 3. Phân trang
                int totalItems = query.Count();
                int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
                if (totalPages < 1) totalPages = 1;
                if (page < 1) page = 1;
                if (page > totalPages) page = totalPages;

                var data = query
                    .OrderByDescending(d => d.Create_at)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(d => new
                    {
                        MaDG = d.MaDG,
                        TenKhachHang = d.TaiKhoan.HoTen,
                        Avatar = d.TaiKhoan.AnhDaiDien,
                        TenSanPham = d.SanPham.TenSP,
                        AnhSanPham = d.SanPham.Anh,
                        SoSao = d.SoSao,
                        BinhLuan = d.BinhLuan,
                        PhanHoi = d.PhanHoi, // Cột trả lời trong DB
                        ThoiGian = d.Create_at,
                        DaAn = (d.isDelete == 1)
                    })
                    .ToList() // Chuyển về bộ nhớ để format ngày tháng bên dưới
                    .Select(d => new {
                        d.MaDG,
                        d.TenKhachHang,
                        d.Avatar,
                        d.TenSanPham,
                        d.AnhSanPham,
                        d.SoSao,
                        d.BinhLuan,
                        d.PhanHoi,
                        d.DaAn,
                        ThoiGian = d.ThoiGian.HasValue ? d.ThoiGian.Value.ToString("dd/MM/yyyy HH:mm") : ""
                    });

                return JsonConvert.SerializeObject(new { success = true, data = data, totalPages = totalPages, currentPage = page });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }

        // API 2: Ẩn/Hiện đánh giá
        [HttpPost]
        public string ToggleAnDanhGia()
        {
            try
            {
                int maDG = int.Parse(Request["maDG"]);
                var dg = db.DanhGias.FirstOrDefault(d => d.MaDG == maDG);
                if (dg != null)
                {
                    // Đảo trạng thái 0 <-> 1
                    dg.isDelete = (dg.isDelete == 1) ? (byte)0 : (byte)1;
                    db.SubmitChanges();
                    return "OK";
                }
                return "Không tìm thấy đánh giá";
            }
            catch (Exception ex) { return "Lỗi: " + ex.Message; }
        }

        // API 3: Xóa vĩnh viễn (Chỉ Quản lý)
        [HttpPost]
        public string XoaDanhGia()
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null || user.VaiTro != "Quản lý") return "Bạn không có quyền xóa!";

            try
            {
                int maDG = int.Parse(Request["maDG"]);
                var dg = db.DanhGias.FirstOrDefault(d => d.MaDG == maDG);
                if (dg != null)
                {
                    db.DanhGias.DeleteOnSubmit(dg);
                    db.SubmitChanges();
                    return "OK";
                }
                return "Không tìm thấy đánh giá";
            }
            catch (Exception ex) { return "Lỗi: " + ex.Message; }
        }

        // API 4: Trả lời đánh giá
        [HttpPost]
        public string TraLoiDanhGia()
        {
            try
            {
                int maDG = int.Parse(Request["maDG"]);
                string noiDung = Request["noiDung"];

                var dg = db.DanhGias.FirstOrDefault(d => d.MaDG == maDG);
                if (dg != null)
                {
                    dg.PhanHoi = noiDung; // Cập nhật cột phản hồi
                    db.SubmitChanges();
                    return "OK";
                }
                return "Không tìm thấy đánh giá";
            }
            catch (Exception ex) { return "Lỗi: " + ex.Message; }
        }
    }
}


