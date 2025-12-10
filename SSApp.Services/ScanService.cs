using SSApp.Data;
using SSApp.Data.Models;
using System.Collections.Generic;

namespace SSApp.Services
{
    public class ScanService
    {
        public List<ScanRecord> GetHistory()
        {
            return Database.GetScanResults();
        }

        public void SaveScan(ScanRecord record)
        {
            Database.AddScanResult(record);
        }
    }
}
