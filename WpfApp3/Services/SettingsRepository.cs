using MySqlConnector;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    public class SettingsRepository
    {
        public List<SettingOptionRecord> GetAll(string tableName)
        {
            var list = new List<SettingOptionRecord>();

            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT id, name, is_active FROM {tableName} ORDER BY id DESC";

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new SettingOptionRecord
                {
                    Id = Convert.ToInt32(r["id"]),
                    Name = Convert.ToString(r["name"]) ?? "",
                    IsActive = r["is_active"] != DBNull.Value && Convert.ToBoolean(r["is_active"])
                });
            }

            return list;
        }

        public int Create(string tableName, string name, bool isActive)
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
INSERT INTO {tableName} (name, is_active)
VALUES (@name, @is_active);
SELECT LAST_INSERT_ID();";

            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@is_active", isActive);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void Update(string tableName, int id, string name, bool isActive)
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
UPDATE {tableName}
SET name = @name,
    is_active = @is_active
WHERE id = @id;";

            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@is_active", isActive);

            cmd.ExecuteNonQuery();
        }

        public void Delete(string tableName, int id)
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM {tableName} WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public bool NameExists(string tableName, string name, int? ignoreId = null)
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
SELECT COUNT(*)
FROM {tableName}
WHERE LOWER(name) = LOWER(@name)
  AND (@ignoreId IS NULL OR id <> @ignoreId);";

            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@ignoreId", ignoreId.HasValue ? ignoreId.Value : DBNull.Value);

            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }
}