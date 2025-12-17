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
        public static extern void StartComplexScan();

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

        private bool _isPlcConnected = false;
        private System.Windows.Threading.DispatcherTimer _statusTimer;
        private CameraHost? _cameraHost;

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
                bool connected = GetIsConnected();
                SetPlcStatus(connected);

                if (connected)
                {
                    // Update machine status from D0
                    int status = GetLastPlcValue();
                    MachineStatusText.Text = GetStatusString(status);
                    
                    // Optional: Color coding based on status
                    if (status == 3) // Error
                        MachineStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                    else if (status == 1) // Running
                        MachineStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green
                    else
                        MachineStatusText.Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)); // Default White
                }
                else
                {
                    MachineStatusText.Text = "Offline";
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

        // ---- Navigation handlers ----

        private void MachineStatusButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new MachineStatusWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void StartScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPlcConnected)
            {
                NotificationService.ShowWarning("PLC is not connected. Please check settings.");
                return;
            }

            // Call Native DLL to start complex scan
            try
            {
                StartComplexScan();

                // Log to database
                var scanService = new ScanService();
                scanService.SaveScan(new ScanRecord
                {
                    Timestamp = DateTime.Now,
                    InitiatedBy = AuthService.CurrentUser ?? "Unknown",
                    Status = "Initiated",
                    ResultCode = "PENDING"
                });

                NotificationService.ShowSuccess("Scan started successfully.");
                Logger.LogInformation($"Scan started by {AuthService.CurrentUser}");
            }
            catch (Exception ex)
            {
                 NotificationService.ShowError("Failed to start scan (Native Module Error).");
                 Logger.LogError("Error calling native StartScan", ex);
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
                NotificationService.ShowInfo($"PLC settings saved: {plcSettingsWindow.PlcIpAddress}:{plcSettingsWindow.PlcPort}");
                Logger.LogInformation($"PLC settings updated to {plcSettingsWindow.PlcIpAddress}:{plcSettingsWindow.PlcPort}");

                // Reconnect to PLC with new settings
                try
                {
                    bool connected = ConnectPlc(plcSettingsWindow.PlcIpAddress, plcSettingsWindow.PlcPort);
                    SetPlcStatus(connected);
                    
                    if (connected)
                    {
                        NotificationService.ShowSuccess("Successfully connected to PLC.");
                    }
                    else
                    {
                        NotificationService.ShowError("Failed to connect to PLC. Check settings and network.");
                        Logger.LogWarning("Failed to connect to PLC after settings update.");
                    }
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
    }
}
