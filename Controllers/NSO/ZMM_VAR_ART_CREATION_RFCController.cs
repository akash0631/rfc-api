using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using Vendor_Application_MVC.Controllers;

namespace Vendor_Application_MVC.Controllers.NSO
{
    /// <summary>
    /// Variant Article Creation RFC.
    /// Creates variant articles in SAP MM. Accepts a list of article records via IM_DATA table.
    /// Returns EX_RETURN (ZMM_VAR_ART_MSG) with creation status and messages.
    /// RFC: ZMM_VAR_ART_CREATION_RFC | SAP function group: 2004_ART_CREATION_FG
    /// </summary>
    [RoutePrefix("api")]
    public class ZMM_VAR_ART_CREATION_RFCController : BaseController
    {
        [HttpPost, Route("ZMM_VAR_ART_CREATION_RFC")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Execute([FromBody] VarArtCreationRequest request)
        {
            try
            {
                if (request == null) request = new VarArtCreationRequest();

                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction rfcFunction = dest.Repository.CreateFunction("ZMM_VAR_ART_CREATION_RFC");

                // Table: IM_DATA (ZMM_VAR_ART_TT) — variant article records — RULE 3
                if (request.IM_DATA != null && request.IM_DATA.Count > 0)
                {
                    IRfcTable imData = rfcFunction.GetTable("IM_DATA");
                    foreach (var item in request.IM_DATA)
                    {
                        imData.Append();
                        imData.SetValue("GENERIC_ARTICLE",  item.GENERIC_ARTICLE  ?? "");
                        imData.SetValue("VARIANT_ARTICLE",  item.VARIANT_ARTICLE  ?? "");
                        imData.SetValue("VAR1CHAR1",        item.VAR1CHAR1        ?? "");
                        imData.SetValue("VAR1VAL1",         item.VAR1VAL1         ?? "");
                        imData.SetValue("VAR1CHAR2",        item.VAR1CHAR2        ?? "");
                        imData.SetValue("VAR1VAL2",         item.VAR1VAL2         ?? "");
                        imData.SetValue("VENDOR",           item.VENDOR           ?? "");
                        imData.SetValue("SITE",             item.SITE             ?? "");
                        imData.SetValue("PUR_GRP",          item.PUR_GRP          ?? "");
                        imData.SetValue("NET_PRICE",        item.NET_PRICE        ?? "");
                        imData.SetValue("SALES_ORG",        item.SALES_ORG        ?? "");
                        imData.SetValue("SALES_UNIT",       item.SALES_UNIT       ?? "");
                        imData.SetValue("MRP_TYPE",         item.MRP_TYPE         ?? "");
                        imData.SetValue("FROM_DATE",        item.FROM_DATE        ?? "");
                        imData.SetValue("TO_DATE",          item.TO_DATE          ?? "");
                        imData.SetValue("OLD_MAT_NO",       item.OLD_MAT_NO       ?? "");
                        imData.SetValue("TAX_CODE",         item.TAX_CODE         ?? "");
                    }
                }

                rfcFunction.Invoke(dest);

                // RULE 1: EX_RETURN is ZMM_VAR_ART_MSG (STRUCTURE export param) → GetStructure
                IRfcStructure exReturn = rfcFunction.GetStructure("EX_RETURN");

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status  = exReturn.GetString("TYPE"),
                    Message = exReturn.GetString("MESSAGE"),
                    Data = new
                    {
                        EX_RETURN = new
                        {
                            TYPE    = exReturn.GetString("TYPE"),
                            MESSAGE = exReturn.GetString("MESSAGE")
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError,
                    new { Status = "E", Message = ex.Message });
            }
        }
    }

    public class VarArtCreationRequest { public List<VarArtItem> IM_DATA { get; set; } }
    public class VarArtItem
    {
        public string GENERIC_ARTICLE { get; set; }  public string VARIANT_ARTICLE { get; set; }
        public string VAR1CHAR1       { get; set; }  public string VAR1VAL1        { get; set; }
        public string VAR1CHAR2       { get; set; }  public string VAR1VAL2        { get; set; }
        public string VENDOR          { get; set; }  public string SITE            { get; set; }
        public string PUR_GRP         { get; set; }  public string NET_PRICE       { get; set; }
        public string SALES_ORG       { get; set; }  public string SALES_UNIT      { get; set; }
        public string MRP_TYPE        { get; set; }  public string FROM_DATE       { get; set; }
        public string TO_DATE         { get; set; }  public string OLD_MAT_NO      { get; set; }
        public string TAX_CODE        { get; set; }
    }
}
