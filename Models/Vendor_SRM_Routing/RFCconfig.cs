using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace Vendor_SRM_Routing_Application.Models.Vendor_SRM_Routing
{
    public static class RFCconfing
    {
        public static RfcConfigParameters rfcConfigparameters()
        {
            string settingValue = ConfigurationManager.AppSettings["Name"];
            RfcConfigParameters rfcPar = null;
            try
            {
                rfcPar = new RfcConfigParameters();
            }
            catch { }

            // NCo caches destinations by NAME, and Web.config ships the placeholder
            // "Connection Name" - the same string every commented-out block in that file
            // carries. Every other builder in this solution sets an env-unique name with
            // the comment "env-unique name prevents NCo dest cache collision"; this one was
            // missed. Honour an operator-supplied name, but never the placeholder.
            string configuredName = ConfigurationManager.AppSettings["Name"];
            if (string.IsNullOrWhiteSpace(configuredName) ||
                configuredName.Trim().Equals("Connection Name", StringComparison.OrdinalIgnoreCase))
            {
                configuredName = "ConnectionWebConfig";
            }
            rfcPar.Add(RfcConfigParameters.Name, configuredName);

            // This destination set NO pool parameters, so it ran on NCo defaults. On
            // 2026-09-11 HHT users got:
            //   Unable to allocate client in pool [NAME=Connection Name USER=POWERBI
            //   CLIENT=600 LANG=EN ASHOST=192.168.144.170 SYSNR=02 SYSID=PRD]
            //   - peak connections limit 10 exceeded
            // That "peak connections limit" IS MAX_POOL_SIZE: in sapnco 3.0.18 the constants
            // PeakConnectionsLimit and MaxPoolSize both map to the key MAX_POOL_SIZE, so
            // there is no separate peak setting to raise - and adding both would throw on a
            // duplicate key. Sized like the secondary lanes in BaseController rather than the
            // PROD lane, because this one carries ASN fetches and not the whole estate.
            rfcPar.Add(RfcConfigParameters.PoolSize, "10");           // warm idle connections kept open
            rfcPar.Add(RfcConfigParameters.MaxPoolSize, "25");        // max concurrent connections = the "peak connections limit"
            rfcPar.Add(RfcConfigParameters.MaxPoolWaitTime, "30000"); // wait up to 30s for a free conn, then error
            rfcPar.Add(RfcConfigParameters.IdleTimeout, "600");       // release idle connections after 10 min

            rfcPar.Add(RfcConfigParameters.AppServerHost, ConfigurationManager.AppSettings["AppServerHost"]);
            rfcPar.Add(RfcConfigParameters.Client, ConfigurationManager.AppSettings["Client"]);

            rfcPar.Add(RfcConfigParameters.User, ConfigurationManager.AppSettings["User"]);
            rfcPar.Add(RfcConfigParameters.Password, ConfigurationManager.AppSettings["Password"]);

            rfcPar.Add(RfcConfigParameters.SystemNumber, ConfigurationManager.AppSettings["SystemNumber"]);



            rfcPar.Add(RfcConfigParameters.Language, ConfigurationManager.AppSettings["Language"]);


            return rfcPar;
        }
    }
}