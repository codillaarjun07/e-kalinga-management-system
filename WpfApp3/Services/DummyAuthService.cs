using System.Collections.Generic;
using System.Linq;

namespace WpfApp3.Services
{
    public class DummyAuthService
    {
        // POC dummy data (mimic DB rows)
        private readonly List<UserRow> _users = new()
        {
            new UserRow { Username = "admin",   Password = "password123", IsActive = true,  Role = "Administrator" },
            new UserRow { Username = "encoder", Password = "encoder123",  IsActive = true,  Role = "Encoder" },
            new UserRow { Username = "disabled",Password = "test123",     IsActive = false, Role = "User" }
        };

        public LoginResult TryLogin(string username, string password)
        {
            var user = _users.FirstOrDefault(u => u.Username == username);

            if (user is null || user.Password != password)
                return LoginResult.Fail("Invalid username or password.");

            if (!user.IsActive)
                return LoginResult.Fail("Your account is disabled. Please contact the administrator.");

            return LoginResult.Ok(user.Role);
        }

        private class UserRow
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = ""; // POC ONLY (later: hash)
            public bool IsActive { get; set; }
            public string Role { get; set; } = "";
        }
    }
}
