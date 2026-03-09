using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    public class BeneficiariesRepository
    {
        public void EnsureTable()
        {
            using var conn = MySqlDb.OpenConnection();

            // Create table (fresh installs)
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS beneficiaries (
  id INT AUTO_INCREMENT PRIMARY KEY,
  source_person_id INT NULL,

  beneficiary_id VARCHAR(50) NOT NULL,
  civil_registry_id VARCHAR(50) NOT NULL,

  first_name VARCHAR(100) NOT NULL,
  middle_name VARCHAR(100) NULL,
  last_name VARCHAR(100) NOT NULL,

  gender VARCHAR(20) NULL,
  date_of_birth VARCHAR(50) NULL,
  classification VARCHAR(50) NULL,

  barangay VARCHAR(100) NULL,
  present_address VARCHAR(255) NULL,

  -- ✅ NEW
  profile_image LONGBLOB NULL,

  status ENUM('Not Validated','Endorsed','Pending','Rejected') NOT NULL DEFAULT 'Not Validated',

  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

  UNIQUE KEY uq_beneficiary_id (beneficiary_id),
  KEY idx_status (status),
  KEY idx_source_person_id (source_person_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                cmd.ExecuteNonQuery();
            }

            // ✅ If table already existed before, ensure column exists
            if (!ColumnExists(conn, "beneficiaries", "profile_image"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = @"ALTER TABLE beneficiaries ADD COLUMN profile_image LONGBLOB NULL;";
                alter.ExecuteNonQuery();
            }
        }

        private static bool ColumnExists(MySqlConnection conn, string table, string column)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT COUNT(*)
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = @table
  AND column_name = @column;";
            cmd.Parameters.AddWithValue("@table", table);
            cmd.Parameters.AddWithValue("@column", column);

            var n = Convert.ToInt32(cmd.ExecuteScalar());
            return n > 0;
        }

        public List<ValidatorRecord> GetByStatus(string status)
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT
  id,
  source_person_id,
  beneficiary_id,
  civil_registry_id,
  first_name,
  middle_name,
  last_name,
  gender,
  date_of_birth,
  classification,
  barangay,
  present_address,
  profile_image,
  status
FROM beneficiaries
WHERE status = @status
ORDER BY updated_at DESC, id DESC;";
            cmd.Parameters.AddWithValue("@status", status);

            var list = new List<ValidatorRecord>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(Map(r));
            }
            return list;
        }

        public Dictionary<string, ValidatorRecord> GetByBeneficiaryIds(IEnumerable<string> beneficiaryIds)
        {
            var ids = beneficiaryIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ids.Count == 0) return new Dictionary<string, ValidatorRecord>(StringComparer.OrdinalIgnoreCase);

            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            var paramNames = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                var p = $"@p{i}";
                paramNames.Add(p);
                cmd.Parameters.AddWithValue(p, ids[i]);
            }

            cmd.CommandText = $@"
SELECT
  id,
  source_person_id,
  beneficiary_id,
  civil_registry_id,
  first_name,
  middle_name,
  last_name,
  gender,
  date_of_birth,
  classification,
  barangay,
  present_address,
  profile_image,
  status
