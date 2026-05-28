using Newtonsoft.Json.Linq;
using SAP.Middleware.Connector;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Web.Http;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// Article Color Master Read API.
    /// RFC:    ZPBI_ART_COLOR (FMODE=R)
    /// Output: ET_ARTICLE_COLOR
    /// Endpoints:
    ///   GET  /api/article-colors?DateFrom=2026-05-26
    ///   POST /api/article-colors  body: { "DateFrom":"2026-05-26" }
    /// Cap: 31 days, default Limit 100, max 50000.
    /// </summary>
    public class ArticleColorsReadController : RfcReadBase
    {
        private const int MAX_WINDOW_DAYS = 31;
        private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new ConcurrentDictionary<string, CacheEntry>();

        [HttpGet, Route("api/article-colors")]
        public IHttpActionResult Get([FromUri] ReadRequest req, string env = "prod")
        {
            return RunRead(req, env, "ZPBI_ART_COLOR", MAX_WINDOW_DAYS, true, Cache, Fetch);
        }

        [HttpPost, Route("api/article-colors")]
        public IHttpActionResult Post([FromBody] ReadRequest req, string env = "prod")
        {
            return RunRead(req, env, "ZPBI_ART_COLOR", MAX_WINDOW_DAYS, true, Cache, Fetch);
        }

        private static CacheEntry Fetch(RfcConfigParameters rfcPar, ReadRequest req, string sapFrom, string sapTo)
        {
            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
            IRfcFunction fn = dest.Repository.CreateFunction("ZPBI_ART_COLOR");
            fn.SetValue("IM_DATE_FROM", sapFrom);
            fn.SetValue("IM_DATE_TO", sapTo);
            fn.Invoke(dest);

            IRfcTable rows = fn.GetTable("ET_ARTICLE_COLOR");
            var output = new List<object>(rows.RowCount);
            foreach (IRfcStructure row in rows) output.Add(ToDict(row));
            return new CacheEntry { Rows = output, SapMessage = "" };
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
