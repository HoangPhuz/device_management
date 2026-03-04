using System.Collections.Generic;
using System.Threading.Tasks;
using App1.Domain.Interfaces;

namespace App1.Domain.UseCases;

public class ReturnDeviceUseCase
{
    private readonly IDeviceRepository _repo;

    public ReturnDeviceUseCase(IDeviceRepository repo) => _repo = repo;

    public Task<bool> ExecuteAsync(List<string> deviceIds)
        => _repo.ReturnAsync(deviceIds);
}
