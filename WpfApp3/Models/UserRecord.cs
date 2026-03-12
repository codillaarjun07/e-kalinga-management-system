using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfApp3.Models
{
    public partial class UserRecord : ObservableObject
    {
        [ObservableProperty] private bool isSelected;

        public int Id { get; set; }

        [ObservableProperty] private string firstName = "";
        [ObservableProperty] private string lastName = "";
        [ObservableProperty] private string office = "";
        [ObservableProperty] private string role = "";
        [ObservableProperty] private string username = "";

        // We do NOT load real passwords from DB.
        // For table display only.
        [ObservableProperty] private string password = "********";

        [ObservableProperty] private bool isPasswordRevealed;

        [ObservableProperty] private byte[]? profilePicture;
        public bool IsCurrentSessionUser { get; set; }

        public bool HasProfileImage => ProfilePicture is { Length: > 0 };

        public ImageSource? ProfileImagePreview => CreateImage(ProfilePicture);

        public string PasswordDisplay => IsPasswordRevealed
            ? Password
            : "********";

        partial void OnIsPasswordRevealedChanged(bool value)
        {
            OnPropertyChanged(nameof(PasswordDisplay));
        }

        partial void OnPasswordChanged(string value)
        {
            OnPropertyChanged(nameof(PasswordDisplay));
        }

        partial void OnProfilePictureChanged(byte[]? value)
        {
            OnPropertyChanged(nameof(HasProfileImage));
            OnPropertyChanged(nameof(ProfileImagePreview));
        }

        private static ImageSource? CreateImage(byte[]? bytes)
        {
            if (bytes is null || bytes.Length == 0) return null;

            try
            {
                using var ms = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
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