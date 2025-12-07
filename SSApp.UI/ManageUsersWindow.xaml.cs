using SSApp.Data;
using SSApp.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace SSApp.UI
{
    public partial class ManageUsersWindow : Window
    {
        private ObservableCollection<UserDto> _users = new ObservableCollection<UserDto>();

        public ManageUsersWindow()
        {
            InitializeComponent();
            LoadUsers();
        }

        private void LoadUsers()
        {
            _users = new ObservableCollection<UserDto>(UserService.GetAllUsers());
            UsersGrid.ItemsSource = _users;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            _users.Add(new UserDto
            {
                Id = 0,
                Username = "newuser",
                Password = "",
                Role = UserRole.Viewer   // ✅ pick default role
            });                                                  
        }

        private void DeleteSelectedUser()
        {
            if (UsersGrid.SelectedItem is not UserDto user)
                return;

            if (MessageBox.Show($"Delete user '{user.Username}'?",
                    "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes)
                return;

            if (user.Id != 0)
            {
                bool ok = UserService.DeleteUser(user.Id);
                if (!ok)
                {
                    MessageBox.Show("Cannot delete the last admin or access denied.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            _users.Remove(user);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedUser();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Save existing users
            foreach (var user in _users.Where(u => u.Id != 0))
            {
                bool ok = UserService.UpdateUser(user);
                if (!ok)
                {
                    MessageBox.Show(
                        $"Failed to update user '{user.Username}'. Username may already exist.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Save newly added users (Id == 0)
            foreach (var user in _users.Where(u => u.Id == 0).ToList())
            {
                bool ok = UserService.CreateUser(user.Username, user.Password, user.Role);    
                if (!ok)
                {
                    MessageBox.Show(
                        $"Failed to create user '{user.Username}'. Username may already exist.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            MessageBox.Show("Changes saved.", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);

            // reload to get real IDs for new users
            LoadUsers();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void UsersGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                e.Handled = true;      
                DeleteSelectedUser();  
            }
        }
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
