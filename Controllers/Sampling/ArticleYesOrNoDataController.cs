using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_SRM_Routing_Application.Models.Sampling;

namespace Sampling.Controllers.Sampling
{
    public class ArticleYesOrNoDataController : ApiController
    {
       

        [HttpPost]

        public async Task<HttpResponseMessage> ArticleYesOrNoData()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;
            string query = $@"SELECT [ID]
      ,[ArticleNo]
      ,[Status]
      ,[CreatedBy]
      ,[CreatedOn]
      ,[UpdatedBy]
      ,[UpdatedOn]
      ,[IsActive]
      ,[IsDeleted]
      ,[Remarks]
  FROM [HUCreation].[dbo].[ArticleYesOrNo] with (nolock)
               ;";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                try
                {
                    await connection.OpenAsync();
                    var result = await command.ExecuteReaderAsync();

                    if (result != null)
                    {
                        var data = new List<object>();

                        if (result.HasRows)
                        {
                            while (result.Read())
                            {
                                var item = new
                                {
                                    ID = result["ID"].ToString(),
                                    ArticleNo = result["ArticleNo"].ToString(),
                                    Status = Convert.ToBoolean( result["Status"]).ToString(),
                                    CreatedOn = result["CreatedOn"].ToString(),
                                    Remarks = result["Remarks"].ToString(),
                                    
                                };
                                data.Add(item);
                            }
                            result.Close();
                            return Request.CreateResponse(HttpStatusCode.OK, new { Status = true, Message = "Data Fetched Successfully", Data = data });
                        }

                        return Request.CreateResponse(HttpStatusCode.OK, new { Status = true, Message = "Nothing to Show" });
                        //string huCarterName = result.ToString();
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.NotFound, "Nothing to Show.");
                    }
                }
                catch (Exception ex)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
                }
            }
        }

    }
}
