using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using VendorSRM_Application.Models;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System.Collections;
using Vendor_Application_MVC.Controllers;

namespace VendorSRM_Application.Controllers.API
{
    public class HUCreationController : BaseController
    {
        // GET: HUCreation
        [System.Web.Mvc.HttpPost]
        public async Task<HttpResponseMessage> Post([FromBody] List<Submit_Routing_StatusRequest> requests)
        {
            Submit_Routing_Status PO_Detail = new Submit_Routing_Status();
            
                try
                {
                    string connectionString = ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;
                    foreach (var request in requests)
                    {
                        if (request.HU_NO != "" && request.HU_NO != null &&
                            request.PO_NO != "" && request.PO_NO != null
                            && request.ARTICLE_NO != "" && request.ARTICLE_NO != null
                            && request.VENDOR_CODE != "" && request.VENDOR_CODE != null
                            //&& request.DESIGN != "" && request.DESIGN != null
                            && request.QUANTITY != "" && request.QUANTITY != null
                            && request.TempHuNumber !=""&& request.TempHuNumber!= null)
                    {
                            //check for max qty case
                            string query2 = $@"Select IsNull(Sum(Isnull(Scan_Qty,0)),0) from tblHuCarterNameEntries where [VendorCode]={request.VENDOR_CODE} and [PONumber]='{request.PO_NO}' and [ArticleNumber]='{request.ARTICLE_NO}' and EAN11 = '{request.Europe_Art}'";

                            using (var connection = new SqlConnection(connectionString))
                            {
                                var command = new SqlCommand(query2, connection);
                                try
                                {
                                    await connection.OpenAsync();
                                    object result = await command.ExecuteScalarAsync();

                                    if (result != null)
                                    {
                                        string qtyExhausted = result.ToString();
                                        var sum = Convert.ToDouble(qtyExhausted) + Convert.ToDouble(request.QUANTITY);
                                        if (sum > request.Max_Qty)
                                        {
                                            throw new Exception($"Max Qty is {request.Max_Qty} for EAN  : {request.Europe_Art}, which you have already exhausted !");
                                        }

                                    }

                                }
                                catch (Exception ex)
                                {
                                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
                                }
                            }


                        }



                        else
                        {
                            PO_Detail.Status = false;
                            PO_Detail.Message = "All request field is Mandatory.";
                            return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                        }
                    }
                    foreach (var request in requests)
                    {

                    //query = $"Update tblHuCarterNameEntries" +
                    //    $" SET [HuNumber] = '{request.HU_NO}',[PONumber] = '{request.PO_NO}',[ArticleNumber] = '{request.ARTICLE_NO}',[Design] = '{request.DESIGN}',[Qty] = {request.QUANTITY},Status=1,[VendorCode] = '{request.VENDOR_CODE}',[EAN11] = '{request.Europe_Art}' where HuCarterName='{request.HU_NO}'";
                    //string query1 = $"INSERT INTO [dbo].[tblHuCarterNameEntries]([HuCarterName],[Status],[HuNumber],[PONumber],[ArticleNumber],[Design],[Scan_Qty],[VendorCode],[EAN11],[PO_Qty])" +
                    //    $"VALUES('{request.HU_NO}',1,'{request.HU_NO}','{request.PO_NO}','{request.ARTICLE_NO}','{request.DESIGN}','{request.QUANTITY}','{request.VENDOR_CODE}','{request.Europe_Art}',{request.Max_Qty});";
                    string query1 = $"INSERT INTO [dbo].[tblHuCarterNameEntries]([HuCarterName],[Status],[HuNumber],[PONumber],[ArticleNumber],[Design],[Scan_Qty],[VendorCode],[EAN11],[PO_Qty],[TempHuNumber])" +
                       $"VALUES('{request.HU_NO}',1,'{request.HU_NO}','{request.PO_NO}','{request.ARTICLE_NO}','{request.DESIGN}','{request.QUANTITY}','{request.VENDOR_CODE}','{request.Europe_Art}',{request.Max_Qty},{request.TempHuNumber});";


                    using (var connection = new SqlConnection(connectionString))
                        {
                            var command = new SqlCommand(query1, connection);
                            if (connection.State == ConnectionState.Closed)
                                await connection.OpenAsync();
                            var result1 = await command.ExecuteNonQueryAsync();
                            if (result1 < 1)
                            {
                                throw new Exception(" Unable to Add Hu at the moment.");
                            }

                        }
                        RfcConfigParameters rfcPar = BaseController.rfcConfigparametersproduction();
                        RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                        // Get RfcTable from SAP
                        RfcRepository rfcrep = dest.Repository;
                        IRfcFunction myfun = null;
                        myfun = rfcrep.CreateFunction("ZVND_HU_PUSH_API_POST"); //RfcFunctionName
                        IRfcStructure E_Data = myfun.GetStructure("EX_DATA");
                        E_Data.SetValue("HU_NO", request.HU_NO.ToUpper());
                        E_Data.SetValue("PO_NO", request.PO_NO);
                        E_Data.SetValue("ARTICLE_NO", request.ARTICLE_NO);
                        E_Data.SetValue("DESIGN", "");
                        E_Data.SetValue("QUANTITY", request.QUANTITY);
                        E_Data.SetValue("VENDOR_CODE", request.VENDOR_CODE);
                        E_Data.SetValue("EAN", request.Europe_Art);
                        //E_Data.SetValue("CREATION_DATE", request.CREATION_DATE);
                        //E_Data.SetValue("CREATION_TIME", request.CREATION_TIME);
                        //E_Data.SetValue("CREATION_USER", request.CREATION_USER);
                        //E_Data.SetValue("MESSAGE", request.MESSAGE);
                        //E_Data.SetValue("STATUS", request.STATUS);




                        myfun.Invoke(dest);
                        IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
                        string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                        string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                        if (SAP_TYPE == "E")
                        {
                            PO_Detail.Status = false;
                            PO_Detail.Message = "" + SAP_Message + "";
                            return Request.CreateResponse(HttpStatusCode.BadRequest, PO_Detail);
                        }


                        PO_Detail.Status = true;
                        PO_Detail.Message = "" + SAP_Message + "";
                        //PO_Detail.Message = "Updated Successfully";
                    }
                    string query = $"Update [tblHuEntries] Set Status =1 where [HuCarterName] = {requests[0].HU_NO}";



                    using (var connection = new SqlConnection(connectionString))
                    {
                        var command = new SqlCommand(query, connection);
                        if (connection.State == ConnectionState.Closed)
                            await connection.OpenAsync();
                        var result1 = await command.ExecuteNonQueryAsync();
                        if (result1 < 1)
                        {
                            throw new Exception(" Unable to Update Hu Status at the moment.");
                        }

                    }
                    return Request.CreateResponse(HttpStatusCode.OK, PO_Detail);
                    //throw new Exception(" Unable to Add Hu at the moment.");
                }
                catch (Exception ex)
                {
                    PO_Detail.Status = false;
                    PO_Detail.Message = "" + ex.Message + "";
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, PO_Detail);
                }
          

        }
    }
}