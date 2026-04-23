using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400API.Repositories.Interfaces;

namespace Project400API.Repositories;

public class UnlockRequestRepository : RepositoryBase<UnlockRequest>, IUnlockRequestRepository
{
    public UnlockRequestRepository(AppDbContext context) : base(context) { }

    public async Task<UnlockRequest?> GetWithDoorAsync(Guid id)
        => await DbSet.Include(r => r.Door)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<UnlockRequest?> GetWithUserAndDoorAsync(Guid id)
        => await DbSet
            .Include(r => r.User)
            .Include(r => r.Door)
            .FirstOrDefaultAsync(r => r.Id == id);
}
