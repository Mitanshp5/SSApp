using System.Windows;
using System.Windows.Media;
using SSApp.Services;
using System.Windows.Input;
using SSApp.Data.Models;
using System.Runtime.InteropServices;


namespace SSApp.UI
{
    public partial class DashboardWindow : Window
    {
        [DllImport("SSApp.Native.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void StartScanNative(string ipAddress, int port);

        private bool _isPlcConnected = false;

        public DashboardWindow()
        {
            InitializeComponent();

            // Basic display of user + role
            UserInfoText.Text = $"Logged in as {AuthService.CurrentUser ?? "Unknown"}";
            RoleInfoText.Text = $"Role: {AuthService.CurrentRole}";

            // Placeholder status values – you’ll wire PLC + machine later
            SetPlcStatus(isConnected: false);
            MachineStatusText.Text = "System OK (stub)";
            // Apply role-based visibility
            ApplyRolePermissions();
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
                MessageBox.Show("PLC is not connected",
                    "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get Config
            var plcService = new PlcConfigService();
            var config = plcService.GetPlcConfig();

            // Call Native DLL to start scan (fire and forget thread)
            try
            {
                StartScanNative(config.IpAddress, config.Port);
                MessageBox.Show("Scan started successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                 MessageBox.Show($"Error calling native module: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"PLC settings saved:\nIP: {plcSettingsWindow.PlcIpAddress}\nPort: {plcSettingsWindow.PlcPort}",
                    "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);

                // TODO: Reconnect to PLC with new settings
            }
        }
    }
}
