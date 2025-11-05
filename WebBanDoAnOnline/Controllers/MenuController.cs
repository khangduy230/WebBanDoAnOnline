using System;
using System.Linq;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;
using System.Globalization;   // thêm
using System.Text;           // thêm

namespace WebBanDoAnOnline.Controllers
{
    public class MenuController : Controller
    {
        private BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext(
            System.Configuration.ConfigurationManager.ConnectionStrings["BanDoAnOnlineConnectionString"].ConnectionString
        );

        // GET: /Menu?dmId=...&page=1&pageSize=8
        [HttpGet]
        public ActionResult Index(int? dmId, int page = 1, int pageSize = 8)
        {
            ViewBag.CurrentPage = "Menu";
            ViewBag.Title = "Thực đơn";

            // Danh mục
            var danhMucList = db.DanhMucs
                                .Where(dm => dm.isDelete != 1)
                                .OrderBy(dm => dm.MaDM)
                                .ToList();
            ViewBag.DanhMucList = danhMucList;

            if (!dmId.HasValue)
            {
                var macDinh = danhMucList.FirstOrDefault(x => x.MacDinh == true);
                if (macDinh != null) dmId = macDinh.MaDM;
            }
            ViewBag.SelectedDMId = dmId;

            // Sản phẩm theo danh mục (nếu có)
            var query = db.SanPhams
                          .Where(sp => sp.isDelete != 1 && sp.TrangThai == "Còn hàng");
            if (dmId.HasValue) query = query.Where(sp => sp.MaDM == dmId.Value);

            query = query.OrderBy(sp => sp.MaSP);

            if (pageSize < 1) pageSize = 8;
            var totalItems = query.Count();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var items = query.Skip((page - 1) * pageSize)
                             .Take(pageSize)
                             .ToList();

            ViewBag.SanPhamList = items;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = totalPages;

            return View(); // Views/Menu/Index.cshtml
        }

        // GET: /Menu/Products?dmId=...&page=1&pageSize=8
        // Dùng cho Ajax phân trang trong Index.cshtml
        [HttpGet]
        public ActionResult Products(int? dmId, int page = 1, int pageSize = 8)
        {
            var query = db.SanPhams
                          .Where(sp => sp.isDelete != 1 && sp.TrangThai == "Còn hàng");
            if (dmId.HasValue) query = query.Where(sp => sp.MaDM == dmId.Value);

            query = query.OrderBy(sp => sp.MaSP);

            if (pageSize < 1) pageSize = 8;
            var totalItems = query.Count();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var items = query.Skip((page - 1) * pageSize)
                             .Take(pageSize)
                             .ToList();

            ViewBag.SanPhamList = items;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = totalPages;
            ViewBag.SelectedDMId = dmId;

            return PartialView("_Products");
        }

        // Helper: bỏ dấu tiếng Việt + chuẩn hoá so sánh
        private static string NormalizeVN(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var s = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            var res = sb.ToString().Normalize(NormalizationForm.FormC);
            res = res.Replace('đ', 'd');
            return res;
        }

        // GET: /Menu/Search?q=tu-khoa&page=1&pageSize=8
        [HttpGet]
        public ActionResult Search(string q, int page = 1, int pageSize = 8)
        {
            ViewBag.CurrentPage = "Menu";
            ViewBag.Title = "Kết quả tìm kiếm";
            ViewBag.Q = q ?? "";

            var baseQuery = db.SanPhams
                              .Where(sp => sp.isDelete != 1 && sp.TrangThai == "Còn hàng")
                              .OrderBy(sp => sp.MaSP);

            var list = baseQuery.ToList();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var kw = NormalizeVN(q);
                list = list.Where(sp => NormalizeVN(sp.TenSP).Contains(kw)).ToList();
            }

            if (pageSize < 1) pageSize = 8;
            var totalItems = list.Count;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var items = list.Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .ToList();

            ViewBag.SanPhamList = items;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = totalPages;

            return View("Search");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}