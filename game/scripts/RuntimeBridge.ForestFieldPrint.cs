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
    private const string FamilyNowPrintKey = "photobook/family-now-print";
    private const string FamilyNowShutterId = "urman.fullgame:interaction/pw-take-now-photo";
    private bool _exposingFieldPrint;
    private object? _fieldPrintCacheSession;
    private readonly System.Collections.Generic.Dictionary<string, FieldPrintImage> _fieldPrintImages = new();

    private sealed class FieldPrintImage(byte[]? jpeg)
    {
        internal byte[]? Jpeg = jpeg;
        internal Texture2D? Front, Thumbnail;
    }

    internal bool HasForestFieldPrint => CachedFieldPrint(FieldPrintKey)?.Jpeg is not null;
    internal bool HasFamilyNowPhotoPrint => FamilyNowPhotoAcquired
        && CachedFieldPrint(FamilyNowPrintKey)?.Jpeg is not null;

    private bool FamilyNowPhotoAcquired => SessionIdentity is { } session && _content.HasPhotoWorlds
        && PhotoFlag(session.SelectState(), "S-NOW", "acquired");

    private bool FieldPrintContextCurrent(object? session, string scene) => session is not null
        && ReferenceEquals(session, SessionIdentity) && !_loadingSlot && !_loadPreparing
        && IsInsideTree() && !IsQueuedForDeletion() && !NeedsPhysicalRecovery && ActiveSceneId == scene;

    // One exposure on the player's shutter press. A second viewport shares only
    // the 3D world: menus, subtitles and the reticle never appear on the print.
    internal async Task<bool> KeepForestFieldPrintAsync(Camera3D camera)
    {
        var session = SessionIdentity;
        bool InForest() => FieldPrintContextCurrent(session, "urman.fullgame:scene/pw-forest")
            && PhotoWorldFactObserved("forest_encounter_resolved");
        if (_exposingFieldPrint || !InForest() || HasForestFieldPrint) return false;
        _exposingFieldPrint = true;
        try
        {
            var bytes = await ExposeFieldPrintAsync(camera, InForest);
            return bytes is not null && await StoreFieldPrintAsync(FieldPrintKey, bytes, InForest);
        }
        finally { _exposingFieldPrint = false; }
    }

    // Only the physical epilogue shutter calls this owner. Semantic/headless
    // dispatch and old saves keep their existing packaged S-NOW fallback.
    internal async Task<bool> TakeFamilyNowPhotoAsync(Camera3D camera, Func<bool> physicalStillValid)
    {
        var session = SessionIdentity;
        bool InHome() => FieldPrintContextCurrent(session, PhotoWorldEpiloguePresence.EpilogueSceneId)
            && CurrentZoneId == "house_old_pc";
        bool Ready() => InHome() && !FamilyNowPhotoAcquired
            && IsInteractionAvailable(FamilyNowShutterId) && physicalStillValid();
        if (_exposingFieldPrint || !Ready()) return false;
        _exposingFieldPrint = true;
        try
        {
            var bytes = await ExposeFieldPrintAsync(camera, Ready);
            if (bytes is null || !await StoreFieldPrintAsync(FamilyNowPrintKey, bytes, Ready) || !Ready()) return false;
            // The normal semantic dispatch's forced checkpoint must already
            // contain these pixels. A rejected shutter leaves no visible art:
            // access requires canonical acquisition, and retry takes a new frame.
            return await DispatchInteractionAsync(FamilyNowShutterId) && InHome() && FamilyNowPhotoAcquired;
        }
        catch (Exception error)
        {
            GD.PushWarning($"Could not keep the family photograph: {error.Message}");
            return false;
        }
        finally { _exposingFieldPrint = false; }
    }

    private async Task<byte[]?> ExposeFieldPrintAsync(Camera3D camera, Func<bool> current)
    {
        if (!current() || !IsInstanceValid(camera) || !camera.Current || camera != GetViewport().GetCamera3D()
            || DisplayServer.GetName() == "headless") return null;
        SubViewport? negative = null;
        try
        {
            negative = new SubViewport
            {
                Name = "FieldPrintExposure", Size = new(PrintWidth, PrintHeight),
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
            // Freeze the original valid shutter pose before any await or semantic
            // effect. The caller's callback rechecks its physical owner afterward.
            lens.GlobalTransform = camera.GlobalTransform;
            lens.Current = true;
            negative.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            if (!current() || !IsInstanceValid(negative) || !IsInstanceValid(camera)
                || !camera.Current || camera != GetViewport().GetCamera3D()) return null;
            using var image = negative.GetTexture().GetImage();
            if (image is null || image.IsEmpty()) return null;
            var bytes = image.SaveJpgToBuffer(.76f);
            if (bytes.Length > MaxPrintBytes) bytes = image.SaveJpgToBuffer(.52f);
            return IsFieldPrintJpeg(bytes) ? bytes : null;
        }
        catch (Exception error)
        {
            GD.PushWarning($"Could not expose the photograph: {error.Message}");
            return null;
        }
        finally
        {
            if (IsInstanceValid(negative)) negative!.QueueFree();
        }
    }

    private async Task<bool> StoreFieldPrintAsync(string key, byte[] bytes, Func<bool> current)
    {
        if (!current()) return false;
        // DispatchWorldProps captures its session synchronously, before its first
        // await. A changed session can only receive false, never the old pixels.
        if (!await DispatchWorldPropsAsync(new JsonArray(new JsonObject
                { ["propId"] = key, ["hasPrint"] = true, ["jpeg"] = Convert.ToBase64String(bytes) }))
            || !current()) return false;
        _fieldPrintImages.Remove(key);
        return true;
    }

    internal Texture2D? DevelopForestFieldPrint() => DevelopFieldPrint(FieldPrintKey, thumbnail: false);
    internal Texture2D? DevelopFamilyNowPhoto(bool thumbnail = false) => FamilyNowPhotoAcquired
        ? DevelopFieldPrint(FamilyNowPrintKey, thumbnail) : null;

    private FieldPrintImage? CachedFieldPrint(string key)
    {
        var session = SessionIdentity;
        if (!ReferenceEquals(session, _fieldPrintCacheSession))
        {
            _fieldPrintImages.Clear();
            _fieldPrintCacheSession = session;
        }
        if (session is null) return null;
        if (_fieldPrintImages.TryGetValue(key, out var cached)) return cached;
        if (!SelectWorldProps().TryGetProperty(key, out var prop) || prop.ValueKind != JsonValueKind.Object
            || !prop.TryGetProperty("jpeg", out var jpeg) || jpeg.ValueKind != JsonValueKind.String) return null;
        var encoded = jpeg.GetString();
        byte[]? bytes = null;
        if (!string.IsNullOrEmpty(encoded) && encoded.Length <= (MaxPrintBytes + 2) / 3 * 4)
        {
            try { bytes = Convert.FromBase64String(encoded); }
            catch (FormatException) { }
        }
        var result = new FieldPrintImage(bytes is not null && IsFieldPrintJpeg(bytes) ? bytes : null);
        _fieldPrintImages.Add(key, result);
        return result;
    }

    private Texture2D? DevelopFieldPrint(string key, bool thumbnail)
    {
        var cached = CachedFieldPrint(key);
        if (cached?.Jpeg is not { } bytes) return null;
        if (cached.Front is null)
        {
            using var image = new Image();
            if (image.LoadJpgFromBuffer(bytes) != Error.Ok)
            {
                cached.Jpeg = null;
                return null;
            }
            cached.Front = ImageTexture.CreateFromImage(image);
            image.Resize(160, 90, Image.Interpolation.Lanczos);
            cached.Thumbnail = ImageTexture.CreateFromImage(image);
        }
        return thumbnail ? cached.Thumbnail : cached.Front;
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
