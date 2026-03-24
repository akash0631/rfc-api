using SAP.Middleware.Connector;

namespace Vendor_Application_MVC.Controllers.HHT.Handlers.Auth
{
    /// <summary>
    /// opcode: scnrec
    /// Input:  scnrec#USERNAME#PASSWORD
    /// RFC:    ZWM_USER_AUTHORITY_CHECK
    /// Output: "1#WERKS" (success) or "0" (fail)
    /// </summary>
    public class ScnrecHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest    = GetProdDestination();
                var fun     = dest.Repository.CreateFunction("ZWM_USER_AUTHORITY_CHECK");
                fun.SetValue("IM_USERID",   P(1));
                fun.SetValue("IM_PASSWORD", P(2));
                fun.Invoke(dest);

                var ret   = fun.GetStructure("EX_RETURN");
                var werks = fun.GetString("EX_WERKS");
                return ret.GetString("TYPE") != "E" ? "1#" + werks : "0";
            }
            catch (RfcCommunicationException ex) { return "0#SAP connection error: " + ex.Message; }
            catch (System.Exception ex)          { return "0#" + ex.Message; }
        }
    }
}
