using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using WebBanDoAnOnline.Models; 

namespace WebBanDoAnOnline.Business
{
    
    public static class TroGiupThongBao
    {
        
        public static void TaoThongBao(int maTK, string title, string subtitle)
        {
            try
            {
                
                using (var db = new BanDoAnOnlineDataContext())
                {
                    var tb = new ThongBao();
                    tb.MaTK = maTK;
                    tb.Title = title;
                    tb.Subtitle = subtitle;
                    tb.IsRead = false; 
                    tb.CreatedAt = DateTime.Now;

                    db.ThongBaos.InsertOnSubmit(tb);
                    db.SubmitChanges();
                }
            }
            catch (Exception)
            {
                
            }
        }
    }
}