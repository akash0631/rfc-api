using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Vendor_Application_MVC.Models
{
    
    public class RoutingStatusList
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
       
        public List<RoutingStatusListResponse> Data;
        public RoutingStatusList()
        {
            Data = new List<RoutingStatusListResponse>();
         
        }
    }
    public class RoutingStatusList_grp
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
       
        public List<RoutingStatusListResponsegrp> Data;
        public RoutingStatusList_grp()
        {
            Data = new List<RoutingStatusListResponsegrp>();
           
        }
    }
    public class RoutingStatusListRequest
    {
        public string IM_PO { get; set; }
        public string IM_DESIGN { get; set; } = string.Empty;
        public string Article_Number { get; set; }=string.Empty;

    }
    public class RoutingStatusListResponsegrp
    {
        public List<RoutingStatusListResponse> PRD_ROUTING;
        public List<RoutingStatusListResponse> Barcode_Routing;
        public List<RoutingStatusListResponse> ACC_Status;
        public List<RoutingStatusListResponse> PP_Sample_Rout;

        public List<RoutingStatusListResponse> Auto_Marker_ST;
        public List<RoutingStatusListResponse> Fabric_TNA;
        public List<RoutingStatusListResponse> PO_Sample_Rout;
        public List<RoutingStatusListResponse> TK_PK_MVT;
        public RoutingStatusListResponsegrp()
        {



            PRD_ROUTING = new List<RoutingStatusListResponse>();
            Barcode_Routing = new List<RoutingStatusListResponse>();
            ACC_Status = new List<RoutingStatusListResponse>();
            PP_Sample_Rout = new List<RoutingStatusListResponse>();

            Auto_Marker_ST = new List<RoutingStatusListResponse>();
            Fabric_TNA = new List<RoutingStatusListResponse>();
            PO_Sample_Rout = new List<RoutingStatusListResponse>();
            TK_PK_MVT = new List<RoutingStatusListResponse>();


        }
    }
    public class RoutingStatusListResponse
    {
        public string TEXT { get; set; }
        public string RTNO { get; set; }

    }

    public class GateEntryRequest {
        public string PO { get; set; } = String.Empty;
    }
    public class GateEntryResponse
    {
        public string VendorCode { get; set; } = String.Empty;
        public string VendorName { get; set; } = String.Empty;
        public string City { get; set; } = String.Empty;
        public string PoNumber { get; set; } = String.Empty;
        public string PoQty { get; set; } = String.Empty;
        public string PoValue { get; set; } = String.Empty;
        public string PoDelDate { get; set; } = String.Empty;
        public string GeNo { get; set; } = String.Empty;
        public string GeDate { get; set; } = String.Empty;
        public string BillNo { get; set; } = String.Empty;
        public string BillQty { get; set; } = String.Empty;
        public string BillVal { get; set; } = String.Empty;
        public string DiffQty { get; set; } = String.Empty;
    }
    public class GateEntryList
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<GateEntryResponse> Data;
        public GateEntryList()
        {
            Data = new List<GateEntryResponse>();
        }
    }

    public class RoutingStatus_grp
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }

        public List<RoutingStatusgrpRespponse> Data;
        public RoutingStatus_grp()
        {
            Data = new List<RoutingStatusgrpRespponse>();

        }
    }
    public class RoutingStatusgrpRespponse
    {
        public string TEXT { get; set; }
        public string RTNO { get; set; }
        public string SUB_GROUP { get; set; }

    }

}