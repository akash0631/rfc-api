using System;

namespace Vendor_SRM_Routing_Application.Models.RfcSync
{
    public class RfcUploadMetadata
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string OriginalFileName { get; set; }
        public string FilePath { get; set; }
        public long FileSizeBytes { get; set; }
        public string ContentType { get; set; }
        public string UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }
        public string Status { get; set; }
        public string Remarks { get; set; }
    }
}
