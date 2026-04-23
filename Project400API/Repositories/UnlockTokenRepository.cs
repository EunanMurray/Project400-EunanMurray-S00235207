using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400API.Repositories.Interfaces;

namespace Project400API.Repositories;

public class UnlockTokenRepository : RepositoryBase<UnlockToken>, IUnlockTokenRepository
{
    public UnlockTokenRepository(AppDbContext context) : base(context) { }

    public async Task<UnlockToken?> GetActiveTokenForDeviceAsync(string deviceId)
        => await DbSet
            .Include(t => t.User)
            .Where(t => t.DeviceId == deviceId &&
                        t.ExpiresAt > DateTime.UtcNow &&
                        !t.Consumed)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
}
