using DocumentFormat.OpenXml.Office2016.Excel;
using FMS_Fabric_Putway_Api.Models;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.HU_Creation;
using Vendor_SRM_Routing_Application.Models.Sampling;

namespace Sampling.Controllers.Sampling
{
    public class ArticleYesOrNoController : ApiController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> ArticleYesOrNo([FromBody] ArticleYesNo obj)
        {
          
                try
                {
                    // Assuming you have a valid connection string in your configuration
                    string connectionString = ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;

                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        await conn.OpenAsync();

                        // Prepare the SQL query for inserting data
                        string insertQuery = @"
            INSERT INTO [HUCreation].[dbo].[ArticleYesOrNo] 
            ([ArticleNo], [Status],[Remarks]) 
            VALUES 
            (@ArticleNo, @Status, @Remarks)";

                        using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                        {
                            // Add parameters
                            cmd.Parameters.AddWithValue("@ArticleNo", obj.ArticleNO);  // Assuming correct property name
                            cmd.Parameters.AddWithValue("@Status", obj.Status);
                            cmd.Parameters.AddWithValue("@Remarks", obj.Remarks);

                            // Execute the command
                            int rowsAffected = await cmd.ExecuteNonQueryAsync();

                            if (rowsAffected > 0)
                            {
                                // Return response with status true
                                var responseContent = new { Status = true, Message = "Inserted Successfully" };
                                string jsonResponse = Newtonsoft.Json.JsonConvert.SerializeObject(responseContent);
                                try
                                {
                                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                                    // Get RfcTable from SAP
                                    RfcRepository rfcrep = dest.Repository;

                                    IRfcFunction myfun = null;
                                    myfun = rfcrep.CreateFunction("ZARTICLE_YES_NO_POST");

                                    //myfun.SetValue("ID", request.ID);
                                    myfun.SetValue("ARTICLE", obj.ArticleNO);
                                    //myfun.SetValue("CREATION_DT", request.Creation_Dt);
                                    //myfun.SetValue("CREATION_TM", request.Creation_Tm);
                                    myfun.SetValue("STATUS", Convert.ToString(obj.Status));
                                    myfun.SetValue("REMARKS", obj.Remarks);

                                    myfun.Invoke(dest);

                                    IRfcStructure E_RETURN = myfun.GetStructure("EX_RETURN");

                                    string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                                    string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                                    if (SAP_TYPE == "E")
                                    {
                                        return Request.CreateResponse(HttpStatusCode.OK, new
                                        {
                                            Status = false,
                                            Message = "" + SAP_Message + ""

                                        });
                                    }
                                    return Request.CreateResponse(HttpStatusCode.OK, new
                                    {
                                        Status = true,
                                        Message = "" + SAP_Message + ""

                                    });
                                }
                                catch (Exception ex)
                                {
                                    return Request.CreateResponse(HttpStatusCode.InternalServerError, new
                                    {
                                        Status = false,
                                        Message = ex.Message
                                    });
                                }
                                //return new HttpResponseMessage(HttpStatusCode.OK)
                                //{
                                //    Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
                                //};
                            }
                            else
                            {
                                var responseContent = new { Status = false, Message = "Something Went Wrong" };
                                string jsonResponse = Newtonsoft.Json.JsonConvert.SerializeObject(responseContent);

                                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                                {
                                    Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
                                };
                            }

                        }
                    }

                }
                catch (Exception ex)
                {
                    // Log the exception (logging mechanism not shown here)
                    var responseContent = new { Status = false, Message = ex.Message };
                    string jsonResponse = Newtonsoft.Json.JsonConvert.SerializeObject(responseContent);

                    return new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
                    };
                }
            
        }

  //      [HttpPost]

  //      public async Task<HttpResponseMessage> ArticleYesOrNoData()
  //      {
  //          string connectionString = ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;
  //          string query = $@"SELECT [ID]
  //    ,[ArticleNo]
  //    ,[Status]
  //    ,[CreatedBy]
  //    ,[CreatedOn]
  //    ,[UpdatedBy]
  //    ,[UpdatedOn]
  //    ,[IsActive]
  //    ,[IsDeleted]
  //    ,[Remarks]
  //FROM [HUCreation].[dbo].[ArticleYesOrNo] with (nolock)
  //             ;";

  //          using (SqlConnection connection = new SqlConnection(connectionString))
  //          {
  //              SqlCommand command = new SqlCommand(query, connection);
  //              try
  //              {
  //                  await connection.OpenAsync();
  //                  var result = await command.ExecuteReaderAsync();

  //                  if (result != null)
  //                  {
  //                      var data = new List<object>();

  //                      if (result.HasRows)
  //                      {
  //                          while (result.Read())
  //                          {
  //                              var item = new
  //                              {
  //                                  ID = result["ID"].ToString(),
  //                                  ArticleNo = result["ArticleNo"].ToString(),
  //                                  Status = Convert.ToBoolean( result["Status"]).ToString(),
  //                                  CreatedOn = result["CreatedOn"].ToString(),
  //                                  Remarks = result["Remarks"].ToString(),
                                    
  //                              };
  //                              data.Add(item);
  //                          }
  //                          result.Close();
  //                          return Request.CreateResponse(HttpStatusCode.OK, new { Status = true, Message = "Data Fetched Successfully", Data = data });
  //                      }

  //                      return Request.CreateResponse(HttpStatusCode.OK, new { Status = true, Message = "Nothing to Show" });
  //                      //string huCarterName = result.ToString();
  //                  }
  //                  else
  //                  {
  //                      return Request.CreateResponse(HttpStatusCode.NotFound, "Nothing to Show.");
  //                  }
  //              }
  //              catch (Exception ex)
  //              {
  //                  return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
  //              }
  //          }
  //      }

        

    }
}
