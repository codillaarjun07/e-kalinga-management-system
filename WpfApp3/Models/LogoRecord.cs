using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace WpfApp3.Models
{
    public partial class LogoRecord : ObservableObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public string FileSizeText =>
            FileSizeBytes >= 1024 * 1024
                ? $"{FileSizeBytes / 1024d / 1024d:0.##} MB"
                : $"{FileSizeBytes / 1024d:0.##} KB";

        public BitmapImage? PreviewImage
        {
            get
            {
                if (ImageData == null || ImageData.Length == 0) return null;

                using var ms = new MemoryStream(ImageData);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }

        public string StatusText => IsActive ? "Current Logo" : "Available";
    }
}