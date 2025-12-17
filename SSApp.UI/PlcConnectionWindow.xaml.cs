using System.Windows;
using System.Windows.Input;
using SSApp.Services;

namespace SSApp.UI
{
    public partial class PlcSettingsWindow : Window
    {
        private readonly PlcConfigService _plcConfigService;

        public string PlcIpAddress { get; private set; } = string.Empty;
        public int PlcPort { get; private set; }

        public PlcSettingsWindow()
        {
            InitializeComponent();
            _plcConfigService = new PlcConfigService();
            LoadConfig();
        }

        public void SetReadOnly(bool isReadOnly)
        {
            if (isReadOnly)
            {
                IpAddressTextBox.IsEnabled = false;
                PortTextBox.IsEnabled = false;
                SaveButton.Visibility = Visibility.Collapsed;
                Title = "PLC Settings (Read Only)";
            }
            else
            {
                IpAddressTextBox.IsEnabled = true;
                PortTextBox.IsEnabled = true;
                SaveButton.Visibility = Visibility.Visible;
                Title = "PLC Settings";
            }
        }

        private void LoadConfig()
        {
            var config = _plcConfigService.GetPlcConfig();
            PlcIpAddress = config.IpAddress;
            PlcPort = config.Port;

            IpAddressTextBox.Text = PlcIpAddress;
            PortTextBox.Text = PlcPort.ToString();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate IP address
            string ip = IpAddressTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(ip))
            {
                MessageBox.Show("Please enter a valid IP address.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate port
            if (!int.TryParse(PortTextBox.Text.Trim(), out int port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("Please enter a valid port number (1-65535).", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Save values
            PlcIpAddress = ip;
            PlcPort = port;

            if (_plcConfigService.UpdatePlcConfig(ip, port))
            {
                DialogResult = true;
                Close();
            }
            else
            {
                 MessageBox.Show("Failed to save configuration to database.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}