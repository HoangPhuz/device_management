namespace App1.Domain.Entities;

public class BorrowedDevice
{
    public long Id { get; set; }
    public long DeviceModelId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string IMEI { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string CircuitSerialNumber { get; set; } = string.Empty;
    public string HWVersion { get; set; } = string.Empty;
    public string BorrowedDate { get; set; } = string.Empty;
    public string ReturnDate { get; set; } = string.Empty;
    public string Invoice { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Inventory { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
}
