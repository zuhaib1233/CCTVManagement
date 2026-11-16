using System.Net.Http.Json;

namespace CCTVManagement.Web.Services;

public class MediaMtxService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public MediaMtxService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
        _http.BaseAddress = new Uri(_config["MediaMtx:ApiBaseUrl"] ?? "http://localhost:9997");
    }

    public async Task<(bool Success, string Error)> RegisterCameraPathAsync(string cameraId, string rtspUrl)
    {
        string rawRoot = _config["MediaMtx:StorageRoot"] ?? @"C:\CCTV_Storage\Recordings";
        string camFolder = Path.Combine(rawRoot, cameraId);
        Directory.CreateDirectory(camFolder);

        string storageRoot = rawRoot.Replace("\\", "/");
        string recordPattern = $"{storageRoot}/%path/%Y-%m-%d_%H-%M-%S";

        var payload = new
        {
            source = rtspUrl,
            sourceOnDemand = false,
            record = true,
            recordPath = recordPattern,
            recordFormat = "fmp4",
            recordSegmentDuration = "15m",
            runOnRecordSegmentComplete = $"curl -k -X POST https://localhost:7027/api/recordings/catalog -H \"Content-Type: application/json\" -d \"{{\\\"cameraId\\\":\\\"{cameraId}\\\",\\\"path\\\":\\\"%MTX_SEGMENT_PATH%\\\",\\\"duration\\\":900}}\""
        };

        try
        {
            var response = await _http.PostAsJsonAsync($"/v3/config/paths/add/{cameraId}", payload);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                if (body.Contains("already exists"))
                {
                    var patchRes = await _http.PatchAsJsonAsync($"/v3/config/paths/patch/{cameraId}", payload);
                    if (patchRes.IsSuccessStatusCode) return (true, string.Empty);
                    body = await patchRes.Content.ReadAsStringAsync();
                }
                return (false, $"Status {(int)response.StatusCode}: {body}");
            }
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}