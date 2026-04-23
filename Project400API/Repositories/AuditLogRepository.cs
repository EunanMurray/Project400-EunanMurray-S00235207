using Project400API.Data;
using Project400API.Repositories.Interfaces;

namespace Project400API.Repositories;

public class AuditLogRepository : RepositoryBase<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(AppDbContext context) : base(context) { }
}
