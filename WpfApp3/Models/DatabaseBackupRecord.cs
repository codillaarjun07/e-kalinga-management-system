using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace WpfApp3.Models
{
    public partial class DatabaseBackupRecord : ObservableObject
    {
        [ObservableProperty] private int id;
        [ObservableProperty] private string fileName = "";
        [ObservableProperty] private string databaseName = "";
        [ObservableProperty] private string serverName = "";
        [ObservableProperty] private string contentType = "application/sql";
        [ObservableProperty] private long fileSizeBytes;
        [ObservableProperty] private DateTime createdAt;
        [ObservableProperty] private string createdBy = "";

        public string FileSizeText
        {
            get
            {
                double size = FileSizeBytes;
                string[] units = { "B", "KB", "MB", "GB" };
                int unit = 0;

                while (size >= 1024 && unit < units.Length - 1)
                {
                    size /= 1024;
                    unit++;
                }

                return $"{size:0.##} {units[unit]}";
            }
        }

        public string CreatedAtText => CreatedAt.ToString("MMMM dd, yyyy hh:mm tt");
    }
}