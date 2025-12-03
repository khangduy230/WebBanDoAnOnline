using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebBanDoAnOnline.Models;
using Newtonsoft.Json;

namespace WebBanDoAnOnline.Controllers
{
    public class GioHangController : Controller
    {
        // GET: GioHang
        public ActionResult Index()
        {
            if (Session["TaiKhoan"] == null) return RedirectToAction("Login", "TaiKhoan");
            return View();
        }

        // 1: Lấy danh sách giỏ hàng
       
        public string Lay_DSGioHang()
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null) return "[]";

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            DateTime now = DateTime.Now;

            var query = from g in db.GioHangs
                        join p in db.SanPhams on g.MaSP equals p.MaSP
                        where g.MaTK == user.MaTK
                              && (g.isDelete == null || g.isDelete == 0)
                              && (p.isDelete == null || p.isDelete == 0)
                        select new { g, p };

            if (query.Any())
            {
                var listResult = new List<object>();
                foreach (var item in query)
                {
                    
                    decimal giaGoc = item.p.Gia ?? 0;
                    decimal giaKM = item.p.GiaKhuyenMai ?? 0;
                    decimal giaBan = giaGoc;
                    bool dangGiamGia = false;

                    if (giaKM > 0 && giaKM < giaGoc)
                    {
                        if ((!item.p.NgayBatDauKM.HasValue || item.p.NgayBatDauKM <= now) &&
                            (!item.p.NgayKetThucKM.HasValue || item.p.NgayKetThucKM >= now))
                        {
                            giaBan = giaKM;
                            dangGiamGia = true;
                        }
                    }

                    listResult.Add(new
                    {
                        MaSP = item.p.MaSP,
                        TenSP = item.p.TenSP,
                        Anh = item.p.Anh,
                        GiaHienTai = giaBan,
                        GiaGoc = giaGoc,
                        DangGiamGia = dangGiamGia,
                        SoLuong = item.g.SoLuong ?? 1,
                        GhiChu = item.g.GhiChu ?? "",
                        ThanhTien = giaBan * (item.g.SoLuong ?? 1)
                    });
                }
                return JsonConvert.SerializeObject(listResult);
            }
            return "[]";
        }

        // 2: Thêm vào giỏ (ThemGioHang)
       
        public string AddToCart()
        {
            string id_str = Request["productId"];
            string qty_str = Request["quantity"];
            string note_str = Request["notes"];

            int id = 0; int qty = 1;
            if (!string.IsNullOrEmpty(id_str)) id = int.Parse(id_str);
            if (!string.IsNullOrEmpty(qty_str)) qty = int.Parse(qty_str);

            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null) return "Bạn cần đăng nhập để mua hàng";

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();

            // Check sản phẩm tồn tại
            var sp = db.SanPhams.FirstOrDefault(x => x.MaSP == id && (x.isDelete == 0 || x.isDelete == null));
            if (sp == null || sp.TrangThai != "Còn hàng") return "Sản phẩm tạm hết hàng hoặc không tồn tại";

            // Check trong giỏ 
            var cartItem = db.GioHangs.FirstOrDefault(g => g.MaSP == id && g.MaTK == user.MaTK);

            if (cartItem != null)
            {
                if (cartItem.isDelete == 1)
                {
                    // Nếu đã xóa -> Khôi phục lại
                    cartItem.isDelete = 0;
                    cartItem.SoLuong = qty;
                }
                else
                {
                    // Nếu đang có -> Cộng dồn
                    cartItem.SoLuong = (cartItem.SoLuong ?? 0) + qty;
                }

                if (!string.IsNullOrEmpty(note_str)) cartItem.GhiChu = note_str;
                cartItem.LastEdit_at = DateTime.Now;
            }
            else
            {
                // Chưa có -> Thêm mới
                GioHang newItem = new GioHang();
                newItem.MaTK = user.MaTK;
                newItem.MaSP = id;
                newItem.SoLuong = qty;
                newItem.GhiChu = note_str;
                newItem.Create_at = DateTime.Now;
                newItem.isDelete = 0;
                db.GioHangs.InsertOnSubmit(newItem);
            }

            db.SubmitChanges();
            return "Đã thêm vào giỏ hàng";
        }

        //  3: Cập nhật số lượng
        
        public string CapNhat_SoLuong()
        {
            string id_str = Request["id"];
            string delta_str = Request["delta"];
            var user = Session["TaiKhoan"] as TaiKhoan;

            if (user != null && !string.IsNullOrEmpty(id_str))
            {
                int id = int.Parse(id_str);
                int delta = int.Parse(delta_str);
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                var item = db.GioHangs.FirstOrDefault(g => g.MaSP == id && g.MaTK == user.MaTK && (g.isDelete == 0 || g.isDelete == null));

                if (item != null)
                {
                    int newQty = (item.SoLuong ?? 1) + delta;
                    if (newQty < 1) newQty = 1;
                    item.SoLuong = newQty;
                    item.LastEdit_at = DateTime.Now;
                    db.SubmitChanges();
                    return "OK";
                }
            }
            return "Fail";
        }

        //4: Cập nhật Ghi chú
       
        public string CapNhat_GhiChu()
        {
            string id_str = Request["id"];
            string note_str = Request["ghichu"];
            var user = Session["TaiKhoan"] as TaiKhoan;

            if (user != null && !string.IsNullOrEmpty(id_str))
            {
                int id = int.Parse(id_str);
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                var item = db.GioHangs.FirstOrDefault(g => g.MaSP == id && g.MaTK == user.MaTK && (g.isDelete == 0 || g.isDelete == null));
                if (item != null)
                {
                    item.GhiChu = note_str;
                    item.LastEdit_at = DateTime.Now;
                    db.SubmitChanges();
                    return "OK";
                }
            }
            return "Fail";
        }

        // 5: Xóa sản phẩm
    
        public string Xoa_SP_GioHang()
        {
            string id_str = Request["id"];
            var user = Session["TaiKhoan"] as TaiKhoan;

            if (user != null && !string.IsNullOrEmpty(id_str))
            {
                int id = int.Parse(id_str);
                BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
                var item = db.GioHangs.FirstOrDefault(g => g.MaSP == id && g.MaTK == user.MaTK && (g.isDelete == 0 || g.isDelete == null));
                if (item != null)
                {
                    item.isDelete = 1;
                    item.Delete_at = DateTime.Now;
                    db.SubmitChanges();
                    return "OK";
                }
            }
            return "Fail";
        }

        //  Lưu danh sách ID sản phẩm được chọn trước khi sang thanh toán
       
        public string LuuSanPhamThanhToan()
        {
            try
            {
                
                string ids = Request["selectedIds"];

                if (string.IsNullOrEmpty(ids))
                {
                   
                    Session["CheckoutItems"] = null;
                    return "Vui lòng chọn ít nhất 1 sản phẩm.";
                }

                
                Session["CheckoutItems"] = ids;

                return "OK";
            }
            catch (Exception ex)
            {
                return "Lỗi: " + ex.Message;
            }
        }
    }
}