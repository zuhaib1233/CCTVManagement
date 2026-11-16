# Multi-Camera CCTV Surveillance & Operations System

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-MVC%20%26%20Web%20API-512BD4?logo=dotnet)](https://learn.microsoft.com/en-us/aspnet/core/)
[![MediaMTX](https://img.shields.io/badge/MediaMTX-RTSP%20%E2%86%92%20HLS-00ADD8)](https://github.com/bluenviron/mediamtx)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-EF%20Core-CC2927?logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![HLS.js](https://img.shields.io/badge/HLS.js-Adaptive%20Streaming-F7DF1E?logo=javascript&logoColor=black)](https://github.com/video-dev/hls.js)
[![SignalR](https://img.shields.io/badge/SignalR-Real--Time%20Hub-0078D4)](https://learn.microsoft.com/en-us/aspnet/core/signalr/)

A production-ready **24/7 Multi-Camera IP Surveillance & Operations Platform** built using **ASP.NET Core (.NET 8/9)**, **MediaMTX**, **Entity Framework Core (SQL Server)**, **SignalR**, and **HLS.js**. Engineered specifically for **Imou IP cameras**, the system provides parallel multi-stream live wall monitoring, automated local subnet camera discovery, continuous 15-minute MP4 chunk recording, and seekable HTTP 206 range video playback.

---

## Table of Contents

1. [Problem Statement & System Architecture](#problem-statement--system-architecture)
2. [Core Features](#core-features)
3. [Technology Stack & Libraries](#technology-stack--libraries)
4. [Project Directory Structure](#project-directory-structure)
5. [Prerequisites](#prerequisites)
6. [Step-by-Step Installation & Setup](#step-by-step-installation--setup)
7. [MediaMTX Configuration](#mediamtx-configuration)
8. [REST API Documentation](#rest-api-documentation)
9. [Hardware Troubleshooting & Imou Engineering Notes](#hardware-troubleshooting--imou-engineering-notes)
10. [Author](#author)

---

## Problem Statement & System Architecture

### Why a Decoupled Proxy Architecture?
1. **Browser Protocol Limitation:** Modern browsers (Chrome, Edge, Firefox) cannot natively render raw **RTSP** (`port 554`) streams.
2. **Hardware Session Lockouts (`403 Forbidden`):** Consumer/prosumer IP cameras (such as Imou Ranger & Cruiser series) strictly limit concurrent RTSP connections (typically 1–2 sessions). Connecting multiple browser tabs directly to a camera locks the hardware stream.
3. **24/7 Disk Recording:** Browsers cannot continuously write video segments to disk without user sessions remaining open.

To solve this, the platform implements a **3-Tier Decoupled Architecture** where **MediaMTX** maintains a single persistent RTSP connection per camera and fans out HLS streams to unlimited web clients while simultaneously archiving MP4 segments to disk:

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
