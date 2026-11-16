using System.ComponentModel.DataAnnotations;

namespace CCTVManagement.Web.Entities;

public class Camera
{
    [Key]
    public string Id { get; set; } = string.Empty; // Example: cam_192_168_1_50
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string SafetyCode { get; set; } = string.Empty;
    public string RtspUrl { get; set; } = string.Empty;
    public bool IsConnected { get; set; } = false;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}