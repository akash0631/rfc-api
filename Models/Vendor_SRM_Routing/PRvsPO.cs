using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vendor_Application_MVC.Models
{
    
    public class PRvsPO
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<PRvsPOResponse> Data;
        public PRvsPO()
        {
            Data = new List<PRvsPOResponse>();
        }
    }
    public class PRvsPORequest
    {
        public string Year { get; set; }=string.Empty;
        public string Majcat { get; set; }=string.Empty ;

    }
    public class PRvsPOResponse
    {
        


        public string MAJ_CAT_CD { get; set; }
        public string MATCAT { get; set; }
        public string JAN_QTY_PR { get; set; }
        public string JAN_QTY_PO { get; set; }
        public string JAN_QTY_DIFF { get; set; }
        public string FEB_QTY_PR { get; set; }
        public string FEB_QTY_PO { get; set; }
        public string FEB_QTY_DIFF { get; set; }
        public string MAR_QTY_PR { get; set; }
        public string MAR_QTY_PO { get; set; }
        public string MAR_QTY_DIFF { get; set; }
        public string APR_QTY_PR { get; set; }
        public string APR_QTY_PO { get; set; }
        public string APR_QTY_DIFF { get; set; }
        public string MAY_QTY_PR { get; set; }
        public string MAY_QTY_PO { get; set; }
        public string MAY_QTY_DIFF { get; set; }
        public string JUN_QTY_PR { get; set; }
        public string JUN_QTY_PO { get; set; }
        public string JUN_QTY_DIFF { get; set; }
        public string JUL_QTY_PR { get; set; }
        public string JUL_QTY_PO { get; set; }
        public string JUL_QTY_DIFF { get; set; }
        public string AUG_QTY_PR { get; set; }
        public string AUG_QTY_PO { get; set; }
        public string AUG_QTY_DIFF { get; set; }
        public string SEP_QTY_PR { get; set; }
        public string SEP_QTY_PO { get; set; }
        public string SEP_QTY_DIFF { get; set; }
        public string OCT_QTY_PR { get; set; }
        public string OCT_QTY_PO { get; set; }
        public string OCT_QTY_DIFF { get; set; }
        public string NOV_QTY_PR { get; set; }
        public string NOV_QTY_PO { get; set; }
        public string NOV_QTY_DIFF { get; set; }
        public string DEC_QTY_PR { get; set; }
        public string DEC_QTY_PO { get; set; }
        public string DEC_QTY_DIFF { get; set; }


    }
}