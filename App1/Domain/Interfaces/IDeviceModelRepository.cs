using System.Collections.Generic;
using System.Threading.Tasks;
using App1.Domain.Entities;
using App1.Domain.ValueObjects;

namespace App1.Domain.Interfaces;

public interface IDeviceModelRepository
{
    Task<PagedResult<DeviceModel>> GetPagedAsync(QueryParameters query);
    Task<List<string>> GetDistinctCategoriesAsync();
    Task<List<string>> GetDistinctSubCategoriesAsync(string? category = null);
    Task<bool> BorrowAsync(string modelId, int quantity, string instanceId);
}
