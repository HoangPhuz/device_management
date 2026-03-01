using System.Threading.Tasks;
using App1.Domain.Entities;
using App1.Domain.Interfaces;
using App1.Domain.ValueObjects;

namespace App1.Domain.UseCases;

public class GetBorrowedDevicesUseCase
{
    private readonly IBorrowedDeviceRepository _repo;

    public GetBorrowedDevicesUseCase(IBorrowedDeviceRepository repo) => _repo = repo;

    public Task<PagedResult<BorrowedDevice>> ExecuteAsync(QueryParameters query, string instanceId)
        => _repo.GetPagedAsync(query, instanceId);
}
