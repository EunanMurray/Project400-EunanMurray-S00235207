using Project400API.Data;

namespace Project400API.Repositories.Interfaces;

public interface IUnlockTokenRepository : IRepository<UnlockToken>
{
    Task<UnlockToken?> GetActiveTokenForDeviceAsync(string deviceId);
}
