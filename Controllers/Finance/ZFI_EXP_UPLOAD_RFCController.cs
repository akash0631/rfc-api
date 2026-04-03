using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using System.Linq;

namespace Vendor_SRM_Routing_Application.Controllers.Finance
{
    public class ZFI_EXP_UPLOAD_RFCController : BaseController
    {
        [HttpPost]
        [Route("api/ZFI_EXP_UPLOAD_RFC")]
        public async Task<HttpResponseMessage> ZFI_EXP_UPLOAD_RFC([FromBody] ZFI_EXP_UPLOAD_RFCRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        Status = "E",
                        Message = "Request body cannot be null",
                        Data = new { EX_RETURN = new List<object>() }
                    });
                }

                RfcConfigParameters rfcPar = BaseController.rfcConfigparametersquality();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository rfcrep = dest.Repository;
                IRfcFunction myfun = rfcrep.CreateFunction("ZFI_EXP_UPLOAD_RFC");

                if (request.IM_INPUT != null)
                {
                    IRfcTable imInputTable = myfun.GetTable("IM_INPUT");
                    foreach (var inputItem in request.IM_INPUT)
                    {
                        IRfcStructure inputRow = imInputTable.Metadata.LineType.CreateStructure();
                        var properties = inputItem.GetType().GetProperties();
                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(inputItem);
                            if (value != null)
                            {
                                inputRow.SetValue(prop.Name, value);
                            }
                        }
                        imInputTable.Append(inputRow);
                    }
                }

                if (request.IM_OUTPUT != null)
                {
                    IRfcTable imOutputTable = myfun.GetTable("IM_OUTPUT");
                    foreach (var outputItem in request.IM_OUTPUT)
                    {
                        IRfcStructure outputRow = imOutputTable.Metadata.LineType.CreateStructure();
                        var properties = outputItem.GetType().GetProperties();
                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(outputItem);
                            if (value != null)
                            {
                                outputRow.SetValue(prop.Name, value);
                            }
                        }
                        imOutputTable.Append(outputRow);
                    }
                }

                myfun.Invoke(dest);

                IRfcStructure exReturnStruct = myfun.GetStructure("EX_RETURN");
                var exReturnList = new List<Dictionary<string, string>>();
                var returnDict = new Dictionary<string, string>();
                for (int f = 0; f < exReturnStruct.Metadata.FieldCount; f++)
                {
                    string fname = exReturnStruct.Metadata[f].Name;
                    returnDict[fname] = exReturnStruct.GetString(fname);
                }
                exReturnList.Add(returnDict);