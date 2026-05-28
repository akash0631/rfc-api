using Newtonsoft.Json.Linq;
using SAP.Middleware.Connector;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Web.Http;

namespace Vendor_SRM_Routing_Application.Controllers.RfcSync
{
    /// <summary>
    /// Plant + Storage Location Master Read API.
    /// RFC:    ZPBI_PLANT_LOCATION (FMODE=R, no IMPORT params — single-shot full master)
    /// Output: ET_PLANT (T001W) + ET_LOCATION (T001L)
    ///
    /// Returns plants in Rows; storage-location rows tagged via __Type field
    /// so downstream Snowflake landing can split into 2 BRONZE tables.
    ///
    /// Endpoints:
    ///   GET  /api/plants
    ///   POST /api/plants
    /// No date param required. Default Limit 100, max 50000.
    /// </summary>
    public class PlantsReadController : RfcReadBase
    {
        private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new ConcurrentDictionary<string, CacheEntry>();

        [HttpGet, Route("api/plants")]
        public IHttpActionResult Get([FromUri] ReadRequest req, string env = "prod")
        {
            return RunRead(req, env, "ZPBI_PLANT_LOCATION", 1, false, Cache, Fetch);
        }

        [HttpPost, Route("api/plants")]
        public IHttpActionResult Post([FromBody] ReadRequest req, string env = "prod")
        {
            return RunRead(req, env, "ZPBI_PLANT_LOCATION", 1, false, Cache, Fetch);
        }

        private static CacheEntry Fetch(RfcConfigParameters rfcPar, ReadRequest req, string sapFrom, string sapTo)
        {
            RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
            IRfcFunction fn = dest.Repository.CreateFunction("ZPBI_PLANT_LOCATION");
            fn.Invoke(dest);

            IRfcTable plants = fn.GetTable("ET_PLANT");
            IRfcTable locs = fn.GetTable("ET_LOCATION");
            var output = new List<object>(plants.RowCount + locs.RowCount);
            foreach (IRfcStructure r in plants)
            {
                JObject o = ToDict(r);
                o["__Type"] = "PLANT";
                output.Add(o);
            }
            foreach (IRfcStructure r in locs)
            {
                JObject o = ToDict(r);
                o["__Type"] = "LOCATION";
                output.Add(o);
            }
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
