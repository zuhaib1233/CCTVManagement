using CCTVManagement.Web.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCTVManagement.Web.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<CameraRecording> CameraRecordings => Set<CameraRecording>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<CameraRecording>()
            .HasIndex(r => new { r.CameraId, r.StartTime, r.EndTime });
    }
}