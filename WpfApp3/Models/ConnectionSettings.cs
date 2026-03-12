namespace WpfApp3.Models
{
    public class ConnectionSettings
    {
        public string Mode { get; set; } = "Local"; // Local or Server
        public string Host { get; set; } = "127.0.0.1";
        public string Port { get; set; } = "3306";
        public string Database { get; set; } = "ekalinga_db";
        public string Username { get; set; } = "root";
        public string Password { get; set; } = "";
        public bool UseSsl { get; set; } = false;
    }
}