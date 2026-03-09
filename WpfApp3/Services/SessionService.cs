namespace WpfApp3.Services
{
    // POC session store (later: replace with proper auth/session)
    public static class SessionService
    {
        public static string? Username { get; private set; }
        public static string? Role { get; private set; }

        public static void Start(string username, string role)
        {
            Username = username;
            Role = role;
        }

        public static void Clear()
        {
            Username = null;
            Role = null;
        }

        public static bool IsLoggedIn => !string.IsNullOrWhiteSpace(Username);
    }
}
