using System.Configuration;
using System.Data;
using System.Windows;
using System.Runtime.InteropServices;
using SSApp.Services.Logging;

namespace SSApp.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void DisconnectPlc();

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                Logger.LogInformation("Application exiting. Disconnecting PLC...");
                DisconnectPlc();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during application shutdown", ex);
            }

            base.OnExit(e);
        }
    }
}