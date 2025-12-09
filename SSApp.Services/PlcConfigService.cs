using SSApp.Data;
using SSApp.Data.Models;
using System;

namespace SSApp.Services
{
    public class PlcConfigService
    {
        public PlcConfigService()
        {
        }

        /// <summary>
        /// Get the current PLC configuration from database
        /// </summary>
        public PlcConfig GetPlcConfig()
        {
            return Database.GetPlcConfig();
        }

        /// <summary>
        /// Update the PLC configuration in database
        /// </summary>
        public bool UpdatePlcConfig(string ipAddress, int port)
        {
            try
            {
                Database.SavePlcConfig(ipAddress, port);
                return true;
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Error updating PLC config: {ex.Message}");
                return false;
            }
        }
    }
}