using System.Threading.Tasks;
using MySqlConnector;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    public static class DbConnectionTester
    {
        public static async Task<(bool ok, string message)> TestAsync(ConnectionSettings settings)
        {
            try
            {
                var builder = new MySqlConnectionStringBuilder
                {
                    Server = settings.Host,
                    Port = uint.TryParse(settings.Port, out var p) ? p : 3306,
                    Database = settings.Database,
                    UserID = settings.Username,
                    Password = settings.Password,
                    SslMode = settings.UseSsl ? MySqlSslMode.Required : MySqlSslMode.None,
                    ConnectionTimeout = 5,
                    DefaultCommandTimeout = 5
                };

                using var conn = new MySqlConnection(builder.ConnectionString);
                await conn.OpenAsync();
                await conn.CloseAsync();

                return (true, "Connection successful.");
            }
            catch (System.Exception ex)
            {
                return (false, $"Connection failed: {ex.Message}");
            }
        }
    }
}