using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class SanPhamController : Controller
    {
        // 1. Các Action trả về View
        public ActionResult KH_DanhSachSanPham() { return View(); } 

        public ActionResult NV_DanhSachSanPham() { return View(); }

       
        public ActionResult QL_DanhSachSanPham()
        {
            if (Session["TaiKhoan"] == null) return RedirectToAction("DangNhap", "TaiKhoan");
            return View();
        }

        public ActionResult QL_ThemSanPham()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            ViewBag.Categories = db.DanhMucs.Where(d => d.isDelete == 0 || d.isDelete == null).ToList();
            return View();
        }

        public ActionResult QL_SuaSanPham(int id)
        {
            ViewBag.MaSP = id;
            return View();
        }

        public ActionResult ChiTietSanPham()
        {
            
            return View();
        }
        // 1: Lấy danh sách 

        public string Lay_Menu()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

            // 1. Lấy tham số
            string maDM_str = Request["maDM"];
            string searchTerm = Request["searchTerm"];
            string page_str = Request["page"];

            int currentPage = 1;
            if (!string.IsNullOrEmpty(page_str)) int.TryParse(page_str, out currentPage);
            int pageSize = 8; // Số sản phẩm mỗi trang

            var query = db.SanPhams.Where(sp => (sp.isDelete == null || sp.isDelete == 0));

            // Kiểm tra quyền hạn xem hàng
            if (Session["TaiKhoan"] == null || (Session["TaiKhoan"] as TaiKhoan).VaiTro == "Khách hàng")
            {
                query = query.Where(sp => sp.TrangThai == "Còn hàng");
            }

            // 3. Lọc theo Danh mục
            if (!string.IsNullOrEmpty(maDM_str))
            {
                int maDM = int.Parse(maDM_str);
                query = query.Where(sp => sp.MaDM == maDM);
            }

            // 4. Tìm kiếm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                string lower = searchTerm.ToLower();
                query = query.Where(sp => sp.TenSP.ToLower().Contains(lower));
            }

            // 5. Phân trang
            var orderedQuery = query.OrderByDescending(sp => sp.MaSP);
            int totalItems = orderedQuery.Count();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            if (totalPages < 1) totalPages = 1;

            if (currentPage < 1) currentPage = 1;
            if (currentPage > totalPages) currentPage = totalPages;

            // --- SỬA ĐOẠN NÀY ---
            // Thay vì select trực tiếp, ta lấy danh sách sản phẩm ra trước (ToList)
            // Sau đó mới tính điểm đánh giá cho từng sản phẩm trong danh sách đó.

            var rawItems = orderedQuery.Skip((currentPage - 1) * pageSize)
                                    .Take(pageSize)
                                    .ToList();

            var items = rawItems.Select(sp => {
                // Truy vấn bảng Đánh Giá để tính điểm realtime
                var listDG = db.DanhGias.Where(d => d.MaSP == sp.MaSP && (d.isDelete == 0 || d.isDelete == null));

                double diemTB = 0;
                if (listDG.Any())
                {
                    diemTB = listDG.Average(d => (double)d.SoSao);
                }

                return new
                {
                    sp.MaSP,
                    sp.TenSP,
                    sp.Gia,
                    sp.Anh,
                    sp.TrangThai,
                    DiemDanhGia = Math.Round(diemTB, 1) // Làm tròn 1 chữ số thập phân (VD: 4.5)
                };
            }).ToList();
            // --------------------

            var result = new
            {
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = currentPage,
                Products = items
            };

            return JsonConvert.SerializeObject(result);
        }


        // API 2: Lấy thông tin 1 sản phẩm (Để sửa/Xem chi tiết)

        // --- API 2: Lấy thông tin 1 sản phẩm (Đã cập nhật tính điểm thật) ---
        public string LayTTSP()
        {
            string id_str = Request["id"];
            if (string.IsNullOrEmpty(id_str)) return "{}";

            int id = int.Parse(id_str);
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

            // 1. Lấy thông tin sản phẩm
            var sp = db.SanPhams.FirstOrDefault(o => o.MaSP == id && (o.isDelete == 0 || o.isDelete == null));
            if (sp == null) return JsonConvert.SerializeObject(new { Error = "Sản phẩm không tồn tại" });

            // 2. TÍNH TOÁN TỐI ƯU (Thực hiện ngay dưới Database)
            // Tạo câu truy vấn (chưa chạy ngay)
            var queryDanhGia = db.DanhGias.Where(d => d.MaSP == id && (d.isDelete == 0 || d.isDelete == null));

            // Đếm số lượng
            int soLuotDanhGia = queryDanhGia.Count();

            // Tính trung bình (chỉ tính nếu có đánh giá để tránh lỗi chia cho 0)
            // Ép kiểu (double) để chia ra số thập phân
            double diemTB = 0;
            if (soLuotDanhGia > 0)
            {
                diemTB = queryDanhGia.Average(d => (double)d.SoSao);
            }

            // 3. Lấy tên danh mục
            string tenDM = "Khác";
            var dm = db.DanhMucs.FirstOrDefault(d => d.MaDM == sp.MaDM);
            if (dm != null) tenDM = dm.TenDM;

            var result = new
            {
                MaSP = sp.MaSP,
                TenSP = sp.TenSP,
                Gia = sp.Gia,
                Anh = sp.Anh,
                MoTa = sp.MoTa,
                TenDM = tenDM,
                DiemTrungBinh = Math.Round(diemTB, 1), // Làm tròn 1 số lẻ (VD: 4.666 -> 4.7)
                SoLuotDanhGia = soLuotDanhGia
            };

            return JsonConvert.SerializeObject(result);
        }

        // --- Tìm API này trong SanPhamController.cs ---
        public string LayDanhSachDanhGia()
        {
            try
            {
                string id_str = Request["id"];
                if (string.IsNullOrEmpty(id_str)) return "[]";
                int maSP = int.Parse(id_str);

                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

                var query = from dg in db.DanhGias
                            join tk in db.TaiKhoans on dg.MaTK equals tk.MaTK
                            where dg.MaSP == maSP && (dg.isDelete == 0 || dg.isDelete == null)
                            orderby dg.Create_at descending
                            select new
                            {
                                TenNguoiDung = tk.HoTen,
                                AnhDaiDien = tk.AnhDaiDien,
                                SoSao = dg.SoSao,
                                NoiDung = dg.BinhLuan,
                                // --- THÊM DÒNG NÀY ---
                                PhanHoi = dg.PhanHoi,
                                // ---------------------
                                NgayDanhGia = string.Format("{0:dd/MM/yyyy}", dg.Create_at)
                            };

                return JsonConvert.SerializeObject(query.ToList());
            }
            catch
            {
                return "[]";
            }
        }


        // API 3: Thêm mới

        [HttpPost]
        public string ThemMoiSanPham()
        {
            try
            {
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

                string tenSP = Request["txt_TenSP"];
                string giaStr = Request["txt_Gia"];
                string moTa = Request["txt_MoTa"];
                string maDMStr = Request["slc_MaDM"];

                // 1. Kiểm tra thông tin cơ bản
                if (string.IsNullOrEmpty(tenSP)) return "Vui lòng nhập tên sản phẩm.";
                if (string.IsNullOrEmpty(giaStr)) return "Vui lòng nhập giá.";
                if (string.IsNullOrEmpty(maDMStr)) return "Vui lòng chọn danh mục.";

                // 2. KIỂM TRA ẢNH BẮT BUỘC (MỚI)
                if (Request.Files.Count == 0 || Request.Files[0].ContentLength == 0)
                {
                    return "Vui lòng chọn ảnh đại diện cho sản phẩm.";
                }

                // Check trùng tên
                if (db.SanPhams.Any(p => p.TenSP == tenSP && (p.isDelete == 0 || p.isDelete == null)))
                    return "Tên sản phẩm này đã tồn tại.";

                decimal gia;
                if (!decimal.TryParse(giaStr, out gia)) return "Giá bán không hợp lệ.";

                int maDM;
                if (!int.TryParse(maDMStr, out maDM)) return "Danh mục không hợp lệ.";

                // Tạo object
                SanPham sp_obj = new SanPham
                {
                    TenSP = tenSP,
                    Gia = gia,
                    MoTa = moTa,
                    MaDM = maDM,
                    TrangThai = "Còn hàng",
                    Create_at = DateTime.Now,
                    isDelete = 0,
                    // Không cần gán ảnh mặc định nữa vì ở trên đã bắt buộc có ảnh rồi
                };

                // Lưu ảnh
                HttpPostedFileBase file = Request.Files[0];
                string ext = Path.GetExtension(file.FileName);
                string[] allowedExt = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                if (!allowedExt.Contains(ext.ToLower())) return "Định dạng ảnh không hỗ trợ.";

                string uniqueFileName = Guid.NewGuid().ToString() + ext;
                string serverPath = Path.Combine(Server.MapPath("~/img/"), uniqueFileName);
                file.SaveAs(serverPath);

                sp_obj.Anh = "~/img/" + uniqueFileName;

                db.SanPhams.InsertOnSubmit(sp_obj);
                db.SubmitChanges();
                return "Thêm mới sản phẩm thành công!";
            }
            catch (Exception ex)
            {
                return "Lỗi hệ thống: " + ex.Message;
            }
        }


        // API 4: Cập nhật

        public string CapNhatSanPham()
        {
            try
            {
                string maSP_str = Request["txt_MaSP_hide"];
                if (string.IsNullOrEmpty(maSP_str)) return "Lỗi ID";

                int maSP = int.Parse(maSP_str);
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                var sp_obj = db.SanPhams.FirstOrDefault(p => p.MaSP == maSP);

                if (sp_obj != null)
                {
                    sp_obj.TenSP = Request["txt_TenSP"];
                    sp_obj.Gia = decimal.Parse(Request["txt_Gia"]);
                    sp_obj.MoTa = Request["txt_MoTa"];
                    sp_obj.MaDM = int.Parse(Request["slc_MaDM"]);

                    // Nếu có gửi trạng thái lên thì update
                    if (!string.IsNullOrEmpty(Request["slc_TrangThai"]))
                        sp_obj.TrangThai = Request["slc_TrangThai"];

                    sp_obj.LastEdit_at = DateTime.Now;

                    // Cập nhật ảnh nếu có chọn ảnh mới
                    if (Request.Files.Count > 0 && Request.Files[0].ContentLength > 0)
                    {
                        HttpPostedFileBase file = Request.Files[0];
                        string ext = Path.GetExtension(file.FileName);
                        string uniqueFileName = Guid.NewGuid().ToString() + ext;
                        string serverPath = Path.Combine(Server.MapPath("~/img/"), uniqueFileName);
                        file.SaveAs(serverPath);
                        sp_obj.Anh = "~/img/" + uniqueFileName;
                    }

                    db.SubmitChanges();
                    return "Cập nhật sản phẩm thành công!";
                }
                return "Không tìm thấy sản phẩm!";
            }
            catch (Exception ex)
            {
                return "Cập nhật thất bại. Lỗi: " + ex.Message;
            }
        }


        // API 5: Xóa 

        public string XoaSanPham()
        {
            try
            {
                string id_str = Request["id"];
                if (string.IsNullOrEmpty(id_str)) return "Lỗi ID";
                int id = int.Parse(id_str);

                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

                // --- THÊM ĐOẠN KIỂM TRA NÀY ---
                // Kiểm tra xem sản phẩm có nằm trong bất kỳ chi tiết đơn hàng nào không (kể cả đơn cũ)
                bool daCoDonHang = db.ChiTietDonHangs.Any(ct => ct.MaSP == id);

                if (daCoDonHang)
                {
                    return "Sản phẩm này đã có lịch sử đặt hàng, không thể xóa! \nBạn hãy chuyển trạng thái sang 'Hết hàng' hoặc 'Tạm ngưng'.";
                }
                // -----------------------------

                var sp = db.SanPhams.FirstOrDefault(p => p.MaSP == id);

                if (sp != null)
                {
                    // Thực hiện Soft Delete
                    sp.isDelete = 1;
                    sp.Delete_at = DateTime.Now;

                    db.SubmitChanges();
                    return "Xóa sản phẩm thành công!";
                }
                return "Không tìm thấy sản phẩm cần xóa.";
            }
            catch (Exception ex)
            {
                return "Xóa thất bại. Chi tiết: " + ex.Message;
            }
        }

        public string CapNhatTrangThai()
        {
            try
            {
                string id_str = Request["id"];
                string status = Request["status"]; 

                int id = int.Parse(id_str);
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                var sp = db.SanPhams.FirstOrDefault(p => p.MaSP == id);

                if (sp != null)
                {
                    sp.TrangThai = status;
                    db.SubmitChanges();
                    return "OK";
                }
                return "Không tìm thấy sản phẩm";
            }
            catch (Exception ex)
            {
                return "Lỗi: " + ex.Message;
            }
        }

        
    }
}