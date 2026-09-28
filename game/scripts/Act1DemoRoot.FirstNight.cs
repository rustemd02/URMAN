using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    private bool _firstNightRunning;
    private const string MorningNewsDialogue = "urman.chapter1:dialogue/first-morning-news";

    internal async Task RunFirstNightAsync()
    {
        if (_firstNightRunning || IntroVisible || MainMenuVisible || _player is null || _bridge is null
            || _bridge.FirstNightPassed || _bridge.CurrentZoneId != "house_old_pc"
            || !_bridge.IsInteractionAvailable("urman.chapter1:interaction/first-night-sleep")) return;
        _firstNightRunning = true;
        var session = _bridge.SessionIdentity;
        var world = GetTree().GetFirstNodeInGroup("act1_connected_world") as Act1ConnectedWorld;
        var weather = world?.GetNodeOrNull<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        var hud = _player.GetNodeOrNull<CanvasLayer>("Hud");
        var hudVisible = hud?.Visible == true;
        Camera3D? camera = null;
        AudioStreamPlayer? wind = null;
        var committed = false;
        try
        {
            // A crash/quit during the cutscene returns to the reachable bed.
            // Save before taking the shared intro lock; that lock blocks all slots.
            if (world is null || !await _bridge.SaveSlotAsync(RuntimeBridge.CheckpointSlot)
                || !ReferenceEquals(session, _bridge.SessionIdentity)) return;
            _prologueForestActive = true;
            _prologueSkipRequested = false;
            _player.SetSessionTransition(true);
            _player.SetModalOpen(true);
            if (hud is not null) hud.Visible = false;
            BuildPrologueOverlay("пропустить ночную сцену");
            FadePrologueBlackout(true);
            await PrologueWaitAsync(.35);
            if (_prologueCaption is not null)
            {
                _prologueCaption.Text = "Ночью ветер переменился.";
                _prologueCaption.Visible = true;
            }
            _main.SwitchZone("kara_urman_night", "forest-approach");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            camera = new Camera3D { Name = "FirstNightBridgeCamera", Fov = 58 };
            _main.AddChild(camera);
            var focus = world.OpeningBridgeFocus;
            camera.GlobalPosition = focus + new Vector3(-10f, 3.1f, 9f);
            camera.LookAt(focus + Vector3.Up * .2f);
            camera.MakeCurrent();
            weather?.SetOpeningBlizzard(true);
            wind = new AudioStreamPlayer
            {
                Name = "FirstNightWind", Stream = GD.Load<AudioStream>("res://assets/audio/zirat_wind.wav"),
                Bus = AudioSettingsService.AmbienceBus, VolumeDb = -7
            };
            AddChild(wind);
            wind.Play();
            RefreshGameplayAudioPauseState();
            FadePrologueBlackout(false);
            await PrologueWaitAsync(2.5);
            if (!_prologueSkipRequested)
            {
                UiFoley.PlayWorld(this, focus, "hollow_board");
                if (_prologueCaption is not null) _prologueCaption.Text = "[Ветер. Треск дерева над оврагом.]";
                var elapsed = 0d;
                while (elapsed < 2.1 && IsInsideTree() && !_prologueSkipRequested)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    elapsed += GetProcessDeltaTime();
                    world.SetOpeningBridgeCollapse(Mathf.Clamp((float)(elapsed / 2.1), 0, 1));
                }
            }
            if (!IsInsideTree() || !ReferenceEquals(session, _bridge.SessionIdentity)) return;
            // Both skip and normal playback commit exactly the same world state.
            committed = await _bridge.SetFirstNightPassedAsync(true);
            if (!committed) return;
            await PrologueWaitAsync(.8);
            FadePrologueBlackout(true);
            await PrologueWaitAsync(.35);
        }
        catch (Exception error) { GD.PushError($"First night failed: {error}"); }
        finally
        {
            if (IsInsideTree())
            {
                weather?.SetOpeningBlizzard(false);
                world?.SetOpeningBridgeCollapse(0);
                wind?.Stop();
                wind?.QueueFree();
                camera?.QueueFree();
                if (ReferenceEquals(session, _bridge.SessionIdentity)) _main.SwitchZone("house_old_pc", "entry");
                _player.GetNodeOrNull<Camera3D>("Head/Camera3D")?.MakeCurrent();
                _player.SetSessionTransition(false);
                _player.SetModalOpen(false);
                if (hud is not null) hud.Visible = hudVisible;
                _prologueForestActive = false;
                ReleasePrologueOverlay();
                RefreshGameplayAudioPauseState();
                UpdateRouteCue();
            }
            _firstNightRunning = false;
        }
        if (!committed || !IsInsideTree() || !ReferenceEquals(session, _bridge.SessionIdentity)) return;
        _bridge.OpenDialogueUi(MorningNewsDialogue);
        var news = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        while (IsInsideTree() && news?.IsOpen == true && ReferenceEquals(session, _bridge.SessionIdentity))
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (IsInsideTree() && ReferenceEquals(session, _bridge.SessionIdentity)) await _bridge.SaveCheckpointAsync(force: true);
    }

    private static string? OpeningRouteCue(RuntimeBridge bridge)
    {
        if (bridge.FirstNightPassed || bridge.ActiveSceneId == "urman.chapter1:scene/arrival_vehicle_dusk") return null;
        if (!KnowledgeConfirmed(bridge, "family_home_pause")) return "Әби ждёт за столом. Сначала — чай и разговор с семьёй.";
        if (!KnowledgeConfirmed(bridge, "alsu_walk_invitation"))
            return bridge.CurrentZoneId == "house_old_pc" ? "После чая можно пройтись по деревне. Алсу живёт рядом." : "Поздороваться с Алсу — давно не виделись.";
        return bridge.CurrentZoneId == "house_old_pc" ? "Диван у стены приготовлен для меня. Можно лечь спать." : "На сегодня хватит дороги. Вернуться к бабаю — диван уже застелен.";
    }
}
