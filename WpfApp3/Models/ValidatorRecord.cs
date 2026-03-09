using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace WpfApp3.Models
{
    public partial class ValidatorRecord : ObservableObject
    {
        public int Id { get; set; }

        [ObservableProperty] private string beneficiaryId = "";
        [ObservableProperty] private string civilRegistryId = "";

        [ObservableProperty] private string firstName = "";
        [ObservableProperty] private string middleName = "";
        [ObservableProperty] private string lastName = "";

        [ObservableProperty] private string gender = "";
        [ObservableProperty] private string dateOfBirth = "";
        [ObservableProperty] private string classification = "";

        [ObservableProperty] private string barangay = "";
        [ObservableProperty] private string presentAddress = "";

        // "", "Not Validated", "Endorsed", "Pending", "Rejected"
        [ObservableProperty] private string status = "";

        // ✅ NEW: stored in DB
        [ObservableProperty] private byte[]? profileImage;

        // ✅ UI preview
        [ObservableProperty] private BitmapImage? profileImagePreview;

        public bool HasProfileImage => ProfileImagePreview != null;

        public string FullName => $"{LastName}, {FirstName} {MiddleName}".Replace("  ", " ").Trim();

        partial void OnProfileImageChanged(byte[]? value)
        {
            ProfileImagePreview = ToBitmap(value);
            OnPropertyChanged(nameof(HasProfileImage));
        }

        private static BitmapImage? ToBitmap(byte[]? bytes)
        {
            if (bytes is null || bytes.Length == 0) return null;

            try
            {
                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }
}