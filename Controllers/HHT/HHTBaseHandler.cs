using SAP.Middleware.Connector;
using System;
using System.Text;
using Vendor_Application_MVC.Controllers;

namespace Vendor_Application_MVC.Controllers.HHT
{
    /// <summary>
    /// Abstract base for all HHT opcode handlers.
    /// Protocol: Android sends "opcode#p1#p2#..." (# delimited), table rows use comma delimiter within a part.
    /// Response: "S#message" or "E#message" or "S#data!eandata" depending on opcode.
    /// </summary>
    public abstract class HHTBaseHandler
    {
        protected string[] Parts { get; private set; }
        protected string Opcode  => Parts.Length > 0 ? Parts[0] : "";

        /// <summary>Safely get a part by index, returns "" if out of range.</summary>
        protected string P(int index) => (Parts != null && index < Parts.Length) ? Parts[index] : "";

        public void SetRequest(string rawBody)
        {
            Parts = (rawBody ?? "").Split('#');
        }

        public abstract string Execute();

        // ─── SAP connection helpers ───────────────────────────────────────────

        protected static RfcDestination GetProdDestination()
        {
            RfcConfigParameters p = new RfcConfigParameters();
            p.Add(RfcConfigParameters.Name,          "HHT_PROD");
            p.Add(RfcConfigParameters.AppServerHost,  "192.168.144.170");
            p.Add(RfcConfigParameters.Client,         "600");
            p.Add(RfcConfigParameters.SystemNumber,   "02");
            p.Add(RfcConfigParameters.SystemID,       "PRD");
            p.Add(RfcConfigParameters.User,           "PIUSER");
            p.Add(RfcConfigParameters.Password,       "vrl@123");
            p.Add(RfcConfigParameters.Language,       "EN");
            return RfcDestinationManager.GetDestination(p);
        }

        protected static RfcDestination GetQADestination()
        {
            RfcConfigParameters p = new RfcConfigParameters();
            p.Add(RfcConfigParameters.Name,          "HHT_QA");
            p.Add(RfcConfigParameters.AppServerHost,  "192.168.144.179");
            p.Add(RfcConfigParameters.Client,         "600");
            p.Add(RfcConfigParameters.SystemNumber,   "00");
            p.Add(RfcConfigParameters.SystemID,       "S4Q");
            p.Add(RfcConfigParameters.User,           "PIUSER");
            p.Add(RfcConfigParameters.Password,       "vrl@123");
            p.Add(RfcConfigParameters.Language,       "EN");
            return RfcDestinationManager.GetDestination(p);
        }

        // ─── Response builders ────────────────────────────────────────────────

        protected static string Ok(string message = "")          => "S#" + message;
        protected static string Err(string message = "")         => "E#" + message;
        protected static string TypeMsg(string type, string msg) => type + "#" + msg;

        protected static string ReturnFromStructure(IRfcStructure ret)
        {
            string type = ret.GetString("TYPE");
            string msg  = ret.GetString("MESSAGE");
            return type + "#" + msg;
        }

        protected static string OkOrErr(IRfcStructure ret, string successPayload = "")
        {
            string type = ret.GetString("TYPE");
            string msg  = ret.GetString("MESSAGE");
            if (type == "E") return "E#" + msg;
            return string.IsNullOrEmpty(successPayload) ? "S#" + msg : "S#" + msg + successPayload;
        }

        // ─── Table serialisers ────────────────────────────────────────────────

        /// <summary>Serialize a JCo table to # delimited rows, fields within each row also # delimited.</summary>
        protected static string SerializeTable(IRfcTable table, params string[] fields)
        {
            if (table == null || table.RowCount == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < table.RowCount; i++)
            {
                foreach (var f in fields)
                {
                    sb.Append(table[i].GetString(f));
                    sb.Append("#");
                }
            }
            return sb.ToString();
        }

        /// <summary>Serialize EAN_DATA table — standard across most store opcodes.</summary>
        protected static string SerializeEanData(IRfcTable ean)
        {
            return SerializeTable(ean, "MANDT", "MATNR", "EAN11", "UMREZ", "EANNR");
        }
    }
}
