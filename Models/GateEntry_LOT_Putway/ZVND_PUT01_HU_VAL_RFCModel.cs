using System;
using System.Collections.Generic;

namespace Vendor_SRM_Routing_Application.Models.GateEntry_LOT_Putway
{
    /// <summary>Request model for ZVND_PUT01_HU_VAL_RFC.</summary>
    public class ZVND_PUT01_HU_VAL_RFCRequest
    {
        /// <summary>SAP User ID (WWWOBJID)</summary>
        public string IM_USER { get; set; }
        /// <summary>Plant code (WERKS_D)</summary>
        public string IM_PLANT { get; set; }
        /// <summary>Handling Unit number (ZEXT_HU)</summary>
        public string IM_HU { get; set; }
    }
}