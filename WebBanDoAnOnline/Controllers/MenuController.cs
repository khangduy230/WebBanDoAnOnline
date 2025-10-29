using System;
using System.Linq;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;
namespace WebBanDoAnOnline.Controllers
{
    public class MenuController : Controller
    {
        private BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext(
            System.Configuration.ConfigurationManager.ConnectionStrings["BanDoAnOnlineConnectionString"].ConnectionString
        );

        // GET: /Menu/Index?dmId=5&page=1&pageSize=8
        public ActionResult Index(int? dmId, int page = 1, int pageSize = 8)
        {
            ViewBag.CurrentPage = "Menu";
            ViewBag.Title = "Thực đơn";

            // Danh mục
            ViewBag.DanhMucList = db.DanhMucs
                                    .Where(d => d.isDelete != 1)
                                    .ToList();

            // Query sản phẩm
            var query = db.SanPhams
                          .Where(sp => sp.isDelete != 1 && sp.TrangThai == "Còn hàng");

            if (dmId.HasValue)
            {
                // đổi "MaDM" nếu FK khác tên
                query = query.Where(sp => sp.MaDM == dmId.Value);
            }

            // Phân trang
            if (pageSize < 1) pageSize = 8;
            var totalItems = query.Count();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var items = query
                        .OrderBy(sp => sp.MaSP) // đổi tiêu chí sắp xếp nếu cần
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

            ViewBag.SanPhamList = items;
            ViewBag.SelectedDMId = dmId;

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = totalPages;

            return View();
        }

        // GET (Partial): /Menu/Products?dmId=5&page=2&pageSize=8
        public PartialViewResult Products(int? dmId, int page = 1, int pageSize = 8)
        {
            var query = db.SanPhams
                          .Where(sp => sp.isDelete != 1 && sp.TrangThai == "Còn hàng");

            if (dmId.HasValue)
            {
                query = query.Where(sp => sp.MaDM == dmId.Value);
            }

            if (pageSize < 1) pageSize = 8;
            var totalItems = query.Count();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var items = query
                        .OrderBy(sp => sp.MaSP)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

            ViewBag.SanPhamList = items;
            ViewBag.SelectedDMId = dmId;

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = totalPages;

            return PartialView("_Products");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}