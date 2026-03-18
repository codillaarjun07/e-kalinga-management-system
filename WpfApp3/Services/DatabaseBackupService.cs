using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WpfApp3.Services
{
    public class DatabaseBackupService
    {
        public void CreateBackup(string outputPath)
        {
            using var conn = MySqlDb.OpenConnection();

            var databaseName = conn.Database;
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new InvalidOperationException("No database selected.");

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

            File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
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

            var upper = sql.ToUpperInvariant();
            var definerIndex = upper.IndexOf("DEFINER=");
            if (definerIndex < 0)
                return sql;

            var nextSpace = sql.IndexOf(' ', definerIndex);
            if (nextSpace < 0)
                return sql;

            return sql.Remove(definerIndex, nextSpace - definerIndex + 1);
        }

        private string ToSqlValue(object value)
        {
            if (value == null || value == DBNull.Value)
                return "NULL";

            return value switch
            {
                string s => $"'{EscapeSqlString(s)}'",
                char c => $"'{EscapeSqlString(c.ToString())}'",
                bool b => b ? "1" : "0",
                byte[] bytes => "0x" + BitConverter.ToString(bytes).Replace("-", ""),
                DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
                DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss}'",
                TimeSpan ts => $"'{ts:hh\\:mm\\:ss}'",
                sbyte or byte or short or ushort or int or uint or long or ulong
                    => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
                float or double or decimal
                    => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
                _ => $"'{EscapeSqlString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")}'"
            };
        }

        private string EscapeSqlString(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\0", "\\0");
        }
    }
}