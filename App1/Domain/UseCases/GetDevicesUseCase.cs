using System.Threading.Tasks;
using App1.Domain.Entities;
using App1.Domain.Interfaces;
using App1.Domain.ValueObjects;

namespace App1.Domain.UseCases;

public class GetDevicesUseCase
{
    private readonly IDeviceRepository _repo;

    public GetDevicesUseCase(IDeviceRepository repo) => _repo = repo;

    public Task<PagedResult<Device>> ExecuteAsync(QueryParameters query, string instanceId)
        => _repo.GetPagedAsync(query, instanceId);

    public Task RefreshAsync(string instanceId) => _repo.RefreshCacheAsync(instanceId);
}
