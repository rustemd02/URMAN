using System.Security.Cryptography;
using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// The car radio's dial without a vehicle or a native window: every station
/// loads its own authored programme with matching bytes and lengths, tuning
/// changes the station and its dashboard frequency, a station resumes where
/// it was left, tuning while off stays silent, and a save restores the tuned
/// station (an older save without a station id restores the first). The full
/// driving proof in vehicle_smoke_test needs a focused window. No listening or
/// language acceptance.
/// </summary>
public partial class VehicleRadioDialSmokeTest : Node
{
    public override async void _Ready()
    {
        try
        {
            var radio = new VehicleRadioPlayer { Name = "Radio" };
            AddChild(radio);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(radio.StationCount == VehicleRadioPlayer.StationPaths.Length && radio.StationCount >= 2,
                "the dial carries every authored station");
            Require(radio.Tuning == "— —", "a switched-off radio shows dashes");
            for (var i = 0; i < radio.StationCount; i++)
            {
                ValidateStation(radio);
                radio.NextStation();
            }
            Require(radio.StationIndex == 0, "cycling the dial returns to the first station");

            radio.SetEnabled(true);
            var first = radio.CurrentStationId;
            var tuning = radio.Tuning;
            for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var segment = radio.CurrentSegmentId;
            var offset = radio.SegmentOffset;
            Require(offset > 0, "an enabled radio advances its programme");
            Require(radio.NextStation() && radio.CurrentStationId != first && radio.Tuning != tuning,
                "tuning changes the station and the dashboard frequency");
            Require(radio.Tuning.Contains('.'), "the dashboard reads a frequency, not a name: " + radio.Tuning);
            radio.NextStation();
            Require(radio.CurrentStationId == first && radio.CurrentSegmentId == segment
                && Math.Abs(radio.SegmentOffset - offset) < .05,
                "tuning away and back resumes the same place");

            radio.SetEnabled(false);
            Require(radio.NextStation() && !radio.Enabled && radio.Tuning == "— —",
                "tuning while off does not switch the radio on");
            var saved = JsonSerializer.SerializeToElement(radio.Capture());
            var tuned = radio.CurrentStationId;
            radio.NextStation();
            radio.Restore(saved);
            Require(radio.CurrentStationId == tuned && !radio.Enabled, "a save restores the tuned station and its power");
            radio.Restore(JsonSerializer.SerializeToElement(new { enabled = true, segmentId = "", offsetSeconds = 0 }));
            Require(radio.StationIndex == 0 && radio.Enabled, "an older save without a station id restores the first station");

            GD.Print($"vehicle-radio-dial: PASS stations={radio.StationCount} bytes+lengths per station, tuning, phase memory, off-dial silence, save/restore; no listening or language acceptance");
            radio.SetEnabled(false);
            radio.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError("vehicle-radio-dial: " + error.Message);
            GetTree().Quit(1);
        }
    }

    private static void ValidateStation(VehicleRadioPlayer radio)
    {
        var path = VehicleRadioPlayer.StationPaths[radio.StationIndex];
        using var file = global::Godot.FileAccess.Open(path, global::Godot.FileAccess.ModeFlags.Read)
            ?? throw new InvalidOperationException("Missing station file: " + path);
        using var document = JsonDocument.Parse(file.GetAsText());
        var programme = document.RootElement.GetProperty("segments").EnumerateArray().Select(VehicleRadioSegment.FromJson).ToArray();
        Require(radio.CurrentStationId == document.RootElement.GetProperty("stationId").GetString(), path + " is the tuned station");
        Require(radio.ProgrammedSegmentCount == programme.Length, path + " reports its own programme");
        foreach (var segment in programme.Where(segment => segment.CanPlay))
        {
            using var input = File.OpenRead(ProjectSettings.GlobalizePath(segment.StreamPath));
            Require(Convert.ToHexString(SHA256.HashData(input)).Equals(segment.Sha256, StringComparison.OrdinalIgnoreCase),
                path + " delivers the provenance bytes of " + segment.Id);
            var stream = ResourceLoader.Load<AudioStream>(segment.StreamPath);
            Require(stream is not null && Math.Abs(stream.GetLength() - segment.DurationSeconds) < .025,
                path + " decodes " + segment.Id + " at its authored length");
        }
        foreach (var repeat in programme.Where(segment => segment.IsRecordingRepeat))
            Require(repeat.HasValidRepeatSource(programme), path + " declares repeat " + repeat.Id + " against its source");
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }
}
