using System.Text.Json.Nodes;
using Urman.Studio.Core.Editing;

var root = "/Users/unterlantas/Documents/GitHub/URMAN-deepseek";
var failures = new List<string>();
void Check(bool condition, string what) { if (!condition) failures.Add(what); Console.WriteLine((condition ? "PASS  " : "FAIL  ") + what); }

// ---------- real checkout ----------
var catalog = CampaignWorldCatalog.Discover(root);
Console.WriteLine($"root: {catalog.Root}");
Console.WriteLine($"campaigns: {catalog.Campaigns.Count}  worlds: {catalog.Worlds.Count}  plots: {catalog.AuthoredWorldPlots.Count}  specs: {catalog.Specifications.Count}");
foreach (var w in catalog.Worlds)
    Console.WriteLine($"  world {w.ScenePath,-34} campaign={(w.CampaignDeclaredByScene ? w.CampaignSelection : "<undeclared>"),-16} zone={(w.ZoneId.Length == 0 ? "<code>" : w.ZoneId),-12} cap={w.Capability}");
foreach (var blocker in catalog.Blockers) Console.WriteLine($"  BLOCKER {blocker.Code}: {blocker.Detail}");
foreach (var n in catalog.Notes) Console.WriteLine($"  note {n}");

var main = catalog.Worlds.Single(w => w.ScenePath == "res://scenes/act1_demo.tscn");
var full = catalog.Worlds.Single(w => w.ScenePath == "res://scenes/full_game.tscn");
Check(main.Capability == WorldAuthoringCapability.AuthoredWorldPlots, "P1-2: main scene claims authored world only from a positive loader signal");
Check(main.CapabilityEvidence.Any(e => e.Contains("Act1DemoRoot.cs:263")), "P1-2: loader signal carries the exact file:line");
Check(full.Capability == WorldAuthoringCapability.PresentationOnly, "P1-2/Q5: full_game is PresentationOnly (no loader signal)");
Check(full.CampaignDeclaredByScene && full.CampaignSelection == "urman.fullgame", "full_game keeps its declared campaign");
Check(catalog.Worlds.All(w => w.Capability == WorldAuthoringCapability.AuthoredWorldPlots || w.Capability == WorldAuthoringCapability.PresentationOnly), "no world is claimed editable without a loader signal");

var fallback = catalog.Default(baseRevision: "test-base-rev");
Check(!fallback.Campaign.IsArchive, "default campaign is not an archive example");
Check(fallback.CampaignBinding == CampaignWorldContext.CampaignBindingExplicit, "undeclared scene is an explicit selection");
Check(fallback.BaseRevision == "test-base-rev" && fallback.HasBaseRevision, "Q4: base revision is carried separately from the content fingerprint");
Check(fallback.AuthoredDependencies.Count == 1 + 1 + 1 + 6 + 1, "Q4: authored dependencies include campaign, scene, pack, the six world plots and the loader script");
Check(fallback.AuthoredDependencies.Contains("game/scripts/Act1DemoRoot.cs"), "P2-B: the real main-scene loader script is part of the fingerprint");
Check(fallback.AuthoredDependencies.Contains("game/content/world/act1_village_kit.world.v1.json"), "Q4: plot files are actual dependencies");
Console.WriteLine($"  fingerprint={fallback.ContentFingerprint[..16]} deps={fallback.AuthoredDependencies.Count} state={fallback.StateId}");

var threw = false;
try { catalog.Resolve("urman.chapter1", "res://scenes/full_game.tscn"); } catch (InvalidOperationException e) { threw = e.Message.Contains("объявляет кампанию"); }
Check(threw, "P1-1: mismatched declared campaign is refused");
var okPairing = catalog.Resolve("urman.fullgame", "res://scenes/full_game.tscn");
Check(okPairing.CampaignBinding == CampaignWorldContext.CampaignBindingDeclared && okPairing.World.ZoneId == "village_day", "P1-1: the declared pair resolves");
Check(okPairing.World.SpawnPointId == "arrival", "declared spawn point is read from the node that owns it");
threw = false;
try { catalog.Resolve("urman.nope", "res://scenes/act1_demo.tscn"); } catch (InvalidOperationException) { threw = true; }
Check(threw, "unknown campaign is a clear error");
Check(catalog.AuthoredWorldKeys.Count == 1, "still exactly one authored world (AI-13 gap)");
Check(catalog.Blockers.Any(b => b.Code == "single-authored-world") && catalog.Blockers.Any(b => b.Code == "photo-worlds-specified-only"), "blockers report the real AI-13 gap");

