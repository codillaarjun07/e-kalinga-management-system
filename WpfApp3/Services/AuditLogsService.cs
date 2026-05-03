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

        public void EnsureNotificationReadsTable()
        {
            EnsureAuditLogsTable();

            using var conn = MySqlDb.OpenConnection();
            using var cmd = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS `notification_reads` (
                    `actor_name` VARCHAR(120) NOT NULL,
                    `last_seen_audit_log_id` INT NOT NULL DEFAULT 0,
                    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    PRIMARY KEY (`actor_name`),
                    KEY `idx_notification_reads_last_seen` (`last_seen_audit_log_id`)
                );", conn);

            cmd.ExecuteNonQuery();
        }

        public void EnsureNotificationReadRow(string actorName)
        {
            EnsureNotificationReadsTable();

            actorName = NormalizeActorName(actorName);

            using var conn = MySqlDb.OpenConnection();
            using var cmd = new MySqlCommand(@"
                INSERT IGNORE INTO `notification_reads`
                (
                    `actor_name`,
                    `last_seen_audit_log_id`
                )
                VALUES
                (
                    @actor_name,
                    0
                );", conn);

            cmd.Parameters.AddWithValue("@actor_name", actorName);
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
            cmd.Parameters.AddWithValue("@actor_name", NormalizeActorName(actorName));
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
                result.Add(ReadAuditLog(reader));
            }

            return result;
        }

        public List<AuditLogRecord> GetRecentNotificationsForUser(string actorName, int limit = 15)
        {
            EnsureNotificationReadRow(actorName);

            actorName = NormalizeActorName(actorName);
            var safeLimit = Math.Min(Math.Max(limit, 1), 50);
            var lastSeenAuditLogId = GetNotificationReadPositionForUser(actorName);

            using var conn = MySqlDb.OpenConnection();
            using var cmd = new MySqlCommand($@"
                SELECT
                    `id`,
                    `operation_type`,
                    `table_name`,
                    COALESCE(`record_id`, '') AS `record_id`,
                    `actor_name`,
                    `description`,
                    `created_at`
                FROM `audit_logs`
                WHERE COALESCE(TRIM(`actor_name`), '') <> ''
                  AND LOWER(TRIM(`actor_name`)) <> LOWER(TRIM(@actor_name))
                ORDER BY `id` DESC
                LIMIT {safeLimit};", conn);

            cmd.Parameters.AddWithValue("@actor_name", actorName);

            using var reader = cmd.ExecuteReader();

            var result = new List<AuditLogRecord>();
            while (reader.Read())
            {
                var item = ReadAuditLog(reader);
                item.IsUnread = item.Id > lastSeenAuditLogId;
                result.Add(item);
            }

            return result;
        }

        public int GetUnreadNotificationCountForUser(string actorName)
        {
            EnsureNotificationReadRow(actorName);

            actorName = NormalizeActorName(actorName);

            using var conn = MySqlDb.OpenConnection();
            using var cmd = new MySqlCommand(@"
                SELECT COUNT(*)
                FROM `audit_logs`
                WHERE `id` > COALESCE
                (
                    (
                        SELECT `last_seen_audit_log_id`
                        FROM `notification_reads`
                        WHERE LOWER(TRIM(`actor_name`)) = LOWER(TRIM(@actor_name))
                        LIMIT 1
                    ),
                    0
                )
                AND COALESCE(TRIM(`actor_name`), '') <> ''
                AND LOWER(TRIM(`actor_name`)) <> LOWER(TRIM(@actor_name));", conn);

            cmd.Parameters.AddWithValue("@actor_name", actorName);

            var value = cmd.ExecuteScalar();
            return Convert.ToInt32(value);
        }

        public int GetNotificationReadPositionForUser(string actorName)
        {
            EnsureNotificationReadRow(actorName);

            actorName = NormalizeActorName(actorName);

            using var conn = MySqlDb.OpenConnection();
            using var cmd = new MySqlCommand(@"
                SELECT COALESCE(`last_seen_audit_log_id`, 0)
                FROM `notification_reads`
                WHERE LOWER(TRIM(`actor_name`)) = LOWER(TRIM(@actor_name))
                LIMIT 1;", conn);

            cmd.Parameters.AddWithValue("@actor_name", actorName);

            var value = cmd.ExecuteScalar();
            return value is null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        public void MarkNotificationsReadForUser(string actorName)
        {
            EnsureNotificationReadsTable();

            actorName = NormalizeActorName(actorName);

            using var conn = MySqlDb.OpenConnection();
            using var getMaxCmd = new MySqlCommand(@"
                SELECT COALESCE(MAX(`id`), 0)
                FROM `audit_logs`
                WHERE COALESCE(TRIM(`actor_name`), '') <> ''
                  AND LOWER(TRIM(`actor_name`)) <> LOWER(TRIM(@actor_name));", conn);

            getMaxCmd.Parameters.AddWithValue("@actor_name", actorName);

            var maxValue = getMaxCmd.ExecuteScalar();
            var maxId = Convert.ToInt32(maxValue);

            SaveNotificationReadPosition(actorName, maxId, conn);
        }

        public void MarkNotificationReadUpToForUser(string actorName, int auditLogId)
        {
            EnsureNotificationReadsTable();

            actorName = NormalizeActorName(actorName);
            if (auditLogId <= 0)
                return;

            using var conn = MySqlDb.OpenConnection();
            SaveNotificationReadPosition(actorName, auditLogId, conn);
        }

        private static void SaveNotificationReadPosition(string actorName, int auditLogId, MySqlConnection conn)
        {
            using var saveCmd = new MySqlCommand(@"
                INSERT INTO `notification_reads`
                (
                    `actor_name`,
                    `last_seen_audit_log_id`
                )
                VALUES
                (
                    @actor_name,
                    @last_seen_audit_log_id
                )
                ON DUPLICATE KEY UPDATE
                    `last_seen_audit_log_id` = GREATEST(`last_seen_audit_log_id`, VALUES(`last_seen_audit_log_id`)),
                    `updated_at` = CURRENT_TIMESTAMP;", conn);

            saveCmd.Parameters.AddWithValue("@actor_name", actorName);
            saveCmd.Parameters.AddWithValue("@last_seen_audit_log_id", auditLogId);
            saveCmd.ExecuteNonQuery();
        }

        private static AuditLogRecord ReadAuditLog(MySqlDataReader reader)
        {
            return new AuditLogRecord
            {
                Id = reader.GetInt32("id"),
                OperationType = reader.GetString("operation_type"),
                TableName = reader.GetString("table_name"),
                RecordId = reader.GetString("record_id"),
                ActorName = reader.GetString("actor_name"),
                Description = reader.GetString("description"),
                CreatedAt = reader.GetDateTime("created_at")
            };
        }

        private static string NormalizeActorName(string? actorName)
        {
            return string.IsNullOrWhiteSpace(actorName) ? "Unknown" : actorName.Trim();
        }
    }
}
