using System.Windows;
using System.Windows.Media;
using SSApp.Services;
using SSApp.Services.Notifications;
using SSApp.Services.Logging;
using System.Windows.Input;
using SSApp.Data.Models;
using System.Runtime.InteropServices;
using System.Windows.Interop;


namespace SSApp.UI
{
    public partial class DashboardWindow : Window
    {
        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void StartScanNative(string ipAddress, int port);



        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void StartLiveView(IntPtr hWnd, int deviceIndex);

        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void StopLiveView();

        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool ConnectPlc(string ipAddress, int port);

        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetLastPlcValue();

        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool GetIsConnected();

        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool GetIsCameraConnected();

        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void SetPlcBit(string device, int value);

        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool CaptureImageCustom(string filename);

        private bool _isPlcConnected = false;
        private System.Windows.Threading.DispatcherTimer _statusTimer;
        private CameraHost? _cameraHost;
        private bool _isScanRunning = false;

        public DashboardWindow()
        {
            InitializeComponent();

            this.Loaded += (s, e) => 
            {
                try {
                     _cameraHost = new CameraHost();
                     CameraContainer.Child = _cameraHost;
                     StartLiveView(_cameraHost.Handle, 0);
                } catch (Exception ex) { Logger.LogError("Camera Init Failed", ex); }
            };
            this.Closed += (s, e) => StopLiveView();

            // Basic display of user + role
            UserInfoText.Text = $"Logged in as {AuthService.CurrentUser ?? "Unknown"}";
            RoleInfoText.Text = $"Role: {AuthService.CurrentRole}";

            // Placeholder status values – you’ll wire PLC + machine later
            SetPlcStatus(isConnected: false);
            
            // Apply role-based visibility
            ApplyRolePermissions();

            // Initialize PLC Connection
            try
            {
                var plcService = new PlcConfigService();
                var config = plcService.GetPlcConfig();
                Logger.LogInformation($"Initializing PLC connection to {config.IpAddress}:{config.Port}...");
                ConnectPlc(config.IpAddress, config.Port);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize PLC connection on startup.", ex);
            }

            // Start polling timer
            _statusTimer = new System.Windows.Threading.DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromMilliseconds(500);
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();
        }

