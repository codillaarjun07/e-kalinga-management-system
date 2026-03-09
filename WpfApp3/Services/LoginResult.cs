namespace WpfApp3.Services
{
    public record LoginResult(bool Success, string Message, string Role)
    {
        public static LoginResult Ok(string role) => new(true, "", role);
        public static LoginResult Fail(string message) => new(false, message, "");
    }
}
