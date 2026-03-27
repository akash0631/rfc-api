using System;
using System.Collections.Generic;

namespace Vendor_SRM_Routing_Application.Models.GateEntry_LOT_Putway
{
    /// <summary>Request model for ZVND_PUT01_SAVE_DATA_RFC.</summary>
    public class ZVND_PUT01_SAVE_DATA_RFCRequest
    {
        /// <summary>SAP User ID (WWWOBJID)</summary>
        public string IM_USER { get; set; }
        /// <summary>Table of scanned HU data to save (ZTT_PUT01_SAVE)</summary>
        public List<PUT01SaveRow> IT_DATA { get; set; }
    }

    /// <summary>Row structure for ZTT_PUT01_SAVE table.</summary>
    public class PUT01SaveRow
    {
        public string HU     { get; set; }
        public string PALETTE { get; set; }
        public string BIN    { get; set; }
        public string PLANT  { get; set; }
        public string QTY    { get; set; }
    }
}