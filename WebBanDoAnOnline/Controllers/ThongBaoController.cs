using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class ThongBaoController : Controller
    {
        private BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

        // GET: /ThongBao/Index
        public ActionResult Index(int? openId)
        {
            if (Session["TaiKhoan"] == null) return RedirectToAction("Login", "TaiKhoan");

            
            if (openId.HasValue)
            {
                var userSession = Session["TaiKhoan"] as TaiKhoan;

                
                var thongBaoCanMo = db.ThongBaos.FirstOrDefault(x => x.MaTB == openId.Value && x.MaTK == userSession.MaTK);

                
                if (thongBaoCanMo != null && thongBaoCanMo.IsRead == false)
                {
                    thongBaoCanMo.IsRead = true;
                    db.SubmitChanges(); 
                }

                
                ViewBag.OpenId = openId.Value;
            }

            
            return View();
        }

        //  Lấy số lượng tin chưa đọc

        public string GetUnreadCount()
        {
            // Nếu chưa đăng nhập -> trả về false
            if (Session["TaiKhoan"] == null)
            {
                return JsonConvert.SerializeObject(new { success = false, count = 0 });
            }

            var userSession = Session["TaiKhoan"] as TaiKhoan;

            // Đếm số tin chưa đọc
            int count = db.ThongBaos.Count(t => t.MaTK == userSession.MaTK && t.IsRead == false);

            // Trả về true và số lượng
            return JsonConvert.SerializeObject(new { success = true, count = count });
        }

        //  Đánh dấu tất cả là đã đọc
        
        public string MarkAllRead()
        {
            if (Session["TaiKhoan"] == null)
            {
                return JsonConvert.SerializeObject(new { success = false, message = "Chưa đăng nhập" });
            }

            var userSession = Session["TaiKhoan"] as TaiKhoan;

            try
            {
                // Lấy các tin chưa đọc
                var listUnread = db.ThongBaos
                                   .Where(t => t.MaTK == userSession.MaTK && t.IsRead == false)
                                   .ToList();

                // Cập nhật
                if (listUnread.Count > 0)
                {
                    foreach (var item in listUnread)
                    {
                        item.IsRead = true;
                    }
                    db.SubmitChanges();
                }

                // Trả về success = true đơn giản
                return JsonConvert.SerializeObject(new { success = true });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, message = ex.Message });
            }
        }
        // LayThongBao
        public JsonResult GetThongBao()
        {
            
            if (Session["TaiKhoan"] == null)
            {
                return Json(new { success = false, message = "Chưa đăng nhập" }, JsonRequestBehavior.AllowGet);
            }

           
            var userSession = Session["TaiKhoan"] as WebBanDoAnOnline.Models.TaiKhoan;

            
            var list = db.ThongBaos
                         .Where(x => x.MaTK == userSession.MaTK) 
                         .OrderByDescending(x => x.CreatedAt)
                         .Take(10)
                         .ToList();

            // 4. CHUYỂN ĐỔI DỮ LIỆU 
            var data = list.Select(x => new {
               
                Id = x.MaTB,
                TieuDe = x.Title,
                NoiDungCon = x.Subtitle,
                ThoiGian = x.CreatedAt.ToString("dd/MM HH:mm"),
                DaDoc = x.IsRead
            });

            return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult MarkAsRead(int id)
        {
            if (Session["TaiKhoan"] == null)
            {
                return Json(new { success = false });
            }

            var userSession = Session["TaiKhoan"] as WebBanDoAnOnline.Models.TaiKhoan;

           
            var thongBao = db.ThongBaos.FirstOrDefault(x => x.MaTB == id && x.MaTK == userSession.MaTK);

            if (thongBao != null)
            {
                thongBao.IsRead = true;
                db.SubmitChanges();
                return Json(new { success = true });
            }

            return Json(new { success = false });
        }

        public JsonResult GetAllThongBaoJson()
        {
            if (Session["TaiKhoan"] == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var userSession = Session["TaiKhoan"] as WebBanDoAnOnline.Models.TaiKhoan;

            var list = db.ThongBaos
                         .Where(x => x.MaTK == userSession.MaTK)
                         .OrderByDescending(x => x.CreatedAt)
                         .ToList();

            var data = list.Select(x => new {
                MaTB = x.MaTB, 
                TieuDe = x.Title,
                NoiDung = x.Subtitle, 
                ThoiGian = x.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                DaDoc = x.IsRead
            });

            return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
        }
    }
}