// ---------- synthetic mini checkout (temporary, lives in %TEMP%) ----------
string Mini(string? zone = "village_day", string? campaignResource = null, string? rootScript = "res://scripts/WorldRoot.cs", bool loaderSignal = true, string plotBody = "{\"entities\":[{\"id\":\"urman.world:act1/kit/a\"}]}", bool instancedConflict = false)
{
    var dir = Path.Combine(Path.GetTempPath(), "dsprobe-mini-" + Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(Path.Combine(dir, "content", "campaigns", "urman.demo"));
    Directory.CreateDirectory(Path.Combine(dir, "game", "scenes"));
    Directory.CreateDirectory(Path.Combine(dir, "game", "scripts"));
    Directory.CreateDirectory(Path.Combine(dir, "game", "content", "world"));
    File.WriteAllText(Path.Combine(dir, "content", "campaigns", "urman.demo", "campaign.json"),
        "{\"id\":\"urman.demo\",\"exactVersion\":\"1.0.0\",\"entrypoint\":\"urman.demo:scene/x\",\"modules\":[]}");
    File.WriteAllText(Path.Combine(dir, "game", "project.godot"), "run/main_scene=\"res://scenes/world.tscn\"\n");
    var node = rootScript is null ? "" : $"\nscript = ExtResource(\"1_root\")\n";
    var ext = rootScript is null ? "" : $"[ext_resource path=\"{rootScript}\" type=\"Script\" id=\"1_root\"]\n\n";
    var zoneLine = zone is null ? "" : $"InitialZoneId = \"{zone}\"\nInitialSpawnPointId = \"arrival\"\n";
    var campaignNode = campaignResource is null ? "" : $"\n[node name=\"Bridge\" type=\"Node\" parent=\".\"]\nCampaignResourcePath = \"{campaignResource}\"\n";
    var instanceExt = instancedConflict ? "[ext_resource path=\"res://scenes/other.tscn\" type=\"PackedScene\" id=\"2_other\"]\n" : "";
    var conflicting = instancedConflict ? "\n[node name=\"Instanced\" parent=\".\" instance=ExtResource(\"2_other\")]\nInitialZoneId = \"zirat_road\"\n" : "";
    File.WriteAllText(Path.Combine(dir, "game", "scenes", "world.tscn"),
        $"[gd_scene load_steps=2 format=3]\n\n{ext}{instanceExt}[node name=\"World\" type=\"Node3D\"]\n{node}{zoneLine}{campaignNode}{conflicting}");
    File.WriteAllText(Path.Combine(dir, "game", "content", "world", "demo.world.v1.json"), plotBody);
    if (rootScript is not null)
        File.WriteAllText(Path.Combine(dir, "game", "scripts", "WorldRoot.cs"), loaderSignal ? "void Ready() { main.EnableAct1ConnectedWorld = true; }\n" : "void Ready() { }\n");
    return dir;
}

var mini = Mini();
var miniCatalog = CampaignWorldCatalog.Discover(mini);
Check(miniCatalog.Campaigns.Count == 1 && miniCatalog.Worlds.Count == 1, "mini checkout: one campaign and one world discovered");
Check(miniCatalog.Worlds[0].Capability == WorldAuthoringCapability.AuthoredWorldPlots, "mini checkout: loader signal in the root script grants authored capability");
var miniContext = miniCatalog.Default();
Check(miniContext.AuthoredDependencies.Count == 4, "mini checkout: dependencies are campaign, scene, plot and loader script");
Check(miniContext.AuthoredDependencies.Contains("game/scripts/WorldRoot.cs"), "P2-B: the loader script read for the signal is a dependency");
var before = miniContext.ContentFingerprint;
File.WriteAllText(Path.Combine(mini, "game", "content", "world", "demo.world.v1.json"), "{\"entities\":[{\"id\":\"urman.world:act1/kit/b\"},{\"id\":\"urman.world:act1/kit/c\"}]}");
var after = CampaignWorldCatalog.Discover(mini).Default().ContentFingerprint;
Check(before != after, "P2-1: editing a world plot changes the content fingerprint");

var signalMini = CampaignWorldCatalog.Discover(mini).Default();
File.WriteAllText(Path.Combine(mini, "game", "scripts", "WorldRoot.cs"), "void Ready() { }\n");
var disarmed = CampaignWorldCatalog.Discover(mini).Default();
Check(disarmed.World.Capability == WorldAuthoringCapability.PresentationOnly, "P2-B: turning the loader signal off flips the capability");
Check(disarmed.ContentFingerprint != signalMini.ContentFingerprint, "P2-B: and the content fingerprint moves with the capability");

var instanced = Mini(instancedConflict: true);
var instancedCatalog = CampaignWorldCatalog.Discover(instanced);
Check(instancedCatalog.Worlds.Count == 0, "P2-A: a conflicting zone on an instanced node without a type attribute is detected, not merged away");
Check(instancedCatalog.Notes.Any(n => n.Contains("InitialZoneId")), "P2-A: the instanced-node conflict is explained in the notes");

var noSignal = Mini(loaderSignal: false);
Check(CampaignWorldCatalog.Discover(noSignal).Worlds[0].Capability == WorldAuthoringCapability.PresentationOnly, "mini checkout: a scene without the loader signal is only PresentationOnly");

var noRootScript = Mini(rootScript: null);
Check(CampaignWorldCatalog.Discover(noRootScript).Worlds[0].Capability == WorldAuthoringCapability.PresentationOnly, "mini checkout: an unresolved root script stays conservative");

var ambiguous = Mini(campaignResource: "res://content/urman.demo.compiled.v1.json");
File.WriteAllText(Path.Combine(ambiguous, "game", "scenes", "world.tscn"),
    File.ReadAllText(Path.Combine(ambiguous, "game", "scenes", "world.tscn")) + "\n[node name=\"Other\" type=\"Node\" parent=\".\"]\nCampaignResourcePath = \"res://content/urman.other.compiled.v1.json\"\n");
var ambiguousCatalog = CampaignWorldCatalog.Discover(ambiguous);
Check(ambiguousCatalog.Worlds.Count == 0, "Q1: a scene with conflicting campaign declarations is refused, not guessed");
Check(ambiguousCatalog.Notes.Any(n => n.Contains("CampaignResourcePath")), "Q1: the refusal is explained in the notes");

var bound = Mini(campaignResource: "res://content/urman.demo.compiled.v1.json", zone: null);
var boundCatalog = CampaignWorldCatalog.Discover(bound);
threw = false;
try { boundCatalog.Resolve("urman.other", "res://scenes/world.tscn"); } catch (InvalidOperationException) { threw = true; }
Check(threw, "Q1: a scene declaring another campaign cannot be opened as a different one");

// ---------- per-world state: CAS, links, isolation ----------
var stateRoot = Path.Combine(Path.GetTempPath(), "dsprobe-state-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(stateRoot);
var store = WorldAuthoringStateStore.Open(stateRoot);
Check(store.FilePath.EndsWith(Path.Combine(".urman-studio", "world-authoring-state.v1.json")), "state file lives under .urman-studio/");
var empty = store.Get(fallback);
Check(empty.Section is null && empty.Pivot == WorldPoint.Origin, "P2-4: a missing world reads as a fresh empty state");
var empty2 = store.Get(okPairing);
Check(!ReferenceEquals(empty, empty2), "P2-4: two missing worlds get separate empty values");
store.Put(fallback, new WorldAuthoringState { Section = "world", Selection = "urman.world:act1/kit/x", Pivot = new WorldPoint(4f, 0f, -40f), Yaw = -35f, Distance = 34f });
store.Put(okPairing, new WorldAuthoringState { Section = "dialogue", Selection = "urman.cutscene:tamara-fence", Pivot = new WorldPoint(1f, 2f, 3f), Yaw = 12f, TopView = true });
Check(store.Save(), "first save writes the file");
Check(!store.Save(), "second save with unchanged state writes nothing");
Check(!store.ChangedOnDisk, "own save does not look like a foreign change");
var reopened = WorldAuthoringStateStore.Open(stateRoot);
var a = reopened.Get(fallback);
var stateB = reopened.Get(okPairing);
Check(a.Section == "world" && a.Pivot.Z == -40f && Math.Abs(a.Yaw + 35f) < 0.001f, "world A state round-trips after reopen");
Check(stateB.Section == "dialogue" && stateB.TopView && stateB.Pivot.X == 1f, "world B state round-trips after reopen");
Check(a.Section != stateB.Section && a.Selection != stateB.Selection, "the two worlds do not mix");
Check(reopened.KnownStateIds.Count == 2, "exactly the two touched worlds are stored");
Check(reopened.Get("urman.demo@1.0.0/other") == WorldAuthoringState.Empty(), "an untouched world reads as empty, never as another world's state");

var other = WorldAuthoringStateStore.Open(stateRoot);
_ = other.KnownStateIds;                       // load the current hash
var writer = WorldAuthoringStateStore.Open(stateRoot);
writer.Put(fallback, new WorldAuthoringState { Section = "world", Yaw = 99f });
writer.Save();
Check(other.ChangedOnDisk, "P2-3: a foreign write is detected");
threw = false;
try { other.Put(okPairing, new WorldAuthoringState { Section = "x" }); other.Save(); } catch (InvalidOperationException) { threw = true; }
Check(threw, "P2-3: stale store refuses to overwrite instead of erasing another world's keys");
Check(JsonNode.Parse(File.ReadAllText(other.FilePath))!["urman.chapter1@1.0.0/act1_demo"]!["yaw"]!.GetValue<double>() == 99d, "P2-3: the foreign value survived the refusal");
other.ReloadFromDisk();
other.Put(okPairing, new WorldAuthoringState { Section = "after-reload" });
Check(other.Save(), "P2-3: after an explicit reload the store can write again");
Check(JsonNode.Parse(File.ReadAllText(other.FilePath))!["urman.chapter1@1.0.0/act1_demo"]!["yaw"]!.GetValue<double>() == 99d, "P2-3: reload keeps the foreign value");

var linkRoot = Path.Combine(Path.GetTempPath(), "dsprobe-link-" + Guid.NewGuid().ToString("N")[..8]);
var outside = Path.Combine(Path.GetTempPath(), "dsprobe-outside-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(linkRoot);
Directory.CreateDirectory(outside);
Directory.CreateSymbolicLink(Path.Combine(linkRoot, ".urman-studio"), outside);
var linked = WorldAuthoringStateStore.Open(linkRoot);
threw = false;
try { linked.Put("x", new WorldAuthoringState()); linked.Save(); } catch (IOException) { threw = true; }
Check(threw, "P2-2: a symlinked .urman-studio outside the checkout is refused");
threw = false;
try { _ = linked.Get("x"); } catch (IOException) { threw = true; }
Check(threw, "P2-2: the refusal repeats as a clear error instead of a null dereference");

foreach (var dir in new[] { mini, noSignal, noRootScript, ambiguous, bound, instanced, stateRoot, linkRoot, outside })
{
    try { Directory.Delete(dir, true); } catch (IOException) { }
}

// ---------- STUDIO/AI-18 atmosphere registry ----------
Console.WriteLine("--- atmosphere registry (AI-18)");
Check(AtmosphereFieldRegistry.Fields.Count >= 40, $"registry lists {AtmosphereFieldRegistry.Fields.Count} fields");
Check(AtmosphereFieldRegistry.Fields.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() == AtmosphereFieldRegistry.Fields.Count, "no duplicate field paths in the registry");
Check(AtmosphereFieldRegistry.Fields.All(f => f.Consumer.Length > 0), "every registered field names its consumer or explicitly says none");

var atmosphere = AtmosphereFieldRegistry.Inspect(root);
Console.WriteLine($"profiles: {atmosphere.Profiles.Count}  unknown: {atmosphere.UnknownDataFields.Count}  missing: {atmosphere.MissingRequiredFields.Count}  out-of-range: {atmosphere.OutOfRangeValues.Count}  dead-in-data: {atmosphere.DeadFieldsInData.Count}");
foreach (var profile in atmosphere.Profiles)
    Console.WriteLine($"  {profile.ProfileId,-24} zones=[{string.Join(",", profile.Zones)}] reachable={profile.ReachableByZone} :: {profile.ReachabilityNote}");
foreach (var u in atmosphere.UnknownDataFields) Console.WriteLine($"  UNKNOWN: {u}");
foreach (var d in atmosphere.DeadFieldsInData.Take(3)) Console.WriteLine($"  dead: {d}");
foreach (var n in atmosphere.Notes) Console.WriteLine($"  note: {n}");

Check(atmosphere.Profiles.Count == 7, "7 real atmosphere profiles were read");
Check(atmosphere.UnknownDataFields.Count == 0, "AI-18: the authored file has no field the registry does not know");
Check(atmosphere.MissingRequiredFields.Count == 0, "AI-18: no required field is missing in the real data");
Check(atmosphere.OutOfRangeValues.Count == 0, "AI-18: no authored value breaks a hard runtime rule");
Check(atmosphere.Profiles.Count(p => p.ReachableByZone) == 3, "exactly three profiles are selectable by zone in normal play");
Check(!atmosphere.Profiles.Single(p => p.ProfileId == "village-green-night").ReachableByZone, "village_day@night is honestly reported as unreachable");
Check(atmosphere.Profiles.Single(p => p.ProfileId == "overcast-day").PhaseOnly, "a profile without zones is reported as phase-only");
Check(!atmosphere.Profiles.Single(p => p.ProfileId == "village-winter-frost").ReachableByZone, "an interior-only claim is not sold as normal-play scope");
Check(atmosphere.DeadFieldsInData.Any(d => d.Contains("weather.flakes")) && atmosphere.DeadFieldsInData.Any(d => d.Contains("weather.blizzardSpeed")) && atmosphere.DeadFieldsInData.Any(d => d.Contains("weather.streak")), "AI-18: fields the game does not read are reported, not hidden");
Check(AtmosphereFieldRegistry.FieldsWithoutFrameEffect.Any(f => f.Path == "weather.flakes" && f.Note is not null), "a field without frame effect carries an author-facing explanation");
Check(AtmosphereFieldRegistry.Validate("no.such.field", JsonValue.Create(1)).Count == 1, "an unknown field is refused");
Check(AtmosphereFieldRegistry.Validate("sky.top", JsonValue.Create("not-a-color")).Count == 1, "a malformed colour is refused");
Check(AtmosphereFieldRegistry.Validate("snow.coverage", JsonValue.Create(2.0)).Count == 1, "a value the runtime clamps is refused with an explanation");
Check(AtmosphereFieldRegistry.Validate("sun.rotation[0]", JsonValue.Create(-40.0)).Count == 0, "a valid value passes");

var atmoMini = Path.Combine(Path.GetTempPath(), "dsprobe-atmo-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(atmoMini, "game", "content", "world"));
File.WriteAllText(Path.Combine(atmoMini, "game", "content", "world", "atmosphere.v1.json"),
    "{\"entities\":[{\"id\":\"urman.world:atmosphere/x\",\"params\":{\"profile\":\"x\",\"surprise\":1,\"zones\":[]}}]}");
var drift = AtmosphereFieldRegistry.Inspect(atmoMini);
Check(drift.UnknownDataFields.Any(u => u.Contains("surprise")), "the drift check catches a field the registry does not know");
Check(drift.MissingRequiredFields.Any(m => m.Contains("ambient.energy")), "the drift check catches required fields the parser would throw on");
Directory.Delete(atmoMini, true);

Console.WriteLine(failures.Count == 0 ? "\nPROBE RESULT: PASS" : $"\nPROBE RESULT: FAIL ({failures.Count})");
foreach (var f in failures) Console.WriteLine("  - " + f);
return failures.Count == 0 ? 0 : 1;
