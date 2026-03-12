using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfApp3.Models
{
    public partial class SettingOptionRecord : ObservableObject
    {
        public int Id { get; set; }

        [ObservableProperty] private string name = "";
        [ObservableProperty] private bool isActive = true;
    }
}