using DocumentFormat.OpenXml.Vml.Office;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vendor_Application_MVC.Models
{
    public class PO_Detail
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<PO_DetailResponse> Data;
        public PO_Detail()
        {
            Data = new List<PO_DetailResponse>();
        }
    }
    public class PO_DetailRequest
    {
        public string IM_PO { get; set; }
       

    }
    public class PO_DetailResponse
    {
        public string PO_NO { get; set; }
        public string Maj_Cat { get; set; }
        public string Design_No { get; set; }
        public string Qty { get; set; }
        public string ArticleNo { get; set; }
        public string Maj_Desc { get; set; }
        public string PO_Creation { get; set; } = String.Empty;
        public string PO_Delivery { get; set; } = String.Empty;
        public string CurrentStatus { get; set; } = String.Empty;
        public string FILEPATH { get; set; } = String.Empty;
        public string Site { get; set; } = String.Empty;
    }

    public class PO_COMP_POST
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<PO_DetailResponseCOMP> Data;
        public PO_COMP_POST()
        {
            Data = new List<PO_DetailResponseCOMP>();
        }
    }
    public class PO_DetailResponseCOMP
    {
        public string MANDT { get; set; }
        public string FIELD_NAME { get; set; }
        public string SNO { get; set; }
        public string FIELD_VALUE { get; set; }

    }

    public class Payment
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<PaymentResponse> Data;
        public Payment()
        {
            Data = new List<PaymentResponse>();
        }
    }
    public class Payment_Request
    {
        public string vendorcode { get; set; }


    }
    public class PaymentResponse
    {
        public string Vendor_CD { get; set; }
        public string Vendor_Name { get; set; }
        public string City        { get; set; }
        public string Payment_Doc_DT        { get; set; }
        public string Payment_Doc_No        { get; set; }
        public string Amount        { get; set; }
        public string PO_Number { get; set; } = String.Empty;
        public string Delivery_Date { get; set; } = String.Empty;
        public string Bill_No { get; set; } = String.Empty;
        public string QTY        { get; set; } = String.Empty;
        public string Net_Value { get; set; }
        public string GR_Qty { get; set; } = String.Empty;
        public string GR_Value { get; set; } = String.Empty;
        public string Gate_Entry_Number        { get; set; } = String.Empty;
        public string Gate_Entry_Date        { get; set; } = String.Empty;
        public string Gate_Entry_Time
        { get; set; } = String.Empty;
        public string Invoice_Qty { get; set; } = String.Empty;
        public string Invoice_Value { get; set; } = String.Empty;
    }
}
