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
        // 1. GET: GioHang (Trang chủ giỏ hàng)
        public ActionResult Index()
        {
            if (Session["TaiKhoan"] == null)
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }
            return View();
        }

        // 2. API: Lấy danh sách (Khớp tên với View JS: Lay_DSGioHang)
        [HttpPost]
        public string Lay_DSGioHang()
        {
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null) return "[]";

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var query = from g in db.GioHangs
                        join p in db.SanPhams on g.MaSP equals p.MaSP
                        where g.MaTK == user.MaTK
                              && (g.isDelete == null || g.isDelete == 0)
                              && (p.isDelete == null || p.isDelete == 0)
                        select new { g, p };

            if (query.Any())
            {
                var listResult = new List<object>();
                DateTime now = DateTime.Now;

                foreach (var item in query)
                {
                    decimal giaGoc = item.p.Gia ?? 0;
                    decimal giaKM = item.p.GiaKhuyenMai ?? 0;
                    decimal giaBan = giaGoc;
                    bool isKM = false;

                    if (giaKM > 0 && giaKM < giaGoc)
                    {
                        if ((!item.p.NgayBatDauKM.HasValue || item.p.NgayBatDauKM <= now) &&
                            (!item.p.NgayKetThucKM.HasValue || item.p.NgayKetThucKM >= now))
                        {
                            giaBan = giaKM;
                            isKM = true;
                        }
                    }

                    listResult.Add(new
                    {
                        MaSP = item.p.MaSP,
                        TenSP = item.p.TenSP,
                        Anh = item.p.Anh,
                        GiaHienTai = giaBan,
                        GiaGoc = giaGoc,
                        DangGiamGia = isKM,
                        SoLuong = item.g.SoLuong ?? 1,
                        GhiChu = item.g.GhiChu,
                        ThanhTien = giaBan * (item.g.SoLuong ?? 1)
                    });
                }
                return JsonConvert.SerializeObject(listResult);
            }
            return "[]";
        }

        // 3. API: Cập nhật số lượng
        [HttpPost]
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

        // 4. API: Cập nhật Ghi chú
        [HttpPost]
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

        // 5. API: Xóa sản phẩm
        [HttpPost]
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

        // 6. API AddToCart (Cho trang sản phẩm)
        [HttpPost]
        public string AddToCart()
        {
            string id_str = Request["productId"];
            string qty_str = Request["quantity"];
            string note_str = Request["notes"];
            var user = Session["TaiKhoan"] as TaiKhoan;
            if (user == null) return "Bạn cần đăng nhập";

            int id = int.Parse(id_str);
            int qty = int.Parse(qty_str);

            BanDoAnOnlineDataContext db = new BanDoAnOnlineDataContext();
            var cartItem = db.GioHangs.FirstOrDefault(g => g.MaSP == id && g.MaTK == user.MaTK);

            if (cartItem != null)
            {
                if (cartItem.isDelete == 1) { cartItem.isDelete = 0; cartItem.SoLuong = qty; }
                else { cartItem.SoLuong = (cartItem.SoLuong ?? 0) + qty; }
                if (!string.IsNullOrEmpty(note_str)) cartItem.GhiChu = note_str;
                cartItem.LastEdit_at = DateTime.Now;
            }
            else
            {
                GioHang newItem = new GioHang { MaTK = user.MaTK, MaSP = id, SoLuong = qty, GhiChu = note_str, Create_at = DateTime.Now, isDelete = 0 };
                db.GioHangs.InsertOnSubmit(newItem);
            }
            db.SubmitChanges();
            return "Thêm thành công";
        }
    }
}