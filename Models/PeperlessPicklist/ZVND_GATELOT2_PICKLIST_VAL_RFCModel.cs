using System;
using System.Collections.Generic;

namespace Vendor_SRM_Routing_Application.Models.PeperlessPicklist
{
    /// <summary>Request model for ZVND_GATELOT2_PICKLIST_VAL_RFC.</summary>
    public class ZVND_GATELOT2_PICKLIST_VAL_RFCRequest
    {
        /// <summary>SAP User ID (WWWOBJID)</summary>
        public string IM_USER { get; set; }
        /// <summary>Plant code (WERKS_D)</summary>
        public string IM_PLANT { get; set; }
    }
}