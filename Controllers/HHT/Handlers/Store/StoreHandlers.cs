using SAP.Middleware.Connector;
using System.Text;

namespace Vendor_Application_MVC.Controllers.HHT.Handlers.Store
{
    // ═══════════════════════════════════════════════════════════════════════════
    // STOCK
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>opcode: getstorestock | Input: getstorestock#WERKS#EAN11[#LGORT]</summary>
    public class GetStoreStockHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_STOCK");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_EAN11", P(2));
                fun.SetValue("IM_LGORT", P(3).StartsWith("00") ? P(3) : "0001");
                fun.Invoke(dest);

                var ret  = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));

                var mard  = fun.GetStructure("EX_MARD");
                var sb    = new StringBuilder("S##");
                sb.Append(mard.GetString("MATNR") + "#");
                sb.Append(mard.GetString("LABST") + "#");
                sb.Append(mard.GetString("PSTAT") + "#");
                sb.Append(mard.GetString("PRCTL") + "!");
                sb.Append(SerializeEanData(fun.GetTable("ET_EAN_DATA")));
                return sb.ToString();
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: getstorestocktake | Input: getstorestocktake#WERKS#EAN11</summary>
    public class GetStoreStockTakeHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_STOCK");
                fun.SetValue("IM_WERKS",      P(1));
                fun.SetValue("IM_EAN11",      P(2));
                fun.SetValue("IM_STOCK_TAKE", "X");
                fun.SetValue("IM_LGORT",      "0001");
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));

                var mard = fun.GetStructure("EX_MARD");
                var sb   = new StringBuilder("S##");
                sb.Append(mard.GetString("MATNR") + "#");
                sb.Append(mard.GetString("LABST") + "#");
                sb.Append(mard.GetString("PSTAT") + "#");
                sb.Append(mard.GetString("PRCTL") + "!");
                sb.Append(SerializeEanData(fun.GetTable("ET_EAN_DATA")));
                return sb.ToString();
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_store_get_grtstock | Input: zwm_store_get_grtstock#WERKS#EAN11[#LGORT]</summary>
    public class GetGrtStockHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_GRTSTOCK");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_EAN11", P(2));
                fun.SetValue("IM_LGORT", P(3).StartsWith("00") ? P(3) : "0001");
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));

                var mard = fun.GetStructure("EX_MARD");
                var sb   = new StringBuilder("S##");
                sb.Append(mard.GetString("MATNR") + "#");
                sb.Append(mard.GetString("LABST") + "!");
                sb.Append(SerializeEanData(fun.GetTable("ET_EAN_DATA")));
                return sb.ToString();
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BINS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>opcode: storegetbin | Input: storegetbin#WERKS</summary>
    public class StoreGetBinHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_BIN");
                fun.SetValue("IM_WERKS", P(1));
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE")) + "!";

                var sb = new StringBuilder("S#");
                sb.Append(SerializeTable(fun.GetTable("ET_LAGP"), "LGPLA"));
                return sb + "!";
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: storegetbin_v2 | Input: storegetbin_v2#WERKS</summary>
    public class StoreGetBinV2Handler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_BIN_V2");
                fun.SetValue("IM_WERKS", P(1));
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE")) + "!";

                return "S#" + SerializeTable(fun.GetTable("ET_LAGP"), "LGPLA") + "!";
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: storegetbinstock | Input: storegetbinstock#WERKS#LGPLA[#LGORT]</summary>
    public class StoreGetBinStockHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_BIN_STOCK");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_LGPLA", P(2));
                fun.SetValue("IM_LGNUM", "SDC");
                fun.SetValue("IM_LGORT", P(3).StartsWith("00") ? P(3) : "0002");
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                var eanData = SerializeEanData(fun.GetTable("ET_EAN_DATA"));
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE")) + "!" + eanData;

                var sb = new StringBuilder("S#");
                sb.Append(SerializeTable(fun.GetTable("ET_STOCK"), "MATERIAL", "AVL_STOCK", "OPEN_STOCK", "SCAN_QTY", "BIN"));
                return sb + "!" + eanData;
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: getmatbinstock | Input: getmatbinstock#WERKS#LGPLA#LGORT#EAN11</summary>
    public class GetMatBinStockHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_MAT_BIN_STOCK");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_LGORT", "0002");
                fun.SetValue("IM_LGPLA", P(2));
                fun.SetValue("IM_LGNUM", "SDC");
                fun.SetValue("IM_EAN11", P(4));
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                var eanData = SerializeEanData(fun.GetTable("ET_EAN_DATA"));
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE")) + "!" + eanData;

                return "S#" + SerializeTable(fun.GetTable("ET_STOCK"), "MATERIAL", "AVL_STOCK", "OPEN_STOCK", "SCAN_QTY", "BIN") + "!" + eanData;
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: getmatbinstockbtob | Input: getmatbinstockbtob#WERKS#LGPLA#LGORT#EAN11</summary>
    public class GetMatBinStockBtoBHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_MAT_BIN_STOCK");
                fun.SetValue("IM_WERKS",    P(1));
                fun.SetValue("IM_LGORT",    "0002");
                fun.SetValue("IM_LGPLA",    P(2));
                fun.SetValue("IM_LGNUM",    "SDC");
                fun.SetValue("IM_EAN11",    P(4));
                fun.SetValue("IM_BIN_TO_BIN", "X");
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                var eanData = SerializeEanData(fun.GetTable("ET_EAN_DATA"));
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE")) + "!" + eanData;

                return "S#" + SerializeTable(fun.GetTable("ET_STOCK"), "MATERIAL", "AVL_STOCK", "OPEN_STOCK", "SCAN_QTY", "BIN") + "!" + eanData;
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: validatebin | Input: validatebin#LGNUM#LGPLA</summary>
    public class ValidateBinHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_GET_BIN_DETAILS");
                fun.SetValue("IM_LGNUM", P(1));
                fun.SetValue("IM_LGPLA", P(2));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_store_bin_list_validation | Input: zwm_store_bin_list_validation#WERKS#PICNR#LGORT#LGNUM</summary>
    public class StoreBinListValidationHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_BIN_LIST_VALIDATION");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_PICNR", P(2));
                fun.SetValue("IM_LGORT", P(3));
                fun.SetValue("IM_LGNUM", P(4));
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                var eanData = SerializeEanData(fun.GetTable("ET_EAN_DATA"));
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE")) + "!" + eanData;

                return "S#" + SerializeTable(fun.GetTable("ET_PICKLIST"), "MATERIAL", "AVL_STOCK", "SCAN_QTY", "BIN") + "!" + eanData;
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_store_binconhu_get_details | Input: zwm_store_binconhu_get_details#WERKS#LGNUM</summary>
    public class StoreBinConHuGetDetailsHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_BINCONHU_GET_DETAILS");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_LGNUM", P(2));
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));

                return "S#" + SerializeTable(fun.GetTable("ET_DATA"), "EXIDV", "LGPLA", "MATNR", "VEMNG");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_save_empty_bin | Input: zwm_save_empty_bin#LGNUM#USER#WERKS#csv(WAREHOUSE,CRATE,BIN_TYPE,BIN,SCAN_QTY,KEY)</summary>
    public class SaveEmptyBinHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_SAVE_EMPTY_BIN");
                fun.SetValue("IM_LGNUM", P(1));
                fun.SetValue("IM_USER",  P(2));
                fun.SetValue("IM_WERKS", P(3));
                var parts = P(4).Split(',');
                var tbl   = fun.GetTable("IT_DATA");
                for (int i = 0; i + 5 < parts.Length; i += 6)
                {
                    tbl.Append();
                    tbl.SetValue("WAREHOUSE", parts[i]);
                    tbl.SetValue("CRATE",     parts[i+1]);
                    tbl.SetValue("BIN_TYPE",  parts[i+2]);
                    tbl.SetValue("BIN",       parts[i+3]);
                    tbl.SetValue("SCAN_QTY",  parts[i+4]);
                    tbl.SetValue("KEY",       parts[i+5]);
                }
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_validate_empty_bin</summary>
    public class ValidateEmptyBinHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_VALIDATE_EMPTY_BIN");
                fun.SetValue("IM_LGNUM", P(1));
                fun.SetValue("IM_LGPLA", P(2));
                fun.SetValue("IM_WERKS", P(3));
                fun.SetValue("IM_CRATE", P(4));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_vali_crate_emptybin</summary>
    public class ValiCrateEmptyBinHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_VALI_CRATE_EMPTYBIN");
                fun.SetValue("IM_LGNUM", P(1));
                fun.SetValue("IM_WERKS", P(2));
                fun.SetValue("IM_CRATE", P(3));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SLOC
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>opcode: validatesloc | Input: validatesloc#WERKS#LGORT</summary>
    public class ValidateSlocHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_VALIDATE_SLOC");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_LGORT", P(2));
                fun.SetValue("IM_LGNUM", "SDC");
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: getsloc | Input: getsloc#WERKS</summary>
    public class GetSlocHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_SLOC");
                fun.SetValue("IM_WERKS",      P(1));
                fun.SetValue("IM_WM_MANAGED", "");
                fun.SetValue("IM_LGNUM",      "SDC");
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));

                return "S#" + SerializeTable(fun.GetTable("ET_SLOC_DST"), "LGORT", "LGOBE") + "!" +
                              SerializeTable(fun.GetTable("ET_SLOC_SRC"), "LGORT", "LGOBE");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PICKLIST / PICKING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>opcode: getstorepicklist | Input: getstorepicklist#WERKS#PICNR</summary>
    public class GetStorePicklistHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_PICKLIST");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_PICNR", P(2));
                fun.SetValue("IM_LGORT", "0002");
                fun.SetValue("IM_LGNUM", "SDC");
                fun.Invoke(dest);

                var ret     = fun.GetStructure("EX_RETURN");
                var eanData = SerializeEanData(fun.GetTable("ET_EAN_DATA"));
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE")) + "!" + eanData;

                return "S#" + SerializeTable(fun.GetTable("ET_PICKLIST"), "MATERIAL", "AVL_STOCK", "SCAN_QTY", "BIN") + "!" + eanData;
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: getstorepicklist_v2 — same RFC, V2 variant</summary>
    public class GetStorePicklistV2Handler : GetStorePicklistHandler { }

    /// <summary>opcode: savedirectpicking | Input: savedirectpicking#csv(WERKS,USER,MATERIAL,SCAN_QTY,BIN,...)</summary>
    public class SaveDirectPickingHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_DIRECT_PICKING");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS", parts[0]);
                fun.SetValue("IM_LGNUM", "SDC");
                fun.SetValue("IM_USER",  parts[1]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 2; i + 2 < parts.Length; i += 3)
                {
                    tbl.Append();
                    tbl.SetValue("MATERIAL", parts[i]);
                    tbl.SetValue("SCAN_QTY", parts[i+1]);
                    tbl.SetValue("BIN",      parts[i+2]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: savedirectpicking_v2 | adds PICNR to each row</summary>
    public class SaveDirectPickingV2Handler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_DIRECT_PICKING");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS", parts[0]);
                fun.SetValue("IM_LGNUM", "SDC");
                fun.SetValue("IM_USER",  parts[1]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 2; i + 3 < parts.Length; i += 4)
                {
                    tbl.Append();
                    tbl.SetValue("MATERIAL", parts[i]);
                    tbl.SetValue("SCAN_QTY", parts[i+1]);
                    tbl.SetValue("BIN",      parts[i+2]);
                    tbl.SetValue("PICNR",    parts[i+3]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_picklist_nos_disp | Input: zwm_picklist_nos_disp#WERKS#DATE</summary>
    public class PicklistNosDispHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_PICKLIST_NOS_DISP");
                fun.SetValue("LV_WERKS", P(1));
                fun.SetValue("LV_DATE",  P(2));
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));

                return "S#" + SerializeTable(fun.GetTable("LT_PICNR"), "PICNR", "PICNR_DATE");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zhhtusr_del_picking_rfc | Input: zhhtusr_del_picking_rfc#WERKS#DATE1#DATE2</summary>
    public class ZhhtusrDelPickingHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZHHTUSR_DEL_PICKING_RFC");
                fun.SetValue("IM_WERKS",    P(1));
                fun.SetValue("IM_DEL_DATE", P(2));
                fun.SetValue("IM_DEL_DATE2",P(3));
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));

                return "S#" + SerializeTable(fun.GetTable("ET_DATA"), "PICNR", "WERKS", "LGNUM");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GRC / PUTAWAY
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>opcode: savegrcputway | Input: savegrcputway#csv(EXIDV,WERKS,USER,MATNR,VEMNG,LGPLA,...)</summary>
    public class SaveGrcPutawayHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_GRC_PUTWAY");
                var parts = P(1).Split(',');
                fun.SetValue("IM_EXIDV",   parts[0]);
                fun.SetValue("IM_WERKS",   parts[1]);
                fun.SetValue("IM_LGNUM",   "SDC");
                fun.SetValue("IM_PARTIAL", "");
                fun.SetValue("IM_USER",    parts[2]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 3; i + 2 < parts.Length; i += 3)
                {
                    tbl.Append();
                    tbl.SetValue("MATNR", parts[i]);
                    tbl.SetValue("VEMNG", parts[i+1]);
                    tbl.SetValue("LGPLA", parts[i+2]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: savefloorputway | Input: savefloorputway#csv(WERKS,USER,MATNR,VEMNG,LGPLA,...)</summary>
    public class SaveFloorPutawayHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_FLOOR_PUTWAY");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS", parts[0]);
                fun.SetValue("IM_LGNUM", "SDC");
                fun.SetValue("IM_USER",  parts[1]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 2; i + 2 < parts.Length; i += 3)
                {
                    tbl.Append();
                    tbl.SetValue("MATNR", parts[i]);
                    tbl.SetValue("VEMNG", parts[i+1]);
                    tbl.SetValue("LGPLA", parts[i+2]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: savefloorputwaytake — same as floor putway but with STOCK_TAKE flag</summary>
    public class SaveFloorPutawayTakeHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_FLOOR_PUTWAY");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS",      parts[0]);
                fun.SetValue("IM_LGNUM",      "SDC");
                fun.SetValue("IM_STOCK_TAKE", "X");
                fun.SetValue("IM_USER",       parts[1]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 2; i + 2 < parts.Length; i += 3)
                {
                    tbl.Append();
                    tbl.SetValue("MATNR", parts[i]);
                    tbl.SetValue("VEMNG", parts[i+1]);
                    tbl.SetValue("LGPLA", parts[i+2]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_floor_puaway_new | Input: zwm_floor_puaway_new#EXIDV#LGPLA#WERKS</summary>
    public class FloorPutawayNewHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_FLOOR_PUAWAY_NEW");
                fun.SetValue("P_EXIDV", P(1));
                fun.SetValue("P_LGPLA", P(2));
                fun.SetValue("P_WERKS", P(3));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_store_floor_putway_hu | Input: zwm_store_floor_putway_hu#WERKS#USER#HU#LGPLA</summary>
    public class StoreFloorPutawayHuHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_FLOOR_PUTWAY_HU");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_LGNUM", "SDC");
                fun.SetValue("IM_USER",  P(2));
                fun.SetValue("IM_HU",    P(3));
                fun.SetValue("IM_LGPLA", P(4));
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_store_hu_putway_bin_con</summary>
    public class StoreHuPutawayBinConHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_HU_PUTWAY_BIN_CON");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS", parts[0]);
                fun.SetValue("IM_LGNUM", parts[1]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 2; i + 1 < parts.Length; i += 2)
                {
                    tbl.Append();
                    tbl.SetValue("BIN",   parts[i]);
                    tbl.SetValue("HU_NO", parts[i+1]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GRT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>opcode: savegrtmsa | Input: savegrtmsa#csv(WERKS,USER,PACK_MAT,MATERIAL,SCAN_QTY,BIN,...)</summary>
    public class SaveGrtFromMsaHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_GRT_FROM_MSA");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS",    parts[0]);
                fun.SetValue("IM_LGNUM",    "SDC");
                fun.SetValue("IM_USER",     parts[1]);
                fun.SetValue("IM_PACK_MAT", parts[2]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 3; i + 2 < parts.Length; i += 3)
                {
                    tbl.Append();
                    tbl.SetValue("MATERIAL", parts[i]);
                    tbl.SetValue("SCAN_QTY", parts[i+1]);
                    tbl.SetValue("BIN",      parts[i+2]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: savegrtfromdisplay</summary>
    public class SaveGrtFromDisplayHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_GRT_FROM_DISP_AREA");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS",     parts[0]);
                fun.SetValue("IM_LGORT_SRC", parts[1]);
                fun.SetValue("IM_LGORT_DEST",parts[2]);
                fun.SetValue("IM_LGNUM",     "0002");
                fun.SetValue("IM_USER",      parts[3]);
                fun.SetValue("IM_PACK_MAT",  parts[4]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 5; i + 1 < parts.Length; i += 2)
                {
                    tbl.Append();
                    tbl.SetValue("MATERIAL", parts[i]);
                    tbl.SetValue("SCAN_QTY", parts[i+1]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_grt_save</summary>
    public class GrtSaveHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_GRT_SAVE");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS", parts[0]);
                fun.SetValue("IM_LGORT", parts[1]);
                fun.SetValue("IM_USER",  parts[2]);
                fun.SetValue("IM_CRATE", parts[3]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 4; i + 18 < parts.Length; i += 19)
                {
                    tbl.Append();
                    tbl.SetValue("WM_NO",       parts[i]);
                    tbl.SetValue("MATERIAL",    parts[i+1]);
                    tbl.SetValue("PLANT",       parts[i+2]);
                    tbl.SetValue("STOR_LOC",    parts[i+3]);
                    tbl.SetValue("BATCH",       parts[i+4]);
                    tbl.SetValue("CRATE",       parts[i+5]);
                    tbl.SetValue("BIN",         parts[i+6]);
                    tbl.SetValue("STORAGE_TYPE",parts[i+7]);
                    tbl.SetValue("MEINS",       parts[i+8]);
                    tbl.SetValue("AVL_STOCK",   parts[i+9]);
                    tbl.SetValue("OPEN_STOCK",  parts[i+10]);
                    tbl.SetValue("SCAN_QTY",    parts[i+11]);
                    tbl.SetValue("PICNR",       parts[i+12]);
                    tbl.SetValue("PICK_QTY",    parts[i+13]);
                    tbl.SetValue("HU_NO",       parts[i+14]);
                    tbl.SetValue("BARCODE",     parts[i+15]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_grt_putway_crate_validation</summary>
    public class GrtPutwayCrateValHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_GRT_PUTWAY_CRATE_VAL");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_CRATE", P(2));
                fun.SetValue("IM_LGPLA", P(3));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_grt_putway_post</summary>
    public class GrtPutawayPostHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_GRT_PUTWAY_POST");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_CRATE", P(2));
                fun.SetValue("IM_LGPLA", P(3));
                fun.SetValue("IM_USER",  P(4));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HU (Handling Units)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>opcode: hugetdetails | Input: hugetdetails#EXIDV#WERKS</summary>
    public class HuGetDetailsHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_HU_GET_DETAILS");
                fun.SetValue("IM_EXIDV", P(1));
                fun.SetValue("IM_WERKS", P(2));
                fun.SetValue("IM_LGNUM", "SDC");
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));

                return "S#" + SerializeTable(fun.GetTable("ET_HU_ITEM"), "MATNR", "VEMNG", "MEINS") +
                       "!" + SerializeTable(fun.GetTable("ET_LAGP"), "LGPLA") +
                       "!" + SerializeEanData(fun.GetTable("ET_EAN_DATA"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: gethus | Input: gethus#WERKS#VBELN#EDOCNO</summary>
    public class GetHusHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_HUS");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_VBELN", P(2));
                fun.SetValue("IM_EDOCNO",P(3));
                fun.SetValue("IM_LGNUM", "SDC");
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));

                return "S#" + SerializeTable(fun.GetTable("ET_HUS"), "EXIDV", "VEMNG", "MATNR");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: savehus (Store HU GRC)</summary>
    public class SaveHusHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_HU_GRC");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS", parts[0]);
                fun.SetValue("IM_LGNUM", "SDC");
                fun.SetValue("IM_USER",  parts[1]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 2; i < parts.Length; i++)
                {
                    tbl.Append();
                    tbl.SetValue("HU_NO", parts[i]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: savehuassign (Create HU and Assign)</summary>
    public class SaveHuAssignHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_CREATE_HU_AND_ASSIGN");
                var parts = P(1).Split(',');
                fun.SetValue("IM_VBELN", parts[0]);
                fun.SetValue("IM_USER",  parts[1]);
                fun.SetValue("IM_EXIDV", parts[2]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 3; i + 5 < parts.Length; i += 6)
                {
                    tbl.Append();
                    tbl.SetValue("MATNR", parts[i]);
                    tbl.SetValue("CHARG", parts[i+1]);
                    tbl.SetValue("WERKS", parts[i+2]);
                    tbl.SetValue("LGORT", parts[i+3]);
                    tbl.SetValue("TMENG", parts[i+4]);
                    tbl.SetValue("VRKME", parts[i+5]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StoreHuGrcHandler : SaveHusHandler { }

    /// <summary>opcode: zwm_store_hu_validate</summary>
    public class StoreHuValidateHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_HU_VALIDATE");
                fun.SetValue("IM_PICNR", P(1));
                fun.SetValue("IM_EXIDV", P(2));
                fun.SetValue("IM_WERKS", P(3));
                fun.SetValue("IM_LGNUM", P(4));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_store_bin_con_picking_hu</summary>
    public class StoreBinConPickingHuHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_BIN_CON_PICKING_HU");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS", parts[0]);
                fun.SetValue("IM_LGNUM", parts[1]);
                fun.SetValue("IM_USER",  parts[2]);
                fun.SetValue("IM_EXIDV", parts[3]);
                fun.SetValue("IM_PICNR", parts[4]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 5; i + 2 < parts.Length; i += 3)
                {
                    tbl.Append();
                    tbl.SetValue("MATERIAL", parts[i]);
                    tbl.SetValue("BIN",      parts[i+1]);
                    tbl.SetValue("SCAN_QTY", parts[i+2]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: zwm_hu_quan</summary>
    public class HuQuanHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_HU_QUAN");
                fun.SetValue("P_EXIDV", P(1));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StoreGetMajorCatHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_MAJOR_CAT");
                fun.SetValue("IM_WERKS", P(1));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeTable(fun.GetTable("ET_DATA"), "SEG", "DIV", "SDIV", "MCAT", "MC");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StoreGetMajorCatDataHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_MAJOR_CAT_DATA");
                fun.SetValue("IM_WERKS",   P(1));
                fun.SetValue("IM_SEG",     P(2));
                fun.SetValue("IM_DIVISION",P(3));
                fun.SetValue("IM_SUB_DIV", P(4));
                fun.SetValue("IM_MAJ_CAT", P(5));
                fun.SetValue("IM_MC",      P(6));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeEanData(fun.GetTable("ET_EAN_DATA"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TO (Transfer Orders)
    // ═══════════════════════════════════════════════════════════════════════════

    public class CreateToHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_TO_CREATE_FROM_GR_DATA");
                var parts = P(1).Split(',');
                fun.SetValue("IM_MBLNR", parts[0]);
                fun.SetValue("IM_MJAHR", parts[1]);
                fun.SetValue("IM_USER",  parts[2]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 3; i + 2 < parts.Length; i += 3)
                {
                    tbl.Append();
                    tbl.SetValue("MATNR", parts[i]);
                    tbl.SetValue("MENGE", parts[i+1]);
                    tbl.SetValue("LGPLA", parts[i+2]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class ToGetDetailsHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_TO_GET_DETAILS");
                fun.SetValue("IM_LGNUM", P(1));
                fun.SetValue("IM_TANUM", P(2));
                fun.SetValue("IM_USER",  P(3));
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));

                return "S#" + SerializeTable(fun.GetTable("ET_LTAP"), "TANUM", "TAPOS", "MATNR", "VEMNG", "LGPLA") +
                       "!" + SerializeEanData(fun.GetTable("ET_EAN_DATA"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class ToScanDataSaveHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_TO_SCAN_DATA_SAVE");
                var parts = P(1).Split(',');
                fun.SetValue("IM_LGNUM", parts[0]);
                fun.SetValue("IM_TANUM", parts[1]);
                fun.SetValue("IM_USER",  parts[2]);
                fun.SetValue("IM_EXIDV", parts[3]);
                fun.SetValue("IM_LGPLA", parts[4]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 5; i + 5 < parts.Length; i += 6)
                {
                    tbl.Append();
                    tbl.SetValue("EXIDV", parts[i]);
                    tbl.SetValue("VBELN", parts[i+1]);
                    tbl.SetValue("TMENG", parts[i+2]);
                    tbl.SetValue("MATNR", parts[i+3]);
                    tbl.SetValue("WERKS", parts[i+4]);
                    tbl.SetValue("LGORT", parts[i+5]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class SaveGrcToDataHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_SAVE_GRC_TO_DATA");
                var parts = P(1).Split(',');
                fun.SetValue("IM_USER", parts[0]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 1; i + 9 < parts.Length; i += 10)
                {
                    tbl.Append();
                    tbl.SetValue("WAREHOUSE",  parts[i]);
                    tbl.SetValue("SITE",       parts[i+1]);
                    tbl.SetValue("SLOC",       parts[i+2]);
                    tbl.SetValue("CRATE",      parts[i+3]);
                    tbl.SetValue("BIN_TYPE",   parts[i+4]);
                    tbl.SetValue("BIN",        parts[i+5]);
                    tbl.SetValue("MATERIAL",   parts[i+6]);
                    tbl.SetValue("SCAN_QTY",   parts[i+7]);
                    tbl.SetValue("KEY",        parts[i+8]);
                    tbl.SetValue("SOURCE_BIN", parts[i+9]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class Store0001StockTakeHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_0001_STOCK_TAKE");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS",      parts[0]);
                fun.SetValue("IM_LGNUM",      parts[1]);
                fun.SetValue("IM_USER",       parts[2]);
                fun.SetValue("IM_STOCK_TAKE", "X");
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 3; i + 2 < parts.Length; i += 3)
                {
                    tbl.Append();
                    tbl.SetValue("MATNR", parts[i]);
                    tbl.SetValue("VEMNG", parts[i+1]);
                    tbl.SetValue("LGPLA", parts[i+2]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class Store0001ReverseStockHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_0001_REVERSE_STOCK");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS",      parts[0]);
                fun.SetValue("IM_LGNUM",      parts[1]);
                fun.SetValue("IM_USER",       parts[2]);
                fun.SetValue("IM_STOCK_TAKE", "X");
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 3; i + 2 < parts.Length; i += 3)
                {
                    tbl.Append();
                    tbl.SetValue("MATNR", parts[i]);
                    tbl.SetValue("VEMNG", parts[i+1]);
                    tbl.SetValue("LGPLA", parts[i+2]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StoreTrf0001To0010Handler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_TRF_0001_TO_0010");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS", parts[0]);
                fun.SetValue("IM_LGNUM", parts[1]);
                fun.SetValue("IM_USER",  parts[2]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 3; i + 14 < parts.Length; i += 15)
                {
                    tbl.Append();
                    tbl.SetValue("WM_NO",       parts[i]);
                    tbl.SetValue("MATERIAL",    parts[i+1]);
                    tbl.SetValue("PLANT",       parts[i+2]);
                    tbl.SetValue("STOR_LOC",    parts[i+3]);
                    tbl.SetValue("BIN",         parts[i+4]);
                    tbl.SetValue("STORAGE_TYPE",parts[i+5]);
                    tbl.SetValue("MEINS",       parts[i+6]);
                    tbl.SetValue("AVL_STOCK",   parts[i+7]);
                    tbl.SetValue("OPEN_STOCK",  parts[i+8]);
                    tbl.SetValue("SCAN_QTY",    parts[i+9]);
                    tbl.SetValue("PICK_QTY",    parts[i+10]);
                    tbl.SetValue("BARCODE",     parts[i+11]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StoreTransferBinToBinHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_TRANSFER_BIN_TO_BIN");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS", parts[0]);
                fun.SetValue("IM_NLPLA", parts[1]);
                fun.SetValue("IM_LGNUM", "SDC");
                fun.SetValue("IM_LGORT", "0002");
                fun.SetValue("IM_USER",  parts[2]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 3; i + 2 < parts.Length; i += 3)
                {
                    tbl.Append();
                    tbl.SetValue("MATERIAL", parts[i]);
                    tbl.SetValue("SCAN_QTY", parts[i+1]);
                    tbl.SetValue("BIN",      parts[i+2]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StoreSlocToSlocHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_TRANSFER_SLOC_TO_SLO");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS",     parts[0]);
                fun.SetValue("IM_LGORT_SRC", parts[1]);
                fun.SetValue("IM_LGORT_DEST",parts[2]);
                fun.SetValue("IM_LGNUM",     "0002");
                fun.SetValue("IM_USER",      parts[3]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 4; i + 1 < parts.Length; i += 2)
                {
                    tbl.Append();
                    tbl.SetValue("MATERIAL", parts[i]);
                    tbl.SetValue("SCAN_QTY", parts[i+1]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class GetV01001sStockHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_V01_001S_STOCK");
                fun.SetValue("IM_WERKS", P(1));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeTable(fun.GetTable("ET_DATA"), "WM_NO","MATERIAL","PLANT","STOR_LOC","BIN","STORAGE_TYPE","MEINS","AVL_STOCK","OPEN_STOCK","SCAN_QTY","PICK_QTY") +
                       "!" + SerializeEanData(fun.GetTable("ET_EAN_DATA"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class GetV01001sPostHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_GET_V01_001S_POST");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS", parts[0]);
                fun.SetValue("IM_LGNUM", parts[1]);
                fun.SetValue("IM_USER",  parts[2]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 3; i + 10 < parts.Length; i += 11)
                {
                    tbl.Append();
                    tbl.SetValue("WM_NO",       parts[i]);
                    tbl.SetValue("MATERIAL",    parts[i+1]);
                    tbl.SetValue("PLANT",       parts[i+2]);
                    tbl.SetValue("STOR_LOC",    parts[i+3]);
                    tbl.SetValue("BIN",         parts[i+4]);
                    tbl.SetValue("STORAGE_TYPE",parts[i+5]);
                    tbl.SetValue("MEINS",       parts[i+6]);
                    tbl.SetValue("AVL_STOCK",   parts[i+7]);
                    tbl.SetValue("OPEN_STOCK",  parts[i+8]);
                    tbl.SetValue("SCAN_QTY",    parts[i+9]);
                    tbl.SetValue("PICK_QTY",    parts[i+10]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EAN / ARTICLE
    // ═══════════════════════════════════════════════════════════════════════════

    public class StoreGetMatFromEanHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_MAT_FROM_EAN");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_EAN",   P(2));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeEanData(fun.GetTable("ET_EAN_DATA")) +
                       "!" + SerializeTable(fun.GetTable("ET_LQUA"), "LGPLA","MATNR","VEMNG");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class ValidateStoreEanHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_STORE_EAN_DATA");
                fun.SetValue("IM_EAN11", P(1));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeEanData(fun.GetTable("ET_EAN_DATA"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class ValidateStoreEanV2Handler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_STORE_GET_MAT_FROM_EAN_V2");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_EAN",   P(2));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeEanData(fun.GetTable("ET_EAN_DATA")) +
                       "!" + SerializeTable(fun.GetTable("ET_LQUA"), "LGPLA","MATNR","VEMNG");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class AppArticleDetailsHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_APP_ARTICLE_DETAILS");
                fun.SetValue("IM_EAN",   P(1));
                fun.SetValue("IM_WERKS", P(2));
                fun.SetValue("IM_LGNUM", P(3));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class GetPackingMaterialHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_GET_PACKING_MATERIAL");
                fun.SetValue("IM_LGNUM", "V2R");
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeTable(fun.GetTable("ET_PACK_MAT"), "MATNR","MAKTX");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // STID (Stock Take ID)
    // ═══════════════════════════════════════════════════════════════════════════

    public class StoreStidPostHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_RFC_STORE_STID_POST");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS", parts[0]);
                fun.SetValue("IM_STID",  parts[1]);
                fun.SetValue("IM_LGPLA", parts[2]);
                fun.SetValue("IM_USER",  parts[3]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 4; i + 3 < parts.Length; i += 4)
                {
                    tbl.Append();
                    tbl.SetValue("STOCK_TAKE", parts[i]);
                    tbl.SetValue("BIN",        parts[i+1]);
                    tbl.SetValue("MATERIAL",   parts[i+2]);
                    tbl.SetValue("SCAN_QTY",   parts[i+3]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StoreStidSaveMcHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_RFC_STORE_STID_SAVE_MC");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS", parts[0]);
                fun.SetValue("IM_STID",  parts[1]);
                fun.SetValue("IM_USER",  parts[2]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 3; i + 5 < parts.Length; i += 6)
                {
                    tbl.Append();
                    tbl.SetValue("STOCK_TAKE", parts[i]);
                    tbl.SetValue("BIN",        parts[i+1]);
                    tbl.SetValue("MATERIAL",   parts[i+2]);
                    tbl.SetValue("SCAN_QTY",   parts[i+3]);
                    tbl.SetValue("LOCATION",   parts[i+4]);
                    tbl.SetValue("SITE",       parts[i+5]);
                    tbl.SetValue("BARCODE",    parts.Length > i+6 ? parts[i+6] : "");
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class ValidateStockTakeIdHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_STORE_VALDIATE_STID");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_STID",  P(2));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeTable(fun.GetTable("IT_BIN"), "BIN","LGPLA");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class ValidateStockTakeIdMcHandler : ValidateStockTakeIdHandler { }

    public class ValidateGandolaMcHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_STORE_VALDIATE_GANDOLA");
                fun.SetValue("IM_WERKS",   P(1));
                fun.SetValue("IM_GANDOLA", P(2));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class GetEanStidMcHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_GET_EAN_STID_MC");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_STID",  P(2));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeEanData(fun.GetTable("ET_EAN_DATA"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DISCOUNT
    // ═══════════════════════════════════════════════════════════════════════════

    public class DiscountGetEanDataHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZSTORE_DISCOUNT_GET_EAN_DATA");
                fun.SetValue("IM_EAN", P(1));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeTable(fun.GetTable("ET_DISCOUNT_DATA"),"MATNR","DISCOUNT","VALID_FROM","VALID_TO") +
                       "!" + SerializeEanData(fun.GetTable("ET_EAN_DATA"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class DiscountSaveEanDataHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZSTORE_DISCOUNT_SAVE_EAN_DATA");
                var parts = P(1).Split(',');
                fun.SetValue("IM_USER", parts[0]);
                fun.SetValue("WERKS",   parts[1]);
                fun.SetValue("EAN11",   parts[2]);
                fun.SetValue("SQNTY",   parts[3]);
                fun.SetValue("MATNR",   parts[4]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 5; i + 3 < parts.Length; i += 4)
                {
                    tbl.Append();
                    tbl.SetValue("WERKS", parts[i]);
                    tbl.SetValue("EAN11", parts[i+1]);
                    tbl.SetValue("SQNTY", parts[i+2]);
                    tbl.SetValue("MATNR", parts[i+3]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class DiscountStoreValiHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZSTORE_DISCOUNT_STORE_VALI");
                fun.SetValue("IM_WERKS", P(1));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PUSH DATA
    // ═══════════════════════════════════════════════════════════════════════════

    public class PushDataToSap1StockHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_PUSHDATATOSAP_1STOCK");
                var parts = P(1).Split(',');
                fun.SetValue("IM_USER",  parts[0]);
                fun.SetValue("EMP_CODE", parts[1]);
                fun.SetValue("SITE",     parts[2]);
                fun.SetValue("GANDOLA",  parts[3]);
                fun.SetValue("ARTICLE",  parts[4]);
                fun.SetValue("QUANTITY", parts[5]);
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class PushDataToSap1DisHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_PUSHDATATOSAP_1DIS");
                var parts = P(1).Split(',');
                fun.SetValue("IM_USER",   parts[0]);
                fun.SetValue("IM_NATURE", parts[1]);
                fun.SetValue("EMP_CODE",  parts[2]);
                fun.SetValue("SITE",      parts[3]);
                fun.SetValue("GANDOLA",   parts[4]);
                fun.SetValue("ARTICLE",   parts[5]);
                fun.SetValue("QUANTITY",  parts[6]);
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class PushDataToSap1TotalHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_STORE_PUSHDATATOSAP_1TOTAL");
                var parts = P(1).Split(',');
                fun.SetValue("IM_USER",  parts[0]);
                fun.SetValue("EMP_CODE", parts[1]);
                fun.SetValue("SITE",     parts[2]);
                fun.SetValue("GANDOLA",  parts[3]);
                fun.SetValue("QUANTITY", parts[4]);
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SDC PUT31
    // ═══════════════════════════════════════════════════════════════════════════

    public class SdcPut31Handler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZRFC_SDC_PUT31");
                fun.SetValue("IM_SITE", P(1));
                fun.SetValue("IM_HU",   P(2));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeEanData(fun.GetTable("ET_EAN_DATA")) +
                       "!" + SerializeTable(fun.GetTable("ET_FINAL"), "LGPLA","MATNR","VEMNG") +
                       "!" + SerializeTable(fun.GetTable("ET_LQUA"),  "LGPLA","MATNR","VEMNG");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class SdcPut31BinValHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZRFC_SDC_PUT31_BIN_VALIDATION");
                fun.SetValue("IM_SITE",  P(1));
                fun.SetValue("IM_LGPLA", P(2));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class HuPut31SaveHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_HUPUT31_SAVE");
                var parts = P(1).Split(',');
                fun.SetValue("IM_COMPLETE_FLAG", "X");
                fun.SetValue("IM_PICNR",         parts[0]);
                var tblStatus = fun.GetTable("IMT_HU_STATUS");
                var tblLqua   = fun.GetTable("IMT_LQUA");
                var tblSave   = fun.GetTable("IT_HUSAVE");
                // rows in IT_HUSAVE: HU,ITEM_NO,ARTICLE,PSTNG_DATE,PLANT,STGE_LOC,SCAN_QTY,REM_QTY,BIN,LGNUM
                for (int i = 1; i + 9 < parts.Length; i += 10)
                {
                    tblSave.Append();
                    tblSave.SetValue("HU",         parts[i]);
                    tblSave.SetValue("ITEM_NO",    parts[i+1]);
                    tblSave.SetValue("ARTICLE",    parts[i+2]);
                    tblSave.SetValue("PLANT",      parts[i+3]);
                    tblSave.SetValue("STGE_LOC",   parts[i+4]);
                    tblSave.SetValue("SCAN_QTY",   parts[i+5]);
                    tblSave.SetValue("REM_QTY",    parts[i+6]);
                    tblSave.SetValue("BIN",        parts[i+7]);
                    tblSave.SetValue("LGNUM",      parts[i+8]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MISC STORE
    // ═══════════════════════════════════════════════════════════════════════════

    public class GetStoDataHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_GET_STO_DATA");
                fun.SetValue("IM_EXIDV", P(1));
                fun.SetValue("IM_STO",   P(2));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeTable(fun.GetTable("ET_DATA"),     "MATNR","VEMNG") +
                       "!" + SerializeEanData(fun.GetTable("ET_EAN_DATA")) +
                       "!" + SerializeTable(fun.GetTable("ET_LAGP_DATA"), "LGPLA");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class GetGrcBinsHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_GET_GRC_BINS");
                fun.SetValue("IM_MBLNR", P(1));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeTable(fun.GetTable("ET_BINS"),  "LGPLA") +
                       "!" + SerializeTable(fun.GetTable("ET_CRATE"), "EXIDV");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class ValidateDcSlocHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_VALIDATE_DC_SLOC");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_LGORT", P(2));
                fun.SetValue("IM_V11",   P(3));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class RfcStoreEanDataStkHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_STORE_EAN_DATA_STK");
                fun.SetValue("IM_EAN11", P(1));
                fun.SetValue("IM_WERKS", P(2));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeEanData(fun.GetTable("ET_EAN_DATA"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class RfcValidateCrateHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_VALIDATE_CRATE");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_CRATE", P(2));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class GetGrDetailsHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_GR_GET_DETAILS");
                fun.SetValue("IM_MBLNR", P(1));
                fun.SetValue("IM_MJAHR", P(2));
                fun.SetValue("IM_USER",  P(3));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeTable(fun.GetTable("ET_MSEG_DATA"), "MATNR","MENGE","LGPLA") +
                       "!" + SerializeEanData(fun.GetTable("ET_EAN_DATA"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class CreateToFromGrDataHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_TO_CREATE_FROM_GR_DATA");
                var parts = P(1).Split(',');
                fun.SetValue("IM_MBLNR", parts[0]);
                fun.SetValue("IM_MJAHR", parts[1]);
                fun.SetValue("IM_USER",  parts[2]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 3; i + 2 < parts.Length; i += 3)
                {
                    tbl.Append();
                    tbl.SetValue("MATNR", parts[i]);
                    tbl.SetValue("MENGE", parts[i+1]);
                    tbl.SetValue("LGPLA", parts[i+2]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }
}
