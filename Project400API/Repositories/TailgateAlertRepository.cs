using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400API.Repositories.Interfaces;

namespace Project400API.Repositories;

public class TailgateAlertRepository : RepositoryBase<TailgateAlert>, ITailgateAlertRepository
{
    public TailgateAlertRepository(AppDbContext context) : base(context) { }

    public async Task<List<TailgateAlert>> GetPendingAlertsAsync()
    {
        return await DbSet
            .Include(a => a.User)
            .Where(a => a.Status == TailgateAlertStatus.Pending)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<TailgateAlert>> GetAllAlertsAsync(int count = 50)
    {
        return await DbSet
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<TailgateAlert>> GetRecentByDeviceAsync(string deviceId, int count = 20)
    {
        return await DbSet
            .Include(a => a.User)
            .Where(a => a.DeviceId == deviceId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync();
    }
}
