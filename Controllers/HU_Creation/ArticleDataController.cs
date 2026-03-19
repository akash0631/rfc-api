using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;

namespace VendorSRM_Application.Controllers.API
{
    
    public class ArticleDataController : BaseController
    {
        [HttpPost]
        
        public async Task<HttpResponseMessage> Post([FromBody] string poNumber)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;
            string query = $@"
                Select ArticleNumber,0 [Max_Qty],ISNULL(Sum(IsNull(Scan_Qty,0)),0) [Scanned],0 [New_Scanned] 
                from tblHuCarterNameEntries
                where PoNumber = {poNumber}
                Group by ArticleNumber
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
                                    ArticleNumber = result["ArticleNumber"].ToString(),
                                    EanNumber = "",
                                    Max_Qty = Convert.ToInt32(result["Max_Qty"]),
                                    Scanned = Convert.ToInt32(result["Scanned"]),
                                    New_Scanned = Convert.ToInt32(result["New_Scanned"])
                                };
                                data.Add(item);
                            }
                            result.Close();
                            return Request.CreateResponse(HttpStatusCode.OK, new { Status = true, Message = "Data Fetched Successfully", Data = data });
                        }

                            return Request.CreateResponse(HttpStatusCode.OK, new { Status = true, Message = "Nothing to Show"});
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
