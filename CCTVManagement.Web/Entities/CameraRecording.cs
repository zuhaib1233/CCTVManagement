using System.ComponentModel.DataAnnotations;

namespace CCTVManagement.Web.Entities;

public class CameraRecording
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CameraId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DurationSeconds { get; set; }
    public long FileSizeBytes { get; set; }
}