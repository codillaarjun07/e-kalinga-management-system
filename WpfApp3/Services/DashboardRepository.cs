using MySqlConnector;
using System.Globalization;

namespace WpfApp3.Services
{
    public class DashboardRepository
    {
        public DashboardSnapshot GetSnapshot()
        {
            using var conn = MySqlDb.OpenConnection();

            return new DashboardSnapshot
            {
                TotalAllotmentAmount = GetTotalMoneyAllotment(conn),
                BeneficiariesCount = GetBeneficiariesCount(conn),
                ProjectsCount = GetProjectsCount(conn),
                ReleasedCount = GetReleasedCount(conn),
                PendingReleaseCount = GetPendingReleaseCount(conn),
                BeneficiaryClassification = GetBeneficiaryClassification(conn),
                YearlyAllotments = GetYearlyAllotments(conn),
                MonthlyProjects = GetMonthlyProjects(conn)
            };
        }

        private static decimal GetTotalMoneyAllotment(MySqlConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT COALESCE(SUM(budget_amount), 0)
FROM allotments
WHERE budget_type = 'Money';";
            return Convert.ToDecimal(cmd.ExecuteScalar() ?? 0m, CultureInfo.InvariantCulture);
        }

        private static int GetBeneficiariesCount(MySqlConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT COUNT(*) FROM beneficiaries;";
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
        }

        private static int GetProjectsCount(MySqlConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT COUNT(*) FROM allotments;";
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
        }

        private static int GetReleasedCount(MySqlConnection conn)
        {
            EnsureAllotmentBeneficiariesTable(conn);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT COUNT(*)
FROM allotment_beneficiaries
WHERE is_released = 1;";
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
        }

        private static int GetPendingReleaseCount(MySqlConnection conn)
        {
            EnsureAllotmentBeneficiariesTable(conn);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT COUNT(*)
FROM allotment_beneficiaries
WHERE COALESCE(is_released, 0) = 0;";
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
        }

        private static List<DashboardSlice> GetBeneficiaryClassification(MySqlConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT
    CASE
        WHEN classification IS NULL OR TRIM(classification) = '' THEN 'Unspecified'
        ELSE classification
    END AS label,
    COUNT(*) AS total
FROM beneficiaries
GROUP BY
    CASE
        WHEN classification IS NULL OR TRIM(classification) = '' THEN 'Unspecified'
        ELSE classification
    END
ORDER BY total DESC, label ASC
LIMIT 6;";

            using var rd = cmd.ExecuteReader();
            var list = new List<DashboardSlice>();

            while (rd.Read())
            {
                list.Add(new DashboardSlice
                {
                    Label = rd.GetString("label"),
                    Value = Convert.ToDouble(rd["total"], CultureInfo.InvariantCulture)
                });
            }

            if (list.Count == 0)
            {
                list.Add(new DashboardSlice { Label = "No Data", Value = 1 });
            }

            return list;
        }

        private static List<DashboardPoint> GetYearlyAllotments(MySqlConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT
    YEAR(created_at) AS yr,
    COALESCE(SUM(CASE WHEN budget_type = 'Money' THEN budget_amount ELSE 0 END), 0) AS total_amount
FROM allotments
GROUP BY YEAR(created_at)
ORDER BY yr ASC;";

            using var rd = cmd.ExecuteReader();
            var list = new List<DashboardPoint>();

            while (rd.Read())
            {
                list.Add(new DashboardPoint
                {
                    Label = rd["yr"].ToString() ?? "",
                    Value = Convert.ToDouble(rd["total_amount"], CultureInfo.InvariantCulture)
                });
            }

            if (list.Count == 0)
            {
                var y = DateTime.Now.Year;
                list.Add(new DashboardPoint { Label = (y - 2).ToString(), Value = 0 });
                list.Add(new DashboardPoint { Label = (y - 1).ToString(), Value = 0 });
                list.Add(new DashboardPoint { Label = y.ToString(), Value = 0 });
            }

            return list;
        }

        private static List<DashboardPoint> GetMonthlyProjects(MySqlConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT
    DATE_FORMAT(created_at, '%b') AS mon,
    YEAR(created_at) AS yr,
    MONTH(created_at) AS mon_num,
    COUNT(*) AS total
FROM allotments
WHERE created_at >= DATE_SUB(CURDATE(), INTERVAL 11 MONTH)
GROUP BY YEAR(created_at), MONTH(created_at), DATE_FORMAT(created_at, '%b')
ORDER BY yr ASC, mon_num ASC;";

            using var rd = cmd.ExecuteReader();
            var list = new List<DashboardPoint>();

            while (rd.Read())
            {
                list.Add(new DashboardPoint
                {
                    Label = rd["mon"].ToString() ?? "",
                    Value = Convert.ToDouble(rd["total"], CultureInfo.InvariantCulture)
                });
            }

            if (list.Count == 0)
            {
                var now = DateTime.Now;
                for (int i = 5; i >= 0; i--)
                {
                    list.Add(new DashboardPoint
                    {
                        Label = now.AddMonths(-i).ToString("MMM", CultureInfo.InvariantCulture),
                        Value = 0
                    });
                }
            }

            return list;
        }

        private static void EnsureAllotmentBeneficiariesTable(MySqlConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS allotment_beneficiaries (
    id INT AUTO_INCREMENT PRIMARY KEY,
    allotment_id INT NOT NULL,
    beneficiary_id INT NOT NULL,
    share_amount DECIMAL(18,2) NULL,
    share_qty INT NULL,
    share_unit VARCHAR(150) NULL,
    is_released TINYINT(1) NOT NULL DEFAULT 0,
    date_released TIMESTAMP NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_allotment_beneficiary (allotment_id, beneficiary_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
            cmd.ExecuteNonQuery();
        }
    }

    public sealed class DashboardSnapshot
    {
        public decimal TotalAllotmentAmount { get; set; }
        public int BeneficiariesCount { get; set; }
        public int ProjectsCount { get; set; }
        public int ReleasedCount { get; set; }
        public int PendingReleaseCount { get; set; }
        public List<DashboardSlice> BeneficiaryClassification { get; set; } = new();
        public List<DashboardPoint> YearlyAllotments { get; set; } = new();
        public List<DashboardPoint> MonthlyProjects { get; set; } = new();
    }

    public sealed class DashboardSlice
    {
        public string Label { get; set; } = "";
        public double Value { get; set; }
    }

    public sealed class DashboardPoint
    {
        public string Label { get; set; } = "";
        public double Value { get; set; }
    }
}