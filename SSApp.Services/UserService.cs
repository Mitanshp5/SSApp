using System.Collections.Generic;
using System.Linq;
using SSApp.Data;

namespace SSApp.Services
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string Password { get; set; } = ""; // plain for input only
        public UserRole Role { get; set; }
        public bool IsAdmin => Role == UserRole.Admin;
    }

    public static class UserService
    {
        private static bool IsCurrentUserAdmin()
        {
            return AuthService.CurrentUserIsAdmin;
        }

        // --------- CRUD with security & last-admin protection ---------

        public static bool CreateUser(string username, string password, UserRole role)
        {
            if (!IsCurrentUserAdmin())
                return false;

            return Database.AddUser(username, password, role);
        }

        public static List<UserDto> GetAllUsers()
        {
            if (!IsCurrentUserAdmin())
                return new List<UserDto>();

            return Database.GetAllUsers()
                           .Select(u => new UserDto
                           {
                               Id = u.Id,
                               Username = u.Username,
                               Role = u.Role
                           })
                           .ToList();
        }

        public static bool UpdateUser(UserDto user)
        {
            if (!IsCurrentUserAdmin())
                return false;

            var existing = Database.GetUserById(user.Id);
            if (existing == null) return false;

            // If changing an admin to non-admin, ensure not last admin
            bool wasAdmin = existing.Role == UserRole.Admin;
            bool willBeAdmin = user.Role == UserRole.Admin;

            if (wasAdmin && !willBeAdmin)
            {
                int adminCount = Database.CountAdmins();
                if (adminCount <= 1)
                {
                    // would remove last admin role
                    return false;
                }
            }

            return Database.UpdateUser(user.Id, user.Username, user.Password, user.Role);
        }

        public static bool DeleteUser(int id)
        {
            if (!IsCurrentUserAdmin())
                return false;

            var user = Database.GetUserById(id);
            if (user == null)
                return false;

            if (user.Role == UserRole.Admin)
            {
                int admins = Database.CountAdmins();
                if (admins <= 1)
                {
                    // cannot delete last admin
                    return false;
                }
            }

            Database.DeleteUser(id);
            return true;
        }
    }
}
