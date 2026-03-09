using System.Collections.Generic;
using System.Threading.Tasks;
using App1.Domain.Entities;
using App1.Domain.ValueObjects;

namespace App1.Domain.Interfaces;

public interface IDeviceRepository
{
    Task<PagedResult<Device>> GetPagedAsync(QueryParameters query, string instanceId);
    Task<bool> ReturnAsync(List<string> deviceIds);
    Task RefreshCacheAsync(string instanceId);
}
