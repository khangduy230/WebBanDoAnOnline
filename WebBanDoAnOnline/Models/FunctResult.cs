using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Web;

namespace DemoAdminLTETheme.Models
{
    public enum EnumErrCode
    {
        Error = -1,
        Empty = 0,
        Success = 1,
        Fail = 2,
        NotExist = 3,
        Existent = 4
    }

    public class FunctResult <T>
    {
        public EnumErrCode ErrCode { get; set; }
        public string ErrDesc { get; set; }
        public T Data { get; set; }
    }
}