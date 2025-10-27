// File: /Controllers/MenuController.cs
using System.Linq;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;
 // <-- Nhớ đổi tên "YourProjectName"

namespace WebBanDoAnOnline.Controllers // <-- Nhớ đổi tên "YourProjectName"
{
    public class MenuController : Controller
    {
        // 1. Khởi tạo "cầu nối" CSDL
        private BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext(
    System.Configuration.ConfigurationManager.ConnectionStrings["BanDoAnOnlineConnectionString"].ConnectionString
);

        // GET: /Menu/Index
        public ActionResult Index()
        {
            // 2. Báo cho _Layout.cshtml biết đây là trang "Menu"
            ViewBag.CurrentPage = "Menu";
            ViewBag.Title = "Thực đơn";

            // === THAY ĐỔI CỐT LÕI BẮT ĐẦU TỪ ĐÂY ===

            // 3. Không tạo ViewModel, thay vào đó, nạp dữ liệu vào 2 túi ViewBag:
            ViewBag.DanhMucList = db.DanhMucs
                                    .Where(d => d.isDelete != 1)
                                    .ToList();

            ViewBag.SanPhamList = db.SanPhams
                                    .Where(sp => sp.isDelete != 1 && sp.TrangThai == "Còn hàng")
                                    .ToList();

            // 4. Trả về View rỗng (nó sẽ tự tìm file /Views/Menu/Index.cshtml)
            return View();

            // === KẾT THÚC THAY ĐỔI ===
        }

        // (Hàm dọn dẹp kết nối)
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}