using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace VendorSRM_Application.Models
{
    public class RoutingStatusList
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<RoutingStatusListResponse> Data;
        public RoutingStatusList()
        {
            Data = new List<RoutingStatusListResponse>();
        }
    }
    public class RoutingStatusListRequest
    {

        public string PO_NUM { get; set; }
     
        public string VENDOR_CODE { get; set; }
     
   

    }
    public class RoutingStatusListResponse
    {
        public string PONUMBER { get; set; }
        public string ARTICLENUMBER { get; set; }
        public string DESIGN { get; set; }
        public string QTY { get; set; }
        public string VENDORCODE { get; set; }
        public string DOCUMENTNO { get; set; }
        public string AMOUNT { get; set; }
        public string Date { get; set; }
        public string EAN11 { get; set; }

    }
}