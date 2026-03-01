using System.Threading.Tasks;
using App1.Domain.Interfaces;

namespace App1.Domain.UseCases;

public class BorrowDeviceUseCase
{
    private readonly IDeviceModelRepository _repo;

    public BorrowDeviceUseCase(IDeviceModelRepository repo) => _repo = repo;

    public Task<bool> ExecuteAsync(long modelId, int quantity, string instanceId)
        => _repo.BorrowAsync(modelId, quantity, instanceId);
}
