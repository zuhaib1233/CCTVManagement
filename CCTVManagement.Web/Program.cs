using CCTVManagement.Web.Data;
using CCTVManagement.Web.Hubs;
using CCTVManagement.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Register Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Custom CCTV Services
builder.Services.AddHttpClient<MediaMtxService>();
builder.Services.AddSingleton<CameraDiscoveryService>();
builder.Services.AddHostedService<CameraHealthMonitorService>();

var app = builder.Build();

// Auto-sync saved cameras from SQL Server to MediaMTX on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var mediaMtx = scope.ServiceProvider.GetRequiredService<MediaMtxService>();

    var savedCameras = await db.Cameras.Where(c => c.IsConnected).ToListAsync();
    foreach (var cam in savedCameras)
    {
        await mediaMtx.RegisterCameraPathAsync(cam.Id, cam.RtspUrl);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Map SignalR Hub
app.MapHub<CameraHub>("/cameraHub");

app.Run();