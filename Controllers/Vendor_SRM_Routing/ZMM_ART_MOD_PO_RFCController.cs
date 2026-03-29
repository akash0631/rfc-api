using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace Vendor_SRM_Routing_Application.Controllers.Vendor
{
    public class ZMM_ART_MOD_PO_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZMM_ART_MOD_PO_RFC")]
        public IHttpActionResult ZMM_ART_MOD_PO_RFC([FromBody] ZMM_ART_MOD_PO_Request request)
        {
            try
            {
                if (request == null)
                {
                    return Ok(new { Status = "E", Message = "Request body cannot be null" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZMM_ART_MOD_PO_RFC");

                // Set IM_INPUT structure
                if (request.IM_INPUT != null)
                {
                    IRfcStructure imInputStructure = myfun.GetStructure("IM_INPUT");
                    SetStructureValues(imInputStructure, request.IM_INPUT);
                }

                // Set IM_OUTPUT table
                if (request.IM_OUTPUT != null && request.IM_OUTPUT.Count > 0)
                {
                    IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var item in request.IM_OUTPUT)
                    {
                        imOutputTable.Append();
                        SetTableRowValues(imOutputTable.CurrentRow, item);
                    }
                }

                myfun.Invoke(dest);
                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

                string status = EX_RETURN.GetString("TYPE");
                string message = EX_RETURN.GetString("MESSAGE");

                if (status == "E")
                {
                    return Ok(new { Status = "E", Message = message });
                }

                return Ok(new { Status = status, Message = message });
            }
            catch (RfcAbapException ex)
            {
                return Ok(new { Status = "E", Message = ex.Message });
            }
            catch (RfcCommunicationException ex)
            {
                return Ok(new { Status = "E", Message = ex.Message });
            }
            catch (Exception ex)
            {
                return Ok(new { Status = "E", Message = ex.Message });
            }
        }

        private void SetStructureValues(IRfcStructure structure, ZMM_PO_ART_ST input)
        {
            var structureType = structure.Metadata;
            foreach (var field in structureType)
            {
                try
                {
                    var property = input.GetType().GetProperty(field.Name);
                    if (property != null)
                    {
                        var value = property.GetValue(input);
                        if (value != null)
                        {
                            structure.SetValue(field.Name, value);
                        }
                    }
                }
                catch
                {
                    // Skip field if conversion fails
                }
            }
        }

        private void SetTableRowValues(IRfcStructure row, ZMM_PO_ART_OUT_IT item)
        {
            var rowType = row.Metadata;
            foreach (var field in rowType)
            {
                try
                {
                    var property = item.GetType().GetProperty(field.Name);
                    if (property != null)
                    {
                        var value = property.GetValue(item);
                        if (value != null)
                        {
                            row.SetValue(field.Name, value);
                        }
                    }
                }
                catch
                {
                    // Skip field if conversion fails
                }
            }
        }
    }

    public class ZMM_ART_MOD_PO_Request
    {
        public ZMM_PO_ART_ST IM_INPUT { get; set; }
        public List<ZMM_PO_ART_OUT_IT> IM_OUTPUT { get; set; }
    }

    public class ZMM_PO_ART_ST
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public decimal MENGE { get; set; }
        public decimal NETPR { get; set; }
        public string MEINS { get; set; }
        public DateTime EEIND { get; set; }
        public string BSART { get; set; }
        public string BUKRS { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public string MATKL { get; set; }
        public string PSTYP { get; set; }
        public string KNTTP { get; set; }
    }

    public class ZMM_PO_ART_OUT_IT
    {
        public string EBELN { get; set; }
        public string EBELP { get; set; }
        public string MATNR { get; set; }
        public string COLOR { get; set; }
        public decimal MENGE { get; set; }
        public decimal NETPR { get; set; }
        public string MEINS { get; set; }
        public DateTime EEIND { get; set; }
        public string STATUS { get; set; }
        public string MESSAGE { get; set; }
        public string BSART { get; set; }
        public string BUKRS { get; set; }
        public string WERKS { get; set; }
        public string LGORT { get; set; }
        public string MATKL { get; set; }
        public string PSTYP { get; set; }
        public string KNTTP { get; set; }
    }
}