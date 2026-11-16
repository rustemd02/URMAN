using System;
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    private const string FieldPrintKey = "photobook/forest-field-print";
    private const int PrintWidth = 640, PrintHeight = 360, MaxPrintBytes = 64 * 1024;
    private bool _exposingForestPrint;

    internal bool HasForestFieldPrint => SelectWorldProps().TryGetProperty(FieldPrintKey, out var prop)
        && prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("jpeg", out var jpeg)
        && jpeg.ValueKind == JsonValueKind.String;

    // One exposure on the player's shutter press. A second viewport shares only the 3D
    // world: menus, subtitles and the interaction reticle never appear on the print.
    internal async Task<bool> KeepForestFieldPrintAsync(Camera3D camera)
    {
        var session = SessionIdentity;
        bool InForest() => session is not null && ReferenceEquals(session, SessionIdentity)
            && IsInsideTree() && !IsQueuedForDeletion() && !NeedsPhysicalRecovery
            && ActiveSceneId == "urman.fullgame:scene/pw-forest"
            && PhotoWorldFactObserved("forest_encounter_resolved");
        if (_exposingForestPrint || !InForest() || HasForestFieldPrint
            || !IsInstanceValid(camera) || !camera.Current || camera != GetViewport().GetCamera3D()
            || DisplayServer.GetName() == "headless") return false;

        _exposingForestPrint = true;
        SubViewport? negative = null;
        try
        {
            negative = new SubViewport
            {
                Name = "ForestPrintExposure", Size = new(PrintWidth, PrintHeight),
                World3D = camera.GetWorld3D(), CanvasCullMask = 0, GuiDisableInput = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled
            };
            AddChild(negative);
            var lens = new Camera3D
            {
                Projection = camera.Projection, Fov = camera.Fov, Near = camera.Near, Far = camera.Far,
                KeepAspect = camera.KeepAspect, Size = camera.Size, FrustumOffset = camera.FrustumOffset,
                HOffset = camera.HOffset, VOffset = camera.VOffset, Environment = camera.Environment,
                Attributes = camera.Attributes, CullMask = camera.CullMask
            };
            negative.AddChild(lens);
            lens.GlobalTransform = camera.GlobalTransform;
            lens.Current = true;
            negative.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            if (!InForest() || !IsInstanceValid(negative)) return false;
            using var image = negative.GetTexture().GetImage();
            if (image is null || image.IsEmpty()) return false;
            var bytes = image.SaveJpgToBuffer(.76f);
            if (bytes.Length > MaxPrintBytes) bytes = image.SaveJpgToBuffer(.52f);
            if (!IsFieldPrintJpeg(bytes)) return false;
            // Save with this slot, not as a global screenshot. One bounded image per story,
            // immutable after the shutter succeeds; reading the page grants no quest facts.
            return await DispatchWorldPropsAsync(new JsonArray(new JsonObject
            {
                ["propId"] = FieldPrintKey, ["hasPrint"] = true, ["jpeg"] = Convert.ToBase64String(bytes)
            })) && InForest();
        }
        catch (Exception error)
        {
            GD.PushWarning($"Could not develop the forest print: {error.Message}");
            return false;
        }
        finally
        {
            if (IsInstanceValid(negative)) negative!.QueueFree();
            _exposingForestPrint = false;
        }
    }

    internal Texture2D? DevelopForestFieldPrint()
    {
        if (!HasForestFieldPrint) return null;
        var encoded = SelectWorldProps().GetProperty(FieldPrintKey).GetProperty("jpeg").GetString();
        if (string.IsNullOrEmpty(encoded) || encoded.Length > (MaxPrintBytes + 2) / 3 * 4) return null;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(encoded); }
        catch (FormatException) { return null; }
        if (!IsFieldPrintJpeg(bytes)) return null;
        using var image = new Image();
        return image.LoadJpgFromBuffer(bytes) == Error.Ok ? ImageTexture.CreateFromImage(image) : null;
    }

    // Check the encoded dimensions before allocating pixels from a loaded save.
    private static bool IsFieldPrintJpeg(byte[] bytes)
    {
        if (bytes.Length is < 16 or > MaxPrintBytes || bytes[0] != 0xff || bytes[1] != 0xd8) return false;
        for (var offset = 2; offset + 4 <= bytes.Length;)
        {
            if (bytes[offset++] != 0xff) return false;
            while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
            if (offset + 3 > bytes.Length) return false;
            var marker = bytes[offset++];
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
            if (length < 2 || length > bytes.Length - offset || marker is 0xda or 0xd9) return false;
            if (marker is 0xc0 or 0xc2)
                return length >= 8 && bytes[offset + 2] == 8
                    && BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 3, 2)) == PrintHeight
                    && BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 5, 2)) == PrintWidth;
            offset += length;
        }
        return false;
    }
}
