using MySqlConnector;

namespace WpfApp3.Services
{
    public static class MySqlDb
    {
        public static string ConnectionString
        {
            get
            {
                var s = ConnectionSettingsService.Load();

                var host = string.IsNullOrWhiteSpace(s.Host) ? "srv1237.hstgr.io" : s.Host;
                var port = uint.TryParse(s.Port, out var p) ? p : 3306;
                var database = string.IsNullOrWhiteSpace(s.Database) ? "u621755393_CDestributions" : s.Database;
                var username = string.IsNullOrWhiteSpace(s.Username) ? "u621755393_destribution" : s.Username;
                var password = string.IsNullOrWhiteSpace(s.Password) ? "Dssc@2026" : s.Password;
                var useSsl = string.IsNullOrWhiteSpace(s.Mode) ? true : s.UseSsl;

                var builder = new MySqlConnectionStringBuilder
                {
                    Server = host,
                    Port = port,
                    Database = database,
                    UserID = username,
                    Password = password,
                    SslMode = useSsl ? MySqlSslMode.Required : MySqlSslMode.None
                };

                return builder.ConnectionString;
            }
        }

        public static MySqlConnection OpenConnection()
        {
            var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }
    }
}