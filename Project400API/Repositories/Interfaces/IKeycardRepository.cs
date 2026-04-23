using Project400API.Data;

namespace Project400API.Repositories.Interfaces;

public interface IKeycardRepository : IRepository<Keycard>
{
    Task<Keycard?> GetByCardUidAsync(string cardUid);
    Task<Keycard?> GetActiveByCardUidWithUserAsync(string cardUid);
    Task<List<Keycard>> GetAllWithUserAsync();
}
