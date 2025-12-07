using System.Windows;
using System.Windows.Media;
using SSApp.Services;
using SSApp.Data;
using System.Windows.Input;


namespace SSApp.UI
{
    public partial class DashboardWindow : Window
    {
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

            // Start Scan: Admin + Operator
            StartScanButton.Visibility =
                (role == UserRole.Admin || role == UserRole.Operator)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            // Viewer still sees: Machine Status, Past Scans
        }

        private void SetPlcStatus(bool isConnected)
        {
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
            // For now just a placeholder – you’ll call C++/PLC later.
            MessageBox.Show("Start Scan – not implemented yet.",
                "Info", MessageBoxButton.OK, MessageBoxImage.Information);
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
    }
}
