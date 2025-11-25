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
        public ActionResult Index() { return View(); } 

        public ActionResult NhanVien() { return View(); }

       
        public ActionResult LaySanPham()
        {
            if (Session["TaiKhoan"] == null) return RedirectToAction("Login", "TaiKhoan");
            return View();
        }

        public ActionResult ThemSanPham()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            ViewBag.Categories = db.DanhMucs.Where(d => d.isDelete == 0 || d.isDelete == null).ToList();
            return View();
        }

        public ActionResult SuaSanPham(int id)
        {
            ViewBag.MaSP = id;
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
            int pageSize = 8;

            
            var query = db.SanPhams.Where(sp => (sp.isDelete == null || sp.isDelete == 0));

           

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

            var items = orderedQuery.Skip((currentPage - 1) * pageSize)
                                    .Take(pageSize)
                                    .Select(sp => new {
                                        sp.MaSP,
                                        sp.TenSP,
                                        sp.Gia,
                                        sp.Anh,
                                        sp.TrangThai, 
                                        sp.DiemDanhGia 
                                    })
                                    .ToList();

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
        
        public string LayTTSP()
        {
            string id_str = Request["id"];
            if (string.IsNullOrEmpty(id_str)) return "{}";

            int id = int.Parse(id_str);
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

            // Lấy sản phẩm chưa xóa
            var sp = db.SanPhams.Where(o => o.MaSP == id && (o.isDelete == 0 || o.isDelete == null))
                                .Select(s => new {
                                    s.MaSP,
                                    s.TenSP,
                                    s.Gia,
                                    s.Anh,
                                    s.TrangThai,
                                    s.MaDM,
                                    s.MoTa
                                }).SingleOrDefault();

            if (sp != null) return JsonConvert.SerializeObject(sp);
            return "{}";
        }

        
        // API 3: Thêm mới
        
        public string InsertSP()
        {
            try
            {
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                string tenSP = Request["txt_TenSP"];

                // Check trùng tên
                if (db.SanPhams.Any(p => p.TenSP == tenSP && (p.isDelete == 0 || p.isDelete == null)))
                    return "Tên sản phẩm này đã tồn tại.";

                SanPham sp_obj = new SanPham
                {
                    TenSP = tenSP,
                    Gia = decimal.Parse(Request["txt_Gia"]),
                    MoTa = Request["txt_MoTa"],
                    MaDM = int.Parse(Request["slc_MaDM"]),
                    TrangThai = "Còn hàng", 
                    Create_at = DateTime.Now,
                    isDelete = 0,
                    Anh = "~/img/no-image.jpg" 
                };

                // Xử lý file ảnh
                if (Request.Files.Count > 0 && Request.Files[0].ContentLength > 0)
                {
                    HttpPostedFileBase file = Request.Files[0];
                    // Tạo tên file ngẫu nhiên tránh trùng
                    string ext = Path.GetExtension(file.FileName);
                    string uniqueFileName = Guid.NewGuid().ToString() + ext;
                    string serverPath = Path.Combine(Server.MapPath("~/img/"), uniqueFileName);
                    file.SaveAs(serverPath);
                    sp_obj.Anh = "~/img/" + uniqueFileName;
                }

                db.SanPhams.InsertOnSubmit(sp_obj);
                db.SubmitChanges();
                return "Thêm mới sản phẩm thành công!";
            }
            catch (Exception ex)
            {
                return "Thêm mới thất bại. Lỗi: " + ex.Message;
            }
        }

        
        // API 4: Cập nhật
        // CapNhatSanPham
        public string UpdateSP()
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
       
        public string Delete()
        {
            try
            {
                string id_str = Request["id"];
                if (string.IsNullOrEmpty(id_str)) return "Lỗi ID";
                int id = int.Parse(id_str);

                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                var sp = db.SanPhams.FirstOrDefault(p => p.MaSP == id);

                if (sp != null)
                {
                    
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
        // API: Cập nhật trạng thái nhanh (NhanVien)
        
        public string UpdateStock()
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