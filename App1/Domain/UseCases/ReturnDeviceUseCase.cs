using System.Collections.Generic;
using System.Threading.Tasks;
using App1.Domain.Interfaces;

namespace App1.Domain.UseCases;

public class ReturnDeviceUseCase
{
    private readonly IBorrowedDeviceRepository _repo;

    public ReturnDeviceUseCase(IBorrowedDeviceRepository repo) => _repo = repo;

    public Task<bool> ExecuteAsync(List<long> deviceIds)
        => _repo.ReturnAsync(deviceIds);
}
