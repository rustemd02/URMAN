using Godot;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

/// <summary>
/// Repeatable check of the Studio window (<c>--urman-studio-selfcheck=&lt;dir&gt;</c>):
/// loads the real village, captures each screen, moves a board through the
/// ordinary edit path, undoes it and confirms the authored file on disk never
/// changed. It never saves sources; frames and a report go to the given folder.
/// </summary>
public static class StudioSelfCheck
{
    public const string Prefix = "--urman-studio-selfcheck=";

    public const string CollabPrefix = "--urman-studio-selfcheck-collab=";

    public static string? CollabOutputDirectory =>
        OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith(CollabPrefix, StringComparison.Ordinal))?[CollabPrefix.Length..];

    /// <summary>
    /// Conflict review in the real window (A18, COLLAB07) on a disposable copy
    /// of the authored files given as --urman-studio-root: a local edit, an
    /// outside edit of the same field and of another field, a decision, save.
    /// </summary>
    public static async void RunCollab(StudioRoot studio, string output)
    {
        var lines = new List<string>();
        var failures = 0;
        void Check(bool ok, string what) { lines.Add($"{(ok ? "PASS" : "FAIL")} {what}"); if (!ok) failures++; }
        async Task Frames(int count) { for (var i = 0; i < count; i++) await studio.ToSignal(studio.GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Capture(string name)
        {
            await Frames(8);
            await studio.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            studio.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output, name + ".png"));
        }

        try
        {
            Directory.CreateDirectory(output);
            var repoRoot = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), ".."));
            Check(Path.GetFullPath(studio.Workspace.Root) != repoRoot, "the check runs on a disposable copy, not the checkout");
            const string path = "content/modules/urman-chapter1/tamara-fence.json";
            var file = studio.Workspace.File(path);
            studio.Session.SetField("urman.chapter1:text/tamara-hand-fit", ["value", "default"], "Эти годятся.", "моя правка");
            studio.Session.SetField("urman.chapter1:text/tamara-hand-stack", ["value", "default"], "Сложи вот тут.", "вторая правка");
            // A developer edits the same line and another one on disk.
            var disk = File.ReadAllText(file.FullPath).Replace("\"Эти подойдут.\"", "\"Эти сойдут.\"", StringComparison.Ordinal).Replace("Доски где?", "Доски-то где?", StringComparison.Ordinal);
            File.WriteAllText(file.FullPath, disk);
            var deadline = Time.GetTicksMsec() + 20_000;
            while (studio.Collab.PendingCount == 0 && Time.GetTicksMsec() < deadline) await Frames(5);
            Check(studio.Collab.PendingCount == 1, "the outside edit of the same field is shown as one decision");
            Check(file.Text.Contains("Эти годятся.", StringComparison.Ordinal), "local work stays until the author decides");
            await Capture("10_conflict_review");
            studio.Collab.DecideAllForTest(Urman.Studio.Core.Collaboration.ConflictChoice.TakeTheirs);
            await Capture("11_conflict_decided");
            studio.Collab.ApplyAllForTest();
            await Frames(4);
            Check(file.Text.Contains("Эти сойдут.", StringComparison.Ordinal) && file.Text.Contains("Доски-то где?", StringComparison.Ordinal)
                  && file.Text.Contains("Сложи вот тут.", StringComparison.Ordinal), "their line, their other change and my other change are all kept");
            Check(studio.SaveAll(autosave: false), "the merged result saves");
            Check(File.ReadAllText(file.FullPath).Contains("Сложи вот тут.", StringComparison.Ordinal), "the saved file carries the merge");
        }
        catch (Exception error)
        {
            Check(false, $"exception: {error.GetType().Name}: {error.Message} @ {error.StackTrace?.Split((char)10).FirstOrDefault()?.Trim()}");
        }

        lines.Add(failures == 0 ? "studio-collab-check: PASS" : $"studio-collab-check: FAIL ({failures})");
        File.WriteAllLines(Path.Combine(output, "report.txt"), lines);
        foreach (var line in lines) GD.Print(line);
        studio.GetTree().Quit(failures == 0 ? 0 : 1);
    }

    private static string LightDiag(StudioRoot studio)
    {
        var environment = (studio.FindChild("AgentBEnvironment", true, false) as WorldEnvironment)?.Environment;
        var sun = studio.FindChild("AgentBSun", true, false) as DirectionalLight3D;
        var world = studio.FindChild("Act1ConnectedWorld", true, false) as Node3D;
        var layers = string.Join(",", studio.FindChildren("*", nameof(CanvasLayer), true, false).OfType<CanvasLayer>().Where(layer => layer.Visible).Select(layer => $"{layer.Name}"));
        var veils = string.Join(",", studio.FindChildren("*", nameof(ColorRect), true, false).OfType<ColorRect>().Where(rect => rect.IsVisibleInTree() && rect.Color.A > .05f && rect.Size.X > 300).Select(rect => $"{rect.GetPath()}:{rect.Color.A:0.00}"));
        return $"layers=[{layers}] veils=[{veils}] fog={environment?.FogDensity} exposure={environment?.TonemapExposure} ambient={environment?.AmbientLightEnergy} sun={sun?.LightEnergy} sunVisible={sun?.Visible} sunRot={sun?.RotationDegrees} zone={(world as Urman.Godot.Act1ConnectedWorld)?.ActiveZoneId} profile={world?.FindChild("Act1CoreWorldGreybox", false, false)?.GetMeta("unifiedAtmosphereProfile", "")}";
    }

    public const string ImportPrefix = "--urman-studio-selfcheck-import=";

    public static string? ImportOutputDirectory =>
        OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith(ImportPrefix, StringComparison.Ordinal))?[ImportPrefix.Length..];

    /// <summary>Import checks (WORLD05, WORLD06) on a disposable copy of the project given as --urman-studio-root.</summary>
    public static async void RunImport(StudioRoot studio, string output)
    {
        var lines = new List<string>();
        var failures = 0;
        void Check(bool ok, string what) { lines.Add($"{(ok ? "PASS" : "FAIL")} {what}"); if (!ok) failures++; }
        async Task Frames(int count) { for (var i = 0; i < count; i++) await studio.ToSignal(studio.GetTree(), SceneTree.SignalName.ProcessFrame); }
        try
        {
            Directory.CreateDirectory(output);
            var repo = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), ".."));
            Check(Path.GetFullPath(studio.Workspace.Root) != repo, "the import check runs on a disposable copy");
            var inbox = Path.Combine(output, "inbox");
            Directory.CreateDirectory(inbox);
            var good = Path.Combine(inbox, "Moya Niva.glb");
            File.Copy(Path.Combine(repo, "game/assets/generated/urman_niva.glb"), good, overwrite: true);
            var broken = Path.Combine(inbox, "broken.glb");
            File.WriteAllText(broken, "this is not a model");
            var missing = Path.Combine(inbox, "missing-texture.gltf");
            File.WriteAllText(missing, "{\"asset\":{\"version\":\"2.0\"},\"images\":[{\"uri\":\"nowhere.png\"}],\"scenes\":[{\"nodes\":[]}],\"scene\":0}");
            var world = (StudioWorldSection)studio.Section("world");

            var bad = StudioImporter.Check(studio, broken);
            Check(!bad.CanImport && bad.Problems.Any(problem => problem.Contains("не читается", StringComparison.Ordinal)), "a broken file is refused with a reason");
            var noTexture = StudioImporter.Check(studio, missing);
            Check(!noTexture.CanImport && noTexture.Problems.Any(problem => problem.Contains("текстур", StringComparison.Ordinal) || problem.Contains("геометрии", StringComparison.Ordinal)),
                "a model with missing texture or no geometry is refused, never a silent success");
            var report = StudioImporter.Check(studio, good);
            Check(report.CanImport && report.Meshes > 0 && report.Size.X is > 1 and < 10, $"a real model passes with its measured size {report.Size.X:0.##}×{report.Size.Z:0.##}×{report.Size.Y:0.##} м");
            world.ShowImport(good);
            await Frames(10);
            studio.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output, "16_import_report.png"));
            var id = StudioImporter.Apply(studio, report, "Моя Нива", "Транспорт");
            Check(id is not null && studio.Workspace.Get(id) is not null, "the imported model is a catalogue entity");
            Check(File.Exists(Path.Combine(studio.Workspace.Root, report.TargetPath)), "the file is copied into the project");
            var again = StudioImporter.Check(studio, good);
            Check(again.Warnings.Any(warning => warning.Contains("уже есть", StringComparison.Ordinal)), "a re-import warns before replacing");
            studio.Undo();
            Check(studio.Workspace.Get(id!) is null, "adding to the catalogue is undoable");
        }
        catch (Exception error)
        {
            Check(false, $"exception: {error.GetType().Name}: {error.Message} @ {error.StackTrace?.Split((char)10).FirstOrDefault()?.Trim()}");
        }

        lines.Add(failures == 0 ? "studio-import-check: PASS" : $"studio-import-check: FAIL ({failures})");
        File.WriteAllLines(Path.Combine(output, "report.txt"), lines);
        foreach (var line in lines) GD.Print(line);
        studio.GetTree().Quit(failures == 0 ? 0 : 1);
    }

    public static string? OutputDirectory =>
        OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith(Prefix, StringComparison.Ordinal))?[Prefix.Length..];

    // NPC02, NPC03, A13, CINE09: the routine walks its route, yields to a scene
    // and resumes, refuses a second scene, and a blocked route ends as authored.
    private static async Task RoutineChecks(StudioRoot studio, StudioWorldSection world, Urman.Studio.Core.Templates.TwoOutcomeQuestIds ids, Vector3 pivot,
        Action<bool, string> Check, Func<int, Task> Frames, Func<string, Task> Capture)
    {
        var npc = ids.NpcEntityId;
        var director = Urman.Godot.AuthoredWorldDirector.Current(studio.GetTree())!;
        System.Text.Json.Nodes.JsonArray At(float dx, float dz) => new(Math.Round(pivot.X + dx, 2), 0.0, Math.Round(pivot.Z + dz, 2));
        System.Text.Json.Nodes.JsonObject Block(string id, string name, System.Text.Json.Nodes.JsonArray place, string motion, string onBlocked = "go-directly",
            System.Text.Json.Nodes.JsonArray? when = null, System.Text.Json.Nodes.JsonArray? route = null)
        {
            var block = new System.Text.Json.Nodes.JsonObject { ["id"] = id, ["name"] = name, ["when"] = when ?? [], ["place"] = place, ["motion"] = motion, ["onBlocked"] = onBlocked };
            if (route is not null) block["route"] = route;
            return block;
        }

        world.SetRoutinesLive(true);
        var afterQuest = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["op"] = "quest.status", ["questId"] = ids.QuestId, ["status"] = "completed" });
        studio.Session.SetField(npc, ["params", "schedule"], new System.Text.Json.Nodes.JsonArray(
            Block("after", "После калитки", At(6, 0), "urman.anim:sit", when: afterQuest),
            Block("usual", "Обычно у поленницы", At(0, 5), "urman.anim:work-kneel", route: [At(1.5f, 2.5f)])), "распорядок");
        studio.Refresh();
        await Frames(6);
        var start = world.PreviewNode(npc)!.GlobalPosition;
        var status = director.RoutineStatus(npc);
        Check(status.Block == "usual", $"the first block whose story condition holds is chosen ({status.Block})");
        var walkedWhileWalking = 0f;
        for (var sample = 0; sample < 40 && walkedWhileWalking < .5f; sample++)
        {
            await Frames(1);
            if (director.RoutineStatus(npc).Walking) walkedWhileWalking = world.PreviewNode(npc)!.GlobalPosition.DistanceTo(start);
        }

        Check(walkedWhileWalking > .5f, $"the character walks the drawn route ({walkedWhileWalking:0.0} м в пути)");
        studio.Navigate(npc);
        world.LookAt(new Vector3(pivot.X, 0, pivot.Z + 2.5f), 11f);
        await Frames(2);
        await Capture("22_routine_route");
        for (var wait = 0; wait < 60 && director.RoutineStatus(npc).Walking; wait++) await Frames(10);
        var there = world.PreviewNode(npc)!.GlobalPosition;
        Check(new Vector2(there.X - (pivot.X), there.Z - (pivot.Z + 5)).Length() < .2f && director.RoutineStatus(npc).Status.StartsWith("на месте", StringComparison.Ordinal),
            $"it arrives and does the block's activity ({director.RoutineStatus(npc).Status})");

        Check(director.Claim(npc, "сцена А", out _), "a scene takes the character");
        Check(!director.Claim(npc, "сцена Б", out var holder) && holder == "сцена А", "a second scene is refused and told who holds the character");
        world.PreviewNode(npc)!.GlobalPosition = new Vector3(pivot.X + 3, there.Y, pivot.Z + 1);
        director.Release(npc, "сцена А");
        await Frames(4);
        Check(director.ClaimedBy(npc) is null && director.RoutineStatus(npc) is { Block: "usual", Walking: true }, "after the scene the routine resumes and walks back");

        // A solid object across the way: the story does not wait on the path.
        var woodpile = world.Place("urman.catalog:urman_village_exterior_kit/woodpile-stackedlogs", new Vector3(pivot.X, 0, pivot.Z - 3));
        await Frames(6);
        studio.Session.SetField(npc, ["params", "schedule"], new System.Text.Json.Nodes.JsonArray(
            Block("usual", "За поленницей", At(0, -6), "urman.anim:idle", onBlocked: "stay")), "распорядок: остаться");
        studio.Refresh();
        await Frames(8);
        var stayed = director.RoutineStatus(npc).Status;
        Check(stayed.Contains("путь перекрыт", StringComparison.Ordinal) && stayed.Contains("остался", StringComparison.Ordinal), $"a blocked route with «остаться» stays and says where ({stayed})");
        studio.Session.SetField(npc, ["params", "schedule"], new System.Text.Json.Nodes.JsonArray(
            Block("usual", "За поленницей", At(0, -6), "urman.anim:idle")), "распорядок: сразу на месте");
        studio.Refresh();
        await Frames(8);
        var direct = world.PreviewNode(npc)!.GlobalPosition;
        Check(director.RoutineStatus(npc).Status.Contains("сразу на месте", StringComparison.Ordinal) && Math.Abs(direct.Z - (pivot.Z - 6)) < .2f,
            "a blocked route with «сразу на месте» puts the character there so the quest never hangs");
        studio.Session.SetField(npc, ["params", "schedule"], new System.Text.Json.Nodes.JsonArray(
            Block("usual", "За поленницей", At(0, -6), "urman.anim:idle", onBlocked: "safe-point")), "распорядок: безопасная точка");
        var safeBlocks = (System.Text.Json.Nodes.JsonArray)studio.Workspace.Get(npc)!["params"]!["schedule"]!.DeepClone();
        safeBlocks[0]!["safePoint"] = At(-4, 1);
        studio.Session.SetField(npc, ["params", "schedule"], safeBlocks, "безопасная точка");
        studio.Refresh();
        await Frames(8);
        var safe = world.PreviewNode(npc)!.GlobalPosition;
        Check(director.RoutineStatus(npc).Status.Contains("безопасную точку", StringComparison.Ordinal) && Math.Abs(safe.X - (pivot.X - 4)) < .2f,
            "a blocked route with «безопасная точка» sends the character there");

        // Route actions: stand at a point looking at the player, then go on (NPC03).
        var waitPoint = new System.Text.Json.Nodes.JsonObject { ["at"] = At(1, 1.5f), ["waitSeconds"] = 1.0, ["look"] = "player" };
        studio.Session.SetField(npc, ["params", "schedule"], new System.Text.Json.Nodes.JsonArray(
            Block("usual", "Постоять и дальше", At(2, 3), "urman.anim:idle", route: [waitPoint])), "распорядок: ожидание в точке");
        studio.Refresh();
        var sawWait = false;
        for (var sample = 0; sample < 200 && !director.RoutineStatus(npc).Status.StartsWith("на месте", StringComparison.Ordinal); sample++)
        {
            await Frames(1);
            sawWait |= director.RoutineStatus(npc).Status.StartsWith("ждёт", StringComparison.Ordinal);
        }

        Check(sawWait && director.RoutineStatus(npc).Status.StartsWith("на месте", StringComparison.Ordinal), "the character waits at a route point, then walks on to the place");

        // Escort: follows while the player is near, waits when the player is far away.
        var escortBlock = Block("escort", "Провожает", At(0, 0), "urman.anim:idle");
        escortBlock["follow"] = new System.Text.Json.Nodes.JsonObject { ["distance"] = 2.5, ["loseMetres"] = 1.0 };
        studio.Session.SetField(npc, ["params", "schedule"], new System.Text.Json.Nodes.JsonArray(escortBlock), "распорядок: сопровождение");
        studio.Refresh();
        await Frames(10);
        var escort = director.RoutineStatus(npc);
        Check(escort.Walking && escort.Status.StartsWith("отстал от игрока", StringComparison.Ordinal), $"an escort waits instead of chasing a player who is far away ({escort.Status})");
        _ = woodpile;
        world.SetRoutinesLive(false);
    }

    public static async void Run(StudioRoot studio, string output)
    {
        var lines = new List<string>();
        var failures = 0;
        void Check(bool ok, string what)
        {
            lines.Add($"{(ok ? "PASS" : "FAIL")} {what}");
            if (!ok) failures++;
        }

        async Task Frames(int count)
        {
            for (var index = 0; index < count; index++)
            {
                await studio.ToSignal(studio.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        async Task Capture(string name)
        {
            await Frames(8);
            await studio.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            studio.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output, name + ".png"));
            lines.Add($"frame {name}.png");
        }

        try
        {
            Directory.CreateDirectory(output);
            const string board = "urman.world:act1/tamara-plot/board-03";
            const string plotPath = "game/content/world/act1_tamara_plot.world.v1.json";
            var diskBefore = AtomicFile.Sha256OfFile(Path.Combine(studio.Workspace.Root, plotPath));
            Check(studio.Workspace.Files.All(file => !file.Dirty), "workspace opens clean");
            Check(studio.Workspace.Get(board) is not null, "world plot entity indexed by ID");

            int Markers() => (studio.FindChild("StudioMarkers", true, false) as Node3D)?.GetChildCount() ?? 0;
            for (var frame = 0; frame < 2400 && Markers() == 0; frame++)
            {
                await Frames(1);
            }

            studio.Tour.Start();
            await Capture("00_tour_first_step");
            Check(studio.Tour.Visible, "the built-in acquaintance opens on its first step");
            studio.Tour.Visible = false;

            var markerCount = (studio.FindChild("StudioMarkers", true, false) as Node3D)?.GetChildCount() ?? 0;
            Check(markerCount >= 13, $"village loaded with {markerCount} authored markers");
            await Capture("01_world");

            studio.Navigate(board);
            await Frames(4);
            Check(studio.Selection == board, "search/navigation selects the board by ID");
            await Capture("02_world_selected_board");

            var world = (StudioWorldSection)studio.Section("world");
            using (studio.Session.Begin("переместить доску"))
            {
                world.MoveEntity(board, 15.5f, -47.4f, "шаг");
                world.MoveEntity(board, 16.0f, -47.4f, "шаг");
            }

            var moved = studio.Workspace.Get(board)!["params"]!["position"]![0]!.GetValue<double>();
            Check(Math.Abs(moved - 16.0) < .001, "drag edits the world plot in memory");
            Check(studio.Workspace.File(plotPath).Dirty, "status shows unsaved changes");
            await Capture("03_world_moved_board");
            Check(studio.Session.UndoLabel == "переместить доску", "a drag is one undo step");
            studio.Undo();
            var restored = studio.Workspace.Get(board)!["params"]!["position"]![0]!.GetValue<double>();
            Check(Math.Abs(restored - 14.5) < .001, "undo restores the position");
            Check(!studio.Workspace.File(plotPath).Dirty, "undo leaves the file clean");

            studio.OpenSection("quests", "urman.chapter1:quest/quest_tamara_fence");
            await Capture("04_quests_list");
            ((StudioQuestSection)studio.Section("quests")).ShowGraph(true);
            await Capture("05_quests_graph");
            var questButton = studio.FindChild("Nav_quests", true, false);
            Check(questButton is not null, "navigation lists the quest section");

            // Play from here: the selected board, the real game, its own saves.
            studio.OpenSection("world", board);
            await Frames(4);
            await Capture("05b_back_to_world_before_play");
            lines.Add("diag before play: " + LightDiag(studio));
            studio.PlayFromHere();
            var playDeadline = Time.GetTicksMsec() + 300_000;
            for (var frame = 0; Time.GetTicksMsec() < playDeadline && (studio.LastRun is null || !File.Exists(studio.LastRun.LogPath) || !File.ReadAllText(studio.LastRun.LogPath).Contains("urman-studio-play: started", StringComparison.Ordinal)); frame++)
            {
                await Frames(1);
                if (studio.ActiveRun is { HasExited: true }) break;
            }

            var gameLog = studio.LastRun is { } run && File.Exists(run.LogPath) ? File.ReadAllText(run.LogPath) : "";
            Check(gameLog.Contains("urman-studio-play: started village_day@arrival", StringComparison.Ordinal), "Play from here starts the real game in a debug session");
            Check(gameLog.Contains("act1-debug-zone: village_day@arrival at (15", StringComparison.Ordinal) || gameLog.Contains("act1-debug-zone: village_day@arrival at (1", StringComparison.Ordinal), "the player starts beside the selected board");
            Check(gameLog.Contains("label=контент ", StringComparison.Ordinal), "the run shows its content revision");
            await Frames(90);
            if (studio.ActiveRun is { HasExited: false } child)
            {
                child.Kill(entireProcessTree: true);
                child.WaitForExit(10000);
            }

            await Capture("05c_after_play");
            lines.Add("diag after play: " + LightDiag(studio));
            Check(!LightDiag(studio).Contains("PauseScreen", StringComparison.Ordinal), "losing focus to the game never covers Studio's world with the game's pause veil");
            // --- the generated village is authored data now (WORLD15, A03) ---
            const string kitWell = "urman.world:act1/kit/village-day-arrival-main-street-landmark";
            Check(studio.Workspace.EntityIds.Count(id => id.StartsWith("urman.world:act1/kit/", StringComparison.Ordinal)) == 205, "all 205 village kit placements are editable entities");
            studio.OpenSection("world", kitWell);
            await Frames(4);
            Node3D? KitNode() => studio.FindChildren("*", nameof(Node3D), true, false).OfType<Node3D>()
                .FirstOrDefault(node => node.HasMeta(Urman.Godot.AuthoredWorldPlot.AuthoredIdMeta) && (string)node.GetMeta(Urman.Godot.AuthoredWorldPlot.AuthoredIdMeta) == kitWell);
            var kitStart = KitNode()?.GlobalPosition;
            Check(kitStart is not null, "the village well's game node is found by its authored ID");
            using (studio.Session.Begin("переместить колодец"))
            {
                world.MoveEntity(kitWell, kitStart!.Value.X + 2f, kitStart.Value.Z, "шаг");
            }

            Check(Mathf.Abs(KitNode()!.GlobalPosition.X - (kitStart.Value.X + 2f)) < .01f, "moving it moves the real village well at once");
            world.LookAt(KitNode()!.GlobalPosition, 14f);
            await Capture("12_village_kit_well_moved");
            studio.Undo();
            await Frames(4);
            Check(Mathf.Abs(KitNode()!.GlobalPosition.X - kitStart.Value.X) < .01f, "undo puts the village well back");

            // --- light, sky and fog (WORLD12) ---
            global::Godot.Environment? Env() => (studio.FindChild("AgentBEnvironment", true, false) as WorldEnvironment)?.Environment;
            var fogBefore = Env()?.FogDensity ?? -1f;
            world.OpenAtmosphere();
            await Frames(4);
            const string village = "urman.world:atmosphere/village-winter-frost";
            var parameters = studio.Workspace.Get(village)!["params"]!.DeepClone().AsObject();
            parameters["fog"]!["density"] = 0.02;
            studio.Session.SetField(village, ["params"], parameters, "плотность тумана");
            await Frames(4);
            Check(Math.Abs((Env()?.FogDensity ?? 0) - 0.02f) < 1e-5, "a fog edit reaches the real village environment");
            await Capture("13_atmosphere_fog");
            studio.Undo();
            await Frames(4);
            Check(Math.Abs((Env()?.FogDensity ?? 0) - fogBefore) < 1e-6, "undo restores the fog");
            Urman.Godot.Act1ConnectedWorld.StudioPreviewProfile = "kara-winter-night-edge";
            world.Atmosphere!.ApplyToWorld();
            await Capture("14_atmosphere_night_preview");
            // VIS-067 recolour made fog density an authored colour-script value,
            // so the night preview is verified against the loaded night profile
            // itself (still a real assertion: it must equal that profile, not the
            // village one), instead of a hard-coded constant that silently drifted.
            var nightFog = Urman.Godot.AtmosphereProfiles.Get("kara-winter-night-edge").FogDensity;
            Check(Math.Abs((Env()?.FogDensity ?? 0) - nightFog) < 1e-5, "«смотреть как ночь» shows the night profile");
            Urman.Godot.Act1ConnectedWorld.StudioPreviewProfile = null;
            world.Atmosphere!.ApplyToWorld();
            world.Atmosphere!.Hide();

            // --- vegetation brush (WORLD11) ---
            var brushAt = new Vector3(world.Pivot.X - 14f, 0, world.Pivot.Z - 10f);
            var stroke = world.PaintStroke(brushAt, StudioWorldSection.DefaultBrushMix, 8f, .05f);
            await Frames(6);
            var planted = world.PreviewNode(stroke);
            var kept = planted is null ? 0 : (int)planted.GetMeta("scatterKept");
            var firstPlant = planted?.GetChildren().OfType<Node3D>().FirstOrDefault()?.GlobalPosition;
            Check(kept > 0, $"one brush click plants {kept} trees and bushes");
            Check(studio.Session.UndoLabel == "штрих кисти растительности", "a stroke is one undo step");
            world.LookAt(brushAt, 22f);
            await Capture("15_vegetation_brush");
            studio.Undo();
            await Frames(3);
            Check(world.PreviewNode(stroke) is null, "undo removes the whole stroke");
            studio.Redo();
            await Frames(6);
            Check(world.PreviewNode(stroke)?.GetChildren().OfType<Node3D>().FirstOrDefault()?.GlobalPosition == firstPlant, "the same data grows the same plants");
            studio.Undo();
            var road = Urman.Experiments.AgentBAct1.AgentBAct1HeightField.RoadInfo(0f, 0f);
            var onRoad = world.PaintStroke(new Vector3(0, 0, 0), StudioWorldSection.DefaultBrushMix, 3f, .3f);
            await Frames(6);
            var skipped = world.PreviewNode(onRoad) is { } roadStroke ? (int)roadStroke.GetMeta("scatterSkippedOnPassages") : 0;
            Check(road.Distance > road.HalfWidth || skipped > 0, $"a stroke over the road keeps the passage clear ({skipped} not planted)");
            studio.Undo();
            await Frames(2);

            // --- cutscene list + timeline (CINE01–CINE05) ---
            const string scenePath = "game/content/cutscenes/tamara_fence.cutscene.v1.json";
            var sceneBefore = studio.Workspace.File(scenePath).Text;
            studio.OpenSection("scenes");
            var dialogues = (StudioDialogueSection)studio.Section("scenes");
            dialogues.ShowCutscenes();
            await Frames(4);
            var panel = dialogues.Cutscenes!;
            Check(panel.Clips.Count == 75, $"the Tamara scene shows its {panel.Clips.Count} actions as list and timeline");
            var punch = panel.Clips.First(clip => clip.Action == "cut" && clip.Label.Contains("главная реплика", StringComparison.Ordinal));
            panel.ScrubForTest(punch.Start + 1.5);
            await Frames(3);
            Check(panel.FrameText.Contains("punchline", StringComparison.Ordinal), "scrubbing shows the shot of that moment");
            await Capture("18_cutscene_timeline");
            panel.SelectForTest(punch.Index);
            var shotId = panel.Insert("wait", new System.Text.Json.Nodes.JsonObject { ["seconds"] = 0.7 }, "Проверочная пауза");
            Check(panel.Clips.Count == 76 && panel.Clips[punch.Index + 1].Id == shotId, "a new action goes right after the selection");
            panel.MoveSelected(-1);
            Check(panel.Clips[punch.Index].Id == shotId, "moving it up changes the play order");
            studio.Undo();
            studio.Undo();
            Check(studio.Workspace.File(scenePath).Text == sceneBefore, "undo restores the scene exactly, order included");

            // --- animation catalogue on real characters (ANIM01–ANIM04, A14 technical part) ---
            studio.OpenSection("characters");
            await Frames(10);
            var people = (StudioCharacterSection)studio.Section("characters");
            var required = new[] { "idle", "walk", "run", "talk", "turn", "point", "pick-up", "hand-over", "carry", "sit-down", "sit", "stand-up", "scared", "work-kneel" };
            var played = required.Where(motion => people.PlayMotion("urman.anim:" + motion).Played).ToArray();
            Check(played.Length == required.Length, $"the minimum motion set plays on a kit character ({played.Length}/{required.Length}: missing {string.Join(",", required.Except(played))})");
            people.PlayMotion("urman.anim:walk");
            await Frames(20);
            await Capture("19_character_walk");
            people.PlayMotion("urman.anim:pick-up");
            await Frames(25);
            await Capture("20_character_pick_up");
            people.PlayMotion("urman.anim:sit");
            await Frames(20);
            await Capture("21_character_sit");
            Check(Urman.Godot.AnimationCatalog.Compatibility(people.Figure!, "Walk_Loop") == "совместим", "library clips fit the kit skeleton");

            // --- terrain strokes (WORLD10) ---
            var hill = new Vector3(world.Pivot.X - 18f, 0, world.Pivot.Z + 14f);
            float Height() => Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(hill.X, hill.Z);
            var groundBefore = Height();
            world.SculptStroke(hill with { Y = groundBefore }, "raise", 7f, 2f);
            await Frames(4);
            var raised = Height();
            Check(raised - groundBefore > 1.5f, $"raising the ground lifts the walkable surface ({groundBefore:0.00} → {raised:0.00} м)");
            world.LookAt(hill, 26f);
            await Capture("17_terrain_raise");
            studio.Undo();
            await Frames(4);
            Check(Math.Abs(Height() - groundBefore) < .01f, "undo puts the ground back");

            // --- catalogue placement, undo/redo on the real world (A01 path, WORLD03) ---
            studio.OpenSection("world");
            await Frames(4);
            var pivot = world.Pivot;
            var well = world.Place("urman.catalog:urman_village_exterior_kit/well-yardlandmark", new Vector3(pivot.X + 2, 0, pivot.Z + 2));
            await Frames(6);
            Check(world.PreviewNode(well) is { } wellNode && wellNode.GetNodeOrNull("Visual") is not null, "a catalogue object appears in the real world");
            Check(studio.Session.UndoLabel?.StartsWith("поставить", StringComparison.Ordinal) == true, "placing is one undo step");
            world.LookAt(new Vector3(pivot.X + 2, 0, pivot.Z + 2), 14f);
            await Capture("06_world_placed_well");
            // group move and duplicate (WORLD07, DATA04)
            var second = world.Place("urman.catalog:urman_village_exterior_kit/woodpile-stackedlogs", new Vector3(pivot.X + 5, 0, pivot.Z + 2));
            world.SelectGroup([well, second]);
            var wellX = (double)studio.Workspace.Get(well)!["params"]!["position"]![0]!;
            var secondX = (double)studio.Workspace.Get(second)!["params"]!["position"]![0]!;
            world.MoveGroup(new Vector3(1.5f, 0, 0));
            Check(Math.Abs((double)studio.Workspace.Get(well)!["params"]!["position"]![0]! - wellX - 1.5) < .01
                  && Math.Abs((double)studio.Workspace.Get(second)!["params"]!["position"]![0]! - secondX - 1.5) < .01, "a group moves together");
            Check(studio.Session.UndoLabel == "переместить 2 объекта", "a group move is one undo step");
            var copies = world.DuplicateSelection();
            Check(copies.Count == 2 && copies.All(copy => copy != well && copy != second && studio.Workspace.Get(copy) is not null), "duplicating makes new objects with new IDs");
            await Frames(4);
            Check(copies.All(copy => world.PreviewNode(copy) is not null), "duplicates stand in the world");
            studio.Undo(); studio.Undo(); studio.Undo();
            await Frames(4);
            Check(world.PreviewNode(second) is null && copies.All(copy => world.PreviewNode(copy) is null), "undo removes duplicates and the second object");
            world.SelectGroup([]);
            studio.Undo();
            await Frames(4);
            Check(world.PreviewNode(well) is null, "undo removes it from the world");
            studio.Redo();
            await Frames(4);
            Check(world.PreviewNode(well) is not null, "redo brings it back");
            studio.Undo();
            await Frames(2);

            // --- side quest from the template, linked map <-> quest (QUEST13, A06) ---
            var ids = Urman.Studio.Core.Templates.TwoOutcomeQuestTemplate.Create(studio.Session, new Urman.Studio.Core.Templates.TwoOutcomeQuestRequest(
                "urman.chapter1", "content/modules/urman-chapter1", "content/campaigns/urman.chapter1/campaign.json",
                "Калитка соседа", "Сосед", "Resident", [pivot.X, 0.0, pivot.Z], 180, "Поможешь с калиткой?", "Помогу.", "Некогда.",
                "Моток проволоки", [pivot.X + 4, 0.0, pivot.Z + 3], "urman.catalog:urman_village_exterior_kit/woodpile-stackedlogs",
                "urman.catalog:urman_village_exterior_kit/gate-crookedtimber", [pivot.X - 3, 0.0, pivot.Z + 2], 90, [pivot.X, 0.0, pivot.Z], [4.0, 2.5, 4.0]));
            studio.Refresh();
            await Frames(8);
            Check(world.PreviewNode(ids.NpcEntityId) is not null && world.PreviewNode(ids.GateEntityId) is not null, "the template's NPC and gate stand in the world");
            await RoutineChecks(studio, world, ids, pivot, Check, Frames, Capture);
            var worldLinks = world.StoryLinkList(ids.GateEntityId);
            Check(worldLinks.Any(link => link.Id == ids.QuestId), "the gate shows which quest changes it");
            studio.Navigate(ids.NpcEntityId);
            world.LookAt(new Vector3(pivot.X, 0, pivot.Z), 16f);
            await Capture("07_world_template_npc");
            studio.OpenSection("quests", ids.QuestId);
            var quests = (StudioQuestSection)studio.Section("quests");
            quests.ShowGraph(true);
            await Capture("08_quest_graph_branch");
            quests.SelectStageForTest("talk");
            quests.ShowGraph(false);
            await Capture("09_quest_stage_phrases");
            Check(quests.MapLinksForTest("fetch").Any(link => link.Item2 == ids.PickupEntityId), "the fetch step finds its item on the map");
            Check(quests.MapLinksForTest("talk").Any(link => link.Item2 == ids.NpcEntityId || link.Item2 == ids.DialogueId), "the answer step finds the NPC's dialogue");
            while (studio.Session.CanUndo) studio.Undo();
            await Frames(4);
            Check(studio.Workspace.Get(ids.QuestId) is null && world.PreviewNode(ids.NpcEntityId) is null, "undo removes the whole template");
            Check(studio.Workspace.Files.All(file => !file.Dirty || file.IsNew), "after undo nothing is left to save");

            Check(AtomicFile.Sha256OfFile(Path.Combine(studio.Workspace.Root, plotPath)) == diskBefore, "authored file on disk untouched by the check");
        }
        catch (Exception error)
        {
            Check(false, $"exception: {error.GetType().Name}: {error.Message} @ {error.StackTrace?.Split((char)10).FirstOrDefault()?.Trim()}");
        }

        lines.Add(failures == 0 ? "studio-selfcheck: PASS" : $"studio-selfcheck: FAIL ({failures})");
        File.WriteAllLines(Path.Combine(output, "report.txt"), lines);
        foreach (var line in lines) GD.Print(line);
        studio.GetTree().Quit(failures == 0 ? 0 : 1);
    }
}
