using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Vendor_Application_MVC.Models;

namespace Vendor_SRM_Routing_Application.Models.Site_Creation
{
    public class Store_Creation_List
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<Store_Creation_ListResponse> Data;
        public Store_Creation_List()
        {
            Data = new List<Store_Creation_ListResponse>();
        }
    }
    public class Store_Creation_ListRequest
    {
        public string IM_SITE_CODE { get; set; }
        //public string IM_PO { get; set; }
        //public string IM_DESIGN { get; set; }

    }
    public class Store_Creation_ListResponse
    {
        public string TEXT { get; set; }
        public string SRNO { get; set; }

    }
}