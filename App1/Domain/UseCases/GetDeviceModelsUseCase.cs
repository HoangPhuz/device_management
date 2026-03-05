using System.Threading.Tasks;
using App1.Domain.Entities;
using App1.Domain.Interfaces;
using App1.Domain.ValueObjects;

namespace App1.Domain.UseCases;

public class GetDeviceModelsUseCase
{
    private readonly IDeviceModelRepository _repo;

    public GetDeviceModelsUseCase(IDeviceModelRepository repo) => _repo = repo;

    public Task<PagedResult<DeviceModel>> ExecuteAsync(QueryParameters query)
        => _repo.GetPagedAsync(query);

    public Task RefreshAsync() => _repo.RefreshCacheAsync();
}
