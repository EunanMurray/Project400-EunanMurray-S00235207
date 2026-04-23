using Project400API.Data;

namespace Project400API.Repositories.Interfaces;

public interface ITailgateAlertRepository : IRepository<TailgateAlert>
{
    Task<List<TailgateAlert>> GetPendingAlertsAsync();
    Task<List<TailgateAlert>> GetAllAlertsAsync(int count = 50);
    Task<List<TailgateAlert>> GetRecentByDeviceAsync(string deviceId, int count = 20);
}
