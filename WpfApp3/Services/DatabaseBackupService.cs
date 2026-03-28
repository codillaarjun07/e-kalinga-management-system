using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    public class DatabaseBackupService
    {
        public void EnsureBackupTable()
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS `database_backups` (
                    `id` INT NOT NULL AUTO_INCREMENT,
                    `file_name` VARCHAR(255) NOT NULL,
                    `database_name` VARCHAR(150) NOT NULL,
                    `server_name` VARCHAR(150) NOT NULL,
                    `content_type` VARCHAR(100) NOT NULL DEFAULT 'application/sql',
                    `file_data` LONGBLOB NOT NULL,
                    `file_size_bytes` BIGINT NOT NULL DEFAULT 0,
                    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `created_by` VARCHAR(120) NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `idx_database_backups_created_at` (`created_at`),
                    KEY `idx_database_backups_created_by` (`created_by`)
                );", conn);

            cmd.ExecuteNonQuery();
        }

        public DatabaseBackupRecord CreateAndStoreBackup(string createdBy)
        {
            using var conn = MySqlDb.OpenConnection();

            var databaseName = conn.Database;
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new InvalidOperationException("No database selected.");

            var builder = new MySqlConnectionStringBuilder(MySqlDb.ConnectionString);
            var serverName = builder.Server ?? "";

            var sqlContent = BuildBackupSql(conn, databaseName);

            var baseFileName = $"ekalinga_backup_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
            var storedFileName = $"{baseFileName}.gz";

            var rawBytes = Encoding.UTF8.GetBytes(sqlContent);
            var compressedBytes = CompressBytes(rawBytes);

            EnsureBackupTable();

            using var insert = new MySqlCommand(@"
        INSERT INTO `database_backups`
        (
            `file_name`,
            `database_name`,
            `server_name`,
            `content_type`,
            `file_data`,
            `file_size_bytes`,
            `created_by`
        )
        VALUES
        (
            @file_name,
            @database_name,
            @server_name,
            @content_type,
            @file_data,
            @file_size_bytes,
            @created_by
        );
        SELECT LAST_INSERT_ID();", conn);

            insert.Parameters.AddWithValue("@file_name", storedFileName);
            insert.Parameters.AddWithValue("@database_name", databaseName);
            insert.Parameters.AddWithValue("@server_name", serverName);
            insert.Parameters.AddWithValue("@content_type", "application/gzip");
            insert.Parameters.AddWithValue("@file_data", compressedBytes);
            insert.Parameters.AddWithValue("@file_size_bytes", compressedBytes.LongLength);
            insert.Parameters.AddWithValue("@created_by", createdBy ?? "Unknown");

            var id = Convert.ToInt32(insert.ExecuteScalar(), CultureInfo.InvariantCulture);

            return new DatabaseBackupRecord
            {
                Id = id,
                FileName = storedFileName,
                DatabaseName = databaseName,
                ServerName = serverName,
                ContentType = "application/gzip",
                FileSizeBytes = compressedBytes.LongLength,
                CreatedAt = DateTime.Now,
                CreatedBy = createdBy ?? "Unknown"
            };
        }

        public List<DatabaseBackupRecord> GetBackupHistory()
        {
            EnsureBackupTable();

            using var conn = MySqlDb.OpenConnection();
            using var cmd = new MySqlCommand(@"
                SELECT
                    `id`,
                    `file_name`,
                    `database_name`,
                    `server_name`,
                    `content_type`,
                    `file_size_bytes`,
                    `created_at`,
                    `created_by`
                FROM `database_backups`
                ORDER BY `created_at` DESC, `id` DESC;", conn);

            using var reader = cmd.ExecuteReader();

            var result = new List<DatabaseBackupRecord>();
            while (reader.Read())
            {
                result.Add(new DatabaseBackupRecord
                {
                    Id = reader.GetInt32("id"),
                    FileName = reader.GetString("file_name"),
                    DatabaseName = reader.GetString("database_name"),
                    ServerName = reader.GetString("server_name"),
                    ContentType = reader.GetString("content_type"),
                    FileSizeBytes = reader.GetInt64("file_size_bytes"),
                    CreatedAt = reader.GetDateTime("created_at"),
                    CreatedBy = reader.GetString("created_by")
                });
            }

            return result;
        }

        public void DownloadBackup(int backupId, string outputPath)
        {
            EnsureBackupTable();

            using var conn = MySqlDb.OpenConnection();
            using var cmd = new MySqlCommand(@"
        SELECT `file_name`, `content_type`, `file_data`
        FROM `database_backups`
        WHERE `id` = @id
        LIMIT 1;", conn);

            cmd.Parameters.AddWithValue("@id", backupId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                throw new InvalidOperationException("Backup file not found.");

            var fileName = reader.GetString("file_name");
            var contentType = reader.GetString("content_type");
            var bytes = (byte[])reader["file_data"];

            if (bytes.Length == 0)
                throw new InvalidOperationException("Backup file is empty.");

            if (string.Equals(contentType, "application/gzip", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                var decompressed = DecompressBytes(bytes);
                File.WriteAllBytes(outputPath, decompressed);
            }
            else
            {
                File.WriteAllBytes(outputPath, bytes);
            }
        }

        private byte[] CompressBytes(byte[] input)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(input, 0, input.Length);
            }

            return output.ToArray();
        }

        private byte[] DecompressBytes(byte[] input)
        {
            using var inputStream = new MemoryStream(input);
            using var gzip = new GZipStream(inputStream, CompressionMode.Decompress);
            using var output = new MemoryStream();

            gzip.CopyTo(output);
            return output.ToArray();
        }

        public void DeleteBackup(int backupId)
        {
            EnsureBackupTable();

            using var conn = MySqlDb.OpenConnection();
            using var cmd = new MySqlCommand(@"
                DELETE FROM `database_backups`
                WHERE `id` = @id;", conn);

            cmd.Parameters.AddWithValue("@id", backupId);
            cmd.ExecuteNonQuery();
        }

        private string BuildBackupSql(MySqlConnection conn, string databaseName)
        {
            var sb = new StringBuilder();

            sb.AppendLine("-- E-Kalinga MySQL Backup");
            sb.AppendLine($"-- Database: `{databaseName}`");
            sb.AppendLine($"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("SET FOREIGN_KEY_CHECKS=0;");
            sb.AppendLine("SET SQL_MODE='NO_AUTO_VALUE_ON_ZERO';");
            sb.AppendLine("SET @OLD_SQL_MODE=@@SQL_MODE;");
            sb.AppendLine("SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS;");
            sb.AppendLine("SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS;");
            sb.AppendLine("SET FOREIGN_KEY_CHECKS=0;");
            sb.AppendLine("SET UNIQUE_CHECKS=0;");
            sb.AppendLine();
            sb.AppendLine("DELIMITER $$");
            sb.AppendLine();

            var tables = GetBaseTables(conn, databaseName);
            var views = GetViews(conn, databaseName);
            var procedures = GetProcedures(conn, databaseName);
            var functions = GetFunctions(conn, databaseName);

            foreach (var table in tables)
            {
                WriteTableSchema(conn, sb, table);
                WriteTableData(conn, sb, table);
                sb.AppendLine();
            }

            foreach (var view in views)
            {
                WriteViewSchema(conn, sb, view);
                sb.AppendLine();
            }

            foreach (var procedure in procedures)
            {
                WriteProcedureSchema(conn, sb, procedure);
                sb.AppendLine();
            }

            foreach (var function in functions)
            {
                WriteFunctionSchema(conn, sb, function);
                sb.AppendLine();
            }

            sb.AppendLine("DELIMITER ;");
            sb.AppendLine();
            sb.AppendLine("SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS;");
            sb.AppendLine("SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS;");
            sb.AppendLine("SET SQL_MODE=@OLD_SQL_MODE;");

            return sb.ToString();
        }

        private List<string> GetBaseTables(MySqlConnection conn, string databaseName)
        {
            var result = new List<string>();

            using var cmd = new MySqlCommand(@"
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = @db
                  AND TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_NAME;", conn);

            cmd.Parameters.AddWithValue("@db", databaseName);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(reader.GetString("TABLE_NAME"));

            return result;
        }

        private List<string> GetViews(MySqlConnection conn, string databaseName)
        {
            var result = new List<string>();

            using var cmd = new MySqlCommand(@"
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.VIEWS
                WHERE TABLE_SCHEMA = @db
                ORDER BY TABLE_NAME;", conn);

            cmd.Parameters.AddWithValue("@db", databaseName);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(reader.GetString("TABLE_NAME"));

            return result;
        }

        private List<string> GetProcedures(MySqlConnection conn, string databaseName)
        {
            var result = new List<string>();

            using var cmd = new MySqlCommand(@"
                SELECT ROUTINE_NAME
                FROM INFORMATION_SCHEMA.ROUTINES
                WHERE ROUTINE_SCHEMA = @db
                  AND ROUTINE_TYPE = 'PROCEDURE'
                ORDER BY ROUTINE_NAME;", conn);

            cmd.Parameters.AddWithValue("@db", databaseName);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(reader.GetString("ROUTINE_NAME"));

            return result;
        }

        private List<string> GetFunctions(MySqlConnection conn, string databaseName)
        {
            var result = new List<string>();

            using var cmd = new MySqlCommand(@"
                SELECT ROUTINE_NAME
                FROM INFORMATION_SCHEMA.ROUTINES
                WHERE ROUTINE_SCHEMA = @db
                  AND ROUTINE_TYPE = 'FUNCTION'
                ORDER BY ROUTINE_NAME;", conn);

            cmd.Parameters.AddWithValue("@db", databaseName);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(reader.GetString("ROUTINE_NAME"));

            return result;
        }

        private void WriteTableSchema(MySqlConnection conn, StringBuilder sb, string table)
        {
            using var cmd = new MySqlCommand($"SHOW CREATE TABLE `{table}`;", conn);
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                throw new InvalidOperationException($"Unable to read schema for table '{table}'.");

            var createSql = reader.GetString("Create Table");

            sb.AppendLine($"-- ----------------------------");
            sb.AppendLine($"-- Table structure for `{table}`");
            sb.AppendLine($"-- ----------------------------");
            sb.AppendLine($"DROP TABLE IF EXISTS `{table}`;");
            sb.AppendLine($"{createSql};");
            sb.AppendLine();
        }

        private void WriteTableData(MySqlConnection conn, StringBuilder sb, string table)
        {
            using var cmd = new MySqlCommand($"SELECT * FROM `{table}`;", conn);
            using var reader = cmd.ExecuteReader();

            if (!reader.HasRows)
            {
                sb.AppendLine($"-- No data for `{table}`");
                return;
            }

            sb.AppendLine($"-- ----------------------------");
            sb.AppendLine($"-- Records of `{table}`");
            sb.AppendLine($"-- ----------------------------");

            var fieldCount = reader.FieldCount;

            while (reader.Read())
            {
                var columns = new StringBuilder();
                var values = new StringBuilder();

                for (int i = 0; i < fieldCount; i++)
                {
                    if (i > 0)
                    {
                        columns.Append(", ");
                        values.Append(", ");
                    }

                    columns.Append('`').Append(reader.GetName(i)).Append('`');
                    values.Append(ToSqlValue(reader.GetValue(i)));
                }

                sb.Append("INSERT INTO `")
                  .Append(table)
                  .Append("` (")
                  .Append(columns)
                  .Append(") VALUES (")
                  .Append(values)
                  .AppendLine(");");
            }
        }

        private void WriteViewSchema(MySqlConnection conn, StringBuilder sb, string view)
        {
            using var cmd = new MySqlCommand($"SHOW CREATE VIEW `{view}`;", conn);
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                throw new InvalidOperationException($"Unable to read schema for view '{view}'.");

            var createSql = reader.GetString("Create View");

            sb.AppendLine($"-- ----------------------------");
            sb.AppendLine($"-- View structure for `{view}`");
            sb.AppendLine($"-- ----------------------------");
            sb.AppendLine($"DROP VIEW IF EXISTS `{view}`;");
            sb.AppendLine($"{createSql} $$");
        }

        private void WriteProcedureSchema(MySqlConnection conn, StringBuilder sb, string procedureName)
        {
            using var cmd = new MySqlCommand($"SHOW CREATE PROCEDURE `{procedureName}`;", conn);
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                throw new InvalidOperationException($"Unable to read procedure '{procedureName}'.");

            var createSql = reader.GetString("Create Procedure");
            createSql = RemoveDefiner(createSql);

            sb.AppendLine($"-- ----------------------------");
            sb.AppendLine($"-- Procedure `{procedureName}`");
            sb.AppendLine($"-- ----------------------------");
            sb.AppendLine($"DROP PROCEDURE IF EXISTS `{procedureName}` $$");
            sb.AppendLine($"{createSql} $$");
        }

        private void WriteFunctionSchema(MySqlConnection conn, StringBuilder sb, string functionName)
        {
            using var cmd = new MySqlCommand($"SHOW CREATE FUNCTION `{functionName}`;", conn);
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                throw new InvalidOperationException($"Unable to read function '{functionName}'.");

            var createSql = reader.GetString("Create Function");
            createSql = RemoveDefiner(createSql);

            sb.AppendLine($"-- ----------------------------");
            sb.AppendLine($"-- Function `{functionName}`");
            sb.AppendLine($"-- ----------------------------");
            sb.AppendLine($"DROP FUNCTION IF EXISTS `{functionName}` $$");
            sb.AppendLine($"{createSql} $$");
        }

        private string RemoveDefiner(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return sql;

            const string marker = " DEFINER=";
            var idx = sql.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return sql;

            var before = sql.Substring(0, idx);
            var rest = sql.Substring(idx + 1);

            var sqlIdx = rest.IndexOf("SQL SECURITY", StringComparison.OrdinalIgnoreCase);
            if (sqlIdx < 0)
                return sql;

            return before + " " + rest.Substring(sqlIdx);
        }

        private string ToSqlValue(object value)
        {
            if (value == DBNull.Value || value is null)
                return "NULL";

            return value switch
            {
                byte[] bytes => "0x" + BitConverter.ToString(bytes).Replace("-", string.Empty),
                bool b => b ? "1" : "0",
                sbyte or byte or short or ushort or int or uint or long or ulong
                    => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
                float or double or decimal
                    => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
                DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
                DateOnly d => $"'{d:yyyy-MM-dd}'",
                TimeSpan ts => $"'{ts}'",
                _ => $"'{MySqlHelper.EscapeString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)}'"
            };
        }
    }
}