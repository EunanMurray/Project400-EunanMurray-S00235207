namespace Project400API.Data;

public class StoredCredential
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();
    public byte[] UserHandle { get; set; } = Array.Empty<byte>();
    public uint SignCount { get; set; }
    public string CredType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid AaGuid { get; set; }

    public User User { get; set; } = null!;
}
