using SSApp.Data;
using SSApp.Data.Models;

namespace SSApp.Services
{
    public static class AuthService
    {
        public static string? CurrentUser { get; private set; }
        public static UserRole CurrentRole { get; private set; }

        public static bool CurrentUserIsAdmin => CurrentRole == UserRole.Admin;

        public static bool Login(string username, string password)
        {
            var (ok, role) = Database.ValidateUser(username, password);
            if (ok)
            {
                CurrentUser = username;
                CurrentRole = role;
            }
            else
            {
                CurrentUser = null;
                CurrentRole = UserRole.Viewer;
            }
            return ok;
        }

        public static bool ValidateAdmin(string username, string password)
        {
            var (ok, role) = Database.ValidateUser(username, password);
            return ok && role == UserRole.Admin;
        }
    }
}
