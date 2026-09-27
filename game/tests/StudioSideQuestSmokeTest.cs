using System.Text.Json;
using Godot;
using Urman.Content.Compilation;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Templates;

namespace Urman.Godot.Tests;

/// <summary>
/// URMAN Studio end to end (spec A07, A08, A09, A06 runtime half): a side
/// quest made with Studio's "choice with two outcomes" template in a
/// disposable copy of the project is compiled by the real compiler and played
/// in the real game. Accepting runs the item fetch and repairs the gate;
/// refusing fails the quest with its named outcome and removes the gate with
/// its collision; a repeated trigger and a repeated pickup change nothing;
/// loading a save restores the world state. Canon content is not touched.
/// </summary>
public partial class StudioSideQuestSmokeTest : Node
{
    private Act1DemoRoot _demo = null!;
    private RuntimeBridge _bridge = null!;
    private AuthoredWorldDirector _director = null!;
    private TwoOutcomeQuestIds _ids = null!;
    private int _checks;

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            _ids = await PrepareWorkspaceAsync();
            _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(_demo);
            await Frames(10);
            Require(await this.StartThroughMainMenuAsync(_demo), "ordinary New Game with the Studio-made pack");
            await Frames(30);
            _bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            _director = AuthoredWorldDirector.Current(GetTree()) ?? throw new InvalidOperationException("Authored world director was not built.");
            Require(_director.ObjectIds.Contains(_ids.NpcEntityId) && _director.ObjectIds.Contains(_ids.GateEntityId)
                    && _director.ObjectIds.Contains(_ids.PickupEntityId) && _director.ObjectIds.Contains(_ids.TriggerEntityId),
                "the template's NPC, item, gate and trigger are built from the world plot");
            await Frames(3);
            Require(_director.AppliedState(_ids.GateEntityId) == "base", "the gate starts in its base state");
            Require(_director.AppliedState(_ids.PickupEntityId) == "not-yet", "the item is hidden until the player agrees");
            Require(Quest().GetProperty("stageIndex").GetInt32() == 0 && Status() == "active", "the quest waits for the answer");

            // --- trigger: once, and never twice -------------------------------------
            var player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
            var area = _director.ObjectRoot(_ids.TriggerEntityId)!.GlobalPosition;
            player.ApplyZoneSpawn(area + new Vector3(0, .1f, 0), 0);
            await PhysicsFrames(20);
            Require(Npc("noticed") is { ValueKind: JsonValueKind.True }, "walking into the authored area fires its interaction");
            var interactionsAfterFirst = State().GetProperty("interactionCount").GetInt32();
            player.ApplyZoneSpawn(area + new Vector3(9, .1f, 0), 0);
            await PhysicsFrames(20);
            player.ApplyZoneSpawn(area + new Vector3(0, .1f, 0), 0);
            await PhysicsFrames(20);
            Require(State().GetProperty("interactionCount").GetInt32() == interactionsAfterFirst, "entering again does not repeat the effect");
            Require(await _bridge.SaveSlotAsync("studio-before-answer"), "save before answering");

            // --- outcome A: accept, fetch, return ------------------------------------
            Require(await _bridge.DispatchInteractionAsync(_ids.TalkInteractionId), "talking to the new NPC is a compiled interaction");
            Require(await _bridge.ChooseDialogueAsync(_ids.DialogueId, "ask", "accept"), "the player agrees in the dialogue");
            await Frames(3);
            Require(Quest().GetProperty("stageIndex").GetInt32() == 1, "agreeing branches to the fetch stage");
            Require(_director.AppliedState(_ids.PickupEntityId) == "base", "the item appears once it is needed");
            Require(await _bridge.DispatchInteractionAsync(_ids.PickupInteractionId), "the item is picked up");
            Require(!await _bridge.DispatchInteractionAsync(_ids.PickupInteractionId), "the same item cannot be taken twice");
            await Frames(3);
            Require(_director.AppliedState(_ids.PickupEntityId) == "taken", "the taken item leaves the world");
            Require(Quest().GetProperty("stageIndex").GetInt32() == 2, "then the return stage");
            Require(await _bridge.ChooseDialogueAsync(_ids.DialogueId, "wait", "give"), "the item is handed over");
            await Frames(3);
            Require(Status() == "completed", "the quest is completed");
            Require(_director.AppliedState(_ids.GateEntityId) == "repaired", "the world shows the repaired gate");
            Require(await _bridge.SaveSlotAsync("studio-accepted"), "save after the good outcome");

