using System.Collections.Generic;
using System.Threading.Tasks;
using App1.Domain.Entities;
using App1.Domain.ValueObjects;

namespace App1.Domain.Interfaces;

public interface IBorrowedDeviceRepository
{
    Task<PagedResult<BorrowedDevice>> GetPagedAsync(QueryParameters query, string instanceId);
    Task<bool> ReturnAsync(List<long> deviceIds);
}
