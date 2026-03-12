using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vendor_SRM_Routing_Application.Models.PeperlessPicklist
{
    public class GetPicklistEtData
    {
        public string WM_NO { get; set; } = String.Empty;
        public string Material { get; set; } = String.Empty;
        public string Plant { get; set; } = String.Empty;
        public string Stor_Loc { get; set; } = String.Empty;
        public string Batch { get; set; } = String.Empty;
        public string Crate { get; set; } = String.Empty;
        public string Bin { get; set; } = String.Empty;
        public string Storage_Type { get; set; } = String.Empty;
        public string MEINS { get; set; } = String.Empty;
        public string Avl_Stock { get; set; } = String.Empty;
        public string Open_Stock { get; set; } = String.Empty;
        public string Scan_Qty { get; set; } = String.Empty;
        public string PICNR { get; set; } = String.Empty;
        public string Pick_Qty { get; set; } = String.Empty;
        public string Hu_No { get; set; } = String.Empty;
        public string Barcode { get; set; } = String.Empty;
        public string Matkl { get; set; } = String.Empty;
        public string WGBEZ { get; set; } = String.Empty;
        public string Sonum { get; set; } = String.Empty;
        public string Delnum { get; set; } = String.Empty;
        public string Posnr { get; set; } = String.Empty;
        public string GNature { get; set; } = String.Empty;
        public string Sammg { get; set; } = String.Empty;
        public string Pick_Status { get; set; } = String.Empty;
    }

    public class GetPickListEtEanData {
        public string MANDT { get; set; } // Client number
        public string MATNR { get; set; } // Material number
        public string MEINH { get; set; } // Unit of measure
        public string UMREZ { get; set; } // Numerator for conversion factor
        public string UMREN { get; set; } // Denominator for conversion factor
        public string EANNR { get; set; } // International Article Number (EAN)
        public string EAN11 { get; set; } // EAN without leading zero
        public string NUMTP { get; set; } // Number type
        public string LAENG { get; set; } // Length
        public string BREIT { get; set; } // Width
        public string HOEHE { get; set; } // Height
        public string MEABM { get; set; } // Unit of measurement for dimensions
        public string VOLUM { get; set; } // Volume
        public string VOLEH { get; set; } // Unit of volume
        public string BRGEW { get; set; } // Gross weight
        public string GEWEI { get; set; } // Unit of weight
        public string MESUB { get; set; } // Measurement unit subfield
        public string ATINN { get; set; } // Internal characteristic number
        public string MESRT { get; set; } // Measurement accuracy
        public string XFHDW { get; set; } // Free dimension indicator
        public string XBEWW { get; set; } // Stockkeeping unit indicator
        public string KZWSO { get; set; } // Indicator for alternative UoM
        public string MSEHI { get; set; } // Alternative unit of measure
        public string BFLME_MARM { get; set; } // Base UoM relevant indicator
        public string GTIN_VARIANT { get; set; } // GTIN variant
        public string NEST_FTR { get; set; } // Nesting factor
        public string MAX_STACK { get; set; } // Maximum stack
        public string TOP_LOAD_FULL { get; set; } // Top load full
        public string TOP_LOAD_FULL_UOM { get; set; } // Unit of measure for top load
        public string CAPAUSE { get; set; } // Capacity usage
        public string TY2TQ { get; set; } // Type-to-quantity mapping
        public string DUMMY_UOM_INCL_EEW_PS { get; set; } // Dummy unit of measure
        public string CWM_TY2TQ { get; set; } // Cross weight to quantity
        public string STTPEC_NCODE { get; set; } // NCODE
        public string STTPEC_NCODE_TY { get; set; } // NCODE type
        public string STTPEC_RCODE { get; set; } // RCODE
        public string STTPEC_SERUSE { get; set; } // Serialized usage
        public string STTPEC_SYNCCHG { get; set; } // Sync change
        public string STTPEC_SERNO_MANAGED { get; set; } // Serial number managed
        public string STTPEC_SERNO_PROV_BUP { get; set; } // Serial number provision backup
        public string STTPEC_UOM_SYNC { get; set; } // Unit of measure sync
        public string STTPEC_SER_GTIN { get; set; } // Serialized GTIN
        public string PCBUT { get; set; } // Additional field (PCBUT)
    }
}