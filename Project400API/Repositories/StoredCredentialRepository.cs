using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400API.Repositories.Interfaces;

namespace Project400API.Repositories;

public class StoredCredentialRepository : RepositoryBase<StoredCredential>, IStoredCredentialRepository
{
    public StoredCredentialRepository(AppDbContext context) : base(context) { }

    public async Task<List<StoredCredential>> GetByUserIdAsync(Guid userId)
        => await DbSet.Where(c => c.UserId == userId).ToListAsync();

    public async Task<bool> ExistsByCredentialIdAsync(byte[] credentialId)
        => await DbSet.AnyAsync(c => c.CredentialId == credentialId);

    public async Task<StoredCredential?> GetWithUserByCredentialIdAsync(byte[] credentialId)
        => await DbSet.Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CredentialId == credentialId);

    public async Task<StoredCredential?> GetByUserHandleAndCredentialIdAsync(byte[] userHandle, byte[] credentialId)
        => await DbSet.FirstOrDefaultAsync(c =>
            c.UserId == new Guid(userHandle) && c.CredentialId.SequenceEqual(credentialId));
}
