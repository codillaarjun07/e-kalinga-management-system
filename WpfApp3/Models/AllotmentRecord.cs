using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace WpfApp3.Models
{
    public partial class AllotmentRecord : ObservableObject
    {
        [ObservableProperty] private bool isSelected;

        public int Id { get; set; }
        public string ProjectName { get; set; } = "";
        public string Company { get; set; } = "";
        public string Department { get; set; } = "";
        public string SourceOfFund { get; set; } = "";
        public int BeneficiariesCount { get; set; }

        // NEW: budget can be money or in-kind
        public string BudgetType { get; set; } = "Money"; // Money | InKind
        public decimal? BudgetAmount { get; set; }        // used when Money
        public int? BudgetQty { get; set; }               // used when InKind
        public string BudgetUnit { get; set; } = "";      // used when InKind (ex: sacks of rice)

        // Table display
        public string BudgetDisplay
        {
            get
            {
                if (string.Equals(BudgetType, "InKind", StringComparison.OrdinalIgnoreCase))
                {
                    var qty = BudgetQty ?? 0;
                    var unit = (BudgetUnit ?? "").Trim();
                    return string.IsNullOrWhiteSpace(unit) ? qty.ToString() : $"{qty} {unit}";
                }

                var amt = BudgetAmount ?? 0m;
                return $"₱ {amt:N2}";
            }
        }
    }
}
