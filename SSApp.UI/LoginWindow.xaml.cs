using System.Windows;
using SSApp.Services;   // ✅ Use services for login
using SSApp.Data;       // ✅ Only for Database.Initialize()
using System.Windows.Input;

namespace SSApp.UI
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            Database.Initialize(); // you already had this
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameBox.Text.Trim();
            var password = PasswordBox.Password;

            bool ok = AuthService.Login(username, password);
            if (!ok)
            {
                MessageBox.Show("Invalid username or password",
                    "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // ✅ Successful login → open dashboard
            var dashboard = new DashboardWindow();
            dashboard.Show();
            this.Close();
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
