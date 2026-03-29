using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    [RoutePrefix("api")]
    public class ZMM_ART_MOD_PO_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZMM_ART_MOD_PO_RFC")]
        public async Task<object> ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_Request request)
        {
            try
            {
                if (request == null)
                {
                    return new { Status = "E", Message = "Request cannot be null" };
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                // Set IM_INPUT structure
                if (request.IM_INPUT != null)
                {
                    IRfcStructure imInput = myfun.GetStructure("IM_INPUT");
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EKKO_EBELN))
                        imInput.SetValue("EKKO_EBELN", request.IM_INPUT.EKKO_EBELN);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EKPO_EBELP))
                        imInput.SetValue("EKPO_EBELP", request.IM_INPUT.EKPO_EBELP);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EKPO_MATNR))
                        imInput.SetValue("EKPO_MATNR", request.IM_INPUT.EKPO_MATNR);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.MARA_COLOR))
                        imInput.SetValue("MARA_COLOR", request.IM_INPUT.MARA_COLOR);
                    if (request.IM_INPUT.EKPO_MENGE != 0)
                        imInput.SetValue("EKPO_MENGE", request.IM_INPUT.EKPO_MENGE);
                    if (request.IM_INPUT.EKPO_NETPR != 0)
                        imInput.SetValue("EKPO_NETPR", request.IM_INPUT.EKPO_NETPR);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EKPO_MEINS))
                        imInput.SetValue("EKPO_MEINS", request.IM_INPUT.EKPO_MEINS);
                    if (!string.IsNullOrEmpty(request.IM_INPUT.EKPO_TXZ01))
                        imInput.SetValue("EKPO_TXZ01", request.IM_INPUT.EKPO_TXZ01);
                }

                // Set IM_OUTPUT table
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var outputItem in request.IM_OUTPUT)
                    {
                        imOutputTable.Append();
                        if (!string.IsNullOrEmpty(outputItem.EBELN))
                            imOutputTable.SetValue("EBELN", outputItem.EBELN);
                        if (!string.IsNullOrEmpty(outputItem.EBELP))
                            imOutputTable.SetValue("EBELP", outputItem.EBELP);
                        if (!string.IsNullOrEmpty(outputItem.MATNR))
                            imOutputTable.SetValue("MATNR", outputItem.MATNR);
                        if (!string.IsNullOrEmpty(outputItem.COLOR))
                            imOutputTable.SetValue("COLOR", outputItem.COLOR);
                        if (outputItem.MENGE != 0)
                            imOutputTable.SetValue("MENGE", outputItem.MENGE);
                        if (outputItem.NETPR != 0)
                            imOutputTable.SetValue("NETPR", outputItem.NETPR);
                        if (!string.IsNullOrEmpty(outputItem.MEINS))
                            imOutputTable.SetValue("MEINS", outputItem.MEINS);
                        if (!string.IsNullOrEmpty(outputItem.TXZ01))
                            imOutputTable.SetValue("TXZ01", outputItem.TXZ01);
                        if (!string.IsNullOrEmpty(outputItem.MESSAGE))
                            imOutputTable.SetValue("MESSAGE", outputItem.MESSAGE);
                        if (!string.IsNullOrEmpty(outputItem.STATUS))
                            imOutputTable.SetValue("STATUS", outputItem.STATUS);
                    }
                }

                myfun.Invoke(dest);
                
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");
                
                if (returnType == "E")
                {
                    return new { Status = "E", Message = returnMessage };
                }
                
                return new { Status = returnType, Message = returnMessage };
            }
            catch (RfcAbapException ex)
            {
                return new { Status = "E", Message = ex.Message };
            }
            catch (RfcCommunicationException ex)
            {
                return new { Status = "E", Message = ex.Message };
            }
            catch (Exception ex)
            {
                return new { Status = "E", Message = ex.Message };
            }
        }
    }

    public class ZMM_ART_MOD_PO_Request
    {
        public IM_INPUT_Structure IM_INPUT { get; set; }
        public List<IM_OUTPUT_Structure> IM_OUTPUT { get; set; }
    }

    public class IM_INPUT_Structure
    {
        public string EKKO_EBELN { get; set; }
        public string EKPO_EBELP { get; set; }
        public string EKPO_MATNR { get; set; }
        public string MARA_COLOR { get; set; }
        public decimal EKPO_MENGE { get; set; }
        public decimal EKPO_NETPR { get; set; }
        public string EKPO_MEINS { get; set; }
        public string EKPO_TXZ01 { get; set; }
    }

    public class IM_OUTPUT_Structure
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public decimal MENGE { get; set; }
        public decimal NETPR { get; set; }
        public string MEINS { get; set; }
        public string TXZ01 { get; set; }
        public string MESSAGE { get; set; }
        public string STATUS { get; set; }
    }
}