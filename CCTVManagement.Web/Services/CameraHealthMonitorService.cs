using System.Net.Sockets;
using CCTVManagement.Web.Data;
using CCTVManagement.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CCTVManagement.Web.Services;

public class CameraHealthMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<CameraHub> _hub;

    public CameraHealthMonitorService(IServiceScopeFactory scopeFactory, IHubContext<CameraHub> hub)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cameras = await db.Cameras.ToListAsync(stoppingToken);

            foreach (var cam in cameras)
            {
                bool isReachable = await CheckTcpPortAsync(cam.IpAddress, 554, 1500);
                if (cam.IsConnected != isReachable)
                {
                    cam.IsConnected = isReachable;
                    cam.LastSeen = DateTime.UtcNow;
                    await _hub.Clients.All.SendAsync("StatusUpdated", cam.Id, isReachable, stoppingToken);
                }
            }

            await db.SaveChangesAsync(stoppingToken);
            await Task.Delay(10000, stoppingToken);
        }
    }

    private static async Task<bool> CheckTcpPortAsync(string ip, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(ip, port);
            return await Task.WhenAny(task, Task.Delay(timeoutMs)) == task && client.Connected;
        }
        catch { return false; }
    }
}