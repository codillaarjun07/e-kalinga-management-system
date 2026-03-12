using MySqlConnector;

namespace WpfApp3.Services
{
    public class UsersRepository
    {
        public List<UserRow> GetAll()
        {
            using var conn = MySqlDb.OpenConnection();

            const string sql = @"
SELECT id, first_name, last_name, office, role, username, is_active, profile_picture
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
                    Role = reader.IsDBNull(reader.GetOrdinal("role")) ? "" : reader.GetString("role"),
                    Username = reader.GetString("username"),
                    IsActive = reader.GetInt32("is_active") == 1,
                    ProfilePicture = reader.IsDBNull(reader.GetOrdinal("profile_picture"))
                        ? null
                        : (byte[])reader["profile_picture"]
                });
            }

            return list;
        }

        public List<string> GetDepartments()
        {
            using var conn = MySqlDb.OpenConnection();

            const string sql = @"
SELECT name
FROM departments
WHERE is_active = 1
ORDER BY name ASC;";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            var list = new List<string>();
            while (reader.Read())
            {
                list.Add(reader.GetString("name"));
            }

            return list;
        }

        public List<string> GetRoles()
        {
            using var conn = MySqlDb.OpenConnection();

            const string sql = @"
SELECT name
FROM roles
WHERE is_active = 1
ORDER BY name ASC;";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            var list = new List<string>();
            while (reader.Read())
            {
                list.Add(reader.GetString("name"));
            }

            return list;
        }

        public int Create(
            string firstName,
            string lastName,
            string? office,
            string role,
            string username,
            string passwordPlain,
            byte[]? profilePicture)
        {
            using var conn = MySqlDb.OpenConnection();

            var hash = BCrypt.Net.BCrypt.HashPassword(passwordPlain);

            const string sql = @"
INSERT INTO users (first_name, last_name, office, role, username, password_hash, profile_picture, is_active)
VALUES (@first_name, @last_name, @office, @role, @username, @password_hash, @profile_picture, 1);
SELECT LAST_INSERT_ID();";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@first_name", firstName);
            cmd.Parameters.AddWithValue("@last_name", lastName);
            cmd.Parameters.AddWithValue("@office", string.IsNullOrWhiteSpace(office) ? null : office);
            cmd.Parameters.AddWithValue("@role", role);
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@password_hash", hash);
            cmd.Parameters.Add("@profile_picture", MySqlDbType.LongBlob).Value =
                profilePicture is null || profilePicture.Length == 0 ? DBNull.Value : profilePicture;

            var idObj = cmd.ExecuteScalar();
            return Convert.ToInt32(idObj);
        }

        public void Update(
            int id,
            string firstName,
            string lastName,
            string? office,
            string role,
            string username,
            string? newPasswordPlainOrNull,
            byte[]? profilePicture)
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
    password_hash=@password_hash,
    profile_picture=@profile_picture
WHERE id=@id;";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@first_name", firstName);
                cmd.Parameters.AddWithValue("@last_name", lastName);
                cmd.Parameters.AddWithValue("@office", string.IsNullOrWhiteSpace(office) ? null : office);
                cmd.Parameters.AddWithValue("@role", role);
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@password_hash", hash);
                cmd.Parameters.Add("@profile_picture", MySqlDbType.LongBlob).Value =
                    profilePicture is null || profilePicture.Length == 0 ? DBNull.Value : profilePicture;
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
    username=@username,
    profile_picture=@profile_picture
WHERE id=@id;";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@first_name", firstName);
                cmd.Parameters.AddWithValue("@last_name", lastName);
                cmd.Parameters.AddWithValue("@office", string.IsNullOrWhiteSpace(office) ? null : office);
                cmd.Parameters.AddWithValue("@role", role);
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.Add("@profile_picture", MySqlDbType.LongBlob).Value =
                    profilePicture is null || profilePicture.Length == 0 ? DBNull.Value : profilePicture;
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
        public byte[]? ProfilePicture { get; set; }
    }
}