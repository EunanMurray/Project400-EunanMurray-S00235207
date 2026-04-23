using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400API.Repositories.Interfaces;

namespace Project400API.Repositories;

public class KeycardRepository : RepositoryBase<Keycard>, IKeycardRepository
{
    public KeycardRepository(AppDbContext context) : base(context) { }

    public async Task<Keycard?> GetByCardUidAsync(string cardUid)
        => await DbSet.FirstOrDefaultAsync(k => k.CardUid == cardUid);

    public async Task<Keycard?> GetActiveByCardUidWithUserAsync(string cardUid)
        => await DbSet
            .Include(k => k.User)
                .ThenInclude(u => u.Credentials)
            .FirstOrDefaultAsync(k => k.CardUid == cardUid && k.IsActive);

    public async Task<List<Keycard>> GetAllWithUserAsync()
        => await DbSet.Include(k => k.User).ToListAsync();
}
