using System;
using System.Collections.Generic;
using Vendor_Application_MVC.Controllers.HHT.Handlers.Auth;
using Vendor_Application_MVC.Controllers.HHT.Handlers.Store;
using Vendor_Application_MVC.Controllers.HHT.Handlers.DC;

namespace Vendor_Application_MVC.Controllers.HHT
{
    /// <summary>
    /// Maps every HHT opcode to its handler class.
    /// To add a new opcode: (1) create a handler in Handlers/, (2) register it here. That's it.
    /// </summary>
    public static class HHTRouter
    {
        private static readonly Dictionary<string, Func<HHTBaseHandler>> _map =
            new Dictionary<string, Func<HHTBaseHandler>>(StringComparer.OrdinalIgnoreCase)
        {
            // ── Authentication ──────────────────────────────────────────────
            { "scnrec",                    () => new ScnrecHandler() },

            // ── Store: Stock ────────────────────────────────────────────────
            { "getstorestock",             () => new GetStoreStockHandler() },
            { "getstorestocktake",         () => new GetStoreStockTakeHandler() },
            { "zwm_store_get_grtstock",    () => new GetGrtStockHandler() },

            // ── Store: Bins ─────────────────────────────────────────────────
            { "storegetbin",               () => new StoreGetBinHandler() },
            { "zwm_store_get_bin",         () => new StoreGetBinHandler() },
            { "storegetbin_v2",            () => new StoreGetBinV2Handler() },
            { "zwm_store_get_bin_v2",      () => new StoreGetBinV2Handler() },
            { "storegetbinstock",          () => new StoreGetBinStockHandler() },
            { "getmatbinstock",            () => new GetMatBinStockHandler() },
            { "getmatbinstockbtob",        () => new GetMatBinStockBtoBHandler() },
            { "validatebin",               () => new ValidateBinHandler() },
            { "zwm_store_bin_list_validation", () => new StoreBinListValidationHandler() },
            { "zwm_store_binconhu_get_details",() => new StoreBinConHuGetDetailsHandler() },
            { "zwm_save_empty_bin",        () => new SaveEmptyBinHandler() },
            { "zwm_validate_empty_bin",    () => new ValidateEmptyBinHandler() },
            { "zwm_vali_crate_emptybin",   () => new ValiCrateEmptyBinHandler() },

            // ── Store: SLOC ─────────────────────────────────────────────────
            { "validatesloc",              () => new ValidateSlocHandler() },
            { "getsloc",                   () => new GetSlocHandler() },

            // ── Store: Picklist / Picking ───────────────────────────────────
            { "getstorepicklist",          () => new GetStorePicklistHandler() },
            { "getstorepicklist_v2",       () => new GetStorePicklistV2Handler() },
            { "savedirectpicking",         () => new SaveDirectPickingHandler() },
            { "savedirectpicking_v2",      () => new SaveDirectPickingV2Handler() },
            { "zwm_picklist_nos_disp",     () => new PicklistNosDispHandler() },
            { "zhhtusr_del_picking_rfc",   () => new ZhhtusrDelPickingHandler() },

            // ── Store: GRC / Putaway ────────────────────────────────────────
            { "savegrcputway",             () => new SaveGrcPutawayHandler() },
            { "savefloorputway",           () => new SaveFloorPutawayHandler() },
            { "savefloorputwaytake",       () => new SaveFloorPutawayTakeHandler() },
            { "zwm_floor_puaway_new",      () => new FloorPutawayNewHandler() },
            { "zwm_store_floor_putway_hu", () => new StoreFloorPutawayHuHandler() },
            { "zwm_store_hu_putway_bin_con",() => new StoreHuPutawayBinConHandler() },

            // ── Store: GRT ──────────────────────────────────────────────────
            { "savegrtmsa",                () => new SaveGrtFromMsaHandler() },
            { "savegrtfromdisplay",        () => new SaveGrtFromDisplayHandler() },
            { "zwm_grt_save",              () => new GrtSaveHandler() },
            { "zwm_grt_putway_crate_validation", () => new GrtPutwayCrateValHandler() },
            { "zwm_grt_putway_post",       () => new GrtPutawayPostHandler() },
            { "zwm_store_get_grtstock",    () => new GetGrtStockHandler() },

            // ── Store: HU ───────────────────────────────────────────────────
            { "hugetdetails",              () => new HuGetDetailsHandler() },
            { "hudetails",                 () => new HuGetDetailsHandler() },
            { "gethus",                    () => new GetHusHandler() },
            { "savehus",                   () => new SaveHusHandler() },
            { "savehuassign",              () => new SaveHuAssignHandler() },
            { "savehudetails",             () => new StoreHuGrcHandler() },
            { "zwm_store_hu_validate",     () => new StoreHuValidateHandler() },
            { "zwm_store_bin_con_picking_hu",  () => new StoreBinConPickingHuHandler() },
            { "zwm_hu_quan",               () => new HuQuanHandler() },
            { "zwm_store_get_major_cat",   () => new StoreGetMajorCatHandler() },
            { "zwm_store_get_major_cat_data",  () => new StoreGetMajorCatDataHandler() },

            // ── Store: TO ───────────────────────────────────────────────────
            { "createto",                  () => new CreateToHandler() },
            { "zwm_to_get_details",        () => new ToGetDetailsHandler() },
            { "zwm_to_scan_data_save",     () => new ToScanDataSaveHandler() },
            { "zwm_save_grc_to_data",      () => new SaveGrcToDataHandler() },
            { "zwm_store_0001_stock_take", () => new Store0001StockTakeHandler() },
            { "store_0001_stock_take",     () => new Store0001StockTakeHandler() },
            { "zwm_store_0001_reverse_stock",  () => new Store0001ReverseStockHandler() },
            { "zwm_store_trf_0001_to_0010",() => new StoreTrf0001To0010Handler() },
            { "store_trf_0001_to_0010",    () => new StoreTrf0001To0010Handler() },
            { "zwm_store_transfer_bin_to_bin", () => new StoreTransferBinToBinHandler() },
            { "savebtob",                  () => new StoreTransferBinToBinHandler() },
            { "savesloctoslocwwm",         () => new StoreSlocToSlocHandler() },
            { "get_v01_001s_stock",        () => new GetV01001sStockHandler() },
            { "get_v01_001s_post",         () => new GetV01001sPostHandler() },

            // ── Store: EAN / Article ────────────────────────────────────────
            { "store_get_mat_from_ean",    () => new StoreGetMatFromEanHandler() },
            { "validatestoreean",          () => new ValidateStoreEanHandler() },
            { "validatestoreean_v2",       () => new ValidateStoreEanV2Handler() },
            { "articledetails",            () => new AppArticleDetailsHandler() },
            { "packgingmaterial",          () => new GetPackingMaterialHandler() },
            { "zwm_store_get_mat_from_ean",() => new StoreGetMatFromEanHandler() },

            // ── Store: STID (Stock Take ID) ─────────────────────────────────
            { "storestidpost",             () => new StoreStidPostHandler() },
            { "storestidpost_mc",          () => new StoreStidSaveMcHandler() },
            { "validatestablestocktakeid", () => new ValidateStockTakeIdHandler() },
            { "validatestablestocktakeid_mc",  () => new ValidateStockTakeIdMcHandler() },
            { "validategandola_mc",        () => new ValidateGandolaMcHandler() },
            { "zwm_rfc_get_ean_stid_mc",   () => new GetEanStidMcHandler() },

            // ── Store: Discount ─────────────────────────────────────────────
            { "zstore_discount_get_ean_data",  () => new DiscountGetEanDataHandler() },
            { "zstore_discount_save_ean_data", () => new DiscountSaveEanDataHandler() },
            { "zstore_discount_store_vali",    () => new DiscountStoreValiHandler() },

            // ── Store: Push Data ────────────────────────────────────────────
            { "pushdatatosap01stock",       () => new PushDataToSap1StockHandler() },
            { "zhwm_store_pushdatasap_1stock", () => new PushDataToSap1StockHandler() },
            { "zwm_store_pushdatatosap_1dis",  () => new PushDataToSap1DisHandler() },
            { "zwm_store_pushdatatosap_1total",() => new PushDataToSap1TotalHandler() },

            // ── Store: SDC Putaway ──────────────────────────────────────────
            { "zrfc_sdc_put31",            () => new SdcPut31Handler() },
            { "zrfc_sdc_put31_bin_validation", () => new SdcPut31BinValHandler() },
            { "zwm_huput31_save",          () => new HuPut31SaveHandler() },

            // ── Store: STO / Misc ───────────────────────────────────────────
            { "zwm_get_sto_data",          () => new GetStoDataHandler() },
            { "zwm_get_grc_bins",          () => new GetGrcBinsHandler() },
            { "zwm_validate_dc_sloc",      () => new ValidateDcSlocHandler() },
            { "zwm_rfc_store_ean_data_stk",() => new RfcStoreEanDataStkHandler() },
            { "zwm_rfc_validate_crate",    () => new RfcValidateCrateHandler() },

            // ── DC / Warehouse: NIT ─────────────────────────────────────────
            { "nitrec",                    () => new NitRecHandler() },
            { "nitdel",                    () => new NitDelHandler() },
            { "nitupd",                    () => new NitUpdHandler() },

            // ── DC / Warehouse: Delivery ────────────────────────────────────
            { "scndelivery",               () => new ScnDeliveryHandler() },
            { "scnsel",                    () => new ScnSelHandler() },
            { "disrec",                    () => new DisRecHandler() },

            // ── DC: Stock Take ──────────────────────────────────────────────
            { "stocktakegetdetails",       () => new StockTakeGetDetailsHandler() },
            { "stocktakesavedata",         () => new StockTakeSaveDataHandler() },
            { "stockvalidatebarcode",      () => new StockValidateBarcodeHandler() },
            { "zwm_rfc_stock_take_arti_vali",  () => new StockTakeArtiValiHandler() },
            { "zwm_rfc_stock_take_bin_vali",   () => new StockTakeBinValiHandler() },
            { "zwm_rfc_stock_take_crate_vali", () => new StockTakeCrateValiHandler() },
            { "zwm_rfc_stock_take_save_v11",   () => new StockTakeSaveV11Handler() },
            { "zwm_rfc_stock_validate_v21",    () => new StockValidateV21Handler() },
            { "zwm_rfc_stock_movement_v21",    () => new StockMovementV21Handler() },
            { "zwm_rfc_store_ean_data_stk",    () => new RfcStoreEanDataStkHandler() },

            // ── DC: HU GRT ──────────────────────────────────────────────────
            { "zwm_dc_hu_grt_val",         () => new DcHuGrtValHandler() },
            { "zwm_dc_hugrt_binhu_val",    () => new DcHuGrtBinHuValHandler() },
            { "zwm_dc_hugrt_hu_val",       () => new DcHuGrtHuValHandler() },
            { "zwm_dc_hugrt_save",         () => new DcHuGrtSaveHandler() },

            // ── DC: CLA ─────────────────────────────────────────────────────
            { "zwm_cla_bin_validate",      () => new ClaBinValidateHandler() },
            { "zwm_cla_hu_validate",       () => new ClaHuValidateHandler() },
            { "zwm_cla_palette_validate",  () => new ClaPaletteValidateHandler() },
            { "zwm_cla_hu_palette_save",   () => new ClaHuPaletteSaveHandler() },
            { "zwm_cla_palette_bin_tag_save", () => new ClaPaletteBinTagSaveHandler() },

            // ── DC: Crate / Validate ────────────────────────────────────────
            { "validatecrateto",           () => new ValidateCrateToHandler() },
            { "savecrate",                 () => new SaveCrateHandler() },
            { "SaveCrate",                 () => new SaveCrateHandler() },
            { "zwm_rfc_validate_crate",    () => new RfcValidateCrateHandler() },
            { "zwm_validate_external_hu",  () => new ValidateExternalHuHandler() },

            // ── GR / TO ─────────────────────────────────────────────────────
            { "getgrdetails",              () => new GetGrDetailsHandler() },
            { "getGRDetails",              () => new GetGrDetailsHandler() },
            { "zwm_to_create_from_gr_data",() => new CreateToFromGrDataHandler() },
        };

        public static HHTBaseHandler Resolve(string opcode)
        {
            if (_map.TryGetValue(opcode, out var factory))
                return factory();
            return null;
        }

        /// <summary>Returns all registered opcodes — useful for diagnostics endpoint.</summary>
        public static IEnumerable<string> AllOpcodes() => _map.Keys;
    }
}
