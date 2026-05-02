using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using Vendor_Application_MVC.Controllers;

namespace Vendor_Application_MVC.Controllers.Finance
{
    /// <summary>
    /// Account Document Posting RFC.
    /// Posts FI accounting documents to SAP — accepts GL lines, currency amounts, vendor payable items.
    /// Returns created document number (GL_NUM) and status (ES_RETURN).
    /// CAUTION: Use DEV environment for testing — creates real FI entries in PROD.
    /// RFC: ZRFC_ACC_DOC_POST | SAP Dev: 192.168.144.174/210
    /// </summary>
    [RoutePrefix("api")]
    public class ZRFC_ACC_DOC_POSTController : BaseController
    {
        [HttpPost, Route("ZRFC_ACC_DOC_POST")]
        [ResponseType(typeof(object))]
        public HttpResponseMessage Execute([FromBody] AccDocPostRequest request)
        {
            try
            {
                if (request == null) request = new AccDocPostRequest();

                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                IRfcFunction rfcFunction = dest.Repository.CreateFunction("ZRFC_ACC_DOC_POST");

                // Import scalar parameters
                if (!string.IsNullOrEmpty(request.OBJ_KEY))
                    rfcFunction.SetValue("OBJ_KEY", request.OBJ_KEY);
                if (!string.IsNullOrEmpty(request.ACT_NO))
                    rfcFunction.SetValue("ACT_NO", request.ACT_NO);

                // Table: LT_ACCOUNTGL (G/L account items) — RULE 3: Append then SetValue
                if (request.LT_ACCOUNTGL != null && request.LT_ACCOUNTGL.Count > 0)
                {
                    IRfcTable glTable = rfcFunction.GetTable("LT_ACCOUNTGL");
                    foreach (var item in request.LT_ACCOUNTGL)
                    {
                        glTable.Append();
                        glTable.SetValue("ACCOUNT", item.ACCOUNT ?? "");
                        glTable.SetValue("COSTCENTER", item.COSTCENTER ?? "");
                    }
                }

                // Table: LT_CURRENCYAMOUNT (currency/amount items) — RULE 3
                if (request.LT_CURRENCYAMOUNT != null && request.LT_CURRENCYAMOUNT.Count > 0)
                {
                    IRfcTable curTable = rfcFunction.GetTable("LT_CURRENCYAMOUNT");
                    foreach (var item in request.LT_CURRENCYAMOUNT)
                    {
                        curTable.Append();
                        curTable.SetValue("CURRENCY", item.CURRENCY ?? "INR");
                        curTable.SetValue("AMT_DOCCUR", item.AMT_DOCCUR);
                    }
                }

                // Table: LT_PAYBLE (vendor payable items) — RULE 3
                if (request.LT_PAYBLE != null && request.LT_PAYBLE.Count > 0)
                {
                    IRfcTable payTable = rfcFunction.GetTable("LT_PAYBLE");
                    foreach (var item in request.LT_PAYBLE)
                    {
                        payTable.Append();
                        payTable.SetValue("VENDOR_NO", item.VENDOR_NO ?? "");
                        payTable.SetValue("BLINE_DATE", item.BLINE_DATE ?? "");
                        payTable.SetValue("REF_TEXT", item.REF_TEXT ?? "");
                    }
                }

                rfcFunction.Invoke(dest);

                // RULE 1: ES_RETURN is BAPIRET2 STRUCTURE → GetStructure (not GetTable)
                IRfcStructure esReturn = rfcFunction.GetStructure("ES_RETURN");
                string glNum = "";
                try { glNum = rfcFunction.GetString("GL_NUM"); } catch { }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status  = esReturn.GetString("TYPE"),
                    Message = esReturn.GetString("MESSAGE"),
                    Data = new
                    {
                        GL_NUM  = glNum,
                        ES_RETURN = new
                        {
                            TYPE    = esReturn.GetString("TYPE"),
                            ID      = esReturn.GetString("ID"),
                            NUMBER  = esReturn.GetString("NUMBER"),
                            MESSAGE = esReturn.GetString("MESSAGE")
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

    public class AccDocPostRequest
    {
        public string OBJ_KEY { get; set; }
        public string ACT_NO  { get; set; }
        public List<GlAccountItem>      LT_ACCOUNTGL      { get; set; }
        public List<CurrencyAmountItem> LT_CURRENCYAMOUNT { get; set; }
        public List<PaybleItem>         LT_PAYBLE         { get; set; }
    }
    public class GlAccountItem      { public string ACCOUNT { get; set; } public string COSTCENTER { get; set; } }
    public class CurrencyAmountItem { public string CURRENCY { get; set; } public string AMT_DOCCUR { get; set; } }
    public class PaybleItem         { public string VENDOR_NO { get; set; } public string BLINE_DATE { get; set; } public string REF_TEXT { get; set; } }
}
