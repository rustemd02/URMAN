using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Presentation-only calm owner for the village mosque (author 2026-10-04: the
/// mosque is a place of calm — the scary layer must never reach the player
/// inside it, and a Quran is always present). No story, save or knowledge
/// state is read or written anywhere in this node.
///
/// Inside detection. The owner computes and passes the authoritative flag; the
/// only honest test is the world's real interior volume:
/// <c>Act1ConnectedWorld.FacilityInteriorAt(player.GlobalPosition) == "mosque"</c>
/// (public method, node path
/// <c>Act1CoreWorldGreybox/VillageMosqueComplex/MosqueInterior</c>). That test
/// is a room-local box X in (-6.60, 5.20), |Z| &lt; 4.57, Y in (-0.25, 3.74),
/// evaluated only while the active zone is village_day, zirat_road or
/// kara_urman_night. The box covers the prayer hall, the east vestibule and
/// the west library; the entrance stair/landing (room X &gt; 5.25) is outside.
/// This node never guesses from a distance sphere; <see cref="IsInsideMosqueVolume"/>
/// delegates to the same world volume (with an identical locally-kept fallback
/// for tests where the connected world is not available) and counts
/// transition-time disagreements in <see cref="VolumeMismatches"/>.
///
/// Sanctuary effect, all through existing public APIs:
/// * the village dread layer is floored with VillageSoundMoodDirector.SetAdhanFocus
///   exactly like the licensed adhan does; the handoff never releases a real
///   call — while an adhan is playing this node does not own or clear the focus;
/// * the three authored mosque lamps settle by <see cref="CalmLampEnergyScale"/>
///   while the player is inside and are restored to their exact authored energy
///   on exit;
/// * the forest-edge presence and the bath spirit are already stopped by the
///   world's interior audible gate and by geometry; this node only counts any
///   new start/manifestation that would contradict that and exposes the counters
///   for verification (no public silence API exists today — proposed in the
///   report, not patched here).
///
/// Wiring contract (the owner calls it): Initialize(Node3D world) once after
/// the world build and the node was added to the tree, then
/// Tick(delta, playerPosition, playerInsideMosque) from the ordinary
/// presentation tick. Per-frame cost is O(1) and allocation-free; the 1 Hz
/// context refresh is bounded like BathSpirit's.
/// </summary>
public partial class MosqueSanctuary : Node3D
{
    public const string MosqueRoomNodePath = "Act1CoreWorldGreybox/VillageMosqueComplex/MosqueInterior";

    // The mosque-room local box Act1ConnectedWorld.FacilityInteriorAt tests.
    // The live world call is preferred; these constants exist only for the
    // fallback when no connected world was handed to Initialize.
    public const float MosqueMinX = -6.60f;
    public const float MosqueMaxX = 5.20f;
    public const float MosqueHalfDepth = 4.57f;
    public const float MosqueMinY = -.25f;
    public const float MosqueMaxY = 3.74f;

    /// <summary>VillageSoundMoodDirector.LayerFloorDb (private const there).</summary>
    public const float DreadFloorDb = -60f;
    public const double ContextRefreshSeconds = 1.0;
    public const int ResolveAttemptLimit = 60;

    private static readonly string[] MosqueLampNames = ["MosqueHallLamp", "MosqueVestibuleLamp", "MosqueLibraryLamp"];

    private Node3D? _worldNode;
    private Act1ConnectedWorld? _connectedWorld;
    private Node3D? _mosqueRoom;
    private VillageSoundMoodDirector? _soundMood;
    private ForestEdgePresence? _forestEdge;
    private BathSpirit? _bathSpirit;
    private readonly List<OmniLight3D> _mosqueLamps = [];
    private readonly List<float> _mosqueLampEnergy = [];

    private bool _initialized;
    private bool _inside;
    private bool _focusOwned;
    private bool _lampsCalm;
    private bool _mismatchWarned;
    private double _contextTimer;
    private int _resolveTries;
    private int _enters;
    private int _ticks;
    private int _volumeMismatches;
    private int _dreadReasserts;
    private int _forestCueStartsWhileInside;
    private int _bathManifestationsWhileInside;
    private int _lastForestCues;
    private int _lastBathManifestations;

    /// <summary>Lamp energy scale applied while the player is inside; 1 disables the settle.</summary>
    public float CalmLampEnergyScale { get; set; } = .92f;

    public bool Initialized => _initialized;
    public bool Inside => _inside;
    public bool DreadFloorOwned => _focusOwned;
    public bool LampCalmApplied => _lampsCalm;
    public bool MosqueRoomResolved => _mosqueRoom is not null && IsInstanceValid(_mosqueRoom);
    public bool SoundMoodResolved => ValidSoundMood() is not null;
    public int Enters => _enters;
    public int Ticks => _ticks;
    public int VolumeMismatches => _volumeMismatches;
    public int DreadReasserts => _dreadReasserts;
    public int ForestCueStartsWhileInside => _forestCueStartsWhileInside;
    public int BathManifestationsWhileInside => _bathManifestationsWhileInside;

    /// <summary>
    /// Builds the references only; no geometry, no state. Safe to call once
    /// after the world build (the mosque room, the sound-mood director and the
    /// lamp nodes must already exist).
    /// </summary>
    public void Initialize(Node3D world)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _worldNode = world;
        _connectedWorld = world as Act1ConnectedWorld
            ?? world.GetTree()?.GetFirstNodeInGroup("act1_connected_world") as Act1ConnectedWorld;
        _mosqueRoom = (world.FindChild("MosqueInterior", true, false) as Node3D)
            ?? world.GetNodeOrNull<Node3D>(MosqueRoomNodePath);
        ResolveDirectors(world);
        ResolveLamps(world);
        SetMeta("presentationOnly", true);
        SetMeta("detectionRule",
            "Act1ConnectedWorld.FacilityInteriorAt(player) == \"mosque\"; mosque-room local box X -6.60..5.20, |Z| < 4.57, Y -0.25..3.74");
        SetMeta("suppressionApis", "VillageSoundMoodDirector.SetAdhanFocus; authored mosque lamp energy; interior audible gate owned by Act1ConnectedWorld");
        SetMeta("mosqueRoomResolved", MosqueRoomResolved);
        SetMeta("calmLampEnergyScale", CalmLampEnergyScale);
        GD.Print($"mosque-sanctuary: room={MosqueRoomResolved} soundMood={SoundMoodResolved} lamps={_mosqueLamps.Count} forest={_forestEdge is not null} bathSpirit={_bathSpirit is not null}");
    }

    /// <summary>
    /// One presentation frame from the owner's tick. The owner's flag is
    /// authoritative and must be computed from the real interior volume (see
    /// the class summary); everything here is presentation only.
    /// </summary>
    public void Tick(double delta, Vector3 playerPosition, bool playerInsideMosque)
    {
        if (!_initialized)
        {
            return;
        }

        _ticks++;
        var step = Math.Min(Math.Max(delta, 0d), .5d);
        if (playerInsideMosque != _inside)
        {
            _inside = playerInsideMosque;
            if (_inside)
            {
                EnterSanctuary(playerPosition);
            }
            else
            {
                ExitSanctuary();
            }
        }

        _contextTimer += step;
        if (_contextTimer < ContextRefreshSeconds)
        {
            return;
        }

        _contextTimer = 0;
        if (_inside)
        {
            RefreshInsideContext();
        }
    }

    /// <summary>
    /// True exactly when the world's real interior volume at this point is the
    /// mosque. Uses the public Act1ConnectedWorld volume test when available.
    /// </summary>
    public bool IsInsideMosqueVolume(Vector3 worldPoint) => InteriorAt(worldPoint) == "mosque";

    private string InteriorAt(Vector3 worldPoint)
    {
        if (_connectedWorld is not null && IsInstanceValid(_connectedWorld))
        {
            return _connectedWorld.FacilityInteriorAt(worldPoint);
        }

        if (_mosqueRoom is null || !IsInstanceValid(_mosqueRoom))
        {
            return string.Empty;
        }

        var local = _mosqueRoom.ToLocal(worldPoint);
        return local.X > MosqueMinX && local.X < MosqueMaxX && Math.Abs(local.Z) < MosqueHalfDepth
            && local.Y > MosqueMinY && local.Y < MosqueMaxY ? "mosque" : string.Empty;
    }

    private void EnterSanctuary(Vector3 playerPosition)
    {
        _enters++;
        _lastForestCues = _forestEdge is not null && IsInstanceValid(_forestEdge) ? _forestEdge.CuesStarted : 0;
        _lastBathManifestations = _bathSpirit is not null && IsInstanceValid(_bathSpirit) ? _bathSpirit.Manifestations : 0;

        // Honest detection: the owner's flag should match the world's own
        // volume, never a sphere around the building.
        var actual = InteriorAt(playerPosition);
        if (actual.Length > 0 && !string.Equals(actual, "mosque", StringComparison.Ordinal))
        {
            _volumeMismatches++;
            if (!_mismatchWarned)
            {
                _mismatchWarned = true;
                GD.PushWarning($"mosque-sanctuary: owner flag says mosque but FacilityInteriorAt returned '{actual}'.");
            }
        }

        ApplyLampCalm();
        var mood = ValidSoundMood();
        if (mood is not null && !AdhanPlaying())
        {
            mood.SetAdhanFocus(true);
            _focusOwned = true;
        }

        SetMeta("mosqueSanctuaryActive", true);
        SetMeta("mosqueSanctuaryEnters", _enters);
        SetMeta("dreadFloorOwned", _focusOwned);
        SetMeta("volumeMismatches", _volumeMismatches);
    }

    private void ExitSanctuary()
    {
        RestoreLampEnergy();
        if (_focusOwned)
        {
            _focusOwned = false;
            // A real recorded call keeps its own focus; its Finished handler
            // releases it. This node only releases what it owns.
            if (!AdhanPlaying())
            {
                ValidSoundMood()?.SetAdhanFocus(false);
            }
        }

        SetMeta("mosqueSanctuaryActive", false);
        SetMeta("dreadFloorOwned", false);
    }

    /// <summary>Bounded 1 Hz refresh: re-assert the floor and verify the gates.</summary>
    private void RefreshInsideContext()
    {
        var mood = ValidSoundMood();
        if (mood is null && _resolveTries < ResolveAttemptLimit)
        {
            _resolveTries++;
            var tree = GetTree();
            if (tree is not null)
            {
                _soundMood = tree.GetFirstNodeInGroup("village_sound_mood") as VillageSoundMoodDirector;
                mood = ValidSoundMood();
            }
        }

        if (!_focusOwned && mood is not null && !AdhanPlaying() && mood.DreadLayerDb > DreadFloorDb + .01f)
        {
            mood.SetAdhanFocus(true);
            _focusOwned = true;
            _dreadReasserts++;
            SetMeta("dreadFloorOwned", true);
            SetMeta("dreadReasserts", _dreadReasserts);
        }

        if (_forestEdge is not null && IsInstanceValid(_forestEdge))
        {
            var cues = _forestEdge.CuesStarted;
            var started = cues - _lastForestCues;
            if (started > 0)
            {
                _forestCueStartsWhileInside += started;
                SetMeta("forestCueStartsWhileInside", _forestCueStartsWhileInside);
            }

            _lastForestCues = cues;
        }

        if (_bathSpirit is not null && IsInstanceValid(_bathSpirit))
        {
            var marks = _bathSpirit.Manifestations;
            var manifested = marks - _lastBathManifestations;
            if (manifested > 0)
            {
                _bathManifestationsWhileInside += manifested;
                SetMeta("bathManifestationsWhileInside", _bathManifestationsWhileInside);
            }

            _lastBathManifestations = marks;
        }
    }

    private void ApplyLampCalm()
    {
        _lampsCalm = false;
        for (var index = 0; index < _mosqueLamps.Count; index++)
        {
            var lamp = _mosqueLamps[index];
            if (!IsInstanceValid(lamp))
            {
                continue;
            }

            lamp.LightEnergy = _mosqueLampEnergy[index] * CalmLampEnergyScale;
            _lampsCalm = true;
        }
    }

    private void RestoreLampEnergy()
    {
        for (var index = 0; index < _mosqueLamps.Count; index++)
        {
            var lamp = _mosqueLamps[index];
            if (IsInstanceValid(lamp))
            {
                lamp.LightEnergy = _mosqueLampEnergy[index];
            }
        }

        _lampsCalm = false;
    }

    private void ResolveLamps(Node3D world)
    {
        _mosqueLamps.Clear();
        _mosqueLampEnergy.Clear();
        foreach (var name in MosqueLampNames)
        {
            var lamp = (_mosqueRoom?.FindChild(name, true, false) ?? world.FindChild(name, true, false)) as OmniLight3D;
            if (lamp is null)
            {
                continue;
            }

            _mosqueLamps.Add(lamp);
            _mosqueLampEnergy.Add(lamp.LightEnergy);
        }
    }

    private void ResolveDirectors(Node3D world)
    {
        var tree = world.GetTree();
        _soundMood = tree?.GetFirstNodeInGroup("village_sound_mood") as VillageSoundMoodDirector;
        _forestEdge = world.FindChild("ForestEdgePresence", true, false) as ForestEdgePresence;
        _bathSpirit = world.FindChild("MunchaIyase", true, false) as BathSpirit;
    }

    private VillageSoundMoodDirector? ValidSoundMood() =>
        _soundMood is not null && IsInstanceValid(_soundMood) ? _soundMood : null;

    private bool AdhanPlaying()
    {
        if (_connectedWorld is not null && IsInstanceValid(_connectedWorld))
        {
            return _connectedWorld.AdhanPlaying;
        }

        return _worldNode is not null && IsInstanceValid(_worldNode)
            && string.Equals(_worldNode.GetMeta("adhanState", "").AsString(), "playing-licensed-recording", StringComparison.Ordinal);
    }
}
