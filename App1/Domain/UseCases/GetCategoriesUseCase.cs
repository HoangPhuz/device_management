using System.Collections.Generic;
using System.Threading.Tasks;
using App1.Domain.Interfaces;

namespace App1.Domain.UseCases;

public class GetCategoriesUseCase
{
    private readonly IDeviceModelRepository _repo;

    public GetCategoriesUseCase(IDeviceModelRepository repo) => _repo = repo;

    public Task<List<string>> GetCategoriesAsync()
        => _repo.GetDistinctCategoriesAsync();

    public Task<List<string>> GetSubCategoriesAsync(string? category = null)
        => _repo.GetDistinctSubCategoriesAsync(category);
}
