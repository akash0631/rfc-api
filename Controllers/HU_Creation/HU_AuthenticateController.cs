using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.UI.WebControls;
using Vendor_Application_MVC.Controllers;
using Vendor_Application_MVC.Models;

namespace VendorSRM_Application.Controllers.API
{
    public class HU_AuthenticateController : BaseController
    {
        // [HttpPost]
        // public void UploadFile()
        // {
        //     var file = HttpContext.Current.Request.Files.Count > 0 ?
        //HttpContext.Current.Request.Files[0] : null;
        // }

        public async Task<HttpResponseMessage> POST([FromBody] AuthenticateRequest request)
        {
            Authenticate Authenticate = new Authenticate();
            return await Task.Run(() =>
            {
                try
                {

                    if (request.Username != "" && request.Password != "" && request.Username != null && request.Password != null)
                    {
                        try
                        {
                            RfcConfigParameters rfcPar = BaseController.rfcConfigparameters();
                            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                            // Get RfcTable from SAP
                            RfcRepository rfcrep = dest.Repository;
                            IRfcFunction myfun = null;
                            myfun = rfcrep.CreateFunction("ZSRM_USER_VALIDATE"); //RfcFunctionName
                            myfun.SetValue("IM_USER", request.Username); //Import Parameter
                            myfun.SetValue("IM_PASSWORD", request.Password); //Import Parameter
                            myfun.Invoke(dest);


                            IRfcTable IrfTable = myfun.GetTable("ET_DATA");
                            IRfcStructure E_RETURN = myfun.GetStructure("ES_RETURN");
                            string SAP_TYPE = E_RETURN.GetValue("TYPE").ToString();
                            string SAP_Message = E_RETURN.GetValue("MESSAGE").ToString();
                            if (SAP_TYPE == "E")
                            {
                                Authenticate.Status = false;
                                Authenticate.Message = "" + SAP_Message + "";
                                return Request.CreateResponse(HttpStatusCode.BadRequest, Authenticate);
                            }
                            else
                            {

                                for (int i = 0; i < IrfTable.RowCount; ++i)
                                {
                                    AuthenticateResponse authenticateResponse = new AuthenticateResponse();
                                    authenticateResponse.Vendor_Code = IrfTable[i].GetString("LIFNR");
                                    authenticateResponse.Vendor_Name = IrfTable[i].GetString("NAME1");
                                    authenticateResponse.PO_Number = IrfTable[i].GetString("EBELN");

                                    Authenticate.Data.Add(authenticateResponse);
                                }
                                Authenticate.Data = Authenticate.Data.GroupBy(o => o.PO_Number).Select(o => o.FirstOrDefault()).ToList();//istinct(row).ToList();
                                Authenticate.Status = true;
                                Authenticate.Message = "" + SAP_Message + "";
                                return Request.CreateResponse(HttpStatusCode.OK, Authenticate);

                            }


                        }
                        catch (Exception ex)
                        {
                            Authenticate.Status = false;
                            Authenticate.Message = "" + ex.Message + "";
                            return Request.CreateResponse(HttpStatusCode.InternalServerError, Authenticate);
                        }
                    }
                    else
                    {
                        Authenticate.Status = false;
                        Authenticate.Message = "Username and Password is Mandatory.";
                        return Request.CreateResponse(HttpStatusCode.BadRequest, Authenticate);
                    }
                }
                catch (Exception ex)
                {
                    Authenticate.Status = false;
                    Authenticate.Message = "" + ex.Message + "";
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, Authenticate);
                }
            });


        }
       
    }

    public static class SapToDataExtensionClass
    {

        public static Dictionary<string, string> ToDictionary(this IRfcStructure stru)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();

            for (int i = 0; i < stru.Metadata.FieldCount; i++)
            {
                dict.Add(stru.Metadata[i].Name, stru.GetString(i));
            }

            return dict;
        }
        public static DataTable GetDataTable(this IRfcTable i_Table)
        {

            DataTable dt = new DataTable();

            dt.GetColumnsFromSapTable(i_Table);

            dt.FillRowsFromSapTable(i_Table);

            return dt;

        }


        public static void FillRowsFromSapTable(this DataTable i_DataTable, IRfcTable i_Table)
        {


            foreach (IRfcStructure tableRow in i_Table)
            {

                DataRow dr = i_DataTable.NewRow();

                dr.ItemArray = tableRow.Select(structField => structField.GetValue()).ToArray();

                i_DataTable.Rows.Add(dr);

            }

        }


        public static void GetColumnsFromSapTable(this DataTable i_DataTable, IRfcTable i_SapTable)
        {

            var DataColumnsArr = i_SapTable.Metadata.LineType.CreateStructure().ToList().Select

            (structField => new DataColumn(structField.Metadata.Name)).ToArray();

            i_DataTable.Columns.AddRange(DataColumnsArr);

        }

    }

}
