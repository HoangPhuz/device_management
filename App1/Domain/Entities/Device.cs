namespace App1.Domain.Entities;

public class Device
{
    public string Id { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IMEI { get; set; } = string.Empty;
    public string SerialLab { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string CircuitSerialNumber { get; set; } = string.Empty;
    public string HWVersion { get; set; } = string.Empty;
    public string Status { get; set; } = "Available";
    public string BorrowedDate { get; set; } = string.Empty;
    public string ReturnDate { get; set; } = string.Empty;
    public string Invoice { get; set; } = string.Empty;
    public string Inventory { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
}
