using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace Vendor_SRM_Routing_Application
{
    public class RfcEndpoint
    {
        public string Name { get; set; }
        public string Route { get; set; }
        public string Group { get; set; }
        public string SapRfc { get; set; }
        public string Description { get; set; }
        public string SapHost { get; set; }
        public string Client { get; set; }
        public string SampleRequest { get; set; }
        public string SampleResponse { get; set; }
        public string SampleError { get; set; }
        public List<RfcParam> Parameters { get; set; }
        public List<string> ResponseTables { get; set; }
    }

    public class RfcParam
    {
        public string Name { get; set; }
        public string SapType { get; set; }
        public bool IsTable { get; set; }
        public bool Required { get; set; } = true;
        public string Description { get; set; }
    }
}

namespace Vendor_SRM_Routing_Application.Controllers
{
    public class RfcDeployController : System.Web.Mvc.Controller
    {
        private const string GH_BRANCH = "Finaltest";
        private const string IIS_BASE = "http://v2retail.net:9005";

        public ActionResult Index() { return RedirectToAction("Explorer"); }

        public ActionResult Explorer()
        {
            var eps = GetAllEndpoints();
            ViewBag.TotalCount = eps.Count;
            ViewBag.GroupCount = eps.Select(e => e.Group).Distinct().Count();
            ViewBag.ParamCount = eps.Sum(e => e.Parameters != null ? e.Parameters.Count : 0);
            return View(eps.AsEnumerable());
        }

        public ActionResult SwaggerUI()
        {
            ViewBag.Title = "V2 Retail - RFC API Swagger UI";
            ViewBag.OpenApiUrl = Url.Action("OpenApiJson", "RfcDeploy", null, Request.Url.Scheme);
            return View();
        }

        public ActionResult OpenApiJson()
        {
            return Content(BuildOpenApiSpec(GetAllEndpoints()), "application/json");
        }

