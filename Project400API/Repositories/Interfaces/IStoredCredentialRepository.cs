using Project400API.Data;

namespace Project400API.Repositories.Interfaces;

public interface IStoredCredentialRepository : IRepository<StoredCredential>
{
    Task<List<StoredCredential>> GetByUserIdAsync(Guid userId);
    Task<bool> ExistsByCredentialIdAsync(byte[] credentialId);
    Task<StoredCredential?> GetWithUserByCredentialIdAsync(byte[] credentialId);
    Task<StoredCredential?> GetByUserHandleAndCredentialIdAsync(byte[] userHandle, byte[] credentialId);
}
