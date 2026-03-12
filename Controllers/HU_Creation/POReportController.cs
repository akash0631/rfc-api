using Newtonsoft.Json;
using OfficeOpenXml;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.DynamicData;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using VendorSRM_Application.Models;

namespace VendorSRM_Application.Controllers.API
{
    public class POReportController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post(ReportRequest request)
        {
            try
            {
                //throw new Exception("Ishu");
                // Define the stored procedure parameters
                SqlParameter[] sqlParameters = new SqlParameter[]
                {
                    //new SqlParameter("@for", request.Type),
                    new SqlParameter("@vendorCode", request.VendorCode),
                    new SqlParameter("@poNumber", request.PONumber),
                };

                // Execute the stored procedure and get the result
                DataTable resultTable = ExecuteStoredProcedure("[GetPOReport]", sqlParameters);

                string json = JsonConvert.SerializeObject(resultTable, Formatting.Indented);
                // Convert the result to an Excel file
                return Request.CreateResponse(HttpStatusCode.OK, new { Status = true,Data = json});
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = false,Message = ex.Message });
            }
        }

        

        
    }

    public class AllPOReportController : BaseController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> Post(AllReportRequest request)
        {
            try
            {
                //throw new Exception("Ishu");
                // Define the stored procedure parameters
                SqlParameter[] sqlParameters = new SqlParameter[]
                {
                    //new SqlParameter("@for", request.Type),
                    new SqlParameter("@vendorCode", request.VendorCode),
                    //new SqlParameter("@poNumber", request.PONumber),
                };

                // Execute the stored procedure and get the result
                DataTable resultTable = ExecuteStoredProcedure("[GetAllPOReport]", sqlParameters);

                string json = JsonConvert.SerializeObject(resultTable, Formatting.Indented);
                // Convert the result to an Excel file
                return Request.CreateResponse(HttpStatusCode.OK, new { Status = true, Data = json });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { Status = false, Message = ex.Message });
            }
        }




    }
}
