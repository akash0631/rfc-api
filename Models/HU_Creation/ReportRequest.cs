using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace VendorSRM_Application.Models
{
    public class ReportRequest
    {
        //public int Type { get; set; }
        public string VendorCode { get; set; } = String.Empty;
        public string PONumber { get; set; } = String.Empty;

    }
    public class AllReportRequest
    {
        //public int Type { get; set; }
        public string VendorCode { get; set; } = String.Empty;
        //public string PONumber { get; set; } = String.Empty;

    }
}