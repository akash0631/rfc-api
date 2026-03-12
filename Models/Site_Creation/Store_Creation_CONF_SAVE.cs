using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Vendor_Application_MVC.Models;

namespace Vendor_SRM_Routing_Application.Models.Site_Creation
{
    public class Store_Creation_CONF_SAVE
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<Store_Creation_CONF_SAVEResponse> Data;
        public Store_Creation_CONF_SAVE()
        {
            Data = new List<Store_Creation_CONF_SAVEResponse>();
        }
    }
    public class Store_Creation_CONF_SAVERequest
    {
        public string SRNO { get; set; }
        public string PROC_DESC { get; set; }
        //public string PROC_CONF { get; set; }
        public string ACT_START_DATE { get; set; }
        public string ACT_END_DATE { get; set; }
        public string REMARK { get; set; }
  

    }
    public class Store_Creation_CONF_SAVEResponse
    {
        public string TEXT { get; set; }
        public string SRNO { get; set; }

    }
}