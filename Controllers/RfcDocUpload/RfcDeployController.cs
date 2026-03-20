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
        public string FilePath { get; set; }
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
            var eps = GetAllEndpoints();
            return Content(BuildOpenApiSpec(eps), "application/json");
        }

        private List<RfcEndpoint> GetAllEndpoints()
        {
            return new List<RfcEndpoint>
            {
                new RfcEndpoint { Name="ZADVANCE_PAYMENT_RFC", Route="api/ZADVANCE_PAYMENT_RFC", Group="Finance", SapRfc="ZADVANCE_PAYMENT_RFC", SapHost="192.168.144.170", Client="600",
                    Description="Fetch advance payment documents from SAP",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="I_COMPANY_CODE", SapType="BUKRS", IsTable=false},
                        new RfcParam{Name="I_POSTING_DATE_LOW", SapType="DATUM", IsTable=false},
                        new RfcParam{Name="I_POSTING_DATE_HIGH", SapType="DATUM", IsTable=false}},
                    ResponseTables=new List<string>{"ET_ADVANCE_PAYMENT"} },

                new RfcEndpoint { Name="ZVND_PUT01_HU_VAL_RFC", Route="api/ZVND_PUT01_HU_VAL_RFC", Group="Inbound", SapRfc="ZVND_PUT01_HU_VAL_RFC", SapHost="192.168.144.174", Client="210",
                    Description="PUT01 - Validate HU and fetch PO No, INV No, HU QTY",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER", SapType="WWWOBJID", IsTable=false},
                        new RfcParam{Name="IM_PLANT", SapType="WERKS_D", IsTable=false},
                        new RfcParam{Name="IM_HU", SapType="ZEXT_HU", IsTable=false}},
                    ResponseTables=new List<string>{"ET_DATA"} },

                new RfcEndpoint { Name="ZVND_PUT01_SAVE_DATA_RFC", Route="api/ZVND_PUT01_SAVE_DATA_RFC", Group="Inbound", SapRfc="ZVND_PUT01_SAVE_DATA_RFC", SapHost="192.168.144.174", Client="210",
                    Description="PUT01 - Save validated HU data to SAP",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER", SapType="WWWOBJID", IsTable=false},
                        new RfcParam{Name="IT_DATA", SapType="ZTT_PUT01_SAVE", IsTable=true}},
                    ResponseTables=new List<string>{"EX_RETURN"} },

                new RfcEndpoint { Name="ZVND_GATELOT2_PICKLIST_VAL_RFC", Route="api/ZVND_GATELOT2_PICKLIST_VAL_RFC", Group="Inbound", SapRfc="ZVND_GATELOT2_PICKLIST_VAL_RFC", SapHost="192.168.144.174", Client="210",
                    Description="GATELOT2 - Validate Picklist No, fetch PO No and INV No",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER", SapType="WWWOBJID", IsTable=false},
                        new RfcParam{Name="IM_PLANT", SapType="WERKS_D", IsTable=false}},
                    ResponseTables=new List<string>{"ET_DATA"} },

                new RfcEndpoint { Name="ZVND_GATELOT2_BIN_VAL_RFC", Route="api/ZVND_GATELOT2_BIN_VAL_RFC", Group="Inbound", SapRfc="ZVND_GATELOT2_BIN_VAL_RFC", SapHost="192.168.144.174", Client="210",
                    Description="GATELOT2 - Validate BIN location for given Picklist",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER", SapType="WWWOBJID", IsTable=false},
                        new RfcParam{Name="IM_PLANT", SapType="WERKS_D", IsTable=false},
                        new RfcParam{Name="IM_PICKLIST", SapType="ZPICKLIST_NO", IsTable=false},
                        new RfcParam{Name="IM_BIN", SapType="LGPLA", IsTable=false}},
                    ResponseTables=new List<string>{"EX_RETURN"} },

                new RfcEndpoint { Name="ZVND_GATELOT2_PALETTE_VAL_RFC", Route="api/ZVND_GATELOT2_PALETTE_VAL_RFC", Group="Inbound", SapRfc="ZVND_GATELOT2_PALETTE_VAL_RFC", SapHost="192.168.144.174", Client="210",
                    Description="GATELOT2 - Validate Palette, returns BIN, Pallet and BOX (HU) data",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER", SapType="WWWOBJID", IsTable=false},
                        new RfcParam{Name="IM_PLANT", SapType="WERKS_D", IsTable=false},
                        new RfcParam{Name="IM_PICKLIST", SapType="ZPICKLIST_NO", IsTable=false},
                        new RfcParam{Name="IM_BIN", SapType="LGPLA", IsTable=false},
                        new RfcParam{Name="IM_PALL", SapType="ZZPALETTE", IsTable=false}},
                    ResponseTables=new List<string>{"ET_BIN","ET_PALL","ET_BOX"} },

                new RfcEndpoint { Name="ZVND_GATELOT2_SAVE_DATA_RFC", Route="api/ZVND_GATELOT2_SAVE_DATA_RFC", Group="Inbound", SapRfc="ZVND_GATELOT2_SAVE_DATA_RFC", SapHost="192.168.144.174", Client="210",
                    Description="GATELOT2 - Save validated Palette/BIN/HU data to SAP",
                    Parameters=new List<RfcParam>{
                        new RfcParam{Name="IM_USER", SapType="WWWOBJID", IsTable=false},
                        new RfcParam{Name="IT_DATA", SapType="ZTT_GATELOT2_SAVE", IsTable=true}},
                    ResponseTables=new List<string>{"EX_RETURN"} },
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
                            ["description"] = p.Description ?? p.Name
                        };

                var req = ep.Parameters != null ? ep.Parameters.Where(p => p.Required).Select(p => p.Name).ToList() : new List<string>();

                paths["/" + ep.Route] = new JObject {
                    ["post"] = new JObject {
                        ["tags"] = new JArray(ep.Group),
                        ["operationId"] = ep.Name,
                        ["summary"] = ep.Description,
                        ["requestBody"] = new JObject {
                            ["required"] = true,
                            ["content"] = new JObject {
                                ["application/json"] = new JObject {
                                    ["schema"] = new JObject {
                                        ["type"] = "object",
                                        ["properties"] = props
                                    }
                                }
                            }
                        },
                        ["responses"] = new JObject {
                            ["200"] = new JObject { ["description"] = "Success - Status: S" },
                            ["400"] = new JObject { ["description"] = "SAP Error - Status: E" },
                            ["500"] = new JObject { ["description"] = "Server Error" }
                        }
                    }
                };
            }

            var spec = new JObject {
                ["openapi"] = "3.0.1",
                ["info"] = new JObject {
                    ["title"] = "V2 Retail - RFC API",
                    ["version"] = "1.0.0",
                    ["description"] = "SAP RFC Integration API - Branch: " + GH_BRANCH
                },
                ["servers"] = new JArray { new JObject { ["url"] = IIS_BASE } },
                ["paths"] = paths
            };

            return spec.ToString(Formatting.Indented);
        }
    }
}
