using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Vendor_SRM_Routing_Application.Models.RfcSync
{
    // ─── Enums ──────────────────────────────────────────────────────────────────
    public enum SyncType
    {
        PARTICULAR_DATE,
        DATE_RANGE
    }

    // ─── RFC Document Row (parsed from uploaded file) ──────────────────────────
    public class RfcDataRow
    {
        public DateTime  RecordDate  { get; set; }
        public string    RfcName     { get; set; }
        public string    FunctionModule { get; set; }
        public string    Description { get; set; }
        public string    Status      { get; set; }
        public string    Category    { get; set; }
        public string    Parameters  { get; set; }
        public string    CreatedBy   { get; set; }
        public string    Remarks     { get; set; }
    }

    // ─── Upload API ──────────────────────────────────────────────────────────────
    public class RfcUploadResponse
    {
        public bool   Success    { get; set; }
        public string Message    { get; set; }
        public int    UploadId   { get; set; }
        public string FileName   { get; set; }
        public long   FileSizeBytes { get; set; }
        public DateTime UploadedAt { get; set; }
        public string Status     { get; set; }
    }

    // ─── Process / Parse API ─────────────────────────────────────────────────────
    public class RfcProcessRequest
    {
        [Required]
        public int UploadId { get; set; }
    }

    public class RfcProcessResponse
    {
        public bool   Success    { get; set; }
        public string Message    { get; set; }
        public int    UploadId   { get; set; }
        public int    TotalRows  { get; set; }
        public List<RfcDataRow> Data { get; set; }
    }

    // ─── Table Management API ─────────────────────────────────────────────────────
    public class RfcTableRequest
    {
        [Required]
        public string TableName { get; set; }
    }

    public class RfcTableResponse
    {
        public bool   Success      { get; set; }
        public string Message      { get; set; }
        public string TableName    { get; set; }
        public bool   AlreadyExisted { get; set; }
        public bool   Created      { get; set; }
    }

    // ─── Data Check API ───────────────────────────────────────────────────────────
    public class RfcDataCheckRequest
    {
        [Required]
        public string   TableName { get; set; }
        [Required]
        public SyncType SyncType  { get; set; }
        public DateTime? Date     { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate   { get; set; }
    }

    public class RfcDataCheckResponse
    {
        public bool   Success      { get; set; }
        public string Message      { get; set; }
        public string TableName    { get; set; }
        public bool   RecordsExist { get; set; }
        public int    RecordCount  { get; set; }
        public string DateFilter   { get; set; }
    }

    // ─── Sync API ─────────────────────────────────────────────────────────────────
    public class RfcSyncRequest
    {
        [Required]
        public string   TableName { get; set; }
        [Required]
        public int      UploadId  { get; set; }
        [Required]
        public SyncType SyncType  { get; set; }
        public DateTime? Date     { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate   { get; set; }
    }

    public class RfcSyncResponse
    {
        public bool   Success          { get; set; }
        public string Message          { get; set; }
        public string TableName        { get; set; }
        public bool   TableCreated     { get; set; }
        public bool   TableAlreadyExisted { get; set; }
        public int    ExistingRowsFound { get; set; }
        public int    DeletedRowsCount { get; set; }
        public int    InsertedRowsCount { get; set; }
        public string RequestedDate    { get; set; }
        public string Status           { get; set; }
        public DateTime SyncedAt       { get; set; }
    }
}
