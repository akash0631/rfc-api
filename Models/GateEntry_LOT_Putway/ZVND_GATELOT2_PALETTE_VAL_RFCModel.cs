using System;
using System.Collections.Generic;

namespace Vendor_SRM_Routing_Application.Models.GateEntry_LOT_Putway
{
    /// <summary>Request model for ZVND_GATELOT2_PALETTE_VAL_RFC.</summary>
    public class ZVND_GATELOT2_PALETTE_VAL_RFCRequest
    {
        /// <summary>SAP User ID (WWWOBJID)</summary>
        public string IM_USER { get; set; }
        /// <summary>Plant code (WERKS_D)</summary>
        public string IM_PLANT { get; set; }
        /// <summary>Picklist number (ZPICKLIST_NO)</summary>
        public string IM_PICKLIST { get; set; }
        /// <summary>Bin location (LGPLA)</summary>
        public string IM_BIN { get; set; }
        /// <summary>Palette number (ZZPALETTE)</summary>
        public string IM_PALL { get; set; }
    }
}