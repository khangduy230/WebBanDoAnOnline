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

            // Nếu có ID được truyền vào (tức là bấm từ menu xuống)
            if (openId.HasValue)
            {
                var userSession = Session["TaiKhoan"] as TaiKhoan;

                // Tìm thông báo đó (kèm điều kiện MaTK để bảo mật)
                var thongBaoCanMo = db.ThongBaos.FirstOrDefault(x => x.MaTB == openId.Value && x.MaTK == userSession.MaTK);

                // Nếu tìm thấy và chưa đọc -> Đánh dấu là đã đọc ngay
                if (thongBaoCanMo != null && thongBaoCanMo.IsRead == false)
                {
                    thongBaoCanMo.IsRead = true;
                    db.SubmitChanges(); // Lưu vào CSDL
                }

                // Truyền ID này sang View để Javascript biết đường mà mở Accordion
                ViewBag.OpenId = openId.Value;
            }

            // Trả về View không cần Model (vì JS sẽ tự lo phần dữ liệu)
            return View();
        }

        // AJAX: Lấy số lượng tin chưa đọc
        [HttpPost]
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

        // AJAX: Đánh dấu tất cả là đã đọc
        [HttpPost]
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

        public JsonResult GetThongBao()
        {
            // 1. KIỂM TRA ĐĂNG NHẬP
            // Nếu chưa đăng nhập thì trả về danh sách rỗng (để không bị lỗi code JS)
            if (Session["TaiKhoan"] == null)
            {
                return Json(new { success = false, message = "Chưa đăng nhập" }, JsonRequestBehavior.AllowGet);
            }

            // 2. LẤY THÔNG TIN USER TỪ SESSION
            var userSession = Session["TaiKhoan"] as WebBanDoAnOnline.Models.TaiKhoan;

            // 3. TRUY VẤN CÓ ĐIỀU KIỆN WHERE (Quan trọng nhất)
            var list = db.ThongBaos
                         .Where(x => x.MaTK == userSession.MaTK) // <--- LỌC THEO ID NGƯỜI DÙNG
                         .OrderByDescending(x => x.CreatedAt)
                         .Take(10)
                         .ToList();

            // 4. CHUYỂN ĐỔI DỮ LIỆU (Mapping)
            var data = list.Select(x => new {
                // Lưu ý: Kiểm tra lại tên cột khóa chính trong DB của bạn là Id hay MaTB
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

            // Tìm tin nhắn theo ID và MaTK (để bảo mật)
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
                MaTB = x.MaTB, // Nhớ dùng đúng tên khóa chính của bạn
                TieuDe = x.Title,
                NoiDung = x.Subtitle, // Hoặc x.Details tùy DB
                ThoiGian = x.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                DaDoc = x.IsRead
            });

            return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
        }
    }
}