        private List<RfcEndpoint> GetAllEndpoints()
        {
            return new List<RfcEndpoint>
            {

                new RfcEndpoint {
                    Name="ZDC_ROUTING_SUB_RFC", Route="api/ZDC_ROUTING_SUB_RFC", Group="DC Routing",
                    SapRfc="ZDC_ROUTING_SUB_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZDC ROUTING SUB RFC SAP RFC",
                    SampleRequest="IM_DC_ROUTING:VALUE, IM_GATE_ENTRY:GE0001, IM_EBELN:4500001234",
                    SampleResponse="Status:S, Message:Success, Data:{LT_PROD:[{FIELD1:VAL1}], LT_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DC_ROUTING",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_GATE_ENTRY",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_EBELN",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"LT_PROD","LT_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_GATE_ENTRY_RFC", Route="api/ZWM_GATE_ENTRY_RFC", Group="DC Routing",
                    SapRfc="ZWM_GATE_ENTRY_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GATE ENTRY RFC SAP RFC",
                    SampleRequest="IM_EBELN:4500001234",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_EBELN",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_HU_STORE_POST_RFC", Route="api/ZWM_HU_STORE_POST_RFC", Group="DC Routing",
                    SapRfc="ZWM_HU_STORE_POST_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZWM HU STORE POST RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_EXIDV:VALUE, IM_SAPHU:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{LT_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_EXIDV",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_SAPHU",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"LT_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_DC_ROUTING_RFC", Route="api/ZWM_DC_ROUTING_RFC", Group="DC Routing",
                    SapRfc="ZWM_DC_ROUTING_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZWM DC ROUTING RFC SAP RFC",
                    SampleRequest="IM_GATE_ENTRY:GE0001",
                    SampleResponse="Status:S, Message:Success, Data:{LT_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_GATE_ENTRY",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"LT_DATA"}
                },

                new RfcEndpoint {
                    Name="ZFMS_RFC_FABPUTWAYGRC_POST", Route="api/ZFMS_RFC_FABPUTWAYGRC_POST", Group="FMS Fabric Putway",
                    SapRfc="ZFMS_RFC_FABPUTWAYGRC_POST", SapHost="192.168.144.170", Client="600",
                    Description="ZFMS RFC FABPUTWAYGRC POST SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_GRC:GRC001, ZWM_BIN_SCAN_T:VALUE, IT_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{ET_EAN_DATA:[{FIELD1:VAL1}], ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_GRC",SapType="string",IsTable=false},

                        new RfcParam{Name="ZWM_BIN_SCAN_T",SapType="string",IsTable=false},

                        new RfcParam{Name="IT_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{"ET_EAN_DATA","ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZFMS_RFC_FABPUTWAYGRC_VALIDATE", Route="api/ZFMS_RFC_FABPUTWAYGRC_VALIDATE", Group="FMS Fabric Putway",
                    SapRfc="ZFMS_RFC_FABPUTWAYGRC_VALIDATE", SapHost="192.168.144.170", Client="600",
                    Description="ZFMS RFC FABPUTWAYGRC VALIDATE SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_GRC:GRC001",
                    SampleResponse="Status:S, Message:Success, Data:{ET_EAN_DATA:[{FIELD1:VAL1}], ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_GRC",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_EAN_DATA","ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZFMS_RFC_FABPUTWAYGRC_BIN_VALI", Route="api/ZFMS_RFC_FABPUTWAYGRC_BIN_VALI", Group="FMS Fabric Putway",
                    SapRfc="ZFMS_RFC_FABPUTWAYGRC_BIN_VALI", SapHost="192.168.144.170", Client="600",
                    Description="ZFMS RFC FABPUTWAYGRC BIN VALI SAP RFC",
                    SampleRequest="IM_SITE:VALUE, IM_BIN:BIN-A-01, IM_LGTYP:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_EAN_DATA:[{FIELD1:VAL1}], ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_SITE",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_BIN",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_LGTYP",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_EAN_DATA","ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZADVANCE_PAYMENT_RFC", Route="api/ZADVANCE_PAYMENT_RFC", Group="Finance",
                    SapRfc="ZADVANCE_PAYMENT_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZADVANCE PAYMENT RFC SAP RFC",
                    SampleRequest="I_COMPANY_CODE:1000, I_POSTING_DATE_LOW:20240101, I_POSTING_DATE_HIGH:20240101",
                    SampleResponse="Status:S, Message:Success, Data:{ET_ADVANCE_PAYMENT:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="I_COMPANY_CODE",SapType="string",IsTable=false},

                        new RfcParam{Name="I_POSTING_DATE_LOW",SapType="string",IsTable=false},

                        new RfcParam{Name="I_POSTING_DATE_HIGH",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_ADVANCE_PAYMENT"}
                },

                new RfcEndpoint {
                    Name="ZFINANCE_DOCUMENT_RFC", Route="api/ZFINANCE_DOCUMENT_RFC", Group="Finance",
                    SapRfc="ZFINANCE_DOCUMENT_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZFINANCE DOCUMENT RFC SAP RFC",
                    SampleRequest="",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZPO_COST_UPD_RFC", Route="api/ZPO_COST_UPD_RFC", Group="Finance",
                    SapRfc="ZPO_COST_UPD_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZPO COST UPD RFC SAP RFC",
                    SampleRequest=", IM_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZPO_DD_UPD_RFC", Route="api/ZPO_DD_UPD_RFC", Group="Finance",
                    SapRfc="ZPO_DD_UPD_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZPO DD UPD RFC SAP RFC",
                    SampleRequest=", IM_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZPO_QTY_UPD_RFC", Route="api/ZPO_QTY_UPD_RFC", Group="Finance",
                    SapRfc="ZPO_QTY_UPD_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZPO QTY UPD RFC SAP RFC",
                    SampleRequest=", IM_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZRFC_CREDITORS_LOVABL", Route="api/ZRFC_CREDITORS_LOVABL", Group="Finance",
                    SapRfc="ZRFC_CREDITORS_LOVABL", SapHost="192.168.144.170", Client="600",
                    Description="ZRFC CREDITORS LOVABL SAP RFC",
                    SampleRequest="COMPANY_CODE:1000, VENDOR:VALUE, POSTING_DATE:20240101",
                    SampleResponse="Status:S, Message:Success, Data:{LT_DATA:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="COMPANY_CODE",SapType="string",IsTable=false},

                        new RfcParam{Name="VENDOR",SapType="string",IsTable=false},

                        new RfcParam{Name="POSTING_DATE",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"LT_DATA","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZSALES_MOP_RFC", Route="api/ZSALES_MOP_RFC", Group="Finance",
                    SapRfc="ZSALES_MOP_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZSALES MOP RFC SAP RFC",
                    SampleRequest="IM_DATE_LOW:20240101, IM_DATE_HIGH:20240101",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DATE_LOW",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_DATE_HIGH",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZTEST_RFC", Route="api/ZTEST_RFC", Group="Finance",
                    SapRfc="ZTEST_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZTEST RFC SAP RFC",
                    SampleRequest="I_COMPANY_CODE:1000, I_DATE:20240101",
                    SampleResponse="Status:S, Message:Success, Data:{ET_RESULT:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="I_COMPANY_CODE",SapType="string",IsTable=false},

                        new RfcParam{Name="I_DATE",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_RESULT"}
                },

                new RfcEndpoint {
                    Name="ZWM_GATE_PALLATE1_N", Route="api/ZWM_GATE_PALLATE1_N", Group="Gate Entry LOT Putway",
                    SapRfc="ZWM_GATE_PALLATE1_N", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GATE PALLATE1 N SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_WERKS:VALUE, IM_PO:VALUE, IM_INV:VALUE, IM_PALL:PAL00001",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PO",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_INV",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PALL",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_GATE_VALIDATION1_N", Route="api/ZWM_GATE_VALIDATION1_N", Group="Gate Entry LOT Putway",
                    SapRfc="ZWM_GATE_VALIDATION1_N", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GATE VALIDATION1 N SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_WERKS:VALUE, IM_PO:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PO",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZVND_PUTWAY_BIN_VAL_RFC", Route="api/ZVND_PUTWAY_BIN_VAL_RFC", Group="Gate Entry LOT Putway",
                    SapRfc="ZVND_PUTWAY_BIN_VAL_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZVND PUTWAY BIN VAL RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_BIN:BIN-A-01",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_BIN",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZVND_PUTWAY_PALETTE_VAL_RFC", Route="api/ZVND_PUTWAY_PALETTE_VAL_RFC", Group="Gate Entry LOT Putway",
                    SapRfc="ZVND_PUTWAY_PALETTE_VAL_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZVND PUTWAY PALETTE VAL RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_BIN:BIN-A-01, IM_PALL:PAL00001",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_BIN",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PALL",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZVND_PUTWAY_SAVE_DATA_RFC", Route="api/ZVND_PUTWAY_SAVE_DATA_RFC", Group="Gate Entry LOT Putway",
                    SapRfc="ZVND_PUTWAY_SAVE_DATA_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZVND PUTWAY SAVE DATA RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IT_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IT_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZWM_GATE_BIN_VALIDATION3_N", Route="api/ZWM_GATE_BIN_VALIDATION3_N", Group="Gate Entry LOT Putway",
                    SapRfc="ZWM_GATE_BIN_VALIDATION3_N", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GATE BIN VALIDATION3 N SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_WERKS:VALUE, IM_BIN:BIN-A-01, IM_PALL:PAL00001, IT_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_BIN",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PALL",SapType="string",IsTable=false},

                        new RfcParam{Name="IT_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{"ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_GATE_BIN_VALIDATION4_N", Route="api/ZWM_GATE_BIN_VALIDATION4_N", Group="Gate Entry LOT Putway",
                    SapRfc="ZWM_GATE_BIN_VALIDATION4_N", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GATE BIN VALIDATION4 N SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_BIN:BIN-A-01, IM_GET:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_BIN",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_GET",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_GATE_BOX3N", Route="api/ZWM_GATE_BOX3N", Group="Gate Entry LOT Putway",
                    SapRfc="ZWM_GATE_BOX3N", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GATE BOX3N SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_WERKS:VALUE, IM_GATE:VALUE, IM_PALL:PAL00001, IM_BOX:VALUE, IM_WEIGHT:VALUE, IT_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_GATE",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PALL",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_BOX",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WEIGHT",SapType="string",IsTable=false},

                        new RfcParam{Name="IT_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{"ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_GATE_PALLATE_VALIDATE3_N", Route="api/ZWM_GATE_PALLATE_VALIDATE3_N", Group="Gate Entry LOT Putway",
                    SapRfc="ZWM_GATE_PALLATE_VALIDATE3_N", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GATE PALLATE VALIDATE3 N SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_WERKS:VALUE, IM_PALL:PAL00001, IT_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PALL",SapType="string",IsTable=false},

                        new RfcParam{Name="IT_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{"ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_GATE_PALLATE_VALIDATE4_N", Route="api/ZWM_GATE_PALLATE_VALIDATE4_N", Group="Gate Entry LOT Putway",
                    SapRfc="ZWM_GATE_PALLATE_VALIDATE4_N", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GATE PALLATE VALIDATE4 N SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_BIN:BIN-A-01, IM_GET:VALUE, IM_PALETTE:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_BIN",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_GET",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PALETTE",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_GATE_SAVE3_N", Route="api/ZWM_GATE_SAVE3_N", Group="Gate Entry LOT Putway",
                    SapRfc="ZWM_GATE_SAVE3_N", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GATE SAVE3 N SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_WERKS:VALUE, IM_GATE:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_GATE",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_GET_GATE_ENTRY_DATA4_RFC", Route="api/ZWM_GET_GATE_ENTRY_DATA4_RFC", Group="Gate Entry LOT Putway",
                    SapRfc="ZWM_GET_GATE_ENTRY_DATA4_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GET GATE ENTRY DATA4 RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_GET:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_GET",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_GET_GATE_ENTRY_DATA_RFC", Route="api/ZWM_GET_GATE_ENTRY_DATA_RFC", Group="Gate Entry LOT Putway",
                    SapRfc="ZWM_GET_GATE_ENTRY_DATA_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GET GATE ENTRY DATA RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_WERKS:VALUE, IM_GATE:VALUE, IT_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_GATE",SapType="string",IsTable=false},

                        new RfcParam{Name="IT_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{"ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_GET_GATE_ENTRY_LIST4_RFC", Route="api/ZWM_GET_GATE_ENTRY_LIST4_RFC", Group="Gate Entry LOT Putway",
                    SapRfc="ZWM_GET_GATE_ENTRY_LIST4_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GET GATE ENTRY LIST4 RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_GET_GATE_ENTRY_LIST_RFC", Route="api/ZWM_GET_GATE_ENTRY_LIST_RFC", Group="Gate Entry LOT Putway",
                    SapRfc="ZWM_GET_GATE_ENTRY_LIST_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GET GATE ENTRY LIST RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_WERKS:VALUE, IM_DOCNO:VALUE, IT_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_DOCNO",SapType="string",IsTable=false},

                        new RfcParam{Name="IT_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{"ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_GET_GATE_ENTRY_PALLATE_RFC", Route="api/ZWM_GET_GATE_ENTRY_PALLATE_RFC", Group="Gate Entry LOT Putway",
                    SapRfc="ZWM_GET_GATE_ENTRY_PALLATE_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZWM GET GATE ENTRY PALLATE RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_WERKS:VALUE, IM_GATE:VALUE, IM_PALL:PAL00001, IT_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_GATE",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PALL",SapType="string",IsTable=false},

                        new RfcParam{Name="IT_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{"ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZESIC_MASTER_POST_RFC", Route="api/ZESIC_MASTER_POST_RFC", Group="HRMS",
                    SapRfc="ZESIC_MASTER_POST_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZESIC MASTER POST RFC SAP RFC",
                    SampleRequest="IM_ST_CD:VALUE, IM_STATUS:VALUE, IM_ST_ESIC_CD:VALUE, IM_ST_ESIC_CD_REF:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_ST_CD",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_STATUS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_ST_ESIC_CD",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_ST_ESIC_CD_REF",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZHR_LEAVE_POLICY_RFC", Route="api/ZHR_LEAVE_POLICY_RFC", Group="HRMS",
                    SapRfc="ZHR_LEAVE_POLICY_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZHR LEAVE POLICY RFC SAP RFC",
                    SampleRequest="PPT_NO:VALUE, RTNO:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="PPT_NO",SapType="string",IsTable=false},

                        new RfcParam{Name="RTNO",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZLWF_MASTER_POST_RFC", Route="api/ZLWF_MASTER_POST_RFC", Group="HRMS",
                    SapRfc="ZLWF_MASTER_POST_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZLWF MASTER POST RFC SAP RFC",
                    SampleRequest="",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZPF_MASTER_POST_RFC", Route="api/ZPF_MASTER_POST_RFC", Group="HRMS",
                    SapRfc="ZPF_MASTER_POST_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZPF MASTER POST RFC SAP RFC",
                    SampleRequest="IM_ST_CD:VALUE, IM_MIN_WAG:VALUE, IM_STATUS:VALUE, IM_ST_PF_CD:VALUE, IM_ST_PF_CD_REF:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_ST_CD",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_MIN_WAG",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_STATUS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_ST_PF_CD",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_ST_PF_CD_REF",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZPT_MASTER_POST_RFC", Route="api/ZPT_MASTER_POST_RFC", Group="HRMS",
                    SapRfc="ZPT_MASTER_POST_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZPT MASTER POST RFC SAP RFC",
                    SampleRequest="",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZVND_HU_PUSH_API_POST", Route="api/ZVND_HU_PUSH_API_POST", Group="HU Creation",
                    SapRfc="ZVND_HU_PUSH_API_POST", SapHost="192.168.144.170", Client="600",
                    Description="ZVND HU PUSH API POST SAP RFC",
                    SampleRequest="",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZVND_HU_CHECK_RFC", Route="api/ZVND_HU_CHECK_RFC", Group="HU Creation",
                    SapRfc="ZVND_HU_CHECK_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZVND HU CHECK RFC SAP RFC",
                    SampleRequest="",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZSRM_PO_DETAIL_FM", Route="api/ZSRM_PO_DETAIL_FM", Group="HU Creation",
                    SapRfc="ZSRM_PO_DETAIL_FM", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM PO DETAIL FM SAP RFC",
                    SampleRequest="PO_NUM:VALUE, VENDOR_CODE:1000",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="PO_NUM",SapType="string",IsTable=false},

                        new RfcParam{Name="VENDOR_CODE",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZVND_HU_VALIDATE_RFC", Route="api/ZVND_HU_VALIDATE_RFC", Group="HU Creation",
                    SapRfc="ZVND_HU_VALIDATE_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZVND HU VALIDATE RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_HU_NUMBER:VALUE, IM_PO:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_STORES:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_HU_NUMBER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PO",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_STORES","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_VEND_OPEN_PO", Route="api/ZWM_VEND_OPEN_PO", Group="HU Creation",
                    SapRfc="ZWM_VEND_OPEN_PO", SapHost="192.168.144.170", Client="600",
                    Description="ZWM VEND OPEN PO SAP RFC",
                    SampleRequest="IM_LIFNR:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_LIFNR",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_HU_STORE_RFC", Route="api/ZWM_HU_STORE_RFC", Group="HU Print",
                    SapRfc="ZWM_HU_STORE_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZWM HU STORE RFC SAP RFC",
                    SampleRequest="IM_HU:HU0001234, IM_USER:USER01, IM_WERKS:VALUE, IM_EXIDV:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_HU",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_EXIDV",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZWM_ACTUAL_HU_SAVE", Route="api/ZWM_ACTUAL_HU_SAVE", Group="HU Print",
                    SapRfc="ZWM_ACTUAL_HU_SAVE", SapHost="192.168.144.170", Client="600",
                    Description="ZWM ACTUAL HU SAVE SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_WERKS:VALUE, IM_EXIDV:VALUE, IM_SAP_HU:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_EXIDV",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_SAP_HU",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZWM_SAVE_HU", Route="api/ZWM_SAVE_HU", Group="HU Scan",
                    SapRfc="ZWM_SAVE_HU", SapHost="192.168.144.170", Client="600",
                    Description="ZWM SAVE HU SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_HU:HU0001234",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_HU",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZWM_SCAN_HU", Route="api/ZWM_SCAN_HU", Group="HU Scan",
                    SapRfc="ZWM_SCAN_HU", SapHost="192.168.144.170", Client="600",
                    Description="ZWM SCAN HU SAP RFC",
                    SampleRequest="IM_HU:HU0001234, IM_USER:USER01, IM_PLANT:1000",
                    SampleResponse="Status:S, Message:Success, Data:{ET_ATICLES:[{FIELD1:VAL1}], ET_EAN:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_HU",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_ATICLES","ET_EAN"}
                },

                new RfcEndpoint {
                    Name="ZVND_GATELOT2_BIN_VAL_RFC", Route="api/ZVND_GATELOT2_BIN_VAL_RFC", Group="Inbound",
                    SapRfc="ZVND_GATELOT2_BIN_VAL_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZVND GATELOT2 BIN VAL RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_PICKLIST:PL0001, IM_BIN:BIN-A-01",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PICKLIST",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_BIN",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZVND_GATELOT2_PALETTE_VAL_RFC", Route="api/ZVND_GATELOT2_PALETTE_VAL_RFC", Group="Inbound",
                    SapRfc="ZVND_GATELOT2_PALETTE_VAL_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZVND GATELOT2 PALETTE VAL RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_PICKLIST:PL0001, IM_BIN:BIN-A-01, IM_PALL:PAL00001",
                    SampleResponse="Status:S, Message:Success, Data:{ET_BOX:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PICKLIST",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_BIN",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PALL",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_BOX"}
                },

                new RfcEndpoint {
                    Name="ZVND_GATELOT2_PICKLIST_VAL_RFC", Route="api/ZVND_GATELOT2_PICKLIST_VAL_RFC", Group="Inbound",
                    SapRfc="ZVND_GATELOT2_PICKLIST_VAL_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZVND GATELOT2 PICKLIST VAL RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZVND_GATELOT2_SAVE_DATA_RFC", Route="api/ZVND_GATELOT2_SAVE_DATA_RFC", Group="Inbound",
                    SapRfc="ZVND_GATELOT2_SAVE_DATA_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZVND GATELOT2 SAVE DATA RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IT_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IT_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZVND_PUT01_HU_VAL_RFC", Route="api/ZVND_PUT01_HU_VAL_RFC", Group="Inbound",
                    SapRfc="ZVND_PUT01_HU_VAL_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZVND PUT01 HU VAL RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_HU:HU0001234",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_HU",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZVND_PUT01_SAVE_DATA_RFC", Route="api/ZVND_PUT01_SAVE_DATA_RFC", Group="Inbound",
                    SapRfc="ZVND_PUT01_SAVE_DATA_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZVND PUT01 SAVE DATA RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IT_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IT_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZVND_UNLOAD_HU_VALIDATE_RFC", Route="api/ZVND_UNLOAD_HU_VALIDATE_RFC", Group="Inbound",
                    SapRfc="ZVND_UNLOAD_HU_VALIDATE_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZVND UNLOAD HU VALIDATE RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_HU:HU0001234",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_HU",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZVND_UNLOAD_PALLATE_VALIDATION", Route="api/ZVND_UNLOAD_PALLATE_VALIDATION", Group="Inbound",
                    SapRfc="ZVND_UNLOAD_PALLATE_VALIDATION", SapHost="192.168.144.170", Client="600",
                    Description="ZVND UNLOAD PALLATE VALIDATION SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_HU:HU0001234, IM_PALL:PAL00001",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_HU",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PALL",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZVND_UNLOAD_SAVE_RFC", Route="api/ZVND_UNLOAD_SAVE_RFC", Group="Inbound",
                    SapRfc="ZVND_UNLOAD_SAVE_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZVND UNLOAD SAVE RFC SAP RFC",
                    SampleRequest="IM_USER:USER01",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZSRM_NSO_CONF_POST", Route="api/ZSRM_NSO_CONF_POST", Group="NSO",
                    SapRfc="ZSRM_NSO_CONF_POST", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM NSO CONF POST SAP RFC",
                    SampleRequest="IM_CSTATUS:VALUE, IM_SRNO:VALUE, IM_BUDGTE_ST_DATE:20240101, IM_BUDGTE_END_DATE:20240101, IM_ACT_START_DATE:20240101, IM_ACT_END_DATE:20240101, IM_START_AT_TIME:VALUE, IM_END_AT_TIME:VALUE, IM_REMARKS:VALUE, IM_REMARKS1:VALUE, IM_REMARKS2:VALUE, IM_ROUTING_NO:VALUE, IM_PROCESS_CONFIRM:VALUE, IM_HYPERLINK:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_CSTATUS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_SRNO",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_BUDGTE_ST_DATE",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_BUDGTE_END_DATE",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_ACT_START_DATE",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_ACT_END_DATE",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_START_AT_TIME",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_END_AT_TIME",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_REMARKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_REMARKS1",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_REMARKS2",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_ROUTING_NO",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PROCESS_CONFIRM",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_HYPERLINK",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZSRM_NSO_CONF_ROUTING", Route="api/ZSRM_NSO_CONF_ROUTING", Group="NSO",
                    SapRfc="ZSRM_NSO_CONF_ROUTING", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM NSO CONF ROUTING SAP RFC",
                    SampleRequest="IM_SITE_CODE:1000",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_SITE_CODE",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZRFC_ACC_DOC_POST", Route="api/ZRFC_ACC_DOC_POST", Group="NSO",
                    SapRfc="ZRFC_ACC_DOC_POST", SapHost="192.168.144.170", Client="600",
                    Description="ZRFC ACC DOC POST SAP RFC",
                    SampleRequest="IM_DC_ROUTING:VALUE, IM_GATE_ENTRY:GE0001",
                    SampleResponse="Status:S, Message:Success, Data:{LT_ACCOUNTGL:[{FIELD1:VAL1}], LT_CURRENCYAMOUNT:[{FIELD1:VAL1}], LT_PAYBLE:[{FIELD1:VAL1}], LT_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DC_ROUTING",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_GATE_ENTRY",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"LT_ACCOUNTGL","LT_CURRENCYAMOUNT","LT_PAYBLE","LT_DATA"}
                },

                new RfcEndpoint {
                    Name="ZNSO_RFC_SITELIST", Route="api/ZNSO_RFC_SITELIST", Group="NSO",
                    SapRfc="ZNSO_RFC_SITELIST", SapHost="192.168.144.170", Client="600",
                    Description="ZNSO RFC SITELIST SAP RFC",
                    SampleRequest="IM_SITE_CODE:1000",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_SITE_CODE",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZRFC_GL_CODE", Route="api/ZRFC_GL_CODE", Group="NSO",
                    SapRfc="ZRFC_GL_CODE", SapHost="192.168.144.170", Client="600",
                    Description="ZRFC GL CODE SAP RFC",
                    SampleRequest="IM_EBELN:4500001234",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_EBELN",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZWM_RFC_GET_PICKLIST_DATA", Route="api/ZWM_RFC_GET_PICKLIST_DATA", Group="Paperless Picklist",
                    SapRfc="ZWM_RFC_GET_PICKLIST_DATA", SapHost="192.168.144.170", Client="600",
                    Description="ZWM RFC GET PICKLIST DATA SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_WERKS:VALUE, IM_PICNR:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PICNR",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_RFC_GET_PICKLIST", Route="api/ZWM_RFC_GET_PICKLIST", Group="Paperless Picklist",
                    SapRfc="ZWM_RFC_GET_PICKLIST", SapHost="192.168.144.170", Client="600",
                    Description="ZWM RFC GET PICKLIST SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_DATUM:VALUE, IM_WERKS:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_DATUM",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_RFC_PICKLIST_SCAN_POST", Route="api/ZWM_RFC_PICKLIST_SCAN_POST", Group="Paperless Picklist",
                    SapRfc="ZWM_RFC_PICKLIST_SCAN_POST", SapHost="192.168.144.170", Client="600",
                    Description="ZWM RFC PICKLIST SCAN POST SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_WERKS:VALUE, IM_PICNR:VALUE, IT_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_WERKS",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PICNR",SapType="string",IsTable=false},

                        new RfcParam{Name="IT_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZEAN_ART_DETAILS", Route="api/ZEAN_ART_DETAILS", Group="Sampling",
                    SapRfc="ZEAN_ART_DETAILS", SapHost="192.168.144.170", Client="600",
                    Description="ZEAN ART DETAILS SAP RFC",
                    SampleRequest="LV_ART:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="LV_ART",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZARTICLE_YES_NO_POST", Route="api/ZARTICLE_YES_NO_POST", Group="Sampling",
                    SapRfc="ZARTICLE_YES_NO_POST", SapHost="192.168.144.170", Client="600",
                    Description="ZARTICLE YES NO POST SAP RFC",
                    SampleRequest="ID:VALUE, ARTICLE:VALUE, CREATION_DT:VALUE, CREATION_TM:VALUE, STATUS:VALUE, REMARKS:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="ID",SapType="string",IsTable=false},

                        new RfcParam{Name="ARTICLE",SapType="string",IsTable=false},

                        new RfcParam{Name="CREATION_DT",SapType="string",IsTable=false},

                        new RfcParam{Name="CREATION_TM",SapType="string",IsTable=false},

                        new RfcParam{Name="STATUS",SapType="string",IsTable=false},

                        new RfcParam{Name="REMARKS",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZQCDONE_RFC", Route="api/ZQCDONE_RFC", Group="Sampling",
                    SapRfc="ZQCDONE_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZQCDONE RFC SAP RFC",
                    SampleRequest="LV_ART:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="LV_ART",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_STORE_SITE_CONF_SAVE", Route="api/ZWM_STORE_SITE_CONF_SAVE", Group="Site Creation",
                    SapRfc="ZWM_STORE_SITE_CONF_SAVE", SapHost="192.168.144.170", Client="600",
                    Description="ZWM STORE SITE CONF SAVE SAP RFC",
                    SampleRequest="",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZSITE_RFC_CREATE", Route="api/ZSITE_RFC_CREATE", Group="Site Creation",
                    SapRfc="ZSITE_RFC_CREATE", SapHost="192.168.144.170", Client="600",
                    Description="ZSITE RFC CREATE SAP RFC",
                    SampleRequest="",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZWM_STORE_SITE_CONF", Route="api/ZWM_STORE_SITE_CONF", Group="Site Creation",
                    SapRfc="ZWM_STORE_SITE_CONF", SapHost="192.168.144.170", Client="600",
                    Description="ZWM STORE SITE CONF SAP RFC",
                    SampleRequest="IM_SITE_CODE:1000",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_SITE_CODE",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_HUBWISE_STORE_LIST_RFC", Route="api/ZWM_HUBWISE_STORE_LIST_RFC", Group="Vehicle Loading",
                    SapRfc="ZWM_HUBWISE_STORE_LIST_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZWM HUBWISE STORE LIST RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_HUB:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_STORES:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_HUB",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_STORES","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_HU_SELECTION_RFC", Route="api/ZWM_HU_SELECTION_RFC", Group="Vehicle Loading",
                    SapRfc="ZWM_HU_SELECTION_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZWM HU SELECTION RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_VEH:VALUE, IM_TRANSPORT_CODE:1000, IM_SEAL_NO:VALUE, IM_DRIVER_NAME:VALUE, IM_DRIVER_MOB:VALUE, IM_HUB_FLAG:VALUE, IM_STORE_FLAG:VALUE, IM_HUB:VALUE, IM_GRP:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_HULIST:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_VEH",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_TRANSPORT_CODE",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_SEAL_NO",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_DRIVER_NAME",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_DRIVER_MOB",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_HUB_FLAG",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_STORE_FLAG",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_HUB",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_GRP",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_HULIST","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_SAVE_SCANNEDHULIST_RFC", Route="api/ZWM_SAVE_SCANNEDHULIST_RFC", Group="Vehicle Loading",
                    SapRfc="ZWM_SAVE_SCANNEDHULIST_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZWM SAVE SCANNEDHULIST RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_VEHICLE:VALUE, HU_LIST:VALUE, IM_REMOVE:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_HULIST:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_VEHICLE",SapType="string",IsTable=false},

                        new RfcParam{Name="HU_LIST",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_REMOVE",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_HULIST","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZWM_TRANSPORTER_DETAILS_RFC", Route="api/ZWM_TRANSPORTER_DETAILS_RFC", Group="Vehicle Loading",
                    SapRfc="ZWM_TRANSPORTER_DETAILS_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZWM TRANSPORTER DETAILS RFC SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PLANT:1000, IM_HUB:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_TRANSPORT_DET:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PLANT",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_HUB",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_TRANSPORT_DET","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZSRM_ASN_APPROVED_DATA_POST", Route="api/ZSRM_ASN_APPROVED_DATA_POST", Group="Vendor SRM Routing",
                    SapRfc="ZSRM_ASN_APPROVED_DATA_POST", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM ASN APPROVED DATA POST SAP RFC",
                    SampleRequest=", IT_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IT_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZSRM_GET_ROUTING_LIST", Route="api/ZSRM_GET_ROUTING_LIST", Group="Vendor SRM Routing",
                    SapRfc="ZSRM_GET_ROUTING_LIST", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM GET ROUTING LIST SAP RFC",
                    SampleRequest="IM_PO:VALUE, IM_DESIGN:VALUE, IM_SATNR:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_PO",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_DESIGN",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_SATNR",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZQC_PO_DATA_NEW", Route="api/ZQC_PO_DATA_NEW", Group="Vendor SRM Routing",
                    SapRfc="ZQC_PO_DATA_NEW", SapHost="192.168.144.170", Client="600",
                    Description="ZQC PO DATA NEW SAP RFC",
                    SampleRequest="LV_ART:VALUE, IM_LIFNR:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="LV_ART",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_LIFNR",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZSRM_GATE_ENTRY_DETAILS", Route="api/ZSRM_GATE_ENTRY_DETAILS", Group="Vendor SRM Routing",
                    SapRfc="ZSRM_GATE_ENTRY_DETAILS", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM GATE ENTRY DETAILS SAP RFC",
                    SampleRequest="IM_EBELN:4500001234",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_EBELN",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZPUR_TREND_F4", Route="api/ZPUR_TREND_F4", Group="Vendor SRM Routing",
                    SapRfc="ZPUR_TREND_F4", SapHost="192.168.144.170", Client="600",
                    Description="ZPUR TREND F4 SAP RFC",
                    SampleRequest="I_GJAHR:VALUE, I_MATCAT:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_CAT2:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="I_GJAHR",SapType="string",IsTable=false},

                        new RfcParam{Name="I_MATCAT",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_CAT2"}
                },

                new RfcEndpoint {
                    Name="ZSRM_RFC_PO_UPDATE_DELV_DATE", Route="api/ZSRM_RFC_PO_UPDATE_DELV_DATE", Group="Vendor SRM Routing",
                    SapRfc="ZSRM_RFC_PO_UPDATE_DELV_DATE", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM RFC PO UPDATE DELV DATE SAP RFC",
                    SampleRequest="IM_PO_NUMBER:VALUE, IM_DELIVERY_DATE:20240101, IT_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_PO_NUMBER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_DELIVERY_DATE",SapType="string",IsTable=false},

                        new RfcParam{Name="IT_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZSRM_PO_DETAIL", Route="api/ZSRM_PO_DETAIL", Group="Vendor SRM Routing",
                    SapRfc="ZSRM_PO_DETAIL", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM PO DETAIL SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PO:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PO",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZSRM_RTL_FAB_PO", Route="api/ZSRM_RTL_FAB_PO", Group="Vendor SRM Routing",
                    SapRfc="ZSRM_RTL_FAB_PO", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM RTL FAB PO SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_EBELN:4500001234",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_EBELN",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZRFC_PPT_GET_ROUT", Route="api/ZRFC_PPT_GET_ROUT", Group="Vendor SRM Routing",
                    SapRfc="ZRFC_PPT_GET_ROUT", SapHost="192.168.144.170", Client="600",
                    Description="ZRFC PPT GET ROUT SAP RFC",
                    SampleRequest="IM_PO_NO:VALUE, IM_DESIGN:VALUE, IM_SATNR:VALUE, IM_PPT_NO:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_PO_NO",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_DESIGN",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_SATNR",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PPT_NO",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZRFC_PPT_GET", Route="api/ZRFC_PPT_GET", Group="Vendor SRM Routing",
                    SapRfc="ZRFC_PPT_GET", SapHost="192.168.144.170", Client="600",
                    Description="ZRFC PPT GET SAP RFC",
                    SampleRequest="IM_PO_NO:VALUE, IM_DESIGN:VALUE, IM_SATNR:VALUE, IM_PO:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_PO_NO",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_DESIGN",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_SATNR",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PO",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZRFC_PPT_CONF_POST", Route="api/ZRFC_PPT_CONF_POST", Group="Vendor SRM Routing",
                    SapRfc="ZRFC_PPT_CONF_POST", SapHost="192.168.144.170", Client="600",
                    Description="ZRFC PPT CONF POST SAP RFC",
                    SampleRequest="PPT_NO:VALUE, RTNO:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="PPT_NO",SapType="string",IsTable=false},

                        new RfcParam{Name="RTNO",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZPUR_TREND_DATA", Route="api/ZPUR_TREND_DATA", Group="Vendor SRM Routing",
                    SapRfc="ZPUR_TREND_DATA", SapHost="192.168.144.170", Client="600",
                    Description="ZPUR TREND DATA SAP RFC",
                    SampleRequest="I_GJAHR:VALUE, I_MATCAT:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DAT:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="I_GJAHR",SapType="string",IsTable=false},

                        new RfcParam{Name="I_MATCAT",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DAT"}
                },

                new RfcEndpoint {
                    Name="ZSRM_VEND_PAYMENT_INFO", Route="api/ZSRM_VEND_PAYMENT_INFO", Group="Vendor SRM Routing",
                    SapRfc="ZSRM_VEND_PAYMENT_INFO", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM VEND PAYMENT INFO SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_LIFNR:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_LIFNR",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZSRM_ROUTING_POST_NEW", Route="api/ZSRM_ROUTING_POST_NEW", Group="Vendor SRM Routing",
                    SapRfc="ZSRM_ROUTING_POST_NEW", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM ROUTING POST NEW SAP RFC",
                    SampleRequest="IM_PO_NO:VALUE, IM_GEN_ART:VALUE, IM_RTNO:VALUE, IM_HHTUSER:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_PO_NO",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_GEN_ART",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_RTNO",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_HHTUSER",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZSRM_PO_RFC_GET_ROUTING", Route="api/ZSRM_PO_RFC_GET_ROUTING", Group="Vendor SRM Routing",
                    SapRfc="ZSRM_PO_RFC_GET_ROUTING", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM PO RFC GET ROUTING SAP RFC",
                    SampleRequest="IM_PO_NO:VALUE, IM_DESIGN:VALUE, IM_SATNR:VALUE, IM_PO:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_GROUT_STAT:[{FIELD1:VAL1}], ET_PRD_ROUTING:[{FIELD1:VAL1}], ET_BR:[{FIELD1:VAL1}], ET_ACCST:[{FIELD1:VAL1}], ET_PPSR:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_PO_NO",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_DESIGN",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_SATNR",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PO",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_GROUT_STAT","ET_PRD_ROUTING","ET_BR","ET_ACCST","ET_PPSR"}
                },

                new RfcEndpoint {
                    Name="ZSRM_ROUTING_POST", Route="api/ZSRM_ROUTING_POST", Group="Vendor SRM Routing",
                    SapRfc="ZSRM_ROUTING_POST", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM ROUTING POST SAP RFC",
                    SampleRequest="IM_USER:USER01, IM_PO:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{RESULT:Processed}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PO",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{}
                },

                new RfcEndpoint {
                    Name="ZME2M_LIVE", Route="api/ZME2M_LIVE", Group="Vendor SRM Routing",
                    SapRfc="ZME2M_LIVE", SapHost="192.168.144.170", Client="600",
                    Description="ZME2M LIVE SAP RFC",
                    SampleRequest="IM_DATE_FROM:20240101, IM_DATE_TO:20240101, IM_COMP:VALUE, IT_WERKS:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{ET_ARTICLE_COLOR:[{FIELD1:VAL1}], ET_PUR_DATA:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DATE_FROM",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_DATE_TO",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_COMP",SapType="string",IsTable=false},

                        new RfcParam{Name="IT_WERKS",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{"ET_ARTICLE_COLOR","ET_PUR_DATA","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZMM_ART_CREATION_RFC", Route="api/ZMM_ART_CREATION_RFC", Group="Vendor SRM Routing",
                    SapRfc="ZMM_ART_CREATION_RFC", SapHost="192.168.144.170", Client="600",
                    Description="ZMM ART CREATION RFC SAP RFC",
                    SampleRequest=", IM_DATA:[{KEY:VALUE}]",
                    SampleResponse="Status:S, Message:Success, Data:{LT_DATA:[{FIELD1:VAL1}], ET_EAN_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_DATA",SapType="table",IsTable=true}
                    },
                    ResponseTables=new List<string>{"LT_DATA","ET_EAN_DATA"}
                },

                new RfcEndpoint {
                    Name="ZPO_MODIFICATION", Route="api/ZPO_MODIFICATION", Group="Vendor SRM Routing",
                    SapRfc="ZPO_MODIFICATION", SapHost="192.168.144.170", Client="600",
                    Description="ZPO MODIFICATION SAP RFC",
                    SampleRequest="IM_PO_NO:VALUE, IM_PO_DEL_DATE:20240101, IM_DEL_CHG_DATE_LOW:20240101, IM_DEL_CHG_DATE_HIGH:20240101",
                    SampleResponse="Status:S, Message:Success, Data:{ET_PO_OUTPUT:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_PO_NO",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_PO_DEL_DATE",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_DEL_CHG_DATE_LOW",SapType="string",IsTable=false},

                        new RfcParam{Name="IM_DEL_CHG_DATE_HIGH",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_PO_OUTPUT"}
                },

                new RfcEndpoint {
                    Name="ZSRM_GET_VENDOR_ZONE_DATA", Route="api/ZSRM_GET_VENDOR_ZONE_DATA", Group="Vendor SRM Zone",
                    SapRfc="ZSRM_GET_VENDOR_ZONE_DATA", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM GET VENDOR ZONE DATA SAP RFC",
                    SampleRequest="IM_ZONE_ID:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_ZONE_ID",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

                new RfcEndpoint {
                    Name="ZSRM_VEND_PEND_PO", Route="api/ZSRM_VEND_PEND_PO", Group="Vendor SRM Zone",
                    SapRfc="ZSRM_VEND_PEND_PO", SapHost="192.168.144.170", Client="600",
                    Description="ZSRM VEND PEND PO SAP RFC",
                    SampleRequest="IM_LIFNR:VALUE",
                    SampleResponse="Status:S, Message:Success, Data:{ET_DATA:[{FIELD1:VAL1}]}",
                    SampleError="Status:E, Message:SAP error - check logs",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_LIFNR",SapType="string",IsTable=false}
                    },
                    ResponseTables=new List<string>{"ET_DATA"}
                },

            };
        }

        private string BuildOpenApiSpec(List<RfcEndpoint> eps)
        {
            var paths = new JObject();
            foreach (var ep in eps)
            {
                var props = new JObject();
                if (ep.Parameters != null)
                    foreach (var p in ep.Parameters)
                        props[p.Name] = new JObject {
                            ["type"] = p.IsTable ? "array" : "string",
                            ["description"] = p.Name
                        };

                paths["/" + ep.Route] = new JObject {
                    ["post"] = new JObject {
                        ["tags"]        = new JArray(ep.Group),
                        ["operationId"] = ep.Name,
                        ["summary"]     = ep.Description,
                        ["requestBody"] = new JObject {
                            ["required"] = true,
                            ["content"]  = new JObject {
                                ["application/json"] = new JObject {
                                    ["schema"]  = new JObject { ["type"] = "object", ["properties"] = props },
                                    ["example"] = new JObject { ["note"] = ep.SampleRequest }
                                }
                            }
                        },
                        ["responses"] = new JObject {
                            ["200"] = new JObject {
                                ["description"] = "SAP Success",
                                ["content"] = new JObject {
                                    ["application/json"] = new JObject {
                                        ["example"] = new JObject {
                                            ["Status"]   = "S",
                                            ["Message"]  = "Success",
                                            ["Sample"]   = ep.SampleResponse
                                        }
                                    }
                                }
                            },
                            ["400"] = new JObject {
                                ["description"] = "SAP Error",
                                ["content"] = new JObject {
                                    ["application/json"] = new JObject {
                                        ["example"] = new JObject {
                                            ["Status"]  = "E",
                                            ["Message"] = ep.SampleError
                                        }
                                    }
                                }
                            },
                            ["500"] = new JObject { ["description"] = "Server Error" }
                        }
                    }
                };
            }

            return new JObject {
                ["openapi"] = "3.0.1",
                ["info"]    = new JObject {
                    ["title"]       = "V2 Retail RFC API",
                    ["version"]     = "1.0.0",
                    ["description"] = "SAP RFC Integration API - Branch: " + GH_BRANCH
                },
                ["servers"] = new JArray { new JObject { ["url"] = IIS_BASE } },
                ["paths"]   = paths
            }.ToString(Formatting.Indented);
        }
    }
}
