using System.Security.Cryptography;
using System.Text.Json;
using Godot;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    // Optional evidence from this game's own viewport. Readback happens after
    // timing has stopped and before the vehicle exit changes the active camera.
    // It does not request desktop capture, force a draw, move input or take focus.
    private void CaptureCompletedPerformanceFrame()
    {
        var directory = OS.GetEnvironment("URMAN_PERF_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        var sample = _performanceSample.Label;
        try
        {
            if (!Path.IsPathFullyQualified(directory) || !Directory.Exists(directory))
                throw new IOException("Performance capture needs an existing absolute evidence directory.");
            if (!_performanceFinishing || _performanceProbe || !_performanceWindowed || !_performanceRealRenderer
                || !string.Equals(_performanceProbeMode, PerformanceProbeModeGameplay, StringComparison.Ordinal)
                || _performanceSamples.Count == 0 || Engine.GetFramesDrawn() == 0
                || _performanceMeasurementElapsed < _performanceDurationSeconds)
                throw new InvalidOperationException("No completed native gameplay measurement frame is available.");
            var viewport = GetViewport();
            using var frame = viewport.GetTexture().GetImage();
            if (frame.IsEmpty()) throw new InvalidOperationException("The completed viewport image is empty.");
            frame.Convert(Image.Format.Rgba8);
            var pixels = frame.GetData();
            var low = 255;
            var high = 0;
            var varied = false;
            var first = pixels.Take(3).ToArray();
            for (var offset = 0; offset + 3 < pixels.Length; offset += 4 * 17)
            {
                for (var channel = 0; channel < 3; channel++)
                {
                    low = Math.Min(low, pixels[offset + channel]);
                    high = Math.Max(high, pixels[offset + channel]);
                    varied |= pixels[offset + channel] != first[channel];
                }
            }
            if (!varied || high - low < 8)
                throw new InvalidOperationException("The viewport frame contains no useful rendered variation.");
            var name = string.Concat(sample.Select(character => char.IsAsciiLetterOrDigit(character)
                || character is '-' or '_' ? character : '_')) + ".png";
            var path = Path.Combine(directory, name);
            var png = frame.SavePngToBuffer();
            if (png.Length == 0) throw new IOException("The viewport did not encode a PNG.");
            using (var file = new FileStream(path, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read))
                file.Write(png);
            GD.Print("act1-performance-frame: " + JsonSerializer.Serialize(new
            {
                status = "CAPTURED", mode = _performanceProbeMode, sample, path, width = frame.GetWidth(), height = frame.GetHeight(),
                sha256 = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant(),
                renderedFramesAtReadback = Engine.GetFramesDrawn(), processFrameAtReadback = Engine.GetProcessFrames(),
                cameraAtReadback = viewport.GetCamera3D()?.GetPath().ToString(),
                poseAtReadback = viewport.GetCamera3D()?.GlobalTransform.ToString(),
                feetAtReadback = _player?.GlobalPosition.ToString(),
                focusedAtReadback = DisplayServer.WindowIsFocused(),
                preset = _player?.GraphicsPreset, scale = viewport.Scaling3DScale, msaa = viewport.Msaa3D.ToString(),
                measuredSamples = _performanceSamples.Count, measuredSeconds = _performanceMeasurementElapsed,
                pixelMinimum = low, pixelMaximum = high, forceDraw = false, poseWrites = 0,
                timing = "last completed viewport frame; readback outside measurement, before transport exit",
                scope = "visual evidence only; performance status remains a separate result"
            }));
        }
        catch (Exception error)
        {
            // A failed picture never changes the already measured FPS status.
            GD.Print("act1-performance-frame: " + JsonSerializer.Serialize(new
                { status = "NOT_CAPTURED", mode = _performanceProbeMode, sample, error = error.Message, timing = "outside measurement" }));
        }
    }
}
