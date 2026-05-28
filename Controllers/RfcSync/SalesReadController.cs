using Newtonsoft.Json.Linq;
using SAP.Middleware.Connector;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Web.Http;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// Sales Read API.
    /// RFC:    ZPBI_ART_SALES (FMODE=R)
    /// Source: VBRP+VBRK billing data
    /// Output: ET_SALES_DATA
    /// Endpoints:
    ///   GET  /api/sales?DateFrom=2026-05-26&Plant=DH24
    ///   POST /api/sales  body: { "DateFrom":"2026-05-26", ... }
    /// Cap: 7 days, default Limit 100, max 50000.
    /// </summary>
    public class SalesReadController : RfcReadBase
    {
        private const int MAX_WINDOW_DAYS = 7;
        private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new ConcurrentDictionary<string, CacheEntry>();

        [HttpGet, Route("api/sales")]
        public IHttpActionResult Get([FromUri] ReadRequest req, string env = "prod")
        {
            return RunRead(req, env, "ZPBI_ART_SALES", MAX_WINDOW_DAYS, true, Cache, Fetch);
        }

        [HttpPost, Route("api/sales")]
        public IHttpActionResult Post([FromBody] ReadRequest req, string env = "prod")
        {
            return RunRead(req, env, "ZPBI_ART_SALES", MAX_WINDOW_DAYS, true, Cache, Fetch);
        }

        private static CacheEntry Fetch(RfcConfigParameters rfcPar, ReadRequest req, string sapFrom, string sapTo)
        {
            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
            IRfcFunction fn = dest.Repository.CreateFunction("ZPBI_ART_SALES");
            fn.SetValue("IM_DATE_FROM", sapFrom);
            fn.SetValue("IM_DATE_TO", sapTo);
            if (!string.IsNullOrEmpty(req.Plant)) fn.SetValue("IM_WERKS", req.Plant.Trim());
            if (!string.IsNullOrEmpty(req.Article)) fn.SetValue("IM_MATNR", req.Article.Trim());
            fn.Invoke(dest);

            IRfcTable rows = fn.GetTable("ET_SALES_DATA");
            var output = new List<object>(rows.RowCount);
            foreach (IRfcStructure row in rows) output.Add(ToDict(row));

            string msg = null;
            try { msg = fn.GetStructure("EX_RETURN").GetString("MESSAGE"); } catch { }
            return new CacheEntry { Rows = output, SapMessage = msg ?? "" };
        }

        private static JObject ToDict(IRfcStructure row)
        {
            var o = new JObject();
            for (int j = 0; j < row.Metadata.FieldCount; j++)
            {
                string f = row.Metadata[j].Name;
                try { o[f] = row.GetString(f); } catch { o[f] = ""; }
            }
            return o;
        }
    }
}
