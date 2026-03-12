using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vendor_SRM_Routing_Application.Models.Sampling
{
    public class YesNoTTRequest
    {
        public string ID { get; set; } = String.Empty;
        public string Article { get; set; } = String.Empty;
        public string Creation_Dt { get; set; } = String.Empty;
        public string Creation_Tm { get; set; } = String.Empty;
        public string Status { get; set; } = String.Empty;
        public string Remarks { get; set; } = String.Empty;
    }
}