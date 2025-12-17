using System.Windows;
using System.Runtime.InteropServices;
using System.Text;
using SSApp.Services.Notifications;

namespace SSApp.UI
{
    public partial class MachineStatusWindow : Window
    {
        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetCameraCount();

        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool GetCameraName(int index, StringBuilder nameBuffer, int bufferSize);

        public MachineStatusWindow()
        {
            InitializeComponent();
            LoadCameras();
        }

        private void LoadCameras()
        {
            try 
            {
                CameraList.Items.Clear();
                int count = GetCameraCount();
                
                if (count == 0)
                {
                    CameraList.Items.Add("No cameras found");
                    CameraList.SelectedIndex = 0;
                    CameraList.IsEnabled = false;
                    return;
                }

                CameraList.IsEnabled = true;
                StringBuilder sb = new StringBuilder(256);
                
                for (int i = 0; i < count; i++)
                {
                    if (GetCameraName(i, sb, sb.Capacity))
                    {
                        CameraList.Items.Add($"{i}: {sb.ToString()}");
                    }
                    else
                    {
                         CameraList.Items.Add($"{i}: Unknown Device");
                    }
                }

                if (CameraList.Items.Count > 0)
                    CameraList.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error listing cameras: " + ex.Message);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadCameras();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (CameraList.SelectedIndex < 0 || !CameraList.IsEnabled) return;

            // Parse index from "0: Name"
            string selected = CameraList.SelectedItem.ToString();
            int index = 0;
            try 
            {
                string indexStr = selected.Split(':')[0];
                index = int.Parse(indexStr);
            } 
            catch { return; }

            if (this.Owner is DashboardWindow dashboard)
            {
                dashboard.RestartLiveView(index);
                NotificationService.ShowSuccess($"Switched to camera {index}");
                this.Close();
            }
        }
    }
}
