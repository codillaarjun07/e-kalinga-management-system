using MySqlConnector;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    public class SettingsRepository
    {
        private readonly AuditLogsService _auditLogsService = new();

        public List<SettingOptionRecord> GetAll(string tableName)
        {
            tableName = NormalizeTableName(tableName);

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
            tableName = NormalizeTableName(tableName);

            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
INSERT INTO {tableName} (name, is_active)
VALUES (@name, @is_active);
SELECT LAST_INSERT_ID();";

            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@is_active", isActive);

            var id = Convert.ToInt32(cmd.ExecuteScalar());

            var actor = string.IsNullOrWhiteSpace(SessionService.Username)
                ? "Unknown"
                : SessionService.Username!;

            _auditLogsService.AddLog(
                operationType: "CREATE",
                tableName: tableName,
                recordId: id.ToString(),
                actorName: actor,
                description: $"Created setting option '{name}' in '{tableName}' with status '{(isActive ? "Active" : "Inactive")}'."
            );

            return id;
        }

        public void Update(string tableName, int id, string name, bool isActive)
        {
            tableName = NormalizeTableName(tableName);
            var existing = GetById(tableName, id);

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

            var actor = string.IsNullOrWhiteSpace(SessionService.Username)
                ? "Unknown"
                : SessionService.Username!;

            _auditLogsService.AddLog(
                operationType: "UPDATE",
                tableName: tableName,
                recordId: id.ToString(),
                actorName: actor,
                description: $"Updated setting option in '{tableName}' from '{existing?.Name ?? "(unknown)"}' to '{name}' with status '{(isActive ? "Active" : "Inactive")}'."
            );
        }

        public void Delete(string tableName, int id)
        {
            tableName = NormalizeTableName(tableName);
            var existing = GetById(tableName, id);

            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM {tableName} WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            var actor = string.IsNullOrWhiteSpace(SessionService.Username)
                ? "Unknown"
                : SessionService.Username!;

            _auditLogsService.AddLog(
                operationType: "DELETE",
                tableName: tableName,
                recordId: id.ToString(),
                actorName: actor,
                description: $"Deleted setting option '{existing?.Name ?? "(unknown)"}' from '{tableName}'."
            );
        }

        public bool NameExists(string tableName, string name, int? ignoreId = null)
        {
            tableName = NormalizeTableName(tableName);

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

        private SettingOptionRecord? GetById(string tableName, int id)
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
SELECT id, name, is_active
FROM {tableName}
WHERE id = @id
LIMIT 1;";

            cmd.Parameters.AddWithValue("@id", id);

            using var r = cmd.ExecuteReader();
            if (!r.Read())
                return null;

            return new SettingOptionRecord
            {
                Id = Convert.ToInt32(r["id"]),
                Name = Convert.ToString(r["name"]) ?? "",
                IsActive = r["is_active"] != DBNull.Value && Convert.ToBoolean(r["is_active"])
            };
        }

        private static string NormalizeTableName(string tableName)
        {
            return (tableName ?? "").Trim() switch
            {
                "departments" => "departments",
                "source_of_funds" => "source_of_funds",
                "companies" => "companies",
                "roles" => "roles",
                "classifications" => "classifications",
                _ => throw new ArgumentException("Invalid settings table name.", nameof(tableName))
            };
        }
    }
}