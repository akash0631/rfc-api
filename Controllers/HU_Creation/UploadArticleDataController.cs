using DocumentFormat.OpenXml.Office2016.Excel;
using Newtonsoft.Json;
using OfficeOpenXml;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_Application_MVC.Models;
using Vendor_SRM_Routing_Application.Models.HU_Creation;

namespace Vendor_SRM_Routing_Application.Controllers.HU_Creation
{
    public class UploadArticleDataController : ApiController
    {
        [HttpPost]
        public async Task<HttpResponseMessage> POST()
        {

            try
            {
                var provider = new MultipartFormDataStreamProvider(Path.GetTempPath());

                // Read the form data
                await Request.Content.ReadAsMultipartAsync(provider);

                // Access form fields
                var vCode = provider.FormData["vCode"];
                var poNumber = provider.FormData["poNumber"];
                var articleData = provider.FormData["articleData"];
                var articleDataObj = JsonConvert.DeserializeObject<List<AddArticle>>(articleData);
                List<string> articleNumbers = articleDataObj.Select(article => article.ARTICLENUMBER.ToString()).ToList();
                //List<string> designs = articleDataObj.Select(article => article.DESIGN.ToString()).ToList();
                //List<string> eanNos = articleDataObj.Select(article => article.EAN11.ToString()).ToList();
                //List<string> maxQties = articleDataObj
                // .Select(article => decimal.TryParse(article.QTY.ToString(), out var qty) ? ((int)qty).ToString() : "0")
                // .ToList();

                string connectionString = ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;
                //string commandText = "";

                var file = provider.FileData.FirstOrDefault();
                if (file != null)
                {
                    var fileInfo = new FileInfo(file.LocalFileName);
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using (var package = new ExcelPackage(fileInfo))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        var rowCount = worksheet.Dimension.End.Row;
                        //make Hu Number 

                        Dictionary<string, string> tempHuToGeneratedHu = new Dictionary<string, string>();
                        //saving in database
                        for (int row = 2; row <= rowCount; row++) // Skip the header row
                        {
                            // Temporary HU Number (group)
                            var tempHuNumber = worksheet.Cells[row, 1].Text;
                            //Article No
                            var valueInSecondColumn = worksheet.Cells[row, 2].Text;
                            // Design is no longer used
                            //Quantity
                            var valueInFourthColumn = worksheet.Cells[row, 3].Text;
                            //EanNO
                            //var valueInFifthColumn = worksheet.Cells[row, 5].Text; 
                            // Max Qty
                            //var valueInSixthColumn = worksheet.Cells[row, 6].Text;

                            if (!articleNumbers.Contains(valueInSecondColumn))
                            {
                                throw new Exception($"Invalid Article Number in row {row}: {valueInSecondColumn}");
                            }
                            // Design validation removed - not reading from Excel anymore
                            //else if (!designs.Contains(valueInThirdColumn))
                            //{
                            //    throw new Exception($"Invalid Design in row {row}: {valueInThirdColumn}");
                            //}
                            //else if (!eanNos.Contains(valueInFifthColumn))
                            //{
                            //    throw new Exception($"Invalid EAN number in row {row}: {valueInFifthColumn}");
                            //}
                            //else if (!maxQties.Contains(valueInSixthColumn))
                            //{
                            //    throw new Exception($"Invalid Max_Qty in row {row}: {valueInSixthColumn}");
                            //}

                            string huCarterNo;
                            if (tempHuToGeneratedHu.ContainsKey(tempHuNumber))
                            {
                                huCarterNo = tempHuToGeneratedHu[tempHuNumber]; // Use the already generated HU number
                            }
                            else
                            {
                                // Generate a new HU number for this group
                                string query = @"
                    DECLARE @LastHuCarterName NVARCHAR(MAX);
                    Declare @index  bigint;
                    DECLARE @NewHuCarterName NVARCHAR(MAX);
                    SEt @index = IDENT_CURRENT('tblHuEntries');

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
                                            huCarterNo = result.ToString();
                                            tempHuToGeneratedHu[tempHuNumber] = huCarterNo; // Store the generated HU number
                                        }
                                        else
                                        {
                                            throw new Exception("Something went wrong, unable to create huCarter No. Contact your administration");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        throw new Exception(ex.Message);
                                    }
                                }
                            }
                            // process request (Design column removed)
                            string query1 = $"INSERT INTO [dbo].[tblHuCarterNameEntries]([HuCarterName],[Status],[HuNumber],[PONumber],[ArticleNumber],[Scan_Qty],[VendorCode],TempHuNumber)" +
                            $"VALUES('{huCarterNo}',1,'{huCarterNo}','{poNumber}','{valueInSecondColumn}','{valueInFourthColumn}','{vCode}','{tempHuNumber}');";
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
                            RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                            // Get RfcTable from SAP
                            RfcRepository rfcrep = dest.Repository;
                            IRfcFunction myfun = null;
                            myfun = rfcrep.CreateFunction("ZVND_HU_PUSH_API_POST"); //RfcFunctionName
                            IRfcStructure E_Data = myfun.GetStructure("EX_DATA");
                            E_Data.SetValue("HU_NO", huCarterNo.ToUpper());
                            E_Data.SetValue("PO_NO", poNumber);
                            E_Data.SetValue("ARTICLE_NO", valueInSecondColumn);
                            E_Data.SetValue("DESIGN", ""); // Empty string instead of reading from Excel
                            E_Data.SetValue("QUANTITY", valueInFourthColumn);
                            E_Data.SetValue("VENDOR_CODE", vCode);
                            E_Data.SetValue("EAN", "83615163");
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
                                //PO_Detail.Status = false;
                                //PO_Detail.Message = "" + SAP_Message + "";
                                return Request.CreateResponse(HttpStatusCode.BadRequest, new
                                {
                                    Status = false,
                                    Message = "" + SAP_Message + ""
                                });
                            }


                            //PO_Detail.Status = true;
                            //PO_Detail.Message = "" + SAP_Message + "";
                            string query2 = $"Update [tblHuEntries] Set Status =1 where [HuCarterName] = {huCarterNo}";

                            using (var connection = new SqlConnection(connectionString))
                            {
                                var command = new SqlCommand(query2, connection);
                                if (connection.State == ConnectionState.Closed)
                                    await connection.OpenAsync();
                                var result1 = await command.ExecuteNonQueryAsync();
                                if (result1 < 1)
                                {
                                    throw new Exception(" Unable to Update Hu Status at the moment.");
                                }

                            }
                        }


                    }
                }
                else
                {
                    throw new Exception("Unable to get the required Excel file,something went wrong. Contact your administrator");
                }


                using (var connection = new SqlConnection(connectionString))
                {
                    //using (var )
                }
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = true,
                    Message = "Submitted"
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
        }
        //public async Task<HttpResponseMessage> POST()
        //{

        //    try {
        //        var provider = new MultipartFormDataStreamProvider(Path.GetTempPath());

        //        // Read the form data
        //        await Request.Content.ReadAsMultipartAsync(provider);

        //        // Access form fields
        //        var vCode = provider.FormData["vCode"];
        //        var poNumber = provider.FormData["poNumber"];
        //        var articleData = provider.FormData["articleData"];
        //        var articleDataObj = JsonConvert.DeserializeObject<List<AddArticle>>(articleData);
        //        List<string> articleNumbers = articleDataObj.Select(article => article.ARTICLENUMBER.ToString()).ToList();
        //        List<string> designs = articleDataObj.Select(article => article.DESIGN.ToString()).ToList();
        //        //List<string> eanNos = articleDataObj.Select(article => article.EAN11.ToString()).ToList();
        //        //List<string> maxQties = articleDataObj
        //        // .Select(article => decimal.TryParse(article.QTY.ToString(), out var qty) ? ((int)qty).ToString() : "0")
        //        // .ToList();

        //        string connectionString = ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;
        //        //string commandText = "";

        //        var file = provider.FileData.FirstOrDefault();
        //        if (file != null)
        //        {
        //            var fileInfo = new FileInfo(file.LocalFileName);
        //            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        //            using (var package = new ExcelPackage(fileInfo))
        //            {
        //                var worksheet = package.Workbook.Worksheets[0];
        //                var rowCount = worksheet.Dimension.End.Row;
        //                //make Hu Number 

        //                Dictionary<string, string> tempHuToGeneratedHu = new Dictionary<string, string>();
        //                //saving in database
        //                for (int row = 2; row <= rowCount; row++) // Skip the header row
        //                {
        //                    // Temporary HU Number (group)
        //                    var tempHuNumber = worksheet.Cells[row, 1].Text;
        //                    //Article No
        //                    var valueInSecondColumn = worksheet.Cells[row, 2].Text;
        //                    //Design
        //                    var valueInThirdColumn = worksheet.Cells[row, 3].Text; 
        //                    //Quantity
        //                    var valueInFourthColumn = worksheet.Cells[row, 4].Text;
        //                    //EanNO
        //                    //var valueInFifthColumn = worksheet.Cells[row, 5].Text; 
        //                    // Max Qty
        //                    //var valueInSixthColumn = worksheet.Cells[row, 6].Text;

        //                    if (!articleNumbers.Contains(valueInSecondColumn))
        //                    {
        //                        throw new Exception($"Invalid Article Number in row {row}: {valueInSecondColumn}");
        //                    }
        //                    else if (!designs.Contains(valueInThirdColumn))
        //                    {
        //                        throw new Exception($"Invalid Design in row {row}: {valueInThirdColumn}");
        //                    }
        //                    //else if (!eanNos.Contains(valueInFifthColumn))
        //                    //{
        //                    //    throw new Exception($"Invalid EAN number in row {row}: {valueInFifthColumn}");
        //                    //}
        //                    //else if (!maxQties.Contains(valueInSixthColumn))
        //                    //{
        //                    //    throw new Exception($"Invalid Max_Qty in row {row}: {valueInSixthColumn}");
        //                    //}

        //                    string huCarterNo;
        //                    if (tempHuToGeneratedHu.ContainsKey(tempHuNumber))
        //                    {
        //                        huCarterNo = tempHuToGeneratedHu[tempHuNumber]; // Use the already generated HU number
        //                    }
        //                    else
        //                    {
        //                        // Generate a new HU number for this group
        //                        string query = @"
        //            DECLARE @LastHuCarterName NVARCHAR(MAX);
        //            Declare @index  bigint;
        //            DECLARE @NewHuCarterName NVARCHAR(MAX);
        //            SEt @index = IDENT_CURRENT('tblHuEntries');

        //            Select @LastHuCarterName = HuCarterName from tblHuEntries where UniqueId = @index
        //            SET @NewHuCarterName = CAST(CAST(@LastHuCarterName AS decimal(38,0)) + 1 AS NVARCHAR(MAX));

        //            DECLARE @InsertedHuCarterNameTable TABLE (HuCarterName NVARCHAR(MAX));

        //            INSERT INTO tblHuEntries (HuCarterName, [Status])
        //            OUTPUT INSERTED.HuCarterName INTO @InsertedHuCarterNameTable
        //            VALUES (@NewHuCarterName, 0);

        //            SELECT HuCarterName FROM @InsertedHuCarterNameTable;
        //        ";

        //                        using (SqlConnection connection = new SqlConnection(connectionString))
        //                        {
        //                            SqlCommand command = new SqlCommand(query, connection);
        //                            try
        //                            {
        //                                await connection.OpenAsync();
        //                                object result = await command.ExecuteScalarAsync();

        //                                if (result != null)
        //                                {
        //                                    huCarterNo = result.ToString();
        //                                    tempHuToGeneratedHu[tempHuNumber] = huCarterNo; // Store the generated HU number
        //                                }
        //                                else
        //                                {
        //                                    throw new Exception("Something went wrong, unable to create huCarter No. Contact your administration");
        //                                }
        //                            }
        //                            catch (Exception ex)
        //                            {
        //                                throw new Exception(ex.Message);
        //                            }
        //                        }
        //                    }
        //                    //process request
        //                    //string query1 = $"INSERT INTO [dbo].[tblHuCarterNameEntries]([HuCarterName],[Status],[HuNumber],[PONumber],[ArticleNumber],[Design],[Scan_Qty],[VendorCode],[EAN11],[PO_Qty],TempHuNumber)" +
        //                    //$"VALUES
        //                    string query1 = $"INSERT INTO [dbo].[tblHuCarterNameEntries]([HuCarterName],[Status],[HuNumber],[PONumber],[ArticleNumber],[Design],[Scan_Qty],[VendorCode],TempHuNumber)" +
        //                    $"VALUES('{huCarterNo}',1,'{huCarterNo}','{poNumber}','{valueInSecondColumn}','{valueInThirdColumn}','{valueInFourthColumn}','{vCode}','{tempHuNumber}');";
        //                    using (var connection = new SqlConnection(connectionString))
        //                    {
        //                        var command = new SqlCommand(query1, connection);
        //                        if (connection.State == ConnectionState.Closed)
        //                            await connection.OpenAsync();
        //                        var result1 = await command.ExecuteNonQueryAsync();
        //                        if (result1 < 1)
        //                        {
        //                            throw new Exception(" Unable to Add Hu at the moment.");
        //                        }

        //                    }
        //                    RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
        //                    RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
        //                    // Get RfcTable from SAP
        //                    RfcRepository rfcrep = dest.Repository;
        //                    IRfcFunction myfun = null;
        //                    myfun = rfcrep.CreateFunction("ZVND_HU_PUSH_API_POST"); //RfcFunctionName
        //                    IRfcStructure E_Data = myfun.GetStructure("EX_DATA");
        //                    E_Data.SetValue("HU_NO", huCarterNo.ToUpper());
        //                    E_Data.SetValue("PO_NO", poNumber);
        //                    E_Data.SetValue("ARTICLE_NO", valueInSecondColumn);
        //                    E_Data.SetValue("DESIGN", valueInThirdColumn);
        //                    E_Data.SetValue("QUANTITY", valueInFourthColumn);
        //                    E_Data.SetValue("VENDOR_CODE", vCode);
        //                    E_Data.SetValue("EAN", "83615163");
        //                    //E_Data.SetValue("CREATION_DATE", request.CREATION_DATE);
        //                    //E_Data.SetValue("CREATION_TIME", request.CREATION_TIME);
        //                    //E_Data.SetValue("CREATION_USER", request.CREATION_USER);
        //                    //E_Data.SetValue("MESSAGE", request.MESSAGE);
        //                    //E_Data.SetValue("STATUS", request.STATUS);




        //                    myfun.Invoke(dest);
        //                    IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
        //                    string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
        //                    string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
        //                    if (SAP_TYPE == "E")
        //                    {
        //                        //PO_Detail.Status = false;
        //                        //PO_Detail.Message = "" + SAP_Message + "";
        //                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
        //                        {
        //                            Status = false,
        //                            Message = "" + SAP_Message + ""
        //                        });
        //                    }


        //                    //PO_Detail.Status = true;
        //                    //PO_Detail.Message = "" + SAP_Message + "";
        //                    string query2 = $"Update [tblHuEntries] Set Status =1 where [HuCarterName] = {huCarterNo}";

        //                    using (var connection = new SqlConnection(connectionString))
        //                    {
        //                        var command = new SqlCommand(query2, connection);
        //                        if (connection.State == ConnectionState.Closed)
        //                            await connection.OpenAsync();
        //                        var result1 = await command.ExecuteNonQueryAsync();
        //                        if (result1 < 1)
        //                        {
        //                            throw new Exception(" Unable to Update Hu Status at the moment.");
        //                        }

        //                    }
        //                }


        //            }
        //        }
        //        else {
        //            throw new Exception("Unable to get the required Excel file,something went wrong. Contact your administrator");
        //        }


        //        using (var connection = new SqlConnection(connectionString))
        //        {
        //            //using (var )
        //        }
        //        return Request.CreateResponse(HttpStatusCode.OK, new { 
        //            Status = true,
        //            Message = "Submitted"
        //        });
        //    }
        //    catch (Exception ex) {
        //        return Request.CreateResponse(HttpStatusCode.InternalServerError, new { 
        //            Status = false,
        //            Message = ex.Message
        //        });
        //    }
        //}
    }
}
