using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Vendor_Application_MVC.Controllers
{
    public class BaseController : ApiController
    {

       
        public static RfcConfigParameters rfcConfigparameters()
        {
            RfcConfigParameters rfcPar = new RfcConfigParameters();
            rfcPar.Add(RfcConfigParameters.Name, "Connection Name"); //Connection Name              // TCODE: SM59 -> check RFC connection
            rfcPar.Add(RfcConfigParameters.AppServerHost, "192.168.144.174");//Target IP Address  // TCODE: SM59 -> check RFC connection
            rfcPar.Add(RfcConfigParameters.Client, "210"); //Client ID
            // system -> status
            rfcPar.Add(RfcConfigParameters.User, "POWERBI");//User Name
            rfcPar.Add(RfcConfigParameters.Password, "India@123456"); //User Password
            //rfcPar.Add(RfcConfigParameters.User, "SAP_PM1");//User Name
            //rfcPar.Add(RfcConfigParameters.Password, "Master@567"); //User Password
            //rfcPar.Add(RfcConfigParameters.SystemID, "01");
            rfcPar.Add(RfcConfigParameters.SystemNumber, "00");
            


            // TCODE: SM59 -> check RFC connection
            rfcPar.Add(RfcConfigParameters.Language, "EN"); // system -> status


            return rfcPar;
        }
        public static RfcConfigParameters rfcConfigparametersproduction()
        {
            //production
            RfcConfigParameters rfcPar = new RfcConfigParameters();
            rfcPar.Add(RfcConfigParameters.Name, "Connection Name"); //Connection Name              // TCODE: SM59 -> check RFC connection
            //rfcPar.Add(RfcConfigParameters.AppServerHost, "192.168.144.194");//Target IP Address  // TCODE: SM59 -> check RFC connection
            rfcPar.Add(RfcConfigParameters.AppServerHost, "192.168.144.170");
            // rfcPar.Add(RfcConfigParameters.Client, "210"); //Client ID
            rfcPar.Add(RfcConfigParameters.Client, "600");
            // system -> status
            rfcPar.Add(RfcConfigParameters.User, "POWERBI");//User Name
            rfcPar.Add(RfcConfigParameters.Password, "India@123456"); //User Password
            rfcPar.Add(RfcConfigParameters.SystemID, "PRD");
            rfcPar.Add(RfcConfigParameters.SystemNumber, "02");
            


            // TCODE: SM59 -> check RFC connection
            rfcPar.Add(RfcConfigParameters.Language, "EN"); // system -> status





            return rfcPar;
        }

        public static RfcConfigParameters rfcConfigparametersquality()
        {
            //production
            RfcConfigParameters rfcPar = new RfcConfigParameters();
            rfcPar.Add(RfcConfigParameters.Name, "Connection Name"); //Connection Name              // TCODE: SM59 -> check RFC connection
            //rfcPar.Add(RfcConfigParameters.AppServerHost, "192.168.144.194");//Target IP Address  // TCODE: SM59 -> check RFC connection
            rfcPar.Add(RfcConfigParameters.AppServerHost, "192.168.144.179");
            // rfcPar.Add(RfcConfigParameters.Client, "210"); //Client ID
            rfcPar.Add(RfcConfigParameters.Client, "600");
            // system -> status
            rfcPar.Add(RfcConfigParameters.User, "POWERBI");//User Name
            rfcPar.Add(RfcConfigParameters.Password, "India@123456"); //User Password
            rfcPar.Add(RfcConfigParameters.SystemID, "S4Q");
            rfcPar.Add(RfcConfigParameters.SystemNumber, "00");



            // TCODE: SM59 -> check RFC connection
            rfcPar.Add(RfcConfigParameters.Language, "EN"); // system -> status





            return rfcPar;
        }
        [NonAction]
        public static DataTable ExecuteStoredProcedure(string storedProcedureName, SqlParameter[] parameters)
        {
            DataTable dataTable = new DataTable();

            // Set your connection string here
            string connectionString = ConfigurationManager.ConnectionStrings["HuCreation"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(storedProcedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddRange(parameters);

                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }
                }
            }

            return dataTable;
        }

    }
}