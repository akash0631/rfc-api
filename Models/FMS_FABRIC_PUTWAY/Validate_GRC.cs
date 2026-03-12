using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FMS_Fabric_Putway_Api.Models
{
    public class Validate_GRC
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public Validate_GRCResponse Data;
        public Validate_GRC()
        {
            Data = new Validate_GRCResponse();
        }
    }
    public class Validate_GRCRequest
    {
        public string IM_USER { get; set; } = String.Empty;
        public string IM_GRC { get; set; } = String.Empty;

    }
    public class Validate_GRCResponse
    {
        public List<scan_barcodeResponse> scan_barcode;
        public List<article_batch_wise_qty_matchResponse> article_batch_wise_qty_match;
       
    }
    public class scan_barcodeResponse
    {
        
        public string material { get; set; }
        public string qty { get; set; }
        public string barcode { get; set; }
    }
    public class article_batch_wise_qty_matchResponse
    {

        public string WAREHOUSE { get; set; }
        public string SITE { get; set; }
        public string SLOC { get; set; }
        public string CRATE { get; set; }
        public string BIN_TYPE { get; set; }
        public string BIN { get; set; }
        public string MATERIAL { get; set; }
        public string SCAN_QTY { get; set; }
        public string KEY { get; set; }
        public string SOURCE_BIN { get; set; }
        public string BATCH { get; set; }
        public string REQ_QTY { get; set; }
        public string UOM { get; set; }
        public string GRC_NO { get; set; }
        public string MJAHR { get; set; }
        public string GR_LINE { get; set; }
        public string PUR_NO { get; set; }
        public string PUR_LINE { get; set; }
    }
}