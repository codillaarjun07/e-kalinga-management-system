using WpfApp3.Models;

namespace WpfApp3.Services
{
    public sealed class LogoService
    {
        private readonly LogosRepository _repo = new();

        public LogoRecord? GetCurrentLogo()
        {
            _repo.EnsureTable();
            return _repo.GetActive();
        }
    }
}