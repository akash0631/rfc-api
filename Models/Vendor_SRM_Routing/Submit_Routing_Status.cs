using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vendor_Application_MVC.Models
{
    public class Submit_Routing_Status
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<Submit_Routing_StatusResponse> Data;
        public string AsnNo { get; set; }
        public Submit_Routing_Status()
        {
            Data = new List<Submit_Routing_StatusResponse>();
        }
    }


    public class Submit_Routing_StatusRequest
    {
        public string PO_NO { get; set; }
        public string HHTUSER { get; set; }
        public string Maj_Cat { get; set; }
        public string Design_No { get; set; }
        public string Qty { get; set; }
        public string Status { get; set; }
        public string Article_Number { get; set; }=string.Empty;
        public string Remarks { get; set; }=string.Empty;
    }
    public class Submit_RR_Routing_StatusRequest
    {
        public string PoId { get; set; }
        public string PO_NO { get; set; }
        public string Article { get; set; } = string.Empty;
        public string Status { get; set; }
    }
    public class Submit_Routing_StatusResponse
    {
        public string PO_Number { get; set; }

    }


    public class PO_COMP_Status
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        
    }
    public class PO_COMP_Request
    {
        public string MANDT { get; set; }
        public string EBELN { get; set; } = string.Empty;
        public string SNO { get; set; } = string.Empty;
        public string VALUE { get; set; } = string.Empty;
        //public string VENDOR_CRM { get; set; }
        //public string COLOR_SD { get; set; }
        //public string PACKING_NCV { get; set; } = string.Empty;
        //public string PACK_SPP { get; set; }
        //public string TAFETA_B { get; set; }
        //public string BARCODING_V { get; set; } = string.Empty;
        //public string BARCODE_TKA { get; set; }
        //public string MIN_SED { get; set; }
        //public string PRIVATE_L { get; set; } = string.Empty;
        //public string NAME_PPL { get; set; }
        //public string STANDARD_C { get; set; }
        //public string PLANNED_B { get; set; } = string.Empty;
        //public string SEALED_S { get; set; }
        //public string PPT_TYPE { get; set; }
    }
    
}
