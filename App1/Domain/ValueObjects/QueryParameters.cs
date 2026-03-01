using System.Collections.Generic;

namespace App1.Domain.ValueObjects;

public class QueryParameters
{
    public Dictionary<string, string> Filters { get; set; } = new();
    public string? SortColumn { get; set; }
    public bool SortAscending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
