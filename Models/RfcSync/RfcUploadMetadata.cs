using System;

namespace Vendor_SRM_Routing_Application.Models.RfcSync
{
    public class RfcUploadMetadata
    {
        public int    Id               { get; set; }
        public string ApiName          { get; set; }   // SAP RFC / API name this doc belongs to
        public string FileName         { get; set; }   // server-generated safe file name
        public string OriginalFileName { get; set; }   // original upload name
        public string FilePath         { get; set; }   // server-side physical path
        public long   FileSizeBytes    { get; set; }
        public string ContentType      { get; set; }
        public string UploadedBy       { get; set; }
        public DateTime UploadedAt     { get; set; }
        public string Status           { get; set; }   // UPLOADED / PROCESSED / FAILED
        public string Remarks          { get; set; }
    }
}
