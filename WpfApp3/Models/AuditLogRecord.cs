using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace WpfApp3.Models
{
    public partial class AuditLogRecord : ObservableObject
    {
        [ObservableProperty] private int id;
        [ObservableProperty] private string operationType = "";
        [ObservableProperty] private string tableName = "";
        [ObservableProperty] private string recordId = "";
        [ObservableProperty] private string actorName = "";
        [ObservableProperty] private string description = "";
        [ObservableProperty] private DateTime createdAt;

        public string CreatedAtText => CreatedAt.ToString("MMMM dd, yyyy hh:mm tt");
    }
}