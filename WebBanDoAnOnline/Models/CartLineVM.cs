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
        public decimal Gia { get; set; }
        public string Anh { get; set; }
        public int SoLuong { get; set; }
        public string GhiChu { get; set; }
        public decimal ThanhTien => Gia * SoLuong;
    }
}