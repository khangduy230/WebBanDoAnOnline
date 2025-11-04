using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebBanDoAnOnline.Models
{
    public class CartLineVM
    {
        public int MaSP { get; set; }
        public string TenSP { get; set; }

        // Giữ tương thích: Gia sẽ là "giá áp dụng" (đã xét khuyến mãi)
        public decimal Gia { get; set; }

        // Bổ sung hiển thị/logic
        public decimal GiaGoc { get; set; }
        public decimal GiaApDung { get; set; }
        public bool OnSale { get; set; }

        public string Anh { get; set; }
        public int SoLuong { get; set; }
        public string GhiChu { get; set; }

        public decimal ThanhTien
            => (GiaApDung > 0 ? GiaApDung : Gia) * SoLuong;
    }
}