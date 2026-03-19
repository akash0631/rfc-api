using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace VendorSRM_Application.Models
{
    public class Submit_Routing_Status
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<Submit_Routing_StatusResponse> Data;
        public Submit_Routing_Status()
        {
            Data = new List<Submit_Routing_StatusResponse>();
        }
    }
    public class Submit_Routing_StatusRequest
    {
        public string HU_NO { get; set; }
        public string PO_NO { get; set; }
        public string ARTICLE_NO { get; set; }
        public string DESIGN { get; set; }
        public string QUANTITY { get; set; }
        public string VENDOR_CODE { get; set; }
        public string Europe_Art { get; set; }
        public float Max_Qty { get; set; }

        //public string CREATION_DATE { get; set; }
        //public string CREATION_TIME { get; set; }
        //public string CREATION_USER { get; set; }
        //public string MESSAGE { get; set; }

        //public string STATUS { get; set; }
        public string TempHuNumber { get; set; }



    }
    public class Submit_Routing_StatusResponse
    {
        public string PO_Number { get; set; }

    }
}