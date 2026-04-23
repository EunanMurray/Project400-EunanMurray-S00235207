using Microsoft.EntityFrameworkCore;
using Project400API.Data;
using Project400API.Repositories.Interfaces;

namespace Project400API.Repositories;

public class UserRepository : RepositoryBase<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<User?> GetByUsernameAsync(string username)
        => await DbSet.FirstOrDefaultAsync(u => u.Username == username);

    public async Task<User?> GetWithCredentialsAsync(string username)
        => await DbSet.Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.Username == username);

    public async Task<List<User>> GetAllWithCredentialsAsync()
        => await DbSet.Include(u => u.Credentials).ToListAsync();
}
