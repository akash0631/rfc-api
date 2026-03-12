using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vendor_SRM_Routing_Application.Models.HU_Creation
{
    public class AddArticle
    {
        public string PONUMBER { get; set; } = String.Empty;
        public string ARTICLENUMBER { get; set; } = String.Empty;
        public string DESIGN { get; set; } = String.Empty;
        public string QTY { get; set; } = String.Empty;
        public string VENDORCODE { get; set; } = String.Empty;
        public string DOCUMENTNO { get; set; } = String.Empty;
        public string AMOUNT { get; set; } = String.Empty;
        public string DATE { get; set; } = String.Empty;
        public string EAN11 { get; set; } = String.Empty;
    }
}