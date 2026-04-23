using Project400API.Data;

namespace Project400API.Repositories.Interfaces;

public interface IDoorRepository : IRepository<Door>
{
    Task<Door?> GetByDeviceIdAsync(string deviceId);
    Task<Door?> GetActiveByDeviceIdAsync(string deviceId);
}
