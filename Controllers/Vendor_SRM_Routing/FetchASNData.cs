using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Configuration;
using System.Web;
using System.Web.Http;
using Vendor_Application_MVC.Models;
using System.Threading.Tasks;
using System.Configuration;
using Newtonsoft.Json.Linq;
using DocumentFormat.OpenXml.Office2016.Excel;
using System.Web.Http.Cors;
using System.IO;
using System.Threading.Tasks;
using Sampling.Controllers.API;
using System.Data.SqlClient;
using Vendor_SRM_Routing_Application.Models.Vendor_SRM_Routing;
using System.Data;
namespace Vendor_Application_MVC.Controllers
{
    public class FetchASNDataController : BaseController
    {
        private float ToFloat(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            value = value.Replace(",", ""); // remove commas

            return float.TryParse(value, out float result) ? result : 0;
        }
        [EnableCors(origins: "*", headers: "*", methods: "*")]
        [HttpPost]
        public async Task<HttpResponseMessage> Post(FetchASNDataRequest request)
        {
            try
            {
                string consString = ConfigurationManager.ConnectionStrings["ASN"].ConnectionString;

                // 1️⃣ SAP CALL
                RfcConfigParameters rfcPar = RFCconfing.rfcConfigparameters();
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                RfcRepository repo = dest.Repository;

                IRfcFunction func = repo.CreateFunction("ZQC_PO_DATA_NEW");
                func.SetValue("IM_LIFNR", request.VendorCode);
                func.Invoke(dest);

                IRfcTable sapTable = func.GetTable("ET_DATA");

                // 2️⃣ Prepare DataTable for Bulk Insert
                DataTable dt = new DataTable();

                // ✅ EXACT SQL COLUMN NAMES
                dt.Columns.Add("Purchasing Document", typeof(string));
                dt.Columns.Add("Delivery date", typeof(DateTime));
                dt.Columns.Add("Created on", typeof(DateTime));
                dt.Columns.Add("New_Maj_Cat", typeof(string));
                dt.Columns.Add("Maj_Cat_cd", typeof(string));
                dt.Columns.Add("Vendor", typeof(string));
                dt.Columns.Add("Name 1", typeof(string));
                dt.Columns.Add("Material", typeof(string));
                dt.Columns.Add("Material Description", typeof(string));
                dt.Columns.Add("Net Order Price", typeof(float));
                dt.Columns.Add("Order Quantity", typeof(float));
                dt.Columns.Add("Process Description", typeof(string));
                dt.Columns.Add("Plant", typeof(string));
                dt.Columns.Add("DesignNo", typeof(string));
                dt.Columns.Add("Color", typeof(string));
                dt.Columns.Add("PR", typeof(string));
                dt.Columns.Add("Store", typeof(string));
                dt.Columns.Add("Season", typeof(string));
                dt.Columns.Add("City", typeof(string));
                dt.Columns.Add("State", typeof(string));
                dt.Columns.Add("PoAmount", typeof(float));
                dt.Columns.Add("Phone", typeof(string));
                dt.Columns.Add("Status", typeof(bool));


                foreach (IRfcStructure row in sapTable)
                {
                    dt.Rows.Add(
                        row.GetString("EBELN"), // Purchasing Document

                        DateTime.TryParse(row.GetString("EINDT"), out var d) ? d : (object)DBNull.Value,

                        DateTime.Now,

                        row.GetString("MAJ_CAT"),
                        row.GetString("MATKL"),
                        row.GetString("LIFNR"),
                        row.GetString("NAME1"),
                        row.GetString("MATNR"),
                        row.GetString("MAKTX"),

                        ToFloat(row.GetString("NETPR")),  // ✅ FIXED
                        ToFloat(row.GetString("MENGE")),  // ✅ FIXED

                        "QC DONE",

                        row.GetString("WERKS"),
                        row.GetString("ZEINR"),
                        row.GetString("COLOR"),
                        row.GetString("BANFN"),
                        row.GetString("WERKS"),
                        row.GetString("SEASON"),
                        row.GetString("ORT01"),
                        row.GetString("BEZEI"),

                        ToFloat(row.GetString("NETWR")),  // ✅ FIXED

                        row.GetString("TELF1"),
                        false
                    );
                }

                // 3️⃣ Bulk Insert + SP in ONE SHORT TRANSACTION
                using (SqlConnection con = new SqlConnection(consString))
                {
                    await con.OpenAsync();

                    using (SqlTransaction tran = con.BeginTransaction())
                    {
                        // BULK COPY
                        using (SqlBulkCopy bulk = new SqlBulkCopy(con, SqlBulkCopyOptions.TableLock, tran))
                        {
                            bulk.DestinationTableName = "dbo.DataMaster";
                            bulk.BatchSize = 1000;
                            bulk.BulkCopyTimeout = 0;

                            // ✅ COLUMN MAPPING (MUST DO)
                            foreach (DataColumn col in dt.Columns)
                            {
                                bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                            }

                            await bulk.WriteToServerAsync(dt);
                        }

                        // STORED PROCEDURE
                        using (SqlCommand cmd = new SqlCommand("FetchASNDataVendorWise", con, tran))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.CommandTimeout = 0;
                            cmd.Parameters.Add("@VID", SqlDbType.VarChar, 20).Value = request.VendorCode;
                            await cmd.ExecuteNonQueryAsync();
                        }

                        tran.Commit();
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Status = true,
                    Message = "Fetched Successfully"
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }


        //[EnableCors(origins: "*", headers: "*", methods: "*")]
        //[HttpPost]
        //public async Task<HttpResponseMessage> Post(FetchASNDataRequest request)
        //{

        //        try
        //        {
        //            string consString = ConfigurationManager.ConnectionStrings["ASN"].ConnectionString;
        //            RfcConfigParameters rfcPar = null;

        //            rfcPar = RFCconfing.rfcConfigparameters();
        //            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
        //            // Get RfcTable from SAP
        //            RfcRepository rfcrep = dest.Repository;
        //            IRfcFunction myfun = null;
        //            myfun = rfcrep.CreateFunction("ZQC_PO_DATA_NEW"); //RfcFunctionName
        //                                                              //IRfcStructure E_Data = myfun.GetStructure("EX_DATA");
        //                                                              //myfun.SetValue("LV_ART", request.Article_Number);
        //                                                              //E_Data.SetValue("HU_NO", request.HU_NO.ToUpper());
        //            myfun.SetValue("IM_LIFNR", request.VendorCode);

        //            ArticleIdentifer Authenticate = new ArticleIdentifer();

        //            myfun.Invoke(dest);
        //            IRfcTable IrfTable = myfun.GetTable("ET_DATA");


        //            ArticleResponse authenticateResponse = new ArticleResponse();

        //            for (int i = 0; i < IrfTable.RowCount; ++i)
        //            {
        //                authenticateResponse.Major_Category = IrfTable[i].GetString("MAJ_CAT");
        //                authenticateResponse.Major_Category_Code = IrfTable[i].GetString("MATKL");
        //                authenticateResponse.PO_Number = IrfTable[i].GetString("EBELN");
        //                authenticateResponse.Document_number = IrfTable[i].GetString("ZEINR");
        //                authenticateResponse.Material_Number = IrfTable[i].GetString("MATNR");
        //                authenticateResponse.Material_Description = IrfTable[i].GetString("MAKTX");
        //                authenticateResponse.Vendor_Code = IrfTable[i].GetString("LIFNR");
        //                authenticateResponse.NAME1 = IrfTable[i].GetString("NAME1");
        //                authenticateResponse.Net_Price = IrfTable[i].GetString("NETPR");
        //                authenticateResponse.Purchase_Order_Quantity = IrfTable[i].GetString("MENGE");
        //                authenticateResponse.Variants = IrfTable[i].GetString("COLOR");
        //                authenticateResponse.Purchase_Requisition_Number = IrfTable[i].GetString("BANFN");
        //                authenticateResponse.Plant = IrfTable[i].GetString("WERKS");
        //                authenticateResponse.status_of_PO = IrfTable[i].GetString("STATU");
        //                authenticateResponse.deliverydate = IrfTable[i].GetString("EINDT");

        //                authenticateResponse.Season = IrfTable[i].GetString("SEASON");
        //                authenticateResponse.City = IrfTable[i].GetString("ORT01");
        //                authenticateResponse.State = IrfTable[i].GetString("BEZEI");
        //                authenticateResponse.PoAmount = IrfTable[i].GetString("NETWR");
        //                authenticateResponse.TelF1 = IrfTable[i].GetString("TELF1");

        //            //Del Date
        //            //del date

        //            //                        using (SqlConnection con = new SqlConnection(consString))
        //            //                        {
        //            //                            string query = $@"
        //            //                            INSERT INTO [dbo].[DataMaster]
        //            //                            ([Purchasing Document]
        //            //                            ,[Delivery date]
        //            //                            ,[Created on]
        //            //                            ,[New_Maj_Cat]
        //            //                            ,[Maj_Cat_cd]
        //            //                            ,[Vendor]
        //            //                            ,[NAME 1]
        //            //                            ,[Material]
        //            //                            ,[Material Description]
        //            //                            ,[Net Order Price]
        //            //                            ,[Order Quantity]
        //            //                            ,[Process Description]
        //            //                            ,[Plant]
        //            //                            ,DesignNo
        //            //                            ,Color
        //            //                            ,PR
        //            //                            ,Store
        //            //                            ,[Season]
        //            //                            ,[City]
        //            //                            ,[State]
        //            //                            ,PoAmount
        //            //                            ,Phone
        //            //                            ,[Status])
        //            //                            VALUES
        //            //           ('{authenticateResponse.PO_Number}','{authenticateResponse.deliverydate}',getdate(),'{authenticateResponse.Major_Category}','{authenticateResponse.Major_Category_Code}','{authenticateResponse.Vendor_Code}','{authenticateResponse.NAME1}','{authenticateResponse.Material_Number}','{authenticateResponse.Material_Description}',
        //            //'{authenticateResponse.Net_Price}','{authenticateResponse.Purchase_Order_Quantity}','QC DONE','{authenticateResponse.Plant}','{authenticateResponse.Document_number}','{authenticateResponse.Variants}','{authenticateResponse.Purchase_Requisition_Number}','{authenticateResponse.Plant}','{authenticateResponse.Season}','{authenticateResponse.City}','{authenticateResponse.State}','{authenticateResponse.PoAmount}','{authenticateResponse.TelF1}',0)
        //            //               ;";

        //            //                            using (SqlCommand SQLCmd = new SqlCommand(query, con))
        //            //                            {

        //            //                                con.Open();
        //            //                                SQLCmd.CommandTimeout = 0;

        //            //                                SQLCmd.ExecuteNonQuery();
        //            //                                con.Close();

        //            //                            }
        //            //                        }
        //            using (SqlConnection con = new SqlConnection(consString))
        //            {
        //                string query = @"
        //INSERT INTO [dbo].[DataMaster]
        //([Purchasing Document]
        //,[Delivery date]
        //,[Created on]
        //,[New_Maj_Cat]
        //,[Maj_Cat_cd]
        //,[Vendor]
        //,[NAME 1]
        //,[Material]
        //,[Material Description]
        //,[Net Order Price]
        //,[Order Quantity]
        //,[Process Description]
        //,[Plant]
        //,DesignNo
        //,Color
        //,PR
        //,Store
        //,[Season]
        //,[City]
        //,[State]
        //,PoAmount
        //,Phone
        //,[Status])
        //VALUES
        //(@PONumber, @DeliveryDate, GETDATE(), @MajorCat, @MajorCatCode, @VendorCode, @Name1,
        // @MaterialNo, @MaterialDesc, @NetPrice, @OrderQty, 'QC DONE', @Plant, @DesignNo,
        // @Color, @PR, @Store, @Season, @City, @State, @PoAmount, @Phone, 0);";

        //                using (SqlCommand SQLCmd = new SqlCommand(query, con))
        //                {
        //                    SQLCmd.Parameters.AddWithValue("@PONumber", authenticateResponse.PO_Number ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@DeliveryDate", authenticateResponse.deliverydate ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@MajorCat", authenticateResponse.Major_Category ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@MajorCatCode", authenticateResponse.Major_Category_Code ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@VendorCode", authenticateResponse.Vendor_Code ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@Name1", authenticateResponse.NAME1 ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@MaterialNo", authenticateResponse.Material_Number ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@MaterialDesc", authenticateResponse.Material_Description ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@NetPrice", authenticateResponse.Net_Price ?? "0");
        //                    SQLCmd.Parameters.AddWithValue("@OrderQty", authenticateResponse.Purchase_Order_Quantity ?? "0");
        //                    SQLCmd.Parameters.AddWithValue("@Plant", authenticateResponse.Plant ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@DesignNo", authenticateResponse.Document_number ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@Color", authenticateResponse.Variants ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@PR", authenticateResponse.Purchase_Requisition_Number ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@Store", authenticateResponse.Plant ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@Season", authenticateResponse.Season ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@City", authenticateResponse.City ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@State", authenticateResponse.State ?? "");
        //                    SQLCmd.Parameters.AddWithValue("@PoAmount", authenticateResponse.PoAmount ?? "0");
        //                    SQLCmd.Parameters.AddWithValue("@Phone", authenticateResponse.TelF1 ?? "");

        //                    con.Open();
        //                    SQLCmd.CommandTimeout = 0;
        //                    SQLCmd.ExecuteNonQuery();
        //                    con.Close();
        //                }
        //            }

        //        }
        //        using (SqlConnection con = new SqlConnection(consString))
        //            {
        //                //using (SqlCommand SQLCmd = new SqlCommand("ishuInsert", con))
        //                using (SqlCommand SQLCmd = new SqlCommand("FetchASNDataVendorWise", con))
        //                {

        //                    //await con.OpenAsync();
        //                    //SQLCmd.CommandTimeout = 0;
        //                    await con.OpenAsync();
        //                    SQLCmd.CommandType = CommandType.StoredProcedure;
        //                    SQLCmd.CommandTimeout = 0;

        //                    // Adding parameters (adjust parameter names and types as needed)
        //                    SQLCmd.Parameters.AddWithValue("@VID", request.VendorCode); // Example parameter
        //                    await SQLCmd.ExecuteNonQueryAsync();
        //                    con.Close();

        //                }
        //            }

        //            return Request.CreateResponse(HttpStatusCode.OK, new
        //            {
        //                Status = true,
        //                Message = "Fetched Successfully"
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            return Request.CreateResponse(HttpStatusCode.BadRequest, new
        //            {
        //                Status = false,
        //                Message = ex.Message
        //            });
        //        }

        //}

        public static bool IsRecognisedImageFile(string fileName)
        {
            string targetExtension = System.IO.Path.GetExtension(fileName);
            if (String.IsNullOrEmpty(targetExtension))
            {
                return false;
            }

            var recognisedImageExtensions = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().SelectMany(codec => codec.FilenameExtension.ToLowerInvariant().Split(';'));

            targetExtension = "*" + targetExtension.ToLowerInvariant();
            return recognisedImageExtensions.Contains(targetExtension);
        }


        public async Task WriteToFileAsync(Stream input, string filename)
        {
            // Open the file asynchronously with the FileStream constructor
            using (FileStream fileStream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            {
                // Copy the input stream to the file stream asynchronously
                await input.CopyToAsync(fileStream);
            }
        }
    }
    public class FetchASNDataRequest
    {
        public string VendorCode { get; set; }
    }
    public class Article_Request
    {
        public string Article_Number { get; set; } = String.Empty;

    }
    public class ArticleIdentifer
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public ArticleResponse Data;
        public ArticleIdentifer()
        {
            Data = new ArticleResponse();
        }
    }
    public class ArticleResponse
    {


        public string Major_Category { get; set; } = String.Empty;
        public string Major_Category_Code { get; set; } = String.Empty;
        public string PO_Number { get; set; } = String.Empty;
        public string Document_number { get; set; } = String.Empty;
        public string Material_Number { get; set; } = String.Empty;
        public string NAME1 { get; set; } = String.Empty;

        public string Material_Description { get; set; } = String.Empty;
        public string Vendor_Code { get; set; }
        public string Net_Price { get; set; } = String.Empty;
        public string Purchase_Order_Quantity { get; set; } = String.Empty;


        public string Variants { get; set; } = String.Empty;
        public string Purchase_Requisition_Number { get; set; } = String.Empty;
        public string Plant { get; set; } = String.Empty;
        public string status_of_PO { get; set; } = String.Empty;
        public string deliverydate { get; set; } = String.Empty;

        public string Season { get; set; } = String.Empty;
        public string City { get; set; } = String.Empty;
        public string State { get; set; } = String.Empty;
        public string PoAmount { get; set; } = String.Empty;
        public string TelF1 { get; set; } = String.Empty;
    }
}