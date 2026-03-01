namespace App1.Domain.Entities;

public class DeviceModel
{
    public long Id { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public int Available { get; set; }
    public int Reserved { get; set; }
}
