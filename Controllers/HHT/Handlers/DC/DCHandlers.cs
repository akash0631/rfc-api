using SAP.Middleware.Connector;
using System.Text;

namespace Vendor_Application_MVC.Controllers.HHT.Handlers.DC
{
    // ═══════════════════════════════════════════════════════════════════════════
    // NIT (Inbound / Delivery scanning)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>opcode: nitrec — Inbound GR receive scan</summary>
    public class NitRecHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_PO_SCAN_DATA_SAVE");
                var parts = P(1).Split(',');
                fun.SetValue("IM_LGNUM",      "V2R");
                fun.SetValue("IM_EBELN",      parts[0]);
                fun.SetValue("IM_XBLNR",      parts[1]);
                fun.SetValue("IM_BILL",        parts[2]);
                fun.SetValue("IM_GATE_ENTRY", parts[3]);
                fun.SetValue("IM_FRBNR",      parts[4]);
                fun.SetValue("IM_USER",       parts[5]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 6; i + 3 < parts.Length; i += 4)
                {
                    tbl.Append();
                    tbl.SetValue("MATERIAL", parts[i]);
                    tbl.SetValue("SCAN_QTY", parts[i+1]);
                    tbl.SetValue("CRATE",    parts[i+2]);
                    tbl.SetValue("LGPLA",    parts[i+3]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: nitdel — Get PO details for inbound</summary>
    public class NitDelHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_PO_GET_DETAILS");
                fun.SetValue("IM_EBELN",      P(1));
                fun.SetValue("IM_XBLNR",      P(2));
                fun.SetValue("IM_GATE_ENTRY", P(3));
                fun.SetValue("IM_BILL",       P(4));
                fun.SetValue("IM_USER",       P(5));
                fun.SetValue("IM_FRBNR",      P(6));
                fun.Invoke(dest);

                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeTable(fun.GetTable("ET_PO_DATA"), "MATNR","MENGE","LGPLA") +
                       "!" + SerializeEanData(fun.GetTable("ET_EAN_DATA"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: nitupd — Validate crate at inbound</summary>
    public class NitUpdHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_VALIDATE_CRATE");
                fun.SetValue("IM_EBELN", P(1));
                fun.SetValue("IM_XBLNR", P(2));
                fun.SetValue("IM_CRATE", P(3));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DELIVERY / DISPATCH SCAN
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>opcode: scndelivery | Input: scndelivery#VBELN</summary>
    public class ScnDeliveryHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_DELIVERY_GET_DETAILS");
                fun.SetValue("IM_VBELN", P(1));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeTable(fun.GetTable("ET_LIPS"), "MATNR","LGMNG","MEINS","WERKS","LGORT") +
                       "!" + SerializeEanData(fun.GetTable("ET_EAN_DATA"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: scnsel | Input: scnsel#VBELN (PLP2 variant)</summary>
    public class ScnSelHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_DELIVERY_GET_DETAILS_PLP2");
                fun.SetValue("IM_VBELN", P(1));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeTable(fun.GetTable("ET_LIPS"),   "MATNR","LGMNG","MEINS","WERKS","LGORT") +
                       "!" + SerializeEanData(fun.GetTable("ET_EAN_DATA")) +
                       "!" + SerializeTable(fun.GetTable("ET_BIN_MC"),  "LGPLA","MATNR","VEMNG");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    /// <summary>opcode: disrec — Dispatch scan (barcode → TO data)</summary>
    public class DisRecHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_FM_BARCODE_GET_TO_DATA");
                fun.SetValue("I_TONUMBER", P(1));
                fun.SetValue("ZWERKS",     "1006");
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeTable(fun.GetTable("E_TOITEMDATA"), "TANUM","TAPOS","MATNR","VEMNG","LGPLA");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DC STOCK TAKE
    // ═══════════════════════════════════════════════════════════════════════════

    public class StockTakeGetDetailsHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_STOCK_TAKE_GET_DETAILS");
                fun.SetValue("IM_STOCK_TAKE", P(1));
                fun.SetValue("IM_RFC",        "X");
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeTable(fun.GetTable("ET_ITEM"),  "MATNR","MENGE","LGPLA") +
                       "!" + SerializeTable(fun.GetTable("ET_BIN"),   "LGPLA") +
                       "!" + SerializeTable(fun.GetTable("ET_CRATE"), "EXIDV");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StockTakeSaveDataHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_RFC_STOCK_TAKE_SAVE_DATA");
                var parts = P(1).Split(',');
                fun.SetValue("IM_STOCK_TAKE", parts[0]);
                fun.SetValue("IM_USER",       parts[1]);
                fun.SetValue("IM_RFC",        "X");
                var tbl = fun.GetTable("IT_ITEM");
                for (int i = 2; i + 21 < parts.Length; i += 22)
                {
                    tbl.Append();
                    tbl.SetValue("MANDT",     parts[i]);
                    tbl.SetValue("STOCK_TAKE",parts[i+1]);
                    tbl.SetValue("POSNR",     parts[i+2]);
                    tbl.SetValue("WERKS",     parts[i+3]);
                    tbl.SetValue("LGNUM",     parts[i+4]);
                    tbl.SetValue("LGTYP",     parts[i+5]);
                    tbl.SetValue("LGPLA",     parts[i+6]);
                    tbl.SetValue("MATNR",     parts[i+7]);
                    tbl.SetValue("MENGE",     parts[i+8]);
                    tbl.SetValue("MEINS",     parts[i+9]);
                    tbl.SetValue("CRATE",     parts[i+10]);
                    tbl.SetValue("TANUM",     parts[i+11]);
                    tbl.SetValue("TAPOS",     parts[i+12]);
                    tbl.SetValue("ERNAM",     parts[i+13]);
                    tbl.SetValue("ERDAT",     parts[i+14]);
                    tbl.SetValue("UZEIT",     parts[i+15]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StockValidateBarcodeHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_STOCK_VALIDATE_BARCODE");
                fun.SetValue("IM_BARCODE", P(1));
                fun.SetValue("IM_LGNUM",   P(2));
                fun.SetValue("IM_LGTYP",   P(3));
                fun.SetValue("IM_RFC",     "X");
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                return "S#" + SerializeEanData(fun.GetTable("ET_EAN_DATA"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StockTakeArtiValiHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_STOCK_TAKE_ARTI_VALI");
                fun.SetValue("IM_BARCODE", P(1));
                fun.SetValue("IM_SITE",    P(2));
                fun.Invoke(dest);
                var ret = fun.GetStructure("EX_RETURN");
                if (ret.GetString("TYPE") == "E") return Err(ret.GetString("MESSAGE"));
                var marm = fun.GetStructure("EX_MARM");
                return "S#" + marm.GetString("MATNR") + "#" + marm.GetString("UMREZ") + "#" + marm.GetString("EAN11");
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StockTakeBinValiHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_STOCK_TAKE_BIN_VALI");
                fun.SetValue("IM_BIN",  P(1));
                fun.SetValue("IM_SITE", P(2));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StockTakeCrateValiHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_STOCK_TAKE_CRATE_VALI");
                fun.SetValue("IM_CRATE", P(1));
                fun.SetValue("IM_SITE",  P(2));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StockTakeSaveV11Handler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_RFC_STOCK_TAKE_SAVE_V11");
                var parts = P(1).Split(',');
                fun.SetValue("IM_USER", parts[0]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 1; i + 8 < parts.Length; i += 9)
                {
                    tbl.Append();
                    tbl.SetValue("WAREHOUSE", parts[i]);
                    tbl.SetValue("SITE",      parts[i+1]);
                    tbl.SetValue("SLOC",      parts[i+2]);
                    tbl.SetValue("CRATE",     parts[i+3]);
                    tbl.SetValue("BIN_TYPE",  parts[i+4]);
                    tbl.SetValue("BIN",       parts[i+5]);
                    tbl.SetValue("MATERIAL",  parts[i+6]);
                    tbl.SetValue("SCAN_QTY",  parts[i+7]);
                    tbl.SetValue("KEY",       parts[i+8]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StockValidateV21Handler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_STOCK_VALIDATE_V21");
                fun.SetValue("IM_USER", P(1));
                fun.SetValue("TYPE",    P(2));
                fun.SetValue("BIN",     P(3));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class StockMovementV21Handler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_RFC_STOCK_MOVEMENT_V21");
                fun.SetValue("IM_USER",        P(1));
                fun.SetValue("PICK_PUTAWAY",   P(2));
                fun.SetValue("TYPE",           P(3));
                fun.SetValue("PLANT",          P(4));
                fun.SetValue("WAREHOUSE",      "V2R");
                fun.SetValue("LOCATION",       "0001");
                fun.SetValue("STORAGE_TYPE",   "E01");
                fun.SetValue("BIN",            P(5));
                fun.SetValue("DESTINATION_BIN",P(6));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DC HU GRT
    // ═══════════════════════════════════════════════════════════════════════════

    public class DcHuGrtValHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_DC_HU_GRT_VAL");
                fun.SetValue("IM_WERKS",  P(1));
                fun.SetValue("IM_SLGORT", P(2));
                fun.SetValue("IM_DLGORT", P(3));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class DcHuGrtBinHuValHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_DC_HUGRT_BINHU_VAL");
                fun.SetValue("IM_LGPLA", P(1));
                fun.SetValue("IM_SITE",  P(2));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class DcHuGrtHuValHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_DC_HUGRT_HU_VAL");
                fun.SetValue("IM_EXIDV", P(1));
                fun.SetValue("IM_WERKS", P(2));
                fun.SetValue("IM_SLOC",  P(3));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class DcHuGrtSaveHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_DC_HUGRT_SAVE");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS",  parts[0]);
                fun.SetValue("IM_USER",   parts[1]);
                fun.SetValue("IM_SLGORT", parts[2]);
                fun.SetValue("IM_DLGORT", parts[3]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 4; i + 1 < parts.Length; i += 2)
                {
                    tbl.Append();
                    tbl.SetValue("LGPLA", parts[i]);
                    tbl.SetValue("EX_HU", parts[i+1]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CLA (Consolidation Area)
    // ═══════════════════════════════════════════════════════════════════════════

    public class ClaBinValidateHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_CLA_BIN_VALIDATE");
                fun.SetValue("PALETTE",   P(1));
                fun.SetValue("CLABIN",    P(2));
                fun.SetValue("INDICATOR", P(3));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class ClaHuValidateHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_CLA_HU_VALIDATE");
                fun.SetValue("EXIDV", P(1));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class ClaPaletteValidateHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_CLA_PALETTE_VALIDATE");
                fun.SetValue("PALETTE",   P(1));
                fun.SetValue("INDICATOR", P(2));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class ClaHuPaletteSaveHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_CLA_HU_PALETTE_SAVE");
                var parts = P(1).Split(',');
                fun.SetValue("EXIDV",   parts[0]);
                fun.SetValue("WERKS",   parts[1]);
                fun.SetValue("PALETTE", parts[2]);
                var tbl = fun.GetTable("IM_DATA");
                for (int i = 3; i + 1 < parts.Length; i += 2)
                {
                    tbl.Append();
                    tbl.SetValue("EXIDV",   parts[i]);
                    tbl.SetValue("PALETTE", parts[i+1]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class ClaPaletteBinTagSaveHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_CLA_PALETTE_BIN_TAG_SAVE");
                fun.SetValue("PALETTE",   P(1));
                fun.SetValue("CLABIN",    P(2));
                fun.SetValue("INDICATOR", P(3));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CRATE / VALIDATE
    // ═══════════════════════════════════════════════════════════════════════════

    public class ValidateCrateToHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_VALIDATE_CRATE");
                fun.SetValue("IM_EBELN", P(1));
                fun.SetValue("IM_XBLNR", P(2));
                fun.SetValue("IM_CRATE", P(3));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class SaveCrateHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                // SaveCrate maps to ZWM_TO_CREATE_FROM_SCAN_DATA
                var dest  = GetProdDestination();
                var fun   = dest.Repository.CreateFunction("ZWM_TO_CREATE_FROM_SCAN_DATA");
                var parts = P(1).Split(',');
                fun.SetValue("IM_WERKS", parts[0]);
                fun.SetValue("IM_MBLNR", parts[1]);
                var tbl = fun.GetTable("IT_DATA");
                for (int i = 2; i + 1 < parts.Length; i += 2)
                {
                    tbl.Append();
                    tbl.SetValue("CRATE", parts[i]);
                    tbl.SetValue("LGPLA", parts[i+1]);
                }
                fun.Invoke(dest);
                return OkOrErr(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }

    public class ValidateExternalHuHandler : HHTBaseHandler
    {
        public override string Execute()
        {
            try
            {
                var dest = GetProdDestination();
                var fun  = dest.Repository.CreateFunction("ZWM_VALIDATE_EXTERNAL_HU");
                fun.SetValue("IM_WERKS", P(1));
                fun.SetValue("IM_EXIDV", P(2));
                fun.SetValue("IM_DWERKS",P(3));
                fun.SetValue("IM_VBELN", P(4));
                fun.Invoke(dest);
                return ReturnFromStructure(fun.GetStructure("EX_RETURN"));
            }
            catch (System.Exception ex) { return Err(ex.Message); }
        }
    }
}
