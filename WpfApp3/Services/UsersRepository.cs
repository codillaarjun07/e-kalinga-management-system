using MySqlConnector;

namespace WpfApp3.Services
{
    public class UsersRepository
    {
        public List<UserRow> GetAll()
        {
            using var conn = MySqlDb.OpenConnection();

            const string sql = @"
SELECT id, first_name, last_name, office, role, username, is_active
FROM users
ORDER BY id DESC;";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            var list = new List<UserRow>();
            while (reader.Read())
            {
                list.Add(new UserRow
                {
                    Id = reader.GetInt32("id"),
                    FirstName = reader.GetString("first_name"),
                    LastName = reader.GetString("last_name"),
                    Office = reader.IsDBNull(reader.GetOrdinal("office")) ? "" : reader.GetString("office"),
                    Role = reader.GetString("role"),
                    Username = reader.GetString("username"),
                    IsActive = reader.GetInt32("is_active") == 1
                });
            }

            return list;
        }

        public int Create(string firstName, string lastName, string? office, string role, string username, string passwordPlain)
        {
            using var conn = MySqlDb.OpenConnection();

            var hash = BCrypt.Net.BCrypt.HashPassword(passwordPlain);

            const string sql = @"
INSERT INTO users (first_name, last_name, office, role, username, password_hash, is_active)
VALUES (@first_name, @last_name, @office, @role, @username, @password_hash, 1);
SELECT LAST_INSERT_ID();";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@first_name", firstName);
            cmd.Parameters.AddWithValue("@last_name", lastName);
            cmd.Parameters.AddWithValue("@office", string.IsNullOrWhiteSpace(office) ? null : office);
            cmd.Parameters.AddWithValue("@role", role);
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@password_hash", hash);

            var idObj = cmd.ExecuteScalar();
            return Convert.ToInt32(idObj);
        }

        public void Update(int id, string firstName, string lastName, string? office, string role, string username, string? newPasswordPlainOrNull)
        {
            using var conn = MySqlDb.OpenConnection();

            if (!string.IsNullOrWhiteSpace(newPasswordPlainOrNull))
            {
                var hash = BCrypt.Net.BCrypt.HashPassword(newPasswordPlainOrNull);

                const string sql = @"
UPDATE users
SET first_name=@first_name,
    last_name=@last_name,
    office=@office,
    role=@role,
    username=@username,
    password_hash=@password_hash
WHERE id=@id;";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@first_name", firstName);
                cmd.Parameters.AddWithValue("@last_name", lastName);
                cmd.Parameters.AddWithValue("@office", string.IsNullOrWhiteSpace(office) ? null : office);
                cmd.Parameters.AddWithValue("@role", role);
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@password_hash", hash);
                cmd.ExecuteNonQuery();
            }
            else
            {
                const string sql = @"
UPDATE users
SET first_name=@first_name,
    last_name=@last_name,
    office=@office,
    role=@role,
    username=@username
WHERE id=@id;";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@first_name", firstName);
                cmd.Parameters.AddWithValue("@last_name", lastName);
                cmd.Parameters.AddWithValue("@office", string.IsNullOrWhiteSpace(office) ? null : office);
                cmd.Parameters.AddWithValue("@role", role);
                cmd.Parameters.AddWithValue("@username", username);
                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int id)
        {
            using var conn = MySqlDb.OpenConnection();

            const string sql = @"DELETE FROM users WHERE id=@id;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public bool UsernameExists(string username, int? ignoreUserId = null)
        {
            using var conn = MySqlDb.OpenConnection();

            const string sql = @"
SELECT COUNT(*) 
FROM users 
WHERE username=@username
  AND (@ignoreId IS NULL OR id <> @ignoreId);";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@ignoreId", ignoreUserId.HasValue ? ignoreUserId.Value : (object?)DBNull.Value);

            var count = Convert.ToInt32(cmd.ExecuteScalar());
            return count > 0;
        }
    }

    public class UserRow
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Office { get; set; } = "";
        public string Role { get; set; } = "";
        public string Username { get; set; } = "";
        public bool IsActive { get; set; }
    }
}
