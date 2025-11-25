using Newtonsoft.Json;
using System;
using System.Linq;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;


namespace WebBanDoAnOnline.Controllers
{
    public class DanhMucController : Controller
    {
        // GET: DanhMuc
        
        public ActionResult SuaDanhMuc()
        {
            return View();
        }


        public string Lay_DSDM()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var danhMucs = db.DanhMucs
                             .Where(dm => dm.isDelete != 1)
                             .OrderBy(dm => dm.MaDM)
                             .Select(dm => new
                             {
                                 dm.MaDM,
                                 dm.TenDM,
                                 dm.MacDinh,
                                 dm.Anh 
                             })
                             .ToList();

            return JsonConvert.SerializeObject(danhMucs);
        }
        

       
        public string ThemDM()
        {
            string tenDM = Request["tenDM"];
            if (string.IsNullOrWhiteSpace(tenDM))
                return "Tên danh mục không được để trống.";

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            if (db.DanhMucs.Any(d => d.TenDM == tenDM && (d.isDelete == 0 || d.isDelete == null)))
                return "Tên danh mục này đã tồn tại.";

            try
            {
                DanhMuc dm_obj = new DanhMuc
                {
                    TenDM = tenDM,
                    Create_at = DateTime.Now,
                    isDelete = 0
                };
                db.DanhMucs.InsertOnSubmit(dm_obj);
                db.SubmitChanges();
                return "Thêm mới danh mục thành công!";
            }
            catch (Exception ex)
            {
                return "Thêm mới thất bại. Lỗi: " + ex.Message;
            }
        }

        
        public string SuaDM()
        {
            int maDM = int.Parse(Request["maDM"]);
            string tenDM = Request["tenDM"];

            if (string.IsNullOrWhiteSpace(tenDM))
                return "Tên danh mục không được để trống.";

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var dm_obj = db.DanhMucs.SingleOrDefault(d => d.MaDM == maDM);

            if (dm_obj != null)
            {
                // Kiểm tra xem tên mới có bị trùng với một danh mục khác không
                if (db.DanhMucs.Any(d => d.TenDM == tenDM && d.MaDM != maDM && (d.isDelete == 0 || d.isDelete == null)))
                {
                    return "Tên danh mục này đã tồn tại.";
                }

                try
                {
                    dm_obj.TenDM = tenDM;
                    dm_obj.LastEdit_at = DateTime.Now;
                    db.SubmitChanges();
                    return "Cập nhật danh mục thành công!";
                }
                catch (Exception ex)
                {
                    return "Cập nhật thất bại. Lỗi: " + ex.Message;
                }
            }
            else
            {
                return "Không tìm thấy danh mục để cập nhật.";
            }
        }

        
        public string XoaDM()
        {
            int maDM = int.Parse(Request["maDM"]);
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

            if (db.SanPhams.Any(p => p.MaDM == maDM && (p.isDelete == 0 || p.isDelete == null)))
                return "Không thể xóa danh mục vì đang có sản phẩm sử dụng.";

            var dm_obj = db.DanhMucs.SingleOrDefault(d => d.MaDM == maDM);
            if (dm_obj != null)
            {
                db.DanhMucs.DeleteOnSubmit(dm_obj);
                db.SubmitChanges();
                return "Xóa danh mục thành công!";
            }
            else
            {
                return "Không tìm thấy danh mục cần xóa.";
            }
        }

       
    }
}