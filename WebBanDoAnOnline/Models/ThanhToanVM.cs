using System.Collections.Generic;

namespace WebBanDoAnOnline.Models
{
    // 1 dòng hàng trong trang thanh toán
    public class DongThanhToanVM
    {
        public int MaSP { get; set; }
        public string TenSP { get; set; }
        public string Anh { get; set; }
        public decimal DonGia { get; set; }      // giá áp dụng
        public decimal GiaGoc { get; set; }      // giá gốc (nếu có)
        public bool OnSale { get; set; }
        public int SoLuong { get; set; }
        public string GhiChu { get; set; }
        public decimal ThanhTien => DonGia * (SoLuong > 0 ? SoLuong : 1);
    }

    // Toàn bộ dữ liệu cần cho trang thanh toán
    public class TrangThanhToanVM
    {
        public List<DongThanhToanVM> DanhSach { get; set; } = new List<DongThanhToanVM>();
        public List<DiaChi> DanhSachDiaChi { get; set; } = new List<DiaChi>();
        public int? MaDiaChiDaChon { get; set; }

        public string PhuongThucThanhToan { get; set; } = "COD"; // COD | BANK
        public string MaVoucher { get; set; }
        public string GhiChu { get; set; }

        // Tóm tắt tiền
        public decimal TongTienHang { get; set; }   // tổng tiền hàng
        public decimal PhiGiaoHang { get; set; }    // phí giao hàng
        public decimal PhuPhi { get; set; }         // phụ phí/dịch vụ
        public decimal GiamGia { get; set; }        // giảm giá từ voucher
        public decimal TongThanhToan { get; set; }  // tổng cuối cùng
    }
}