namespace WpfApp3.Models
{
    public class AllotmentProjectOption
    {
        public int Id { get; set; }
        public string ProjectName { get; set; } = "";
        public string Company { get; set; } = "";
        public string Department { get; set; } = "";
        public string SourceOfFund { get; set; } = "";

        // "Money" | "InKind"
        public string BudgetType { get; set; } = "Money";

        public decimal? BudgetAmount { get; set; }
        public int? BudgetQty { get; set; }
        public string? BudgetUnit { get; set; }

        public string TotalBudgetText =>
            BudgetType == "InKind"
                ? $"{(BudgetQty ?? 0)} {(BudgetUnit ?? "").Trim()}".Trim()
                : $"₱ {(BudgetAmount ?? 0m):N2}";

        public override string ToString() => ProjectName;
    }
}
