using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Vendor_SRM_Routing_Application.Models.PeperlessPicklist;

namespace Vendor_SRM_Routing_Application.Models.HU_Creation
{
    public class GetPicklistRequest
    {
        public string IM_USER { get; set; }
        public string IM_WERKS { get; set; }
        public DateTime? IM_DATUM { get; set; }
        //public string IM_PICNR { get; set; }
        //public string IT_DATA { get; set; }
    }
    public class GetPicklistDataRequest
    {
        public string IM_USER { get; set; }
        public string IM_WERKS { get; set; }
        public DateTime? IM_DATUM { get; set; }
        public string IM_PICNR { get; set; }
        //public string IT_DATA { get; set; }
    }
    public class PostPicklistDataRequest
    {
        public string IM_USER { get; set; }
        public string IM_WERKS { get; set; }
        public DateTime? IM_DATUM { get; set; }
        public string IM_PICNR { get; set; }
        public List<GetPicklistEtData> IT_DATA { get; set; } = new List<GetPicklistEtData>();
    }
}