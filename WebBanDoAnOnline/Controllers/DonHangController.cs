using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class DonHangController : Controller
    {
        // Kiểm tra phiên đăng nhập và vai trò
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null)
            {
                filterContext.Result = RedirectToAction("DangNhap", "TaiKhoan");
                return;
            }
            base.OnActionExecuting(filterContext);
        }

        // 1. GET: Trang quản lý đơn hàng

        public ActionResult NV_DanhSachDonHang()
        {
            return View();
        }
        // GET: /DonHang/QL_DanhSachDonHang
        public ActionResult QL_DanhSachDonHang()
        {

            return View();
        }
        // 2. GET: Trang chi tiết đơn hàng
        public ActionResult ChiTietDonHang(string id, string code)
        {
            int maDH = 0;

            // Trường hợp 1: URL là ?id=5 (thường từ trang Nhân viên)
            if (!string.IsNullOrEmpty(id))
            {
                int.TryParse(id, out maDH);
            }

            // Trường hợp 2: URL là ?code=DH00005 (từ trang Quản lý)
            // Code này sẽ ghi đè nếu có tham số code truyền vào
            if (!string.IsNullOrEmpty(code))
            {
                string numberPart = code.ToUpper().Replace("DH", "");
                int.TryParse(numberPart, out maDH);
            }

            // Truyền ID chuẩn (số nguyên) xuống View
            ViewBag.MaDH = maDH;

            // Giữ lại viewbag cũ để phòng hờ logic cũ của bạn
            ViewBag.OrderId = maDH > 0 ? maDH.ToString() : "";

            return View();
        }


        //  1: LẤY DANH SÁCH ĐƠN HÀNG (JSON)


        [HttpPost]
        public string LayDonHang()
        {
            // 1. Nhận tham số từ Client
            string filterStatus = Request["status"];
            string keyword = Request["keyword"];
            int page = 1;
            int.TryParse(Request["page"], out page);
            int pageSize = 6; // Số đơn hàng trên 1 trang

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

            // 2. Query cơ bản (Kết nối bảng để tìm kiếm tên khách hàng nếu cần)
            // Lưu ý: Join bảng để tìm kiếm chính xác hơn
            var query = from d in db.DonHangs
                        join t in db.TaiKhoans on d.MaTK equals t.MaTK
                        join a in db.DiaChis on d.MaDiaChi equals a.MaDiaChi into da
                        from addr in da.DefaultIfEmpty() // Left join địa chỉ
                        where d.isDelete == 0 || d.isDelete == null
                        select new { d, TenKhach = (addr != null ? addr.TenNguoiNhan : t.HoTen), SDT = (addr != null ? addr.SDTNhan : t.SoDienThoai) };

            // 3. Lọc theo trạng thái
            if (!string.IsNullOrEmpty(filterStatus) && filterStatus != "all")
            {
                if (filterStatus == "pending") query = query.Where(x => x.d.TrangThai == "Chờ xác nhận");
                else if (filterStatus == "shipping") query = query.Where(x => x.d.TrangThai == "Đang giao" || x.d.TrangThai == "Đã xác nhận");
                else if (filterStatus == "completed") query = query.Where(x => x.d.TrangThai == "Đã nhận hàng");
                else if (filterStatus == "cancelled") query = query.Where(x => x.d.TrangThai == "Đã hủy");
            }

            // 4. Lọc theo từ khóa (Logic mới: Thông minh hơn)
            if (!string.IsNullOrEmpty(keyword))
            {
                string kw = keyword.ToLower().Trim(); // Xóa khoảng trắng thừa
                int idSearch = 0;
                bool isNumber = int.TryParse(kw, out idSearch);

                if (isNumber)
                {
                    // TRƯỜNG HỢP 1: Nhập số
                    if (kw.Length < 4) // Nếu số ngắn (dưới 4 ký tự) -> Ưu tiên tìm Mã Đơn Hàng chính xác
                    {
                        query = query.Where(x => x.d.MaDH == idSearch);
                    }
                    else // Nếu số dài (ví dụ nhập sđt: 0982...) -> Tìm cả Mã Đơn lẫn SĐT
                    {
                        query = query.Where(x => x.d.MaDH == idSearch || x.SDT.Contains(kw));
                    }
                }
                else
                {
                    // TRƯỜNG HỢP 2: Nhập chữ -> Tìm Tên hoặc SĐT (nếu tên có số)
                    query = query.Where(x => x.TenKhach.ToLower().Contains(kw)
                                          || x.SDT.Contains(kw));
                }
            }

            // 5. Tính toán phân trang
            int totalRecord = query.Count();
            int totalPages = (int)Math.Ceiling((double)totalRecord / pageSize);

            // Đảm bảo page không vượt quá giới hạn
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            // 6. Lấy dữ liệu trang hiện tại (Skip & Take)
            var listRaw = query.OrderByDescending(x => x.d.Create_at)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToList();

            // 7. Map dữ liệu hiển thị (Logic cũ giữ nguyên)
            var listResult = new List<object>();
            foreach (var item in listRaw)
            {
                var firstDetail = db.ChiTietDonHangs.FirstOrDefault(c => c.MaDH == item.d.MaDH);
                string img = "/img/no-image.jpg";
                string prodName = "Đơn hàng #" + item.d.MaDH;
                int qty = 0;

                if (firstDetail != null)
                {
                    var sp = db.SanPhams.FirstOrDefault(s => s.MaSP == firstDetail.MaSP);
                    if (sp != null && !string.IsNullOrEmpty(sp.Anh)) img = sp.Anh.Replace("~", "");

                    qty = db.ChiTietDonHangs.Where(c => c.MaDH == item.d.MaDH).Sum(c => c.SoLuong) ?? 0;
                    prodName = firstDetail.TenSP + (qty > firstDetail.SoLuong ? $"... (+{qty - firstDetail.SoLuong} món)" : "");
                }

                string statusUI = "pending";
                if (item.d.TrangThai == "Đang giao" || item.d.TrangThai == "Đã xác nhận") statusUI = "shipping";
                else if (item.d.TrangThai == "Đã nhận hàng") statusUI = "completed";
                else if (item.d.TrangThai == "Đã hủy") statusUI = "cancelled";

                listResult.Add(new
                {
                    id = item.d.MaDH,
                    name = prodName,
                    customer = item.TenKhach, // Thêm tên khách để hiển thị nếu cần
                    quantity = qty,
                    price = item.d.TongTien,
                    time = item.d.Create_at.HasValue ? item.d.Create_at.Value.ToString("HH:mm - dd/MM/yyyy") : "",
                    status = statusUI,
                    statusText = item.d.TrangThai,
                    image = img
                });
            }

            // 8. Trả về JSON bao gồm cả thông tin phân trang
            return JsonConvert.SerializeObject(new
            {
                totalPages = totalPages,
                currentPage = page,
                list = listResult
            });
        }


        // API 2: CẬP NHẬT TRẠNG THÁI ĐƠN

        public string CapNhatTrangThai()
        {
            try
            {
                string id_str = Request["id"];
                string action = Request["action"];
                string note = Request["note"];

                int id = int.Parse(id_str);
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                var dh = db.DonHangs.FirstOrDefault(d => d.MaDH == id);

                if (dh != null)
                {
                    string oldStatus = dh.TrangThai;

                    if (action == "accept") dh.TrangThai = "Đang giao";
                    else if (action == "complete") dh.TrangThai = "Đã nhận hàng";
                    else if (action == "reject")
                    {
                        dh.TrangThai = "Đã hủy";
                        dh.GhiChu = string.IsNullOrEmpty(dh.GhiChu) ? note : dh.GhiChu + ". Lý do hủy: " + note;
                    }

                    // Lưu lịch sử thay đổi trạng thái
                    db.LichSuTrangThais.InsertOnSubmit(new LichSuTrangThai
                    {
                        MaDH = id,
                        TrangThaiCu = oldStatus,
                        TrangThaiMoi = dh.TrangThai,
                        ThoiGian = DateTime.Now,
                        GhiChu = note,
                        MaNhanVien = (Session["TaiKhoan"] as TaiKhoan)?.MaTK
                    });

                    db.SubmitChanges();
                    return "OK";
                }
                return "Lỗi: Không tìm thấy đơn hàng";
            }
            catch (Exception ex)
            {
                return "Lỗi server: " + ex.Message;
            }
        }


        // 3: LẤY CHI TIẾT 1 ĐƠN HÀNG
        public string LayChiTietDonHang()
        {
            try
            {
                string id_str = Request["id"];
                if (string.IsNullOrEmpty(id_str)) return "{}";
                int id = int.Parse(id_str);

                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                var dh = db.DonHangs.FirstOrDefault(d => d.MaDH == id);
                if (dh == null) return "{}";

                var user = db.TaiKhoans.FirstOrDefault(u => u.MaTK == dh.MaTK);
                var addr = db.DiaChis.FirstOrDefault(a => a.MaDiaChi == dh.MaDiaChi);

                // Lấy chi tiết sản phẩm
                var details = (from ct in db.ChiTietDonHangs
                               join sp in db.SanPhams on ct.MaSP equals sp.MaSP
                               where ct.MaDH == id
                               select new { sp.TenSP, sp.Anh, ct.SoLuong, ct.ThanhTien }).ToList();

                // --- SỬA LẠI: Lấy lịch sử xử lý an toàn hơn ---
                // Thay vì join phức tạp, ta lấy list lịch sử trước rồi mới map tên nhân viên
                var historyRaw = db.LichSuTrangThais
                                   .Where(ls => ls.MaDH == id)
                                   .OrderByDescending(ls => ls.ThoiGian)
                                   .ToList();

                var history = historyRaw.Select(ls => {
                    string actorName = "Khách hàng / Hệ thống";
                    if (ls.MaNhanVien != null)
                    {
                        var nv = db.TaiKhoans.FirstOrDefault(t => t.MaTK == ls.MaNhanVien);
                        if (nv != null) actorName = nv.HoTen;
                    }

                    return new
                    {
                        Time = ls.ThoiGian.HasValue ? ls.ThoiGian.Value.ToString("HH:mm dd/MM/yyyy") : "",
                        StatusOld = ls.TrangThaiCu,
                        StatusNew = ls.TrangThaiMoi,
                        Actor = actorName,
                        Note = ls.GhiChu
                    };
                }).ToList();
                // ------------------------------

                string statusUI = "pending";
                if (dh.TrangThai == "Đang giao") statusUI = "shipping";
                else if (dh.TrangThai == "Đã nhận hàng") statusUI = "completed";
                else if (dh.TrangThai == "Đã hủy") statusUI = "cancelled";

                decimal tongTienHang = dh.TongTienSanPham;
                decimal phiShip = dh.TongTien - tongTienHang + (dh.TienGiamGia ?? 0);

                var result = new
                {
                    id = dh.MaDH,
                    time = dh.Create_at.HasValue ? dh.Create_at.Value.ToString("HH:mm - dd/MM/yyyy") : "",
                    status = statusUI,
                    statusText = dh.TrangThai,
                    payMethod = dh.PhuongThucTT,
                    subTotal = tongTienHang,
                    shipFee = phiShip,
                    discount = dh.TienGiamGia ?? 0,
                    total = dh.TongTien,
                    note = dh.GhiChu,
                    cusName = addr != null ? addr.TenNguoiNhan : (user != null ? user.HoTen : "Khách vãng lai"),
                    cusPhone = addr != null ? addr.SDTNhan : (user != null ? user.SoDienThoai : ""),
                    cusAddr = addr != null ? addr.DiaChiCuThe : "Tại cửa hàng",
                    items = details.Select(d => new {
                        name = d.TenSP,
                        image = !string.IsNullOrEmpty(d.Anh) ? d.Anh.Replace("~", "") : "/img/no-image.jpg",
                        qty = d.SoLuong,
                        price = d.ThanhTien
                    }),
                    history = history
                };

                return JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                // Trả về lỗi JSON để client biết thay vì chết trang
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }

        // Helper: Map trạng thái tiếng Việt trong DB sang Class CSS trong View
        public string LayDanhSachDonHang()
        {
            try
            {
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

                // 1. Lấy tham số từ Client gửi lên
                string keyword = Request["keyword"];      // Tìm kiếm
                string status = Request["status"];        // Trạng thái
                string dateFromStr = Request["dateFrom"]; // Ngày bắt đầu
                string dateToStr = Request["dateTo"];     // Ngày kết thúc
                int page = 1;
                int.TryParse(Request["page"], out page);
                int pageSize = 10; // Số dòng mỗi trang

                // 2. Query cơ bản (Join bảng để lấy thông tin hiển thị)
                var query = from d in db.DonHangs
                            join t in db.TaiKhoans on d.MaTK equals t.MaTK
                            // Left join địa chỉ để lấy tên người nhận thực tế (nếu có)
                            join a in db.DiaChis on d.MaDiaChi equals a.MaDiaChi into da
                            from addr in da.DefaultIfEmpty()
                            where d.isDelete == 0 || d.isDelete == null
                            select new
                            {
                                d.MaDH,
                                d.Create_at,
                                d.TongTien,
                                d.TrangThai,
                                // Ưu tiên lấy tên/sđt từ địa chỉ giao hàng, nếu không có thì lấy từ tài khoản
                                TenKhach = addr != null ? addr.TenNguoiNhan : t.HoTen,
                                SDT = addr != null ? addr.SDTNhan : t.SoDienThoai
                            };

                // 3. Xử lý Lọc (Filter)

                // 3.1. Lọc theo trạng thái
                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    // Map value từ <select> của View sang dữ liệu trong DB
                    if (status == "pending") query = query.Where(x => x.TrangThai == "Chờ xác nhận" );
                    else if (status == "confirmed") query = query.Where(x => x.TrangThai == "Đã xác nhận");
                    else if (status == "shipping") query = query.Where(x => x.TrangThai == "Đang giao");
                    else if (status == "completed") query = query.Where(x => x.TrangThai == "Đã nhận hàng" );
                    else if (status == "cancelled") query = query.Where(x => x.TrangThai == "Đã hủy");
                }

                // 3.2. Lọc theo ngày (Date Range)
                if (!string.IsNullOrEmpty(dateFromStr))
                {
                    DateTime dtFrom;
                    if (DateTime.TryParse(dateFromStr, out dtFrom))
                        query = query.Where(x => x.Create_at >= dtFrom);
                }
                if (!string.IsNullOrEmpty(dateToStr))
                {
                    DateTime dtTo;
                    if (DateTime.TryParse(dateToStr, out dtTo))
                    {
                        // Cộng thêm 1 ngày để lấy trọn vẹn ngày kết thúc (vì DB lưu cả giờ phút)
                        dtTo = dtTo.AddDays(1);
                        query = query.Where(x => x.Create_at < dtTo);
                    }
                }

                // 3.3. Lọc theo từ khóa (Mã đơn, Tên, SĐT)
                if (!string.IsNullOrEmpty(keyword))
                {
                    string kw = keyword.ToLower().Trim();
                    // Thử parse xem có phải tìm theo mã số đơn hàng không
                    int searchId = 0;
                    bool isNumber = int.TryParse(kw.Replace("dh", ""), out searchId);

                    if (isNumber && kw.StartsWith("dh")) // Tìm chính xác mã đơn (VD: DH123)
                    {
                        query = query.Where(x => x.MaDH == searchId);
                    }
                    else
                    {
                        // Tìm theo Tên hoặc SĐT
                        query = query.Where(x => x.TenKhach.ToLower().Contains(kw) || x.SDT.Contains(kw));
                    }
                }

                // 4. Phân trang (Pagination)
                int totalItems = query.Count();
                int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
                if (page < 1) page = 1;
                if (page > totalPages && totalPages > 0) page = totalPages;

                var items = query.OrderByDescending(x => x.Create_at)
                                 .Skip((page - 1) * pageSize)
                                 .Take(pageSize)
                                 .ToList();

                // 5. Chuẩn bị dữ liệu trả về khớp với Javascript
                var resultList = items.Select(x => new
                {
                    id = x.MaDH,
                    code = "DH" + x.MaDH.ToString("D5"), // Format thành DH00001
                    customerName = x.TenKhach,
                    phone = x.SDT,
                    createdAt = x.Create_at.HasValue ? x.Create_at.Value.ToString("dd/MM/yyyy HH:mm") : "",
                    total = x.TongTien,
                    // Map trạng thái DB sang trạng thái CSS của View
                    status = MapStatusToCss(x.TrangThai),
                    statusText = x.TrangThai // Text hiển thị
                });

                return JsonConvert.SerializeObject(new
                {
                    TotalPages = totalPages,
                    CurrentPage = page,
                    List = resultList
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Error = ex.Message });
            }
        }

        private string MapStatusToCss(string dbStatus)
        {
            if (string.IsNullOrEmpty(dbStatus)) return "pending";
            string s = dbStatus.ToLower();
            if (s.Contains("chờ")) return "pending";
            if (s.Contains("xác nhận")) return "confirmed";
            if (s.Contains("giao")) return "shipping";
            if (s.Contains("nhận") || s.Contains("hoàn thành")) return "completed";
            if (s.Contains("hủy")) return "cancelled";
            return "pending";
        }
    }
}