# Multi-Camera CCTV Surveillance and Operations System

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-MVC%20%26%20Web%20API-512BD4?logo=dotnet)](https://learn.microsoft.com/en-us/aspnet/core/)
[![MediaMTX](https://img.shields.io/badge/MediaMTX-RTSP%20%E2%86%92%20HLS-00ADD8)](https://github.com/bluenviron/mediamtx)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-EF%20Core-CC2927?logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![HLS.js](https://img.shields.io/badge/HLS.js-Adaptive%20Streaming-F7DF1E?logo=javascript&logoColor=black)](https://github.com/video-dev/hls.js)
[![SignalR](https://img.shields.io/badge/SignalR-Real--Time%20Hub-0078D4)](https://learn.microsoft.com/en-us/aspnet/core/signalr/)

A production-ready **24/7 Multi-Camera IP Surveillance and Operations Platform** built using **ASP.NET Core (.NET 8/9)**, **MediaMTX**, **Entity Framework Core (SQL Server)**, **SignalR**, and **HLS.js**. Engineered specifically for **Imou IP cameras**, the system provides parallel multi-stream live wall monitoring, automated local subnet camera discovery, continuous 15-minute MP4 chunk recording, and seekable HTTP 206 range video playback.

---

## Problem Statement and System Architecture

Modern web browsers such as Google Chrome, Microsoft Edge, and Firefox cannot natively render raw **RTSP** (`port 554`) streams emitted by IP surveillance cameras. Furthermore, consumer and prosumer IP cameras—such as the Imou Ranger (`IPC-A42D`) and Cruiser (`IPC-S41FP`) series—strictly restrict concurrent direct RTSP connections to one or two sessions. Opening multiple browser tabs directly against a camera's IP address triggers hardware session lockouts (`403 Forbidden`), while browser-based players alone cannot continuously archive video segments to local storage 24/7.

To overcome these constraints, the platform implements a **3-Tier Decoupled Architecture**. In this design, **MediaMTX** acts as a centralized media proxy that maintains a single persistent RTSP connection per camera, repackages the video stream into browser-compatible **HLS (`.m3u8` + `.ts`)** segments for unlimited concurrent viewers, and archives continuous 15-minute `.mp4` chunks to disk. Meanwhile, the **ASP.NET Core** backend serves as the control plane—discovering cameras on the local Ethernet subnet, registering paths dynamically via MediaMTX's Control API, cataloging recorded segments in **SQL Server** via webhooks, and broadcasting real-time health telemetry over **SignalR**.

```text
┌───────────────────────────┐       RTSP (TCP :554)       ┌────────────────────────────────────┐
│     Imou IP Cameras       │ ──────────────────────────► │             MediaMTX               │
│ • IPC-A42D (Ranger 2)     │   H.264 Sub/Main Stream     │ • Single RTSP Source Puller       │
│ • IPC-S41FP (Cruiser SE)  │                             │ • HLS Muxer (:8888/index.m3u8)     │
└───────────────────────────┘                             │ • Control REST API (:9997)         │
                                                          │ • 15-Min MP4 Segment Recorder      │
                                                          └─────────────────┬──────────────────┘
                                                                            │
                                           ┌────────────────────────────────┴────────────────────────────────┐
                                           │ HLS (.m3u8 + .ts) Stream                      Recording Webhook │
                                           ▼                                                                 ▼
                         ┌───────────────────────────────────┐                             ┌───────────────────────────────────┐
                         │    Operations Web Dashboard       │                             │    ASP.NET Core Backend (.NET)    │
                         │ • Parallel Stacked Camera Wall    │ ◄─────── SignalR ─────────► │ • Subnet Auto-Discovery Scanner   │
                         │ • Isolated HLS.js Video Players   │      Real-Time Status       │ • Startup MediaMTX Path Sync      │
                         │ • Zero-Blink Incremental DOM      │                             │ • EF Core + SQL Server Catalog    │
                         │ • HTTP 206 Seekable VOD Playback  │                             │ • 24/7 Background Health Worker   │
                         └───────────────────────────────────┘                             └───────────────────────────────────┘
```

---

## Core Features and Operational Workflow

The operations dashboard renders multiple IP camera feeds simultaneously in a stacked parallel live wall, allowing security operators to monitor all rooms or entry points from a single screen. Whenever a new camera is added or an offline device is removed, the frontend updates the DOM incrementally so that existing active `HLS.js` players continue streaming smoothly without blinking, reloading, or losing their live buffer position.

For onboarding hardware without manual IP lookups, the backend includes an asynchronous TCP port `554` scanner (`CameraDiscoveryService`) that sweeps active local IPv4 subnets in under two seconds, matches discovered cameras against configured sticker Safety Codes, persists them in SQL Server, and registers their RTSP paths in MediaMTX. Additionally, a startup synchronization routine in `Program.cs` automatically re-registers all database-saved cameras with MediaMTX whenever the application restarts, ensuring zero-touch recovery after server reboots.

Alongside live monitoring, MediaMTX continuously records incoming camera streams into 15-minute `.mp4` segments. As each segment finishes writing to disk, MediaMTX fires an HTTP webhook (`POST /api/recordings/catalog`) to log the file path, start timestamp, end timestamp, duration, and byte size in SQL Server. Operators can query historical footage by timestamp, receiving a dynamically generated sliding `.m3u8` playlist backed by **HTTP 206 Partial Content** range streaming for instant timeline scrubbing.

---

## Technology Stack and Libraries

The backend is built on **ASP.NET Core MVC and Web API (.NET 8/9)**, handling REST endpoints, webhook ingestion, dependency injection, and Razor dashboard rendering. **Entity Framework Core** paired with **Microsoft SQL Server** manages relational persistence for camera credentials (`Cameras` table) and time-indexed video chunks (`CameraRecordings` table).

For video ingestion and delivery, **MediaMTX** handles low-overhead RTSP pulling, zero-transcode MPEG-TS HLS muxing on port `8888`, disk recording, and dynamic path management via its REST Control API on port `9997`. On the client side, **HLS.js** enables Media Source Extensions (MSE) playback of `.m3u8` streams in Chrome and Edge with automatic `recoverMediaError()` handling, while **ASP.NET Core SignalR** pushes real-time `ONLINE` and `RECONNECTING...` telemetry from the background `IHostedService` health worker (`CameraHealthMonitorService`) directly to the Bootstrap 5 operations UI.

---

## Project Directory Structure

```text
CCTVManagement.Web/
├── Controllers/
│   ├── ApiController.cs                 # REST API: Camera CRUD, Auto-Discovery, Recording Webhook, & VOD Playback
│   └── HomeController.cs                # Renders the main Operations Panel view
├── Data/
│   └── ApplicationDbContext.cs          # EF Core DbContext defining Cameras and CameraRecordings DbSets
├── Entities/
│   ├── Camera.cs                        # Entity: Id, Name, IpAddress, SafetyCode, RtspUrl, IsConnected
│   └── CameraRecording.cs               # Entity: Id, CameraId, FilePath, StartTime, EndTime, DurationSeconds, FileSizeBytes
├── Hubs/
│   └── CameraHub.cs                     # SignalR Hub for pushing live health telemetry to connected dashboards
├── Services/
│   ├── CameraDiscoveryService.cs        # Scans active NIC IPv4 subnets for open RTSP (port 554) endpoints
│   ├── CameraHealthMonitorService.cs    # Hosted background service monitoring stream uptime 24/7
│   └── MediaMtxService.cs               # Typed HttpClient wrapper for MediaMTX Control API (PATCH/POST :9997)
├── Views/
│   └── Home/
│       └── Index.cshtml                 # Parallel Live Wall UI, HLS.js buffer management, & Camera Controls
├── appsettings.json                     # SQL Server connection string, MediaMTX endpoints, & Safety Code pool
└── Program.cs                           # Service registration, startup DB-to-MediaMTX sync, & HTTP pipeline
```

---

## Prerequisites and Installation

To run this system locally or on a Windows surveillance server, ensure you have the **.NET 8.0 or 9.0 SDK**, **Microsoft SQL Server** (Express, Developer, or LocalDB), the **MediaMTX** binary (`mediamtx.exe`), and **Imou IP cameras** connected to the same local Ethernet subnet with their physical sticker Safety Codes ready.

First, clone the repository and navigate into the project directory:

```bash
git clone [https://github.com/zuhaib1233/CCTVManagement.Web.git](https://github.com/zuhaib1233/CCTVManagement.Web.git)
cd CCTVManagement.Web
```

Next, update `appsettings.json` with your SQL Server connection string and list all camera sticker **Safety Codes** under `CameraDefaults:SafetyCodes` so the auto-discovery engine can authenticate newly detected devices automatically:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=CCTVManagementDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "MediaMtx": {
    "ApiBaseUrl": "http://localhost:9997",
    "HlsBaseUrl": "http://localhost:8888"
  },
  "CameraDefaults": {
    "DefaultSafetyCode": "L2A54EB0",
    "SafetyCodes": [
      "L2A54EB0",
      "L2C4B3C8"
    ]
  }
}
```

Apply the Entity Framework Core migrations to create the database schema, start `mediamtx.exe`, and launch the ASP.NET Core application:

```bash
dotnet ef database update
dotnet run
```

Once running, open `https://localhost:7027` in Chrome or Edge to access the **CCTV Operations Panel**.

---

## MediaMTX Configuration

To prevent 1–2 second browser buffer stalls caused by variable keyframe intervals on Imou cameras and to enable automated 15-minute disk cataloging, configure `mediamtx.yml` as follows:

```yaml
# Enable Control API for dynamic ASP.NET Core path registration
api: yes
apiAddress: :9997

# HLS Settings (MPEG-TS prevents fMP4 keyframe stalls on Imou variable GOPs)
hls: yes
hlsAddress: :8888
hlsAllowOrigin: '*'
hlsVariant: mpegts
hlsSegmentCount: 5
hlsSegmentDuration: 1s

# Default Path Recording & Webhook Settings
pathDefaults:
  record: yes
  recordPath: ./recordings/%path/%Y-%m-%d_%H-%M-%S
  recordFormat: fmp4
  recordSegmentDuration: 15m
  runOnRecordSegmentComplete: >
    curl -X POST http://localhost:5000/api/recordings/catalog
    -H "Content-Type: application/json"
    -d "{\"cameraId\":\"$MTX_PATH\",\"path\":\"$MTX_SEGMENT_PATH\",\"duration\":900}"
```

---

## REST API Reference

The `ApiController` exposes endpoints for camera lifecycle management and historical recording playback:

* **`GET /api/cameras`** — Retrieves all registered cameras from SQL Server to populate the parallel live wall.
* **`POST /api/cameras/auto-configure`** — Scans local subnets on TCP port `554`, matches Safety Codes, persists devices to SQL Server, and registers paths in MediaMTX.
* **`POST /api/cameras/add-manual?ip={ip}&safetyCode={code}`** — Manually registers or updates a single camera using its IP address and sticker Safety Code.
* **`DELETE /api/cameras/{id}`** — Removes a camera record from SQL Server and detaches it from the dashboard.
* **`POST /api/recordings/catalog`** — Receives JSON payloads (`cameraId`, `path`, `duration`) from MediaMTX's `runOnRecordSegmentComplete` hook to index 15-minute `.mp4` segments.
* **`GET /api/playback/{cameraId}/playlist.m3u8?startTime={isoDate}&take={count}`** — Dynamically generates a sliding HLS VOD playlist covering the requested timestamp window.
* **`GET /api/playback/stream/{id}`** — Serves the physical `.mp4` recording chunk using HTTP 206 Partial Content range processing for instant seeking.

---

## Hardware Troubleshooting and Imou Engineering Notes

When deploying Imou IP cameras in a browser-based surveillance system, several hardware-specific behaviors require attention. By default, Imou cameras encode their Main Stream (`subtype=0`) in **H.265 (HEVC)**, which causes black video screens in standard desktop browsers. Pointing the RTSP URL to the Sub-Stream (`rtsp://admin:{code}@{ip}:554/cam/realmonitor?channel=1&subtype=1`) forces **H.264** output for universal browser compatibility, though certain models such as the `IPC-S41FP` may require `subtype=0` if sub-stream RTSP is disabled in firmware.

If MediaMTX logs `403 (Forbidden)` when pulling a camera stream, the camera's hardware RTSP slot is either locked by an active mobile app session or the device has not yet been initialized following a factory reset. Closing the **Imou Life** mobile app and power-cycling the camera for five seconds clears hung sockets, while opening **Imou Life** (`Me -> Tools -> LAN Live View`) once with the sticker Safety Code unlocks local RTSP access on newly reset units. Finally, if a newly connected camera does not immediately appear in Windows `arp -a`, running a local subnet ping sweep refreshes the ARP table—or holding the camera's physical reset button for ten seconds restores DHCP if the unit retained an old static IP address.

---

## Author

**Zuhaib Shamsher**  
GitHub: [@zuhaib1233](https://github.com/zuhaib1233) | Karachi, Pakistan
