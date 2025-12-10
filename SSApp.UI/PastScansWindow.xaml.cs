using System.Windows;
using System.Windows.Input;
using SSApp.Services;

namespace SSApp.UI
{
    public partial class PastScansWindow : Window
    {
        private readonly ScanService _scanService;

        public PastScansWindow()
        {
            InitializeComponent();
            _scanService = new ScanService();
            LoadData();
        }

        private void LoadData()
        {
            var history = _scanService.GetHistory();
            ScansGrid.ItemsSource = history;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}