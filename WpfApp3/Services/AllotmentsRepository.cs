using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    public class AllotmentsRepository
    {
        public void EnsureTable()
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS allotments (
  id INT AUTO_INCREMENT PRIMARY KEY,
  project_name VARCHAR(150) NOT NULL,
  company VARCHAR(150) NOT NULL,
  department VARCHAR(100) NOT NULL,
  source_of_fund VARCHAR(100) NOT NULL,
  beneficiaries_count INT NOT NULL DEFAULT 0,

  budget_type ENUM('Money','InKind') NOT NULL DEFAULT 'Money',
  budget_amount DECIMAL(18,2) NULL,
  budget_qty INT NULL,
  budget_unit VARCHAR(150) NULL,

  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

  KEY idx_department (department),
  KEY idx_source_of_fund (source_of_fund),
  KEY idx_budget_type (budget_type)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
            cmd.ExecuteNonQuery();
        }

        public List<AllotmentRecord> GetAll()
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT id, project_name, company, department, source_of_fund, beneficiaries_count,
       budget_type, budget_amount, budget_qty, budget_unit
FROM allotments
ORDER BY id DESC;";

            var list = new List<AllotmentRecord>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new AllotmentRecord
                {
                    Id = Convert.ToInt32(r["id"]),
                    ProjectName = Convert.ToString(r["project_name"]) ?? "",
                    Company = Convert.ToString(r["company"]) ?? "",
                    Department = Convert.ToString(r["department"]) ?? "",
                    SourceOfFund = Convert.ToString(r["source_of_fund"]) ?? "",
                    BeneficiariesCount = Convert.ToInt32(r["beneficiaries_count"]),
                    BudgetType = Convert.ToString(r["budget_type"]) ?? "Money",
                    BudgetAmount = r["budget_amount"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["budget_amount"]),
                    BudgetQty = r["budget_qty"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["budget_qty"]),
                    BudgetUnit = r["budget_unit"] == DBNull.Value ? "" : Convert.ToString(r["budget_unit"]) ?? ""
                });
            }

            return list;
        }

        public int Insert(AllotmentRecord a)
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
INSERT INTO allotments (
  project_name, company, department, source_of_fund, beneficiaries_count,
  budget_type, budget_amount, budget_qty, budget_unit
)
VALUES (
  @project_name, @company, @department, @source_of_fund, @beneficiaries_count,
  @budget_type, @budget_amount, @budget_qty, @budget_unit
);
SELECT LAST_INSERT_ID();";

            FillParams(cmd, a, includeId: false);

            var id = Convert.ToInt32(cmd.ExecuteScalar());
            return id;
        }

        public void Update(AllotmentRecord a)
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
UPDATE allotments
SET project_name=@project_name,
    company=@company,
    department=@department,
    source_of_fund=@source_of_fund,
    beneficiaries_count=@beneficiaries_count,
    budget_type=@budget_type,
    budget_amount=@budget_amount,
    budget_qty=@budget_qty,
    budget_unit=@budget_unit
WHERE id=@id;";

            FillParams(cmd, a, includeId: true);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM allotments WHERE id=@id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static void FillParams(MySqlCommand cmd, AllotmentRecord a, bool includeId)
        {
            if (includeId)
                cmd.Parameters.AddWithValue("@id", a.Id);

            cmd.Parameters.AddWithValue("@project_name", a.ProjectName ?? "");
            cmd.Parameters.AddWithValue("@company", a.Company ?? "");
            cmd.Parameters.AddWithValue("@department", a.Department ?? "");
            cmd.Parameters.AddWithValue("@source_of_fund", a.SourceOfFund ?? "");
            cmd.Parameters.AddWithValue("@beneficiaries_count", a.BeneficiariesCount);

            var type = (a.BudgetType ?? "Money").Trim();
            type = type.Equals("InKind", StringComparison.OrdinalIgnoreCase) ? "InKind" : "Money";
            cmd.Parameters.AddWithValue("@budget_type", type);

            if (type == "Money")
            {
                cmd.Parameters.AddWithValue("@budget_amount", (object?)a.BudgetAmount ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@budget_qty", DBNull.Value);
                cmd.Parameters.AddWithValue("@budget_unit", DBNull.Value);
            }
            else
            {
                cmd.Parameters.AddWithValue("@budget_amount", DBNull.Value);
                cmd.Parameters.AddWithValue("@budget_qty", (object?)a.BudgetQty ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@budget_unit", string.IsNullOrWhiteSpace(a.BudgetUnit) ? DBNull.Value : a.BudgetUnit);
            }
        }

        public List<AllotmentProjectOption> GetAllProjects()
        {
            using var conn = MySqlDb.OpenConnection();
            using var cmd = conn.CreateCommand();

            // ⚠️ assumes your allotments table has these columns
            cmd.CommandText = @"
SELECT
    id,
    project_name,
    company,
    department,
    source_of_fund,
    budget_type,
    budget_amount,
    budget_qty,
    budget_unit
FROM allotments
ORDER BY id ASC;";

            using var rd = cmd.ExecuteReader();
            var list = new List<AllotmentProjectOption>();

            while (rd.Read())
            {
                list.Add(new AllotmentProjectOption
                {
                    Id = rd.GetInt32("id"),
                    ProjectName = rd.GetString("project_name"),
                    Company = rd.GetString("company"),
                    Department = rd.GetString("department"),
                    SourceOfFund = rd.GetString("source_of_fund"),
                    BudgetType = rd.GetString("budget_type"),
                    BudgetAmount = rd.IsDBNull("budget_amount") ? null : rd.GetDecimal("budget_amount"),
                    BudgetQty = rd.IsDBNull("budget_qty") ? null : rd.GetInt32("budget_qty"),
                    BudgetUnit = rd.IsDBNull("budget_unit") ? null : rd.GetString("budget_unit"),
                });
            }

            return list;
        }
    }
}
