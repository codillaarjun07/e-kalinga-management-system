using System.Configuration;
using MySqlConnector;

namespace WpfApp3.Services
{
    public static class MySqlDb
    {
        public static string ConnectionString =>
            ConfigurationManager.ConnectionStrings["EkalingaDb"]?.ConnectionString
            ?? throw new ConfigurationErrorsException("Missing connection string: EkalingaDb");

        public static MySqlConnection OpenConnection()
        {
            var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }
    }
}
