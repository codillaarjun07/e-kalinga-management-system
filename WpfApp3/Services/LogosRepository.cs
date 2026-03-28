using System;
using System.Collections.Generic;
using MySqlConnector;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    public class LogosRepository
    {
        private readonly AuditLogsService _auditLogService = new();

        public void EnsureTable()
        {
            using var conn = MySqlDb.OpenConnection();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS system_logos (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    content_type VARCHAR(100) NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    image_data LONGBLOB NOT NULL,
    is_active BIT NOT NULL DEFAULT b'0',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
            cmd.ExecuteNonQuery();
        }

        public List<LogoRecord> GetAll()
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT
    id,
    name,
    file_name,
    content_type,
    file_size_bytes,
    image_data,
    is_active,
    created_at
FROM system_logos
ORDER BY is_active DESC, created_at DESC, id DESC;";

            var list = new List<LogoRecord>();

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new LogoRecord
                {
                    Id = Convert.ToInt32(r["id"]),
                    Name = Convert.ToString(r["name"]) ?? "",
                    FileName = Convert.ToString(r["file_name"]) ?? "",
                    ContentType = Convert.ToString(r["content_type"]) ?? "",
                    FileSizeBytes = Convert.ToInt64(r["file_size_bytes"]),
                    ImageData = r["image_data"] == DBNull.Value ? Array.Empty<byte>() : (byte[])r["image_data"],
                    IsActive = Convert.ToBoolean(r["is_active"]),
                    CreatedAt = Convert.ToDateTime(r["created_at"])
                });
            }

            return list;
        }

        public int Insert(LogoRecord item)
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
INSERT INTO system_logos
(
    name,
    file_name,
    content_type,
    file_size_bytes,
    image_data,
    is_active
)
VALUES
(
    @name,
    @file_name,
    @content_type,
    @file_size_bytes,
    @image_data,
    @is_active
);

SELECT LAST_INSERT_ID();";

            cmd.Parameters.AddWithValue("@name", item.Name);
            cmd.Parameters.AddWithValue("@file_name", item.FileName);
            cmd.Parameters.AddWithValue("@content_type", item.ContentType);
            cmd.Parameters.AddWithValue("@file_size_bytes", item.FileSizeBytes);

            var pImg = cmd.Parameters.Add("@image_data", MySqlDbType.LongBlob);
            pImg.Value = item.ImageData;

            cmd.Parameters.AddWithValue("@is_active", item.IsActive);

            var newId = Convert.ToInt32(cmd.ExecuteScalar());

            try
            {
                _auditLogService.AddLog(
                    operationType: "CREATE",
                    tableName: "system_logos",
                    recordId: newId.ToString(),
                    actorName: SessionService.Username ?? "Unknown",
                    description: $"Created logo ID {newId} with name '{item.Name}' and file '{item.FileName}'."
                );
            }
            catch
            {
                // do not block main operation if audit logging fails
            }

            return newId;
        }

        public void SetActive(int id)
        {
            using var conn = MySqlDb.OpenConnection();
            using var tx = conn.BeginTransaction();

            string? previousActiveName = null;
            int? previousActiveId = null;
            string? newActiveName = null;

            using (var beforeCmd = conn.CreateCommand())
            {
                beforeCmd.Transaction = tx;
                beforeCmd.CommandText = @"
SELECT id, name
FROM system_logos
WHERE is_active = b'1'
ORDER BY id DESC
LIMIT 1;";

                using var r = beforeCmd.ExecuteReader();
                if (r.Read())
                {
                    previousActiveId = Convert.ToInt32(r["id"]);
                    previousActiveName = Convert.ToString(r["name"]) ?? "";
                }
            }

            using (var targetCmd = conn.CreateCommand())
            {
                targetCmd.Transaction = tx;
                targetCmd.CommandText = @"
SELECT name
FROM system_logos
WHERE id = @id
LIMIT 1;";
                targetCmd.Parameters.AddWithValue("@id", id);

                var result = targetCmd.ExecuteScalar();
                newActiveName = result == null || result == DBNull.Value
                    ? null
                    : Convert.ToString(result);
            }

            using (var clearCmd = conn.CreateCommand())
            {
                clearCmd.Transaction = tx;
                clearCmd.CommandText = @"UPDATE system_logos SET is_active = b'0';";
                clearCmd.ExecuteNonQuery();
            }

            using (var setCmd = conn.CreateCommand())
            {
                setCmd.Transaction = tx;
                setCmd.CommandText = @"UPDATE system_logos SET is_active = b'1' WHERE id = @id;";
                setCmd.Parameters.AddWithValue("@id", id);
                setCmd.ExecuteNonQuery();
            }

            tx.Commit();

            try
            {
                _auditLogService.AddLog(
                    operationType: "UPDATE",
                    tableName: "system_logos",
                    recordId: id.ToString(),
                    actorName: SessionService.Username ?? "Unknown",
                    description: previousActiveId == id
                        ? $"Updated logo ID {id}. Logo '{newActiveName ?? "(unknown)"}' remains active."
                        : $"Changed active logo from ID {previousActiveId?.ToString() ?? "none"} '{previousActiveName ?? "none"}' to ID {id} '{newActiveName ?? "(unknown)"}'."
                );
            }
            catch
            {
                // do not block main operation if audit logging fails
            }
        }

        public LogoRecord? GetActive()
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT
    id,
    name,
    file_name,
    content_type,
    file_size_bytes,
    image_data,
    is_active,
    created_at
FROM system_logos
WHERE is_active = b'1'
ORDER BY id DESC
LIMIT 1;";

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new LogoRecord
            {
                Id = Convert.ToInt32(r["id"]),
                Name = Convert.ToString(r["name"]) ?? "",
                FileName = Convert.ToString(r["file_name"]) ?? "",
                ContentType = Convert.ToString(r["content_type"]) ?? "",
                FileSizeBytes = Convert.ToInt64(r["file_size_bytes"]),
                ImageData = r["image_data"] == DBNull.Value ? Array.Empty<byte>() : (byte[])r["image_data"],
                IsActive = Convert.ToBoolean(r["is_active"]),
                CreatedAt = Convert.ToDateTime(r["created_at"])
            };
        }

        public void Delete(int id)
        {
            using var conn = MySqlDb.OpenConnection();

            string logoName = "";
            string fileName = "";
            bool wasActive = false;

            using (var getCmd = conn.CreateCommand())
            {
                getCmd.CommandText = @"
SELECT name, file_name, is_active
FROM system_logos
WHERE id = @id
LIMIT 1;";
                getCmd.Parameters.AddWithValue("@id", id);

                using var r = getCmd.ExecuteReader();
                if (r.Read())
                {
                    logoName = Convert.ToString(r["name"]) ?? "";
                    fileName = Convert.ToString(r["file_name"]) ?? "";
                    wasActive = Convert.ToBoolean(r["is_active"]);
                }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"DELETE FROM system_logos WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }

            try
            {
                _auditLogService.AddLog(
                    operationType: "DELETE",
                    tableName: "system_logos",
                    recordId: id.ToString(),
                    actorName: SessionService.Username ?? "Unknown",
                    description: $"Deleted logo ID {id} with name '{logoName}' and file '{fileName}'. Active: {(wasActive ? "Yes" : "No")}."
                );
            }
            catch
            {
                // do not block main operation if audit logging fails
            }
        }
    }
}