using MySqlConnector;
using System;
using System.Collections.Generic;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.Repositories
{
    public sealed class ExternalValBeneficiariesRepository
    {
        public int Count(string? search = null)
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM val_benificiaries vb
                WHERE
                    (@search IS NULL OR @search = '')
                    OR vb.benificiary_id LIKE CONCAT('%', @search, '%')
                    OR vb.first_name LIKE CONCAT('%', @search, '%')
                    OR vb.last_name LIKE CONCAT('%', @search, '%')
                    OR vb.full_name LIKE CONCAT('%', @search, '%')";

            cmd.Parameters.AddWithValue("@search", search ?? string.Empty);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public List<ValidatorRecord> GetPage(int page, int pageSize, string? search = null)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var offset = (page - 1) * pageSize;
            var items = new List<ValidatorRecord>();

            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT
                    vb.id,
                    vb.benificiary_id,
                    vb.civilregistry_id,
                    vb.first_name,
                    vb.middle_name,
                    vb.last_name,
                    vb.full_name,
                    vb.sex AS gender,
                    vb.date_of_birth,
                    vb.age,
                    vb.marital_status,
                    vb.address AS present_address,
                    vb.is_pwd,
                    vb.pwd_id_no,
                    vb.is_senior,
                    vb.senior_id_no,
                    vb.disability_type,
                    vb.cause_of_disability
                FROM val_benificiaries vb
                WHERE
                    (@search IS NULL OR @search = '')
                    OR vb.benificiary_id LIKE CONCAT('%', @search, '%')
                    OR vb.first_name LIKE CONCAT('%', @search, '%')
                    OR vb.last_name LIKE CONCAT('%', @search, '%')
                    OR vb.full_name LIKE CONCAT('%', @search, '%')
                ORDER BY vb.id DESC
                LIMIT @limit OFFSET @offset;";

            cmd.Parameters.AddWithValue("@search", search ?? string.Empty);
            cmd.Parameters.AddWithValue("@limit", pageSize);
            cmd.Parameters.AddWithValue("@offset", offset);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new ValidatorRecord
                {
                    Id = reader["id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["id"]),
                    BeneficiaryId = reader["benificiary_id"]?.ToString() ?? "",
                    CivilRegistryId = reader["civilregistry_id"]?.ToString() ?? "",
                    FirstName = reader["first_name"]?.ToString() ?? "",
                    MiddleName = reader["middle_name"]?.ToString() ?? "",
                    LastName = reader["last_name"]?.ToString() ?? "",

                    // Inline logic: Convert to string, then check value
                    Gender = (reader["gender"]?.ToString() == "1") ? "Male" :
                             (reader["gender"]?.ToString() == "2") ? "Female" : "",

                    DateOfBirth = FormatDate(reader["date_of_birth"]),
                    Classification = BuildClassification(
                        reader["is_pwd"],
                        reader["is_senior"],
                        reader["disability_type"]),
                    Barangay = "",
                    PresentAddress = reader["present_address"]?.ToString() ?? "",
                    Status = "Not Validated"
                });
            }

            return items;
        }

        private static string FormatDate(object? value)
        {
            if (value == null || value == DBNull.Value)
                return "";

            if (value is DateTime dt)
                return dt.ToString("dd MMMM yyyy");

            return value.ToString() ?? "";
        }

        private static string BuildClassification(object? isPwd, object? isSenior, object? disabilityType)
        {
            var pwd = ToBool(isPwd);
            var senior = ToBool(isSenior);
            var disability = disabilityType?.ToString() ?? "";

            if (pwd && !string.IsNullOrWhiteSpace(disability))
                return disability;

            if (pwd)
                return "PWD";

            if (senior)
                return "Senior Citizen";

            return "None";
        }

        private static bool ToBool(object? value)
        {
            if (value == null || value == DBNull.Value)
                return false;

            var s = value.ToString()?.Trim().ToLowerInvariant();

            return s == "1" || s == "true" || s == "yes";
        }
    }
}