        public void RestartLiveView(int cameraIndex)
        {
            try 
            {
                StopLiveView();
                if (_cameraHost != null)
                {
                    StartLiveView(_cameraHost.Handle, cameraIndex);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to restart camera", ex);
                NotificationService.ShowError("Failed to restart camera stream.");
            }
        }

        private void StatusTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Check connection status
                bool plcConnected = GetIsConnected();
                bool cameraConnected = false;
                try { cameraConnected = GetIsCameraConnected(); } catch { }

                // Detect transition from Disconnected -> Connected
                if (plcConnected && !_isPlcConnected)
                {
                    NotificationService.ShowSuccess("PLC Connected");
                }

                SetPlcStatus(plcConnected);

                if (plcConnected)
                {
                    if (!cameraConnected)
                    {
                        MachineStatusText.Text = "Camera Disconnected";
                        MachineStatusText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Orange/Amber
                    }
                    else
                    {
                        // Update machine status from D0
                        int status = GetLastPlcValue();
                        
                        if (status == 1) // Running
                        {
                            MachineStatusText.Text = "Running";
                            MachineStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green
                        }
                        else
                        {
                            MachineStatusText.Text = "Connected";
                            MachineStatusText.Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)); // Default White
                        }
                    }
                }
                else
                {
                    MachineStatusText.Text = "Disconnected";
                    MachineStatusText.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Gray
                }
            }
            catch 
            {
                // Ignore errors during polling (e.g. DLL not loaded yet)
            }
        }

        private string GetStatusString(int code)
        {
            return code switch
            {
                0 => "Idle",
                1 => "Running",
                2 => "Paused",
                3 => "Error",
                _ => $"Code {code}"
            };
        }

        private void ApplyRolePermissions()
        {
            var role = AuthService.CurrentRole;

            // Manage Users: only Admin
            ManageUsersButton.Visibility =
                role == UserRole.Admin ? Visibility.Visible : Visibility.Collapsed;

            // Settings: Admin + Operator
            SettingsButton.Visibility =
                (role == UserRole.Admin || role == UserRole.Operator)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            // Start Scan Button (sidebar): Admin + Operator
            StartScanButton.Visibility =
                (role == UserRole.Admin || role == UserRole.Operator)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            
            // Start Scan Tile (main area): Admin + Operator
            StartScanTile.Visibility =
                (role == UserRole.Admin || role == UserRole.Operator)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            // Viewer still sees: Machine Status, Past Scans
        }

        private void SetPlcStatus(bool isConnected)
        {
            _isPlcConnected = isConnected;
            PlcStatusText.Text = isConnected ? "Connected" : "Disconnected";
            PlcStatusIndicator.Fill = isConnected
                ? new SolidColorBrush(Color.FromRgb(34, 197, 94))   // green
                : new SolidColorBrush(Color.FromRgb(248, 113, 113)); // red
        }

        private async void StartScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isScanRunning) return;

            if (!_isPlcConnected)
            {
                NotificationService.ShowWarning("PLC is not connected. Please check settings.");
                return;
            }

            bool cameraConnected = false;
            try { cameraConnected = GetIsCameraConnected(); } catch { }

            if (!cameraConnected)
            {
                NotificationService.ShowWarning("Camera is not connected. Please check connection.");
                return;
            }

            _isScanRunning = true;
            StartScanButton.IsEnabled = false;
            StartScanTile.IsEnabled = false;
            MachineStatusText.Text = "Scanning...";
            MachineStatusText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber

            try
            {
                await Task.Run(async () =>
                {
                    // Sequence: Top(2), Right(1), Bottom(8), Left(4), All(15)
                    int[] scanSequence = { 2, 1, 8, 4 };

                    foreach (int i in scanSequence)
                    {
                        bool r = (i & 1) != 0;
                        bool t = (i & 2) != 0;
                        bool l = (i & 4) != 0;
                        bool b = (i & 8) != 0;

                        // Set Lights
                        SetPlcBit("Y1", r ? 1 : 0);
                        SetPlcBit("Y3", t ? 1 : 0);
                        SetPlcBit("Y4", l ? 1 : 0);
                        SetPlcBit("Y5", b ? 1 : 0);

                        // Wait for light adjustment (max 200ms)
                        await Task.Delay(150);

                        // Generate Filename
                        string filename = "";
                        if (t) filename += "T";
                        if (r) filename += "R";
                        if (b) filename += "B";
                        if (l) filename += "L";
                        
                        // Append timestamp
                        filename += $"_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";

                        // Capture
                        bool captured = CaptureImageCustom(filename);
                        if (!captured)
                        {
                            Logger.LogError($"Failed to capture {filename}");
                        }
                    }

                    // Turn off all lights
                    SetPlcBit("Y1", 0);
                    SetPlcBit("Y3", 0);
                    SetPlcBit("Y4", 0);
                    SetPlcBit("Y5", 0);
                });

                // Log to database
                var scanService = new ScanService();
                scanService.SaveScan(new ScanRecord
                {
                    Timestamp = DateTime.Now,
                    InitiatedBy = AuthService.CurrentUser ?? "Unknown",
                    Status = "Completed",
                    ResultCode = "SUCCESS"
                });

                NotificationService.ShowSuccess("Multi-light scan completed successfully.");
                Logger.LogInformation($"Multi-light scan completed by {AuthService.CurrentUser}");
            }
            catch (Exception ex)
            {
                 NotificationService.ShowError("Error during scan process.");
                 Logger.LogError("Error in multi-light scan", ex);
                 
                 // Attempt to turn off lights on error
                 try {
                    SetPlcBit("Y1", 0);
                    SetPlcBit("Y3", 0);
                    SetPlcBit("Y4", 0);
                    SetPlcBit("Y5", 0);
                 } catch { }
            }
            finally
            {
                _isScanRunning = false;
                StartScanButton.IsEnabled = true;
                StartScanTile.IsEnabled = true;
                MachineStatusText.Text = "Connected"; // Revert status (timer will update it anyway)
            }
        }

        private void PastScansButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new PastScansWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void ManageUsersButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AuthService.CurrentUserIsAdmin)
            {
                MessageBox.Show("Only admins can manage users.",
                    "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var win = new ManageUsersWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void MachineStatusButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new MachineStatusWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        // Drag window
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Restore before dragging if maximized
                if (WindowState == WindowState.Maximized)
                    WindowState = WindowState.Normal;

                DragMove();
            }
        }

        // Minimize
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        // ✅ NEW: Maximize / Restore toggle
        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void StartScanCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            StartScanButton_Click(sender, e);
        }

        private void MachineStatusCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MachineStatusButton_Click(sender, e);
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var login = new LoginWindow();
            login.Show();
            Close();
        }

        private void PlcConnectionCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Open PLC Settings window
            var plcSettingsWindow = new PlcSettingsWindow()
            {
                Owner = this
            };

            // Set ReadOnly if not Admin
            if (AuthService.CurrentRole != UserRole.Admin)
            {
                plcSettingsWindow.SetReadOnly(true);
            }

            if (plcSettingsWindow.ShowDialog() == true)
            {
                // Settings are saved in the window via service
                Logger.LogInformation($"PLC settings updated to {plcSettingsWindow.PlcIpAddress}:{plcSettingsWindow.PlcPort}");

                // Reconnect to PLC with new settings
                try
                {
                    NotificationService.ShowInfo("Attempting to connect to PLC...");
                    ConnectPlc(plcSettingsWindow.PlcIpAddress, plcSettingsWindow.PlcPort);
                }
                catch (Exception ex)
                {
                    NotificationService.ShowError($"Error connecting to native module: {ex.Message}");
                    Logger.LogError("Native module error during connection", ex);
                    SetPlcStatus(false);
                }
            }
        }
    }

    public class CameraHost : HwndHost
    {
        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            var hwnd = CreateWindowEx(0, "static", "",
                WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN,
                0, 0, (int)Width, (int)Height,
                hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            return new HandleRef(this, hwnd);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            DestroyWindow(hwnd.Handle);
        }

        protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_SIZE = 0x0005;
            if (msg == WM_SIZE)
            {
                int width = (int)(lParam.ToInt64() & 0xFFFF);
                int height = (int)((lParam.ToInt64() >> 16) & 0xFFFF);
                
                // Create rounded region matching CornerRadius="12" (approx 24px diameter)
                IntPtr hRgn = CreateRoundRectRgn(0, 0, width, height, 24, 24);
                SetWindowRgn(hwnd, hRgn, true);
            }

            return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
        }

        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_CLIPCHILDREN = 0x02000000;

        [DllImport("user32.dll", EntryPoint = "CreateWindowEx", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(int dwExStyle, string lpszClassName,
                                                      string lpszWindowName, int style,
                                                      int x, int y, int width, int height,
                                                      IntPtr hwndParent, IntPtr hMenu,
                                                      IntPtr hInst, IntPtr lpParam);

        [DllImport("user32.dll", EntryPoint = "DestroyWindow", CharSet = CharSet.Unicode)]
        private static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
    }
}
