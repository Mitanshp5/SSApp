using System;

namespace SSApp.Data.Models
{
    public class ScanRecord
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string InitiatedBy { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // e.g., "Completed", "Failed"
        public string ResultCode { get; set; } = string.Empty; // e.g., "OK", "NG"
    }
}
