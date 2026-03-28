using MySqlConnector;
using System;
using System.Collections.Generic;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    public class AuditLogsService
    {
        public void EnsureAuditLogsTable()
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS `audit_logs` (
                    `id` INT NOT NULL AUTO_INCREMENT,
                    `operation_type` VARCHAR(20) NOT NULL,
                    `table_name` VARCHAR(120) NOT NULL,
                    `record_id` VARCHAR(120) NULL,
                    `actor_name` VARCHAR(120) NOT NULL,
                    `description` TEXT NOT NULL,
                    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_audit_logs_created_at` (`created_at`),
                    KEY `idx_audit_logs_operation_type` (`operation_type`),
                    KEY `idx_audit_logs_actor_name` (`actor_name`),
                    KEY `idx_audit_logs_table_name` (`table_name`)
                );", conn);

            cmd.ExecuteNonQuery();
        }

        public void AddLog(string operationType, string tableName, string? recordId, string actorName, string description)
        {
            EnsureAuditLogsTable();

            using var conn = MySqlDb.OpenConnection();
            using var cmd = new MySqlCommand(@"
                INSERT INTO `audit_logs`
                (
                    `operation_type`,
                    `table_name`,
                    `record_id`,
                    `actor_name`,
                    `description`
                )
                VALUES
                (
                    @operation_type,
                    @table_name,
                    @record_id,
                    @actor_name,
                    @description
                );", conn);

            cmd.Parameters.AddWithValue("@operation_type", (operationType ?? "").Trim());
            cmd.Parameters.AddWithValue("@table_name", (tableName ?? "").Trim());
            cmd.Parameters.AddWithValue("@record_id", string.IsNullOrWhiteSpace(recordId) ? DBNull.Value : recordId.Trim());
            cmd.Parameters.AddWithValue("@actor_name", string.IsNullOrWhiteSpace(actorName) ? "Unknown" : actorName.Trim());
            cmd.Parameters.AddWithValue("@description", (description ?? "").Trim());

            cmd.ExecuteNonQuery();
        }

        public List<AuditLogRecord> GetAll()
        {
            EnsureAuditLogsTable();

            using var conn = MySqlDb.OpenConnection();
            using var cmd = new MySqlCommand(@"
                SELECT
                    `id`,
                    `operation_type`,
                    `table_name`,
                    COALESCE(`record_id`, '') AS `record_id`,
                    `actor_name`,
                    `description`,
                    `created_at`
                FROM `audit_logs`
                ORDER BY `created_at` DESC, `id` DESC;", conn);

            using var reader = cmd.ExecuteReader();

            var result = new List<AuditLogRecord>();
            while (reader.Read())
            {
                result.Add(new AuditLogRecord
                {
                    Id = reader.GetInt32("id"),
                    OperationType = reader.GetString("operation_type"),
                    TableName = reader.GetString("table_name"),
                    RecordId = reader.GetString("record_id"),
                    ActorName = reader.GetString("actor_name"),
                    Description = reader.GetString("description"),
                    CreatedAt = reader.GetDateTime("created_at")
                });
            }

            return result;
        }
    }
}