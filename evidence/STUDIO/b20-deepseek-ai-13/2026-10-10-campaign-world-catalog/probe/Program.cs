using Urman.Studio.Core.Editing;

var root = args.Length > 0 ? args[0] : "/Users/unterlantas/Documents/GitHub/URMAN-deepseek";
var failures = new List<string>();
void Check(bool condition, string what) { if (!condition) failures.Add(what); Console.WriteLine((condition ? "PASS  " : "FAIL  ") + what); }

var catalog = CampaignWorldCatalog.Discover(root);
Console.WriteLine($"root: {catalog.Root}");
Console.WriteLine($"campaigns: {catalog.Campaigns.Count}  worlds: {catalog.Worlds.Count}  plots: {catalog.AuthoredWorldPlots.Count}  specs: {catalog.Specifications.Count}");
foreach (var c in catalog.Campaigns)
    Console.WriteLine($"  campaign {c.Selection,-20} id={c.Id,-20} v{c.ExactVersion,-6} pack={(c.CompiledPackExists ? "yes" : "no ")} archive={(c.IsArchive ? "yes" : "no ")} modules={c.ModuleIds.Count}");
foreach (var w in catalog.Worlds)
    Console.WriteLine($"  world    {w.ScenePath,-34} campaign={(w.CampaignSelection.Length == 0 ? "<undeclared>" : w.CampaignSelection),-16} zone={(w.ZoneId.Length == 0 ? "<code>" : w.ZoneId),-14} spawn={(w.SpawnPointId.Length == 0 ? "<code>" : w.SpawnPointId),-12} cap={w.Capability} title={w.Title}");
foreach (var p in catalog.AuthoredWorldPlots)
    Console.WriteLine($"  plot     {p.RelativePath,-52} entities={p.EntityCount,4} ns={string.Join(",", p.Namespaces)}");
foreach (var s in catalog.Specifications)
    Console.WriteLine($"  spec     {s.CampaignSelection}/{s.WorldId} «{s.Title}» {s.Status} -> {s.Capability}");
foreach (var blocker in catalog.Blockers) Console.WriteLine($"  BLOCKER  {blocker.Code}: {blocker.Detail}");
foreach (var n in catalog.Notes) Console.WriteLine($"  note     {n}");

Console.WriteLine("--- assertions");
Check(catalog.Campaigns.Count == 4, "4 real campaigns discovered");
Check(catalog.Campaigns.Count(c => !c.IsArchive) == 2, "2 selectable campaigns (archive examples excluded)");
Check(catalog.Campaigns.Any(c => c.Selection == "dev-legacy-mainmap" && c.IsArchive && c.Id == "urman.legacy.mainmap"), "dev-legacy-mainmap is archive and keeps its real id");
Check(catalog.Worlds.Select(w => w.ScenePath).Contains("res://scenes/act1_demo.tscn"), "main scene act1_demo is a startable world");
Check(catalog.Worlds.Select(w => w.ScenePath).Contains("res://scenes/full_game.tscn"), "full_game scene is a startable world");
Check(catalog.AuthoredWorldPlots.Count == 6, "6 authored world plot files");
Check(catalog.Specifications.Count == 5 && catalog.Specifications.All(s => s.Capability == WorldAuthoringCapability.SpecifiedNotImplemented), "W01-W05 marked SpecifiedNotImplemented");
Console.WriteLine($"  authored worlds: {string.Join(", ", catalog.AuthoredWorldKeys)}");
Check(catalog.AuthoredWorldKeys.Count == 1 && catalog.AuthoredWorldKeys[0] == "urman.world:act1", "exactly one authored world exists today (the AI-13 gap)");
Check(catalog.Blockers.Any(x => x.Code == "single-authored-world"), "blocker: only one authored world");
Check(catalog.Blockers.Any(x => x.Code == "photo-worlds-specified-only"), "blocker: PhotoWorlds exist only as a specification");
Check(catalog.Blockers.Any(x => x.Code == "campaign-world-link-undeclared"), "blocker: campaign-to-world link is undeclared");
Check(catalog.Blockers.Any(x => x.Code == "world-capability-unverified"), "blocker: inferred world capability needs owner confirmation");
Check(catalog.Blockers.All(x => x.Code != "no-selectable-campaign"), "no false 'no campaign' blocker on a healthy checkout");

