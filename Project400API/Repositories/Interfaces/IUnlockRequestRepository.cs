using Project400API.Data;

namespace Project400API.Repositories.Interfaces;

public interface IUnlockRequestRepository : IRepository<UnlockRequest>
{
    Task<UnlockRequest?> GetWithDoorAsync(Guid id);
    Task<UnlockRequest?> GetWithUserAndDoorAsync(Guid id);
}
