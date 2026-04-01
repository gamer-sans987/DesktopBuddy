using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace DesktopBuddy;

/// <summary>
/// HTTP server that serves:
/// - GET /windows -> JSON list of open windows
/// - GET /stream -> MPEG-TS stream via FFmpeg gdigrab (no pipe, FFmpeg captures directly)
/// </summary>
public sealed class MjpegServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Thread _listenThread;
    private volatile bool _running;
    private readonly int _port;

    private readonly List<Process> _ffmpegProcesses = new();
    private readonly object _processLock = new();

    public int Port => _port;
    public string BaseUrl => $"http://localhost:{_port}";

    public MjpegServer(int port = 48080)
    {
        _port = port;
        _listener = new HttpListener();
        _running = true;
        _listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "DesktopBuddy_HTTP" };
    }

    public void Start()
    {
        _listener.Prefixes.Add($"http://localhost:{_port}/");
        _listener.Start();
        _listenThread.Start();
    }

    private void ListenLoop()
    {
        while (_running)
        {
            try
            {
                var ctx = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
            }
            catch (HttpListenerException) { break; }
            catch { }
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";

            if (path == "/windows")
                ServeWindowList(ctx);
            else if (path.StartsWith("/stream"))
                ServeStream(ctx);
            else
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
            }
        }
        catch { try { ctx.Response.Close(); } catch { } }
    }

    private void ServeWindowList(HttpListenerContext ctx)
    {
        var windows = WindowEnumerator.GetOpenWindows();
        var sb = new StringBuilder("[");
        for (int i = 0; i < windows.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var w = windows[i];
            var title = w.Title.Replace("\\", "\\\\").Replace("\"", "\\\"");
            sb.Append($"{{\"handle\":{w.Handle.ToInt64()},\"title\":\"{title}\",\"pid\":{w.ProcessId}}}");
        }
        sb.Append(']');
        var data = Encoding.UTF8.GetBytes(sb.ToString());
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentLength64 = data.Length;
        ctx.Response.OutputStream.Write(data, 0, data.Length);
        ctx.Response.Close();
    }

    private void ServeStream(HttpListenerContext ctx)
    {
        var fpsStr = ctx.Request.QueryString["fps"];
        int fps = 30;
        if (fpsStr != null) int.TryParse(fpsStr, out fps);

        var ffmpegPath = FindFfmpeg();
        if (ffmpegPath == null)
        {
            ctx.Response.StatusCode = 500;
            var err = Encoding.UTF8.GetBytes("FFmpeg not found");
            ctx.Response.OutputStream.Write(err, 0, err.Length);
            ctx.Response.Close();
            return;
        }

        // FFmpeg captures desktop directly via gdigrab
        // Try hardware encoders first, fall back to software
        string encoder = DetectEncoder(ffmpegPath);

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = $"-probesize 32 -fflags nobuffer " +
                        $"-f gdigrab -framerate {fps} -i desktop " +
                        $"-c:v {encoder} -bf 0 -g {fps} " +
                        $"-f mpegts -flush_packets 1 -muxdelay 0 -an pipe:1",
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        Process ffmpeg;
        try
        {
            ffmpeg = Process.Start(psi)!;
        }
        catch
        {
            ctx.Response.StatusCode = 500;
            ctx.Response.Close();
            return;
        }

        ffmpeg.ErrorDataReceived += (s, e) => { };
        ffmpeg.BeginErrorReadLine();

        lock (_processLock) { _ffmpegProcesses.Add(ffmpeg); }

        // Stream FFmpeg stdout to HTTP response
        ctx.Response.ContentType = "video/mp2t";
        ctx.Response.SendChunked = true;
        ctx.Response.StatusCode = 200;

        try
        {
            var buffer = new byte[65536];
            var stdout = ffmpeg.StandardOutput.BaseStream;
            while (_running && !ffmpeg.HasExited)
            {
                int read = stdout.Read(buffer, 0, buffer.Length);
                if (read <= 0) break;
                ctx.Response.OutputStream.Write(buffer, 0, read);
                ctx.Response.OutputStream.Flush();
            }
        }
        catch { }
        finally
        {
            try { ffmpeg.Kill(); } catch { }
            lock (_processLock) { _ffmpegProcesses.Remove(ffmpeg); }
            try { ctx.Response.Close(); } catch { }
        }
    }

    private static string? _cachedEncoder;

    private static string DetectEncoder(string ffmpegPath)
    {
        if (_cachedEncoder != null) return _cachedEncoder;

        // Try NVENC (NVIDIA), AMF (AMD), QSV (Intel), then software fallback
        string[] encoders = { "h264_nvenc", "h264_amf", "h264_qsv", "libx264" };
        foreach (var enc in encoders)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-f lavfi -i nullsrc=s=64x64:d=0.1 -c:v {enc} -f null -",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                p?.WaitForExit(3000);
                if (p?.ExitCode == 0)
                {
                    _cachedEncoder = enc;
                    return enc;
                }
            }
            catch { }
        }
        _cachedEncoder = "libx264";
        return "libx264";
    }

    private static string? FindFfmpeg()
    {
        string[] candidates = {
            @"C:\bins\ffmpeg.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe"),
            "ffmpeg"
        };

        foreach (var c in candidates)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo { FileName = c, Arguments = "-version", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
                p?.WaitForExit(1000);
                if (p?.ExitCode == 0) return c;
            }
            catch { }
        }
        return null;
    }

    public void Dispose()
    {
        _running = false;

        lock (_processLock)
        {
            foreach (var p in _ffmpegProcesses)
            {
                try { p.Kill(); } catch { }
            }
            _ffmpegProcesses.Clear();
        }

        try { _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
    }
}
