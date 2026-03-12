using DocumentFormat.OpenXml.Bibliography;
using OfficeOpenXml.Table.PivotTable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Vendor_Application_MVC.Models;

namespace Vendor_SRM_Routing_Application.Models.Site_Creation
{
    public class Store_Site_Creation
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public Store_Site_CreationResponse Data;
        public Store_Site_Creation()
        {
            Data = new Store_Site_CreationResponse();
        }
    }
    public class Store_Site_CreationRequest
    {

        public string RM_NAME { get; set; }
        public string ZONE_1 { get; set; }
        public string ZSTATE { get; set; }
        public string DISTRICT_NAME { get; set; }
        public string CITY { get; set; }
        public string CITY_POPULATION { get; set; }
        public string C_B_PASS { get; set; }
        public string B_PASS { get; set; }
        public string F_PASS { get; set; }
        public string LLRATE { get; set; }
        public string VRATE { get; set; }
        public string RANK { get; set; }
        public string SITE_TYPE { get; set; }
        public string MRKT_NAME { get; set; }
        public string FRONTAGE { get; set; }
        public string TOTAL_AREA { get; set; }
        public string BSMT_PRKG { get; set; }
        public string FRONT_PRKG { get; set; }
        public string UGF { get; set; }
        public string LGF { get; set; }
        public string GROUND_FLOOR { get; set; }
        public string FIRST_FLOOR { get; set; }
        public string SECOND_FLOOR { get; set; }
        public string THIRD_FLOOR { get; set; }
        public string FORTH_FLOOR { get; set; }
        public string FIFTH_FLOOR { get; set; }
        public string GOOGLE_COORDINATES { get; set; }
        public string COMPETITORS_NAME { get; set; }
        public string COMPETITORS_SALE { get; set; }
        public string REMARKS { get; set; }
        public string REMARKS1 { get; set; }
        public string REMARKS2 { get; set; }
        public string BROKER_NAME { get; set; }
        public string BROKERM_NO { get; set; }
        public string LANDLORD_NAME { get; set; }
        public string LANDLORD_M_NO { get; set; }
        public string PROOF { get; set; }
        public string ROAD_CONDITION { get; set; }
        public string ROAD_WIDTH { get; set; }
        public string PROPERTY_CEILING_HIGHT { get; set; }
        // new properties added
        // New properties
        public string ZDE_DISTRICT_POP { get; set; }
        public string ZDE_CITY_POP { get; set; }
        public string ZDE_DIST_POP_PER_KM { get; set; }
        public string ZDE_CITY_POP_PER_KM { get; set; }
        public string ZDE_LITERACY_RATE { get; set; }
        public string ZDE_SCHOOLS_10KM { get; set; }
        public string ZDE_COLLEGES_10KM { get; set; }
        public string ZDE_AVG_INCOME_DISTT { get; set; }
        public string ZDE_ATMS_CITY { get; set; }
        public string ZDE_BANK_BRANCHES_CITY { get; set; }
        public string ZDE_FACTORIES_CITY { get; set; }
        public string ZDE_UNEMPLOYMENT_RATE { get; set; }
        public string ZDE_DISTANCE_RAILWAY { get; set; }
        public string ZDE_DISTANCE_BUS { get; set; }
        public string ZDE_4WHEELER_PASSING { get; set; }
        public string ZDE_2WHEELER_PASSING { get; set; }
        public string ZDE_SHOPPING_MALLS { get; set; }
        public string ZDE_MULTIPLEX_CINEMAS { get; set; }
        public string ZDE_FOOD_COURT { get; set; }

    }
    public class Store_Site_CreationResponse
    {
        public string TEXT { get; set; }


    }
}