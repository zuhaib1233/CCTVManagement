using System.Text;
using CCTVManagement.Web.Data;
using CCTVManagement.Web.Entities;
using CCTVManagement.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CCTVManagement.Web.Controllers;

public record SegmentCatalogDto(string CameraId, string Path, int Duration);

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly CameraDiscoveryService _discovery;
    private readonly MediaMtxService _mediaMtx;
    private readonly IConfiguration _config;

    public ApiController(
        ApplicationDbContext db,
        CameraDiscoveryService discovery,
        MediaMtxService mediaMtx,
        IConfiguration config)
    {
        _db = db;
        _discovery = discovery;
        _mediaMtx = mediaMtx;
        _config = config;
    }

    // 0. Get All Registered Cameras for Parallel Live Wall
    [HttpGet("cameras")]
    public async Task<IActionResult> GetCameras()
    {
        var cameras = await _db.Cameras.ToListAsync();
        return Ok(cameras);
    }

    // 0b. Delete Dead/Old Camera from Wall & Database
    [HttpDelete("cameras/{id}")]
    public async Task<IActionResult> DeleteCamera(string id)
    {
        var cam = await _db.Cameras.FindAsync(id);
        if (cam == null) return NotFound();

        _db.Cameras.Remove(cam);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // 1. Auto-Discover & Configure Cameras
    [HttpPost("cameras/auto-configure")]
    public async Task<IActionResult> AutoConfigure()
    {
        var ips = await _discovery.DiscoverImouCamerasAsync();
        var knownCodes = _config.GetSection("CameraDefaults:SafetyCodes").Get<List<string>>() ?? new List<string>();
        string fallbackCode = _config["CameraDefaults:DefaultSafetyCode"] ?? "L2A54EB0";
        if (!knownCodes.Contains(fallbackCode)) knownCodes.Insert(0, fallbackCode);

        var configured = new List<string>();

        foreach (var ip in ips)
        {
            string cameraId = $"cam_{ip.Replace(".", "_")}";
            var camera = await _db.Cameras.FirstOrDefaultAsync(c => c.IpAddress == ip || c.Id == cameraId);

            string selectedCode = camera?.SafetyCode ?? fallbackCode;
            string rtspUrl = $"rtsp://admin:{selectedCode}@{ip}:554/cam/realmonitor?channel=1&subtype=1";

            if (camera == null)
            {
                camera = new Camera
                {
                    Id = cameraId,
                    Name = $"Imou Camera ({ip})",
                    IpAddress = ip,
                    SafetyCode = selectedCode,
                    RtspUrl = rtspUrl,
                    IsConnected = true
                };
                await _db.Cameras.AddAsync(camera);
            }
            else
            {
                camera.RtspUrl = rtspUrl;
                camera.IsConnected = true;
            }

            await _mediaMtx.RegisterCameraPathAsync(cameraId, rtspUrl);
            configured.Add(cameraId);
        }

        await _db.SaveChangesAsync();
        return Ok(new { Count = configured.Count, Cameras = configured });
    }

    // 2. Manual Camera Add (Direct IP & Safety Code se connect karne ke liye)
    [HttpPost("cameras/add-manual")]
    public async Task<IActionResult> AddManualCamera([FromQuery] string ip, [FromQuery] string? safetyCode = null)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return BadRequest("IP address zaroori hai.");

        string code = string.IsNullOrWhiteSpace(safetyCode)
            ? _config["CameraDefaults:DefaultSafetyCode"]!
            : safetyCode;

        string cameraId = $"cam_{ip.Replace(".", "_")}";
        string rtspUrl = $"rtsp://admin:{code}@{ip}:554/cam/realmonitor?channel=1&subtype=1";

        var camera = await _db.Cameras.FirstOrDefaultAsync(c => c.Id == cameraId);
        if (camera == null)
        {
            camera = new Camera
            {
                Id = cameraId,
                Name = $"Imou Camera ({ip})",
                IpAddress = ip,
                SafetyCode = code,
                RtspUrl = rtspUrl,
                IsConnected = true
            };
            await _db.Cameras.AddAsync(camera);
        }
        else
        {
            camera.SafetyCode = code;
            camera.RtspUrl = rtspUrl;
            camera.IsConnected = true;
        }

        await _db.SaveChangesAsync();
        var (registered, errorMsg) = await _mediaMtx.RegisterCameraPathAsync(cameraId, rtspUrl);

        return Ok(new { CameraId = cameraId, MediaMtxRegistered = registered, Error = errorMsg, RtspUrl = rtspUrl });
    }

    // 3. MediaMTX Webhook (Har 15 min baad chunk DB mein save karega)
    [HttpPost("recordings/catalog")]
    public async Task<IActionResult> CatalogSegment([FromBody] SegmentCatalogDto dto)
    {
        var end = DateTime.UtcNow;
        var start = end.AddSeconds(-dto.Duration);

        var recording = new CameraRecording
        {
            CameraId = dto.CameraId,
            FilePath = dto.Path,
            StartTime = start,
            EndTime = end,
            DurationSeconds = dto.Duration,
            FileSizeBytes = System.IO.File.Exists(dto.Path) ? new FileInfo(dto.Path).Length : 0
        };

        await _db.CameraRecordings.AddAsync(recording);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // 4. Dynamic Sliding HLS .m3u8 Playlist Generation
    [HttpGet("playback/{cameraId}/playlist.m3u8")]
    public async Task<IActionResult> GetPlaybackPlaylist(string cameraId, [FromQuery] DateTime startTime, [FromQuery] int take = 3)
    {
        var chunks = await _db.CameraRecordings
            .Where(r => r.CameraId == cameraId && r.EndTime >= startTime)
            .OrderBy(r => r.StartTime)
            .Take(take)
            .ToListAsync();

        if (!chunks.Any()) return NotFound("No recordings found.");

        var m3u8 = new StringBuilder();
        m3u8.AppendLine("#EXTM3U");
        m3u8.AppendLine("#EXT-X-VERSION:3");
        m3u8.AppendLine("#EXT-X-TARGETDURATION:900");
        m3u8.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");

        foreach (var chunk in chunks)
        {
            m3u8.AppendLine($"#EXTINF:{chunk.DurationSeconds}.0,");
            m3u8.AppendLine($"/api/playback/stream/{chunk.Id}");
        }

        return Content(m3u8.ToString(), "application/vnd.apple.mpegurl");
    }

    // 5. Physical MP4 Stream with HTTP 206 Range Processing
    [HttpGet("playback/stream/{id}")]
    public async Task<IActionResult> StreamChunk(Guid id)
    {
        var chunk = await _db.CameraRecordings.FindAsync(id);
        if (chunk == null || !System.IO.File.Exists(chunk.FilePath))
            return NotFound();

        return PhysicalFile(chunk.FilePath, "video/mp4", enableRangeProcessing: true);
    }
}