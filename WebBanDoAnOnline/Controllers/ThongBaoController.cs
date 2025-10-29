using System;
using System.Collections.Generic;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;
using System.Linq;

namespace WebBanDoAnOnline.Controllers
{
    [Authorize]
    public class ThongBaoController : Controller
    {
        // GET: /ThongBao
        public ActionResult Index()
        {
            // Demo dữ liệu. Khi có bảng thông báo bạn thay bằng truy vấn DB.
            var items = new List<ThongBao>
            {
                new ThongBao { MaTB = 1, Title = "Bạn có voucher chưa sử dụng!", Subtitle = "Kiểm tra ưu đãi ngay.", IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-1) },
                new ThongBao { MaTB = 2, Title = "Món hot bạn đã thử chưa", Subtitle = "Nem chua chỉ 36.000đ", IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-3) },
                new ThongBao { MaTB = 3, Title = "Dark dark bùh bùh lmao", Subtitle = "Bí content", IsRead = true, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            };

            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAllAsRead()
        {
            // TODO: Cập nhật trạng thái đã đọc trong DB (nếu có).
            TempData["Message"] = "Đã đánh dấu tất cả thông báo là đã đọc.";
            return RedirectToAction("Index");
        }
        [HttpGet]
        public JsonResult UnreadCount()
        {
            // TODO: thay bằng logic đếm thông báo chưa đọc
            var unread = 0;
            if (Session["UnreadCount"] is int u) unread = u;

            return Json(new { ok = true, unread }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult MarkAllRead()
        {
            // TODO: cập nhật trạng thái đã đọc trong DB, sau đó set unread = 0
            // Ví dụ: NotificationService.MarkAllRead(userId);
            Session["UnreadCount"] = 0;
            return Json(new { ok = true, unread = 0 });
        }
    }
}