FROM beneficiaries
WHERE beneficiary_id IN ({string.Join(",", paramNames)});";

            var dict = new Dictionary<string, ValidatorRecord>(StringComparer.OrdinalIgnoreCase);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var rec = Map(r);
                if (!string.IsNullOrWhiteSpace(rec.BeneficiaryId))
                    dict[rec.BeneficiaryId] = rec;
            }

            return dict;
        }

        public void Upsert(ValidatorRecord person, string status)
        {
            if (person is null) throw new ArgumentNullException(nameof(person));
            if (string.IsNullOrWhiteSpace(person.BeneficiaryId)) throw new ArgumentException("BeneficiaryId is required.");
            if (string.IsNullOrWhiteSpace(person.CivilRegistryId)) throw new ArgumentException("CivilRegistryId is required.");
            if (string.IsNullOrWhiteSpace(person.FirstName)) throw new ArgumentException("FirstName is required.");
            if (string.IsNullOrWhiteSpace(person.LastName)) throw new ArgumentException("LastName is required.");

            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
INSERT INTO beneficiaries (
  source_person_id,
  beneficiary_id,
  civil_registry_id,
  first_name,
  middle_name,
  last_name,
  gender,
  date_of_birth,
  classification,
  barangay,
  present_address,
  profile_image,
  status
)
VALUES (
  @source_person_id,
  @beneficiary_id,
  @civil_registry_id,
  @first_name,
  @middle_name,
  @last_name,
  @gender,
  @date_of_birth,
  @classification,
  @barangay,
  @present_address,
  @profile_image,
  @status
)
ON DUPLICATE KEY UPDATE
  source_person_id = VALUES(source_person_id),
  civil_registry_id = VALUES(civil_registry_id),
  first_name = VALUES(first_name),
  middle_name = VALUES(middle_name),
  last_name = VALUES(last_name),
  gender = VALUES(gender),
  date_of_birth = VALUES(date_of_birth),
  classification = VALUES(classification),
  barangay = VALUES(barangay),
  present_address = VALUES(present_address),
  profile_image = VALUES(profile_image),
  status = VALUES(status);";

            cmd.Parameters.AddWithValue("@source_person_id", person.Id); // external id (from left list)
            cmd.Parameters.AddWithValue("@beneficiary_id", person.BeneficiaryId.Trim());
            cmd.Parameters.AddWithValue("@civil_registry_id", person.CivilRegistryId.Trim());
            cmd.Parameters.AddWithValue("@first_name", person.FirstName.Trim());
            cmd.Parameters.AddWithValue("@middle_name", (person.MiddleName ?? "").Trim());
            cmd.Parameters.AddWithValue("@last_name", person.LastName.Trim());
            cmd.Parameters.AddWithValue("@gender", (person.Gender ?? "").Trim());
            cmd.Parameters.AddWithValue("@date_of_birth", (person.DateOfBirth ?? "").Trim());
            cmd.Parameters.AddWithValue("@classification", (person.Classification ?? "").Trim());
            cmd.Parameters.AddWithValue("@barangay", (person.Barangay ?? "").Trim());
            cmd.Parameters.AddWithValue("@present_address", (person.PresentAddress ?? "").Trim());

            var pImg = cmd.Parameters.Add("@profile_image", MySqlDbType.LongBlob);
            pImg.Value = person.ProfileImage is null ? DBNull.Value : person.ProfileImage;

            cmd.Parameters.AddWithValue("@status", status);

            cmd.ExecuteNonQuery();
        }

        private static ValidatorRecord Map(MySqlDataReader r)
        {
            var internalId = Convert.ToInt32(r["id"]);
            var sourceIdObj = r["source_person_id"];
            var sourceId = sourceIdObj == DBNull.Value ? (int?)null : Convert.ToInt32(sourceIdObj);

            byte[]? img = null;
            if (r["profile_image"] != DBNull.Value)
                img = (byte[])r["profile_image"];

            return new ValidatorRecord
            {
                Id = sourceId ?? internalId,
                BeneficiaryId = Convert.ToString(r["beneficiary_id"]) ?? "",
                CivilRegistryId = Convert.ToString(r["civil_registry_id"]) ?? "",
                FirstName = Convert.ToString(r["first_name"]) ?? "",
                MiddleName = Convert.ToString(r["middle_name"]) ?? "",
                LastName = Convert.ToString(r["last_name"]) ?? "",
                Gender = Convert.ToString(r["gender"]) ?? "",
                DateOfBirth = Convert.ToString(r["date_of_birth"]) ?? "",
                Classification = Convert.ToString(r["classification"]) ?? "",
                Barangay = Convert.ToString(r["barangay"]) ?? "",
                PresentAddress = Convert.ToString(r["present_address"]) ?? "",
                ProfileImage = img,
                Status = Convert.ToString(r["status"]) ?? ""
            };
        }

        public int? GetInternalIdByBeneficiaryId(string beneficiaryId)
        {
            if (string.IsNullOrWhiteSpace(beneficiaryId)) return null;

            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT id FROM beneficiaries WHERE beneficiary_id = @b LIMIT 1;";
            cmd.Parameters.AddWithValue("@b", beneficiaryId.Trim());

            var val = cmd.ExecuteScalar();
            return val == null || val == DBNull.Value ? null : Convert.ToInt32(val);
        }


        public sealed class BeneficiaryDetails
        {
            public int Id { get; set; }
            public string BeneficiaryId { get; set; } = "";
            public string CivilRegistryId { get; set; } = "";
            public string FirstName { get; set; } = "";
            public string MiddleName { get; set; } = "";
            public string LastName { get; set; } = "";
            public string Gender { get; set; } = "";
            public string Classification { get; set; } = "";
            public string Barangay { get; set; } = "";
            public string PresentAddress { get; set; } = "";
            public byte[]? ProfileImage { get; set; }
        }

        public BeneficiaryDetails? GetDetailsByInternalId(int internalId)
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT
  id,
  beneficiary_id,
  civil_registry_id,
  first_name,
  middle_name,
  last_name,
  gender,
  classification,
  barangay,
  present_address,
  profile_image
FROM beneficiaries
WHERE id = @id
LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", internalId);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            byte[]? img = null;
            if (r["profile_image"] != DBNull.Value)
                img = (byte[])r["profile_image"];

            return new BeneficiaryDetails
            {
                Id = Convert.ToInt32(r["id"]),
                BeneficiaryId = Convert.ToString(r["beneficiary_id"]) ?? "",
                CivilRegistryId = Convert.ToString(r["civil_registry_id"]) ?? "",
                FirstName = Convert.ToString(r["first_name"]) ?? "",
                MiddleName = Convert.ToString(r["middle_name"]) ?? "",
                LastName = Convert.ToString(r["last_name"]) ?? "",
                Gender = Convert.ToString(r["gender"]) ?? "",
                Classification = Convert.ToString(r["classification"]) ?? "",
                Barangay = Convert.ToString(r["barangay"]) ?? "",
                PresentAddress = Convert.ToString(r["present_address"]) ?? "",
                ProfileImage = img
            };
        }
    }
}