var fallback = catalog.Default();
Check(!fallback.Campaign.IsArchive, "default campaign is not an archive example");
Console.WriteLine($"  default  {fallback.Describe()} scene={fallback.World.ScenePath} zone={(fallback.World.ZoneId.Length == 0 ? "<code>" : fallback.World.ZoneId)} binding={fallback.CampaignBinding} rev={fallback.SourceRevision[..12]} pack={fallback.PackPath} state={fallback.StateId}");

var fullgame = catalog.Resolve("urman.fullgame", "res://scenes/full_game.tscn");
Check(fullgame.CampaignBinding == CampaignWorldContext.CampaignBindingDeclared, "full_game declares its campaign in the scene");
Check(fullgame.World.ZoneId == "village_day" && fullgame.World.SpawnPointId == "arrival", "full_game declares zone/spawn");
Check(fullgame.World.Capability == WorldAuthoringCapability.AuthoredWorldPlots, "full_game keeps the authored-plot world capability with evidence attached");
Check(fullgame.Campaign.CompiledPackExists, "fullgame compiled pack exists");
Check(fullgame.SourceRevision != fallback.SourceRevision, "two contexts have different source revisions");
Check(fullgame.StateId != fallback.StateId, "two contexts get different state ids");
foreach (var e in fullgame.World.CapabilityEvidence) Console.WriteLine($"    evidence: {e}");

var threw = false;
try { catalog.Resolve("urman.nope", "res://scenes/act1_demo.tscn"); } catch (InvalidOperationException) { threw = true; }
Check(threw, "unknown campaign is a clear error");
threw = false;
try { catalog.Resolve("urman.chapter1", "res://scenes/nope.tscn"); } catch (InvalidOperationException) { threw = true; }
Check(threw, "unknown scene is a clear error");

Console.WriteLine("--- per-world authoring state");
var temp = Path.Combine(Path.GetTempPath(), "dsprobe-state-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(temp);
var store = WorldAuthoringStateStore.Open(temp);
Check(store.FilePath.EndsWith(Path.Combine(".urman-studio", "world-authoring-state.v1.json")), "state file lives under .urman-studio/");
Check(store.Get(fallback) == WorldAuthoringState.Empty, "missing state reads as empty");
store.Put(fallback, new WorldAuthoringState { Section = "world", Selection = "urman.world:act1/kit/x", Pivot = [4f, 0f, -40f], Yaw = -35f, Pitch = -38f, Distance = 34f });
store.Put(fullgame, new WorldAuthoringState { Section = "dialogue", Selection = "urman.cutscene:tamara-fence", Pivot = [1f, 2f, 3f], Yaw = 12f, Distance = 8f, TopView = true });
Check(store.Save(), "first save writes the file");
Check(!store.Save(), "second save with unchanged state writes nothing");
var reopened = WorldAuthoringStateStore.Open(temp);
var a = reopened.Get(fallback);
var stateB = reopened.Get(fullgame);
Check(a.Section == "world" && a.Selection == "urman.world:act1/kit/x" && Math.Abs(a.Yaw - -35f) < 0.001f && Math.Abs(a.Pivot[2] - -40f) < 0.001f, "world A state round-trips after reopen");
Check(stateB.Section == "dialogue" && stateB.TopView && Math.Abs(stateB.Yaw - 12f) < 0.001f && stateB.Pivot[0] == 1f, "world B state round-trips after reopen");
Check(a.Section != stateB.Section && a.Selection != stateB.Selection && a.Yaw != stateB.Yaw, "the two worlds do not mix");
Check(reopened.Get("urman.chapter1@1.0.0/unknown-world") == WorldAuthoringState.Empty, "unknown world reads as empty, never as another world's state");
Check(reopened.KnownStateIds.Count == 2, "exactly the two touched worlds are stored");
Directory.Delete(temp, true);

Console.WriteLine(failures.Count == 0 ? "\nPROBE RESULT: PASS" : $"\nPROBE RESULT: FAIL ({failures.Count})");
foreach (var f in failures) Console.WriteLine("  - " + f);
return failures.Count == 0 ? 0 : 1;