            // --- outcome B: a separate run refuses ------------------------------------
            Require(await _bridge.LoadSlotAsync("studio-before-answer"), "load the save made before answering");
            await Frames(5);
            Require(Status() == "active" && _director.AppliedState(_ids.GateEntityId) == "base", "loading restores the unanswered quest and base gate");
            Require(await _bridge.ChooseDialogueAsync(_ids.DialogueId, "ask", "refuse"), "the player refuses");
            await Frames(3);
            Require(Status() == "failed" && Quest().TryGetProperty("outcomeId", out var outcome) && outcome.GetString() == "refused",
                "refusing fails the quest with the named outcome");
            Require(Npc("gate_broken") is { ValueKind: JsonValueKind.True }, "the refusal branch applies its own consequence");
            var gate = _director.ObjectRoot(_ids.GateEntityId)!;
            Require(_director.AppliedState(_ids.GateEntityId) == "broken" && !gate.Visible
                    && gate.GetNode<StaticBody3D>("Collision").CollisionLayer == 0, "the broken gate is gone with its collision");
            Require(_director.AppliedState(_ids.PickupEntityId) == "not-yet", "the refused branch never shows the item");

            // --- save/load restores the other outcome's world -------------------------------
            Require(await _bridge.LoadSlotAsync("studio-accepted"), "load the accepted outcome");
            await Frames(5);
            Require(Status() == "completed" && _director.AppliedState(_ids.GateEntityId) == "repaired"
                    && gate.Visible && gate.GetNode<StaticBody3D>("Collision").CollisionLayer != 0, "loading brings back the repaired gate and its collision");
            GD.Print($"studio-side-quest-smoke: {_checks} checks passed");
            exit = 0;
        }
        catch (Exception error)
        {
            GD.PushError($"studio-side-quest-smoke: FAIL {error.Message}");
        }
        finally
        {
            CompiledCampaignRepository.PackOverrideForTest = null;
            AuthoredWorldDirector.WorldDirectoryOverrideForTest = null;
        }

        GetTree().Quit(exit);
    }

    /// <summary>A disposable Studio workspace inside the guarded userdata; the checkout is only read.</summary>
    private static async Task<TwoOutcomeQuestIds> PrepareWorkspaceAsync()
    {
        var repo = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), ".."));
        var root = ProjectSettings.GlobalizePath("user://studio-smoke/workspace");
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        foreach (var folder in new[] { "content", "game/content/world" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(repo, folder), "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(root, folder, Path.GetRelativePath(Path.Combine(repo, folder), file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        }

        var workspace = StudioWorkspace.Open(root);
        var session = new EditSession(workspace);
        var ids = TwoOutcomeQuestTemplate.Create(session, new TwoOutcomeQuestRequest(
            "urman.chapter1", "content/modules/urman-chapter1", "content/campaigns/urman.chapter1/campaign.json",
            "Калитка соседа", "Сосед", "Resident", [-1.2, 0.0, -33.5], 90, "Поможешь с калиткой? Нужна проволока.", "Помогу.", "Некогда.",
            "Моток проволоки", [0.6, 0.0, -30.0], "urman.catalog:urman_village_exterior_kit/woodpile-stackedlogs",
            "urman.catalog:urman_village_exterior_kit/gate-crookedtimber", [1.8, 0.0, -33.0], 90, [-1.2, 0.0, -33.5], [4.0, 2.5, 4.0]));
        foreach (var file in workspace.Files.Where(file => file.Dirty)) file.Save();
        var result = await new ContentCompiler().CompileAsync(root, "urman.chapter1", []);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("template pack did not compile: " + string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        }

        var pack = Path.Combine(root, "pack.json");
        File.WriteAllText(pack, CompiledPackText.Serialize(result.Pack!));
        CompiledCampaignRepository.PackOverrideForTest = pack;
        AuthoredWorldDirector.WorldDirectoryOverrideForTest = Path.Combine(root, "game/content/world");
        return ids;
    }

    private JsonElement State() => _bridge.SelectRuntimeState();
    private JsonElement Quest() => State().GetProperty("quests").GetProperty(_ids.QuestId);
    private string Status() => Quest().GetProperty("status").GetString()!;

    private JsonElement? Npc(string key) =>
        State().GetProperty("npc").TryGetProperty(_ids.CharacterId, out var npc) && npc.TryGetProperty(key, out var value) ? value : null;

    private void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task PhysicsFrames(int count)
    {
        for (var index = 0; index < count; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
}
