using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

public partial class Main
{
    private string? _interactiveFrameDirectory;
    private string? _interactiveFrameJob;
    private SceneTree? _interactiveFrameTree;
    private long _interactiveFrameNextPoll;
    private bool _interactiveFrameBusy;
    private FirstPersonController? _interactiveMovementPlayer;
    private Vector3 _interactiveMovementBefore;
    private Vector2 _interactiveMovementInput;
    private bool _interactiveMovementEligible;
    private JsonObject? _interactiveMovementSample;

    // A protected native play job may export its current viewport on demand.
    // This never moves the camera/player, applies input, changes saves or quits.
    private void ConfigureInteractiveFrameCapture()
    {
        var directory = System.Environment.GetEnvironmentVariable("URMAN_INTERACTIVE_FRAME_DIR");
        var job = System.Environment.GetEnvironmentVariable("URMAN_INTERACTIVE_FRAME_JOB");
        if (string.IsNullOrEmpty(directory) && string.IsNullOrEmpty(job)) return;
        try
        {
            if (!OperatingSystem.IsWindows()
                || System.Environment.GetEnvironmentVariable("URMAN_PROTECTED_RUN") != "1"
                || job is null || job.Length is < 8 or > 64
                || job.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-')
                || directory is null || !System.IO.Path.IsPathFullyQualified(directory))
                throw new InvalidDataException("Protected play frame context is invalid.");

            DirectoryInfo? run;
            if (!OS.HasFeature("editor"))
            {
                // GlobalizePath(res://) is not supported by exported projects.
                // Bind that branch to the exact staged executable instead.
                var executable = new FileInfo(System.IO.Path.GetFullPath(OS.GetExecutablePath()));
                var windows = executable.Directory;
                var candidate = windows?.Parent;
                run = candidate?.Parent;
                if (executable.Name != "URMAN.exe" || windows?.Name != "windows"
                    || candidate?.Name != "candidate" || run?.Name != job
                    || job.Length != 32 || job.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                    || !executable.Exists || (executable.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("The frame context is not this isolated job.");
                for (var parent = windows; parent is not null; parent = parent.Parent)
                    if (!parent.Exists || (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("The candidate ancestry must not contain links.");
            }
            else
            {
                var game = new DirectoryInfo(ProjectSettings.GlobalizePath("res://"));
                var source = game.Parent;
                run = source?.Parent;
                if (game.Name != "game" || source?.Name != "source" || run?.Name != job)
                    throw new InvalidDataException("The frame context is not this isolated job.");
            }
            var expected = System.IO.Path.GetFullPath(System.IO.Path.Combine(run.FullName, "artifacts", "frames"));
            var actual = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(directory));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The frame directory is not this job's artifact directory.");
            for (var parent = new DirectoryInfo(expected); parent is not null; parent = parent.Parent)
                if (!parent.Exists || (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("The frame directory ancestry must not contain links.");

            _interactiveFrameDirectory = expected;
            _interactiveFrameJob = job;
            _interactiveFrameTree = GetTree();
            _interactiveFrameTree.ProcessFrame += PollInteractiveFrameRequest;
            _interactiveFrameTree.PhysicsFrame += ObserveInteractiveMovement;
            TreeExiting += ReleaseInteractiveFrameCapture;
        }
        catch (Exception error)
        {
            GD.PushWarning($"Interactive viewport capture disabled: {error.GetType().Name}.");
        }
    }

    private void ReleaseInteractiveFrameCapture()
    {
        if (_interactiveFrameTree is { } tree && GodotObject.IsInstanceValid(tree))
        {
            tree.ProcessFrame -= PollInteractiveFrameRequest;
            tree.PhysicsFrame -= ObserveInteractiveMovement;
        }
        _interactiveFrameTree = null;
        _interactiveFrameDirectory = null;
        _interactiveFrameJob = null;
        _interactiveMovementPlayer = null;
        _interactiveMovementSample = null;
        _interactiveMovementEligible = false;
    }

    private void ObserveInteractiveMovement()
    {
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (player is null || !GodotObject.IsInstanceValid(player))
        {
            _interactiveMovementPlayer = null;
            _interactiveMovementSample = null;
            _interactiveMovementEligible = false;
            return;
        }

        // PhysicsFrame precedes node physics: these contacts and travel belong to the preceding tick.
        var frame = Engine.GetPhysicsFrames();
        var eligible = player.IsPhysicsProcessing() && !player.ModalOpen
            && !player.VehicleControlled && !player.SessionTransitionActive;
        if (!ReferenceEquals(player, _interactiveMovementPlayer)) _interactiveMovementSample = null;
        if (ReferenceEquals(player, _interactiveMovementPlayer) && _interactiveMovementEligible && eligible
            && _interactiveMovementInput.LengthSquared() > .01f && frame % 6 == 0)
        {
            static JsonArray Vector(Vector3 value) => new(value.X, value.Y, value.Z);
            var movementFrame = frame == 0 ? 0 : frame - 1;
            var contacts = new JsonArray();
            for (var index = 0; index < Math.Min(player.GetSlideCollisionCount(), 8); index++)
            {
                using var hit = player.GetSlideCollision(index);
                var body = hit.GetCollider() as CollisionObject3D;
                if (body is null || !GodotObject.IsInstanceValid(body)) continue;
                var owner = body.ShapeFindOwner(hit.GetColliderShapeIndex());
                var shape = owner < 0 ? null : body.ShapeOwnerGetOwner(owner) as Node;
                contacts.Add(new JsonObject
                {
                    ["collider"] = body.GetPath().ToString(), ["shape"] = shape?.GetPath().ToString(),
                    ["authoredSourceMesh"] = shape is { } source && source.HasMeta("authoredSourceMesh")
                        ? source.GetMeta("authoredSourceMesh").AsString() : null,
                    ["point"] = Vector(hit.GetPosition()), ["normal"] = Vector(hit.GetNormal())
                });
            }
            _interactiveMovementSample = new JsonObject
            {
                ["physicsFrame"] = movementFrame,
                ["playerPath"] = player.GetPath().ToString(),
                ["feetBefore"] = Vector(_interactiveMovementBefore), ["feetAfter"] = Vector(player.GlobalPosition),
                ["input"] = new JsonArray(_interactiveMovementInput.X, _interactiveMovementInput.Y),
                ["displacement"] = Vector(player.GlobalPosition - _interactiveMovementBefore),
                ["stepAttemptFrame"] = player.LastStepAttemptFrame,
                ["stepRejection"] = player.LastStepAttemptFrame == movementFrame ? player.LastStepRejection : null,
                ["contacts"] = contacts
            };
        }
        _interactiveMovementPlayer = player;
        _interactiveMovementBefore = player.GlobalPosition;
        _interactiveMovementInput = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        _interactiveMovementEligible = eligible;
    }

    private void PollInteractiveFrameRequest()
    {
        if (_interactiveFrameBusy || _interactiveFrameDirectory is null
            || System.Environment.TickCount64 < _interactiveFrameNextPoll) return;
        _interactiveFrameNextPoll = System.Environment.TickCount64 + 200;
        var request = System.IO.Path.Combine(_interactiveFrameDirectory, "interactive.request.json");
        if (!File.Exists(request)) return;
        CaptureInteractiveFrameAsync(_interactiveFrameDirectory, _interactiveFrameJob!, request);
    }

    private async void CaptureInteractiveFrameAsync(string directory, string job, string request)
    {
        _interactiveFrameBusy = true;
        var processing = System.IO.Path.Combine(directory, "interactive.request.processing.json");
        string? id = null;
        var claimed = false;
        try
        {
            if ((File.GetAttributes(request) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The frame request is invalid.");
            File.Move(request, processing, overwrite: false);
            claimed = true;
            byte[] requestBytes;
            using (var input = new FileStream(processing, FileMode.Open, System.IO.FileAccess.Read, FileShare.None))
            {
                if (input.Length is <= 0 or > 1024)
                    throw new InvalidDataException("The frame request exceeds its bound.");
                requestBytes = new byte[checked((int)input.Length)];
                input.ReadExactly(requestBytes);
                if (input.ReadByte() != -1) throw new InvalidDataException("The frame request grew.");
            }
            using var document = JsonDocument.Parse(requestBytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 3
                || !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != 1
                || !root.TryGetProperty("jobId", out var jobId) || jobId.ValueKind != JsonValueKind.String || jobId.GetString() != job
                || !root.TryGetProperty("requestId", out var requestId) || requestId.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("The frame request schema is invalid.");
            id = requestId.GetString();
            if (id is null || id.Length != 32 || id.Any(c => !char.IsAsciiHexDigit(c)))
            {
                id = null;
                throw new InvalidDataException("The frame request ID is invalid.");
            }

            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree()
                || _interactiveFrameDirectory != directory || _interactiveFrameJob != job)
                throw new InvalidOperationException("The frame owner exited.");
            var viewport = GetViewport();
            var texture = viewport.GetTexture();
            if (texture.GetWidth() is <= 0 or > 4096 || texture.GetHeight() is <= 0 or > 4096)
                throw new InvalidDataException("The viewport exceeds the frame dimension bound.");
            using var image = texture.GetImage();
            if (image is null || image.IsEmpty()) throw new InvalidDataException("The rendered frame is empty.");
            var png = image.SavePngToBuffer();
            if (png.Length is <= 0 or > 16 * 1024 * 1024)
                throw new InvalidDataException("The rendered PNG exceeds the frame bound.");
            var file = System.IO.Path.Combine(directory, $"interactive-{id}.png");
            using (var output = new FileStream(file, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None))
                output.Write(png);
            var camera = viewport.GetCamera3D();
            var ray = camera?.GetNodeOrNull<RayCast3D>("InteractionRay");
            // Record the ordinary interaction ray with the current image. Do not
            // refresh it, skip colliders, move the player, or dispatch an action.
            var collider = ray?.IsColliding() == true ? ray.GetCollider() as Node : null;
            var target = collider as InteractionTarget;
            WriteInteractiveFrameResult(directory, id, new JsonObject
            {
                ["kind"] = "interactive-game-viewport", ["jobId"] = job, ["requestId"] = id,
                ["pid"] = OS.GetProcessId(), ["status"] = "ok",
                ["width"] = image.GetWidth(), ["height"] = image.GetHeight(),
                ["pngSha256"] = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant(),
                ["scenePath"] = GetTree().CurrentScene?.SceneFilePath,
                ["cameraPath"] = camera?.GetPath().ToString(),
                ["cameraPosition"] = camera is null ? null : new JsonArray(camera.GlobalPosition.X, camera.GlobalPosition.Y, camera.GlobalPosition.Z),
                ["cameraRotationDegrees"] = camera is null ? null : new JsonArray(camera.GlobalRotationDegrees.X, camera.GlobalRotationDegrees.Y, camera.GlobalRotationDegrees.Z),
                ["interactionRayCollider"] = collider?.GetPath().ToString(),
                ["interactionId"] = target?.InteractionId,
                ["interactionAvailable"] = target is null ? null : JsonValue.Create(target.IsAvailable())
            });
            // Keep the worker's strict frame schema unchanged; join this passive sample by requestId.
            GD.Print("interactive-movement-sample: " + new JsonObject
            {
                ["jobId"] = job, ["requestId"] = id, ["capturePhysicsFrame"] = Engine.GetPhysicsFrames(),
                ["lastMovingSample"] = _interactiveMovementSample?.DeepClone()
            }.ToJsonString());
        }
        catch (Exception error)
        {
            // A pre-claim refusal (link or an occupied processing file) cannot
            // safely be removed. Stop polling rather than repeatedly touching it.
            if (!claimed) ReleaseInteractiveFrameCapture();
            if (id is not null)
            {
                try
                {
                    WriteInteractiveFrameResult(directory, id, new JsonObject
                    {
                        ["kind"] = "interactive-game-viewport", ["jobId"] = job, ["requestId"] = id,
                        ["pid"] = OS.GetProcessId(), ["status"] = "error", ["error"] = error.GetType().Name
                    });
                }
                catch (Exception) { /* Preserve the refusal; never replace an existing receipt. */ }
            }
            if (GodotObject.IsInstanceValid(this))
                GD.PushWarning($"Interactive viewport request refused: {error.GetType().Name}.");
        }
        finally
        {
            if (claimed)
            {
                try { File.Delete(processing); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            _interactiveFrameBusy = false;
        }
    }

    private static void WriteInteractiveFrameResult(string directory, string id, JsonObject result)
    {
        var path = System.IO.Path.Combine(directory, $"interactive-{id}.json");
        var temporary = path + ".tmp";
        using (var output = new FileStream(temporary, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(output)) writer.Write(result.ToJsonString());
        File.Move(temporary, path, overwrite: false);
    }
}
