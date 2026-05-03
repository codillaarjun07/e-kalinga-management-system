using System.Windows.Controls;
using WpfApp3.ViewModels.AuditLogs;

namespace WpfApp3.Views.AuditLogs
{
    public partial class AuditLogsView : UserControl
    {
        public AuditLogsView()
        {
            InitializeComponent();
            if (DataContext is null)
                DataContext = new AuditLogsViewModel();
        }

        public AuditLogsView(int focusedAuditLogId)
        {
            InitializeComponent();
            DataContext = new AuditLogsViewModel(focusedAuditLogId);
        }
    }
}
