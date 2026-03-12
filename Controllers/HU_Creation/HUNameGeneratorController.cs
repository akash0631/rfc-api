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
using VendorSRM_Application.Models;

namespace VendorSRM_Application.Controllers.API
{
    public class HUNameGeneratorController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post() {
            string connectionString = ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;
            string query = @"
               DECLARE @LastHuCarterName NVARCHAR(MAX);
                 Declare @index  bigint;
                DECLARE @NewHuCarterName NVARCHAR(MAX);
                SEt @index =IDENT_CURRENT('tblHuEntries');

                Select @LastHuCarterName = HuCarterName from tblHuEntries where UniqueId = @index
                SET @NewHuCarterName = CAST(CAST(@LastHuCarterName AS decimal(38,0)) + 1 AS NVARCHAR(MAX));

                DECLARE @InsertedHuCarterNameTable TABLE (HuCarterName NVARCHAR(MAX));

                INSERT INTO tblHuEntries (HuCarterName, [Status])
                OUTPUT INSERTED.HuCarterName INTO @InsertedHuCarterNameTable
                VALUES (@NewHuCarterName, 0);

                SELECT HuCarterName FROM @InsertedHuCarterNameTable;

";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                try
                {
                    await connection.OpenAsync();
                    object result = await command.ExecuteScalarAsync();

                    if (result != null)
                    {
                        string huCarterName = result.ToString();
                        return Request.CreateResponse(HttpStatusCode.OK,new { Status = true,Message="HU Name Generated",CarterName = huCarterName});
                    }
                    else
                    {
                        return Request.CreateResponse(HttpStatusCode.NotFound, "No entries found.");
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
