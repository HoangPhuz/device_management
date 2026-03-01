using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace App1.Infrastructure;

/// <summary>
/// Quản lý InstanceId theo slot: mỗi process lấy một slot (theo thứ tự), InstanceId được persist.
/// Mở lại app → process mới nhận đúng slot (slot cũ đã free vì PID cũ không còn chạy) → dùng lại InstanceId → thấy lại thiết bị đã mượn.
/// Nhiều instance đồng thời → mỗi instance một slot, mỗi slot một InstanceId riêng → không thấy thiết bị của instance khác.
/// </summary>
public static class InstanceSlotManager
{
    private const string SubDir = "DeviceManagement";
    private const string FileName = "instance_slots.json";
    private const int InstanceIdLength = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public static string GetOrCreateInstanceId()
    {
        var path = GetSlotsPath();
        var pid = Environment.ProcessId;

        for (int retry = 0; retry < 5; retry++)
        {
            try
            {
                return GetOrCreateCore(path, pid);
            }
            catch (IOException)
            {
                Thread.Sleep(50 * (retry + 1));
            }
        }

        return Guid.NewGuid().ToString("N")[..InstanceIdLength];
    }

    private static string GetOrCreateCore(string path, int currentPid)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        SlotsData data;
        using (var fs = OpenFileStream(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read))
        {
            data = ReadSlots(fs);
        }

        int slotIndex = -1;
        string? instanceId = null;
        for (int i = 0; i < data.Slots.Count; i++)
        {
            var slot = data.Slots[i];
            if (slot.Pid == 0 || !IsProcessRunning(slot.Pid))
            {
                slotIndex = i;
                instanceId = slot.InstanceId;
                break;
            }
        }

        if (slotIndex < 0)
        {
            slotIndex = data.Slots.Count;
            data.Slots.Add(new SlotEntry { InstanceId = string.Empty, Pid = 0 });
        }

        if (string.IsNullOrEmpty(instanceId))
            instanceId = Guid.NewGuid().ToString("N")[..InstanceIdLength];

        data.Slots[slotIndex] = new SlotEntry { InstanceId = instanceId, Pid = currentPid };

        using (var fs = OpenFileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            WriteSlots(fs, data);
        }

        return instanceId;
    }

    private static bool IsProcessRunning(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static FileStream OpenFileStream(string path, FileMode mode, FileAccess access, FileShare share)
    {
        return new FileStream(path, mode, access, share, bufferSize: 4096, FileOptions.None);
    }

    private static string GetSlotsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, SubDir, FileName);
    }

    private static SlotsData ReadSlots(Stream s)
    {
        if (s.Length == 0)
            return new SlotsData { Slots = new List<SlotEntry>() };

        try
        {
            var doc = JsonSerializer.Deserialize<SlotsData>(s, JsonOptions);
            return doc ?? new SlotsData { Slots = new List<SlotEntry>() };
        }
        catch
        {
            return new SlotsData { Slots = new List<SlotEntry>() };
        }
    }

    private static void WriteSlots(Stream s, SlotsData data)
    {
        JsonSerializer.Serialize(s, data, JsonOptions);
    }

    private class SlotsData
    {
        public List<SlotEntry> Slots { get; set; } = new();
    }

    private class SlotEntry
    {
        public string InstanceId { get; set; } = string.Empty;
        public int Pid { get; set; }
    }
}
