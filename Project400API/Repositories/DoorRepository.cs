using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400API.Repositories.Interfaces;

namespace Project400API.Repositories;

public class DoorRepository : RepositoryBase<Door>, IDoorRepository
{
    public DoorRepository(AppDbContext context) : base(context) { }

    public async Task<Door?> GetByDeviceIdAsync(string deviceId)
        => await DbSet.FirstOrDefaultAsync(d => d.DeviceId == deviceId);

    public async Task<Door?> GetActiveByDeviceIdAsync(string deviceId)
        => await DbSet.FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.IsActive);
}
