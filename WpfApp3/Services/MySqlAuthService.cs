using MySqlConnector;

namespace WpfApp3.Services
{
    public class MySqlAuthService
    {
        public LoginResult TryLogin(string username, string password)
        {
            using var conn = MySqlDb.OpenConnection();

            const string sql = @"
SELECT id, username, password_hash, role, is_active
FROM users
WHERE username = @username
LIMIT 1;";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@username", username);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return LoginResult.Fail("Invalid username or password.");

            var isActive = reader.GetInt32("is_active") == 1;
            if (!isActive)
                return LoginResult.Fail("Your account is disabled. Please contact the administrator.");

            var hash = reader.GetString("password_hash");
            var role = reader.GetString("role");

            // BCrypt verify
            var ok = BCrypt.Net.BCrypt.Verify(password, hash);
            if (!ok)
                return LoginResult.Fail("Invalid username or password.");

            return LoginResult.Ok(role);
        }
    }
}
