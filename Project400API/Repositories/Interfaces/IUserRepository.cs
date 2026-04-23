using Project400API.Data;

namespace Project400API.Repositories.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetWithCredentialsAsync(string username);
    Task<List<User>> GetAllWithCredentialsAsync();
}
