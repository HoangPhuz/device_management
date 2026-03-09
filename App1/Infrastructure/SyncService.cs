using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace App1.Infrastructure;

public class SyncService : IDisposable
{
    private const int Port = 54321;
    private const string Message = "DATA_CHANGED";
    private readonly UdpClient _listener;
    private readonly string _instanceId;
    private CancellationTokenSource? _cts;

    public event Action? DataChanged;
    public event Action? LocalDataChanged;

    public SyncService(string instanceId)
    {
        _instanceId = instanceId;
        _listener = new UdpClient();
        _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
    }

    public void StartListening()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => ListenLoop(_cts.Token));
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _listener.ReceiveAsync(ct);
                var text = Encoding.UTF8.GetString(result.Buffer);

                if (text.StartsWith(Message) && !text.EndsWith(_instanceId))
                {
                    DataChanged?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore transient network errors
            }
        }
    }

    public void Broadcast()
    {
        try
        {
            var data = Encoding.UTF8.GetBytes($"{Message}|{_instanceId}");
            using var client = new UdpClient();
            client.EnableBroadcast = true;
            client.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, Port));
        }
        catch
        {
            // Best effort broadcast
        }

        LocalDataChanged?.Invoke();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _listener.Dispose();
    }
}
