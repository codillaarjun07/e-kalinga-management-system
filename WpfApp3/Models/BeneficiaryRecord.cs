using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace WpfApp3.Models
{
    // EKALINGA_DISTRIBUTION_SEARCH_PROFILE_V2
    public partial class BeneficiaryRecord : ObservableObject
    {
        [ObservableProperty] private bool isSelected;

        public int Id { get; set; }

        [ObservableProperty] private string firstName = "";
        [ObservableProperty] private string lastName = "";
        [ObservableProperty] private string gender = "";
        [ObservableProperty] private string barangay = "";

        // ✅ NEW
        [ObservableProperty] private string classification = "None";

        // share fields from allotment_beneficiaries
        [ObservableProperty] private decimal? shareAmount;
        [ObservableProperty] private int? shareQty;
        [ObservableProperty] private string? shareUnit;

        [ObservableProperty] private bool isReleased;
        [ObservableProperty] private string beneficiaryId = "";  // e.g. "BENE-000123"

        [ObservableProperty] private string civilRegistryId = "";
        [ObservableProperty] private string middleName = "";
        [ObservableProperty] private string presentAddress = "";
        [ObservableProperty] private byte[]? profileImage;
        [ObservableProperty] private BitmapImage? profileImagePreview;

        public bool HasProfileImage => ProfileImagePreview != null;

        // ✅ display text used by XAML binding ShareText
        public string ShareText
        {
            get
            {
                if (ShareAmount is not null)
                    return $"₱ {ShareAmount.Value:N2}";

                if (ShareQty is not null && !string.IsNullOrWhiteSpace(ShareUnit))
                    return $"{ShareQty.Value:N0} {ShareUnit}";

                return "";
            }
        }

        public string ReleasedText => IsReleased ? "Released" : "Not Released";

        // make ShareText refresh whenever underlying fields change
        partial void OnShareAmountChanged(decimal? value) => OnPropertyChanged(nameof(ShareText));
        partial void OnShareQtyChanged(int? value) => OnPropertyChanged(nameof(ShareText));
        partial void OnShareUnitChanged(string? value) => OnPropertyChanged(nameof(ShareText));

        partial void OnProfileImageChanged(byte[]? value)
        {
            ProfileImagePreview = ToBitmap(value);
            OnPropertyChanged(nameof(HasProfileImage));
        }

        private static BitmapImage? ToBitmap(byte[]? bytes)
        {
            if (bytes is null || bytes.Length == 0)
                return null;

            try
            {
                using var stream = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
