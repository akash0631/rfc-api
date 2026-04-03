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
    public class ZVND_UNLOAD_SAVE_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZVND_UNLOAD_SAVE_RFC")]
        public IHttpActionResult UnloadSaveData([FromBody] UnloadSaveRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Ok(new { Status = "E", Message = "Request cannot be null" });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZVND_UNLOAD_SAVE_RFC");

                myfun.SetValue("IM_USER", request.IM_USER ?? string.Empty);
                
                if (request.IM_PARMS != null && request.IM_PARMS.Count > 0)
                {
                    IRfcTable parmsTable = myfun.GetTable("IM_PARMS");
                    foreach (var parm in request.IM_PARMS)
                    {
                        IRfcStructure row = parmsTable.Metadata.LineType.CreateStructure();
                        foreach (var property in parm.GetType().GetProperties())
                        {
                            var value = property.GetValue(parm);
                            if (value != null)
                            {
                                row.SetValue(property.Name.ToUpper(), value.ToString());
                            }
                        }
                        parmsTable.Append(row);
                    }
                }

                myfun.SetValue("PLANT", request.PLANT ?? string.Empty);
                myfun.SetValue("VEHICLE", request.VEHICLE ?? string.Empty);
                myfun.SetValue("EXT_HU", request.EXT_HU ?? string.Empty);
                myfun.SetValue("PALETTE", request.PALETTE ?? string.Empty);
                myfun.SetValue("PO_NO", request.PO_NO ?? string.Empty);
                myfun.SetValue("BILL_NO", request.BILL_NO ?? string.Empty);
                myfun.SetValue("HU_WT", request.HU_WT ?? string.Empty);

                myfun.Invoke(dest);

                IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");
                
                string returnType = EX_RETURN.GetString("TYPE");
                string returnMessage = EX_RETURN.GetString("MESSAGE");

                if (returnType == "E")
                {
                    return Ok(new { Status = "E", Message = returnMessage });
                }

                return Ok(new { Status = "S", Message = returnMessage });
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
    }

    public class UnloadSaveRequest
    {
        public string IM_USER { get; set; }
        public List<object> IM_PARMS { get; set; }
        public string PLANT { get; set; }
        public string VEHICLE { get; set; }
        public string EXT_HU { get; set; }
        public string PALETTE { get; set; }
        public string PO_NO { get; set; }
        public string BILL_NO { get; set; }
        public string HU_WT { get; set; }
    }
}