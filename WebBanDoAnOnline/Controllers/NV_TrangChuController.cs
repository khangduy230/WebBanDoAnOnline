using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using WebBanDoAnOnline.Models;

namespace WebBanDoAnOnline.Controllers
{
    public class NV_TrangChuController : Controller
    {
        // Kiểm tra phiên đăng nhập và vai trò
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var user = Session["TaiKhoan"] as TaiKhoan;

            // 1. Chưa đăng nhập -> Về trang login
            if (user == null)
            {
                filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary(new { controller = "TaiKhoan", action = "Login", area = "" }));
                return;
            }

            // 2. Đã đăng nhập nhưng khác nhân viên
            if (user.VaiTro != "Nhân viên")
            {
                if (user.VaiTro == "Khách hàng")
                {
                    filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary(new { controller = "Home", action = "Index", area = "" }));
                }
                else
                {
                    filterContext.Result = new RedirectToRouteResult(new System.Web.Routing.RouteValueDictionary(new { controller = "QL_TrangChu", action = "Index", area = "" }));
                }
                return;
            }

            // Nếu là Nhân viên thì cho qua
            base.OnActionExecuting(filterContext);
        }

        // 1. GET: Trang quản lý đơn hàng
        public ActionResult Index()
        {
            return View();
        }

        // 2. GET: Trang chi tiết đơn hàng
        public ActionResult ThongTinDonHang(string id)
        {
            ViewBag.OrderId = id;
            return View();
        }

        
        //  1: LẤY DANH SÁCH ĐƠN HÀNG (JSON)
       
        // LayDonHang
        public string GetOrders()
        {
            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            string filterStatus = Request["status"]; 

            var query = db.DonHangs.Where(d => d.isDelete == 0 || d.isDelete == null);

            
            if (!string.IsNullOrEmpty(filterStatus) && filterStatus != "all")
            {
                if (filterStatus == "pending") query = query.Where(d => d.TrangThai == "Chờ xác nhận");
                else if (filterStatus == "shipping") query = query.Where(d => d.TrangThai == "Đang giao" || d.TrangThai == "Đã xác nhận");
                else if (filterStatus == "completed") query = query.Where(d => d.TrangThai == "Đã nhận hàng");
                else if (filterStatus == "cancelled") query = query.Where(d => d.TrangThai == "Đã hủy");
            }

            // Lấy danh sách đơn mới nhất trước
            var listRaw = query.OrderByDescending(d => d.Create_at).ToList();

            var listResult = new List<object>();
            foreach (var item in listRaw)
            {
                // Lấy sản phẩm đầu tiên để làm ảnh đại diện
                var firstDetail = db.ChiTietDonHangs.FirstOrDefault(c => c.MaDH == item.MaDH);
                string img = "/img/no-image.jpg";
                string prodName = "Đơn hàng " + item.MaDH;
                int qty = 0;
                
                if (firstDetail != null)
                {
                    var sp = db.SanPhams.FirstOrDefault(s => s.MaSP == firstDetail.MaSP);
                    if (sp != null && !string.IsNullOrEmpty(sp.Anh)) img = sp.Anh.Replace("~", ""); 

                    qty = db.ChiTietDonHangs.Where(c => c.MaDH == item.MaDH).Sum(c => c.SoLuong) ?? 0;
                    prodName = firstDetail.TenSP + (qty > firstDetail.SoLuong ? $"... (+{qty - firstDetail.SoLuong} món)" : "");
                }

                
                string statusUI = "pending";
                if (item.TrangThai == "Đang giao" || item.TrangThai == "Đã xác nhận") statusUI = "shipping";
                else if (item.TrangThai == "Đã nhận hàng") statusUI = "completed";
                else if (item.TrangThai == "Đã hủy") statusUI = "cancelled";

                listResult.Add(new
                {
                    id = item.MaDH,
                    name = prodName,
                    quantity = qty,
                    price = item.TongTien,
                    time = item.Create_at.HasValue ? item.Create_at.Value.ToString("HH:mm - dd/MM/yyyy") : "",
                    status = statusUI,
                    statusText = item.TrangThai,
                    image = img,
                    note = item.GhiChu
                });
            }

            return JsonConvert.SerializeObject(listResult);
        }

        
        // API 2: CẬP NHẬT TRẠNG THÁI ĐƠN
        // CapNhatTrangThai
        public string UpdateStatus()
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
        //LayChiTietDonHang

        public string GetOrderDetail()
        {
            string id_str = Request["id"];
            if (string.IsNullOrEmpty(id_str)) return "{}";
            int id = int.Parse(id_str);

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var dh = db.DonHangs.FirstOrDefault(d => d.MaDH == id);
            if (dh == null) return "{}";

            // Lấy thông tin khách
            var user = db.TaiKhoans.FirstOrDefault(u => u.MaTK == dh.MaTK);
            var addr = db.DiaChis.FirstOrDefault(a => a.MaDiaChi == dh.MaDiaChi);

            // Lấy chi tiết món
            var details = (from ct in db.ChiTietDonHangs
                           join sp in db.SanPhams on ct.MaSP equals sp.MaSP
                           where ct.MaDH == id
                           select new
                           {
                               sp.TenSP,
                               sp.Anh,
                               ct.SoLuong,
                               ct.DonGia,
                               ct.ThanhTien
                           }).ToList();

            
            string statusUI = "pending";
            if (dh.TrangThai == "Đang giao") statusUI = "shipping"; 
            else if (dh.TrangThai == "Đã nhận hàng") statusUI = "completed";
            else if (dh.TrangThai == "Đã hủy") statusUI = "cancelled";

            var result = new
            {
                id = dh.MaDH,
                time = dh.Create_at.HasValue ? dh.Create_at.Value.ToString("HH:mm - dd/MM/yyyy") : "",
                status = statusUI,
                statusText = dh.TrangThai,
                payMethod = dh.PhuongThucTT,
                shipFee = 25000, 
                total = dh.TongTien,

                cusName = addr != null ? addr.TenNguoiNhan : user.HoTen,
                cusPhone = addr != null ? addr.SDTNhan : user.SoDienThoai,
                cusAddr = addr != null ? addr.DiaChiCuThe : "Tại cửa hàng",

                items = details.Select(d => new {
                    name = d.TenSP,
                    image = !string.IsNullOrEmpty(d.Anh) ? d.Anh.Replace("~", "") : "/img/no-image.jpg",
                    qty = d.SoLuong,
                    price = d.ThanhTien
                })
            };

            return JsonConvert.SerializeObject(result);
        }
    }
}