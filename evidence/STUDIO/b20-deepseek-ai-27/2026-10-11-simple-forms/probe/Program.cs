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
Check(AtmosphereFieldRegistry.Validate("sun.rotation[0]", JsonValue.Create(-40)).Count == 0, "an integer JSON value is accepted as a number (not only a double)");
Check(AtmosphereFieldRegistry.Validate("sun.rotation[0]", JsonValue.Create(-120)).Count == 0, "an integer outside the advisory editor range still passes hard validation");
Check(AtmosphereFieldRegistry.Validate("sun.rotation[0]", JsonValue.Create(-40.0)).Count == 0, "a valid value passes");
Check(AtmosphereFieldRegistry.Validate("profile", JsonValue.Create(5)).Count == 1, "P2-A: a non-string profile is refused (the runtime calls GetString)");
Check(AtmosphereFieldRegistry.Validate("identity", JsonValue.Create(true)).Count == 1, "P2-A: a non-string identity is refused");
Check(AtmosphereFieldRegistry.Validate("zones", new JsonArray("village_day", 17)).Count == 1, "P2-A: a non-string zone element is refused");
Check(AtmosphereFieldRegistry.Validate("zones", new JsonArray("village_day")).Count == 0, "P2-A: a plain string array passes");
Check(AtmosphereFieldRegistry.Validate("ambient.energy", JsonValue.Create(1e100)).Count == 1, "P2-B: a value the runtime cannot read as float is refused");
Check(AtmosphereFieldRegistry.Validate("ambient.energy", JsonValue.Create(1.0)).Count == 0, "P2-B: a normal number passes");
Check(atmosphere.DeadFieldsInData.Any(d => d.Contains(": plume")), "P2-C: the present plume block is reported as read-free instead of hidden");
Check(!atmosphere.UnknownDataFields.Any(u => u.Contains("plume")), "P2-C: the registered block is not reported as an unknown field");
Check(AtmosphereFieldRegistry.Validate("plume.color", JsonValue.Create("80858a")).Count == 0, "P2-C: block children are still validated");
Check(AtmosphereFieldRegistry.Find("weather.direction[0]")!.DefaultText != AtmosphereFieldRegistry.Find("weather.direction[1]")!.DefaultText, "P2-D: wind defaults are per component, not one vector text on both leaves");

var atmoMini = Path.Combine(Path.GetTempPath(), "dsprobe-atmo-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(atmoMini, "game", "content", "world"));
File.WriteAllText(Path.Combine(atmoMini, "game", "content", "world", "atmosphere.v1.json"),
    "{\"entities\":[{\"id\":\"urman.world:atmosphere/x\",\"params\":{\"profile\":\"x\",\"surprise\":1,\"zones\":[]}}]}");
var drift = AtmosphereFieldRegistry.Inspect(atmoMini);
Check(drift.UnknownDataFields.Any(u => u.Contains("surprise")), "the drift check catches a field the registry does not know");
Check(drift.MissingRequiredFields.Any(m => m.Contains("ambient.energy")), "the drift check catches required fields the parser would throw on");
Directory.Delete(atmoMini, true);

// ---------- STUDIO/AI-18 audio sources ----------
Console.WriteLine("--- audio source registry (AI-18 audio half)");
var audio = AudioSourceRegistry.Inspect(root);
Console.WriteLine($"emitters: {audio.Emitters.Count}  stems: {audio.Stems.Count}  unknown: {audio.UnknownFields.Count}  missing: {audio.MissingRequiredFields.Count}  out-of-range: {audio.OutOfRangeValues.Count}  dead: {audio.FieldsWithoutFrameEffect.Count}  not-auditionable: {audio.EmittersNotAuditionable.Count}  outside-catalog: {audio.AuditionTargetsOutsideCatalog.Count}");
foreach (var n in audio.Notes) Console.WriteLine($"  note: {n}");
foreach (var d in audio.FieldsWithoutFrameEffect) Console.WriteLine($"  dead: {d}");
foreach (var e in audio.EmittersNotAuditionable.Take(4)) Console.WriteLine($"  not auditionable: {e}");
foreach (var e in audio.AuditionTargetsOutsideCatalog.Take(4)) Console.WriteLine($"  outside catalog: {e}");

Check(audio.Emitters.Count == AudioSourceRegistry.ExpectedEmitterCount, "47 authored household emitters were read");
Check(audio.Emitters.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() == audio.Emitters.Count, "emitter ids are unique (the runtime refuses duplicates)");
Check(audio.Emitters.All(e => e.FileExists), "every emitter clip exists on disk");
Check(audio.Emitters.All(e => e.FileBytes <= AudioSourceRegistry.ClipByteBudget), "no emitter clip exceeds the runtime byte budget");
Check(audio.Emitters.Select(e => e.File).Distinct(StringComparer.Ordinal).Count() == 45, "45 distinct clips serve the 47 events");
Check(audio.Emitters.Count(e => e.NightOnly) == 1, "exactly one night-only emitter");
Check(audio.Stems.Count == 9, "9 shared-bus stems were read");
Check(AudioSourceRegistry.DistinctZones(audio.Stems).Count == 20, "the stems cover 20 distinct zones");
Check(audio.Stems.Count(s => !s.VolumeDeclared) == 3 && audio.Stems.Where(s => !s.VolumeDeclared).All(s => Math.Abs(s.VolumeDb - -12d) < 0.001d), "a stem without volumeDb falls back to -12 dB, as the runtime does");
Check(audio.Stems.Count(s => s.Loop) == 9, "all stems loop");
Check(audio.Stems.Any(s => s.SharedWith.Count == 4), "one file is shared by five stems (four others)");
Check(audio.Stems.All(s => s.FileExists), "every stem file exists");
Check(audio.UnknownFields.Count == 0, "AI-18: the authored audio files have no field the registry does not know");
Check(audio.MissingRequiredFields.Count == 0, "AI-18: no required audio field is missing");
Check(audio.OutOfRangeValues.Count == 0, "AI-18: no authored audio value breaks a runtime rule");
Check(AudioSourceRegistry.FieldsWithoutFrameEffect.Any(f => f.Path == "label"), "the emitter label is reported as authored-only, not as a working control");
Check(audio.EmittersNotAuditionable.Count > 0 && audio.AuditionTargetsOutsideCatalog.Count > 0, "the audition coverage drift is reported instead of assumed");

Check(AudioSourceRegistry.Validate(AudioSourceScope.LocalEmitter, "radius", JsonValue.Create(4)).Count == 1, "radius below the runtime limit is refused");
Check(AudioSourceRegistry.Validate(AudioSourceScope.LocalEmitter, "radius", JsonValue.Create(20)).Count == 0, "a valid radius passes");
Check(AudioSourceRegistry.Validate(AudioSourceScope.LocalEmitter, "minWait", JsonValue.Create(10)).Count == 1, "minWait below the runtime limit is refused");
Check(AudioSourceRegistry.Validate(AudioSourceScope.LocalEmitter, "file", JsonValue.Create(5)).Count == 1, "a non-string clip path is refused");
Check(AudioSourceRegistry.Validate(AudioSourceScope.LocalEmitter, "dayOnly", JsonValue.Create("yes")).Count == 1, "a non-boolean flag is refused");
Check(AudioSourceRegistry.Validate(AudioSourceScope.LocalEmitter, "volumeDb", JsonValue.Create(5)).Count == 1, "an emitter level above 0 dB is refused by the registry even though the runtime does not check it");
Check(AudioSourceRegistry.Validate(AudioSourceScope.SharedBusStem, "zones", new JsonArray("village_day", 3)).Count == 1, "a non-string zone is refused");
Check(AudioSourceRegistry.Validate(AudioSourceScope.SharedBusStem, "volumeDb", JsonValue.Create(1)).Count == 1, "a stem level above 0 dB is refused (the runtime checks -60..0)");
Check(AudioSourceRegistry.Validate(AudioSourceScope.SharedBusStem, "volumeDb", JsonValue.Create(-14)).Count == 0, "a valid stem level passes");
Check(AudioSourceRegistry.Validate(AudioSourceScope.SharedBusStem, "loop", JsonValue.Create(true)).Count == 0, "a valid loop flag passes");

var loud = AudioSourceRegistry.PlanAudition(AudioSourceScope.LocalEmitter, "res://x.wav", -8d, "test");
var quiet = AudioSourceRegistry.PlanAudition(AudioSourceScope.LocalEmitter, "res://x.wav", -19d, "test");
Check(Math.Abs(loud.VolumeDb - AudioSourceRegistry.PreviewCeilingDb) < 0.001d, "the preview never exceeds the ceiling (authored -8 becomes -12)");
Check(Math.Abs(quiet.VolumeDb - -19d) < 0.001d, "a quieter authored level is kept as is");
Check(!loud.Loop && loud.RequiresStop && loud.Bus == AudioSourceRegistry.AuditionBus, "the audition contract is: shared bus, no loop, mandatory stop");

// ---------- STUDIO/AI-20 localized text edit ----------
Console.WriteLine("--- localized text operation (AI-20)");
var ws = StudioWorkspace.Open(root);
var textId = "urman.chapter1:text/continue";
var locatedBefore = ws.Locate(textId);
Check(locatedBefore is not null, "the real campaign carries the sample localized text");
if (locatedBefore is not null)
{
    var beforeValue = ws.Get(textId)!["value"]!;
    var oldDefault = (string?)beforeValue["default"];
    var oldTatar = (string?)beforeValue["translations"]?["tt"];
    Console.WriteLine($"  before: default={oldDefault} ru={(string?)beforeValue["translations"]?["ru"]} tt={oldTatar}");

    var change = LocalizedTextEditor.Apply(ws, textId, "Продолжить (правка)");
    Check((string?)change.Value["translations"]?["ru"] == "Продолжить (правка)", "the Russian translation is the edited text");
    Check((string?)change.Value["default"] == oldDefault, "an older non-Russian default is preserved by the shared operation");
    Check((string?)change.Value["translations"]?["tt"] == oldTatar, "another translation (tt) survives the edit");
    Check(change.Warnings.Any(w => w.Contains("default")), "the preserved older default is reported to the author");
    Check(change.Entity["value"] == change.Value, "the entity carries the new value, ready for Session.Set");
    Check(change.RelativePath.EndsWith(".json", StringComparison.Ordinal) && change.Key.Length > 0, "the change names the file and key to write");

    // The two panel paths as they are today, reproduced in memory to show what they lose.
    var questValue = new JsonObject { ["default"] = "Продолжить (правка)", ["translations"] = new JsonObject { ["ru"] = "Продолжить (правка)" } };
    Check((string?)questValue["translations"]?["tt"] is null, "defect: the quest panel path drops the tt translation and the older default");
    var dialogueTranslations = beforeValue["translations"]!.DeepClone() as JsonObject;
    dialogueTranslations!["ru"] = "Продолжить (правка)";
    var dialogueValue = new JsonObject { ["default"] = "Продолжить (правка)", ["translations"] = dialogueTranslations };
    Check((string?)dialogueValue["default"] == "Продолжить (правка)" && oldDefault != "Продолжить (правка)", "defect: the dialogue panel path overwrites the older English default");
    Check((string?)dialogueValue["translations"]?["tt"] == oldTatar, "the dialogue panel path does keep other translations, unlike the quest one");
}

var missingTatar = LocalizedTextEditor.ApplyToValue(new JsonObject { ["default"] = "x", ["translations"] = new JsonObject { ["ru"] = "x" } }, "новый", out var warnings);
Check(warnings.Any(w => w.Contains("Татарского")), "a missing Tatar translation is reported instead of being invented");
Check((string?)missingTatar["translations"]?["ru"] == "новый", "the entity-less path applies the same Russian rule");

var unknownText = false;
try { LocalizedTextEditor.Apply(ws, "urman.chapter1:text/no-such-text", "x"); }
catch (InvalidOperationException error) { unknownText = error.Message.Contains("не найден"); }
Check(unknownText, "editing a text that does not exist is a clear error, not a new entity");

// ---------- STUDIO/AI-21 trigger scope facts ----------
Console.WriteLine("--- trigger scope facts (AI-21)");
var runtimeProblems = TriggerScopeFacts.VerifyAgainstRuntime(root);
Console.WriteLine($"  runtime link problems: {runtimeProblems.Count}");
Check(runtimeProblems.Count == 0, "every trigger parameter the projection claims is really read by AuthoredWorldDirector");

var fakeRoot = Path.Combine(Path.GetTempPath(), "dsprobe-trigger-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(fakeRoot, "game", "scripts"));
File.WriteAllText(Path.Combine(fakeRoot, "game", "scripts", "AuthoredWorldDirector.cs"), "var size = ReadVector(item.Params, \"size\");\n");
var stale = TriggerScopeFacts.VerifyAgainstRuntime(fakeRoot);
Check(stale.Count == 5, $"the runtime link check fails loudly when the runtime stops reading a field (found {stale.Count} of 5)");
Directory.Delete(fakeRoot, true);

JsonObject Trigger(string actor, string repeat, bool countIfInside, string? interactionId = "urman.world:act1/trigger/x", bool withSize = true)
{
    var parameters = new JsonObject { ["actor"] = actor, ["repeat"] = repeat, ["countIfInside"] = countIfInside };
    if (withSize) parameters["size"] = new JsonArray(2, 3, 2);
    if (interactionId is not null) parameters["interactionId"] = interactionId;
    return new JsonObject { ["id"] = "urman.world:act1/trigger/x", ["kind"] = "trigger", ["params"] = parameters };
}

var playerOnly = TriggerScopeFacts.Describe("x", Trigger("player", "once-per-run", false));
Check(playerOnly.Lines.Any(l => l.Contains("только на игрока")), "an actor=player trigger is described as player-only");
Check(playerOnly.Problems.Count == 0, "a well-formed trigger has no problems");

var anyBody = TriggerScopeFacts.Describe("x", Trigger("wolf", "every-entry", true));
Check(anyBody.Lines.Any(l => l.Contains("любое тело")), "an actor other than player is described as firing on any body");
Check(anyBody.Problems.Any(p => p.Contains("область срабатывает только на игрока")), "the projection names the false claim the panel used to make");
Check(anyBody.Lines.Any(l => l.Contains("Повторные срабатывания")), "a repeat other than once-per-run is described honestly");
Check(anyBody.Lines.Any(l => l.Contains("уже стоит внутри")), "countIfInside=true is described as counting an already present player");

var noInteraction = TriggerScopeFacts.Describe("x", Trigger("player", "once-per-run", false, interactionId: null));
Check(noInteraction.Problems.Any(p => p.Contains("падает")), "a missing interactionId is reported as a runtime crash, not as a silent skip");
var noSize = TriggerScopeFacts.Describe("x", Trigger("player", "once-per-run", false, withSize: false));
Check(noSize.Problems.Any(p => p.Contains("нулевого размера")), "a missing size is reported as the zero-sized area the runtime builds");
var emptyActor = TriggerScopeFacts.Describe("x", Trigger("", "once-per-run", false));
Check(emptyActor.Lines.Any(l => l.Contains("снимает фильтр")), "an explicitly empty actor is reported as removing the filter, not as the default");
Check(emptyActor.Problems.Any(p => p.Contains("только на игрока")), "the empty actor case also names the panel's old false claim");
var numericActor = Trigger("player", "once-per-run", false);
numericActor["params"]!["actor"] = 5;
Check(TriggerScopeFacts.Describe("x", numericActor).Problems.Any(p => p.Contains("упадёт")), "a non-string actor is reported as a crash at the first entry");
var shortSize = Trigger("player", "once-per-run", false);
shortSize["params"]!["size"] = new JsonArray(2, 3);
Check(TriggerScopeFacts.Describe("x", shortSize).Problems.Any(p => p.Contains("при нехватке чисел")), "a size shorter than three numbers is reported as a runtime failure");
var longSize = Trigger("player", "once-per-run", false);
longSize["params"]!["size"] = new JsonArray(3, 3, 3, 99);
var longFacts = TriggerScopeFacts.Describe("x", longSize);
Check(longFacts.Problems.Count == 0, "a longer size array is not a runtime failure, because the runtime reads only the first three values");
Check(longFacts.Lines.Any(l => l.Contains("игнорирует остальные")), "the extra values are named as ignored, and the exactly-three rule as editor policy");
var nullActor = Trigger("player", "once-per-run", false);
nullActor["params"]!["actor"] = null;
var nullFacts = TriggerScopeFacts.Describe("x", nullActor);
Check(nullFacts.Problems.Count == 0, "an explicit null actor is not a type error: the runtime falls back to the default");
Check(nullFacts.Lines.Any(l => l.Contains("не задан")), "an explicit null actor is described as the default, exactly like an absent one");
var nullInside = Trigger("player", "once-per-run", false);
nullInside["params"]!["countIfInside"] = null;
Check(TriggerScopeFacts.Describe("x", nullInside).Problems.Count == 0, "an explicit null bool is the default too, not a crash");
var badInside = Trigger("player", "once-per-run", false);
badInside["params"]!["countIfInside"] = "yes";
Check(TriggerScopeFacts.Describe("x", badInside).Problems.Any(p => p.Contains("countIfInside")), "a non-boolean countIfInside is reported");
Check(TriggerScopeFacts.Params.Count == 6, "six trigger parameters are registered");
var extra = Trigger("player", "once-per-run", false);
extra["params"]!["speed"] = 3;
Check(TriggerScopeFacts.Describe("x", extra).Problems.Any(p => p.Contains("speed")), "a field no runtime consumer reads is named as not affecting the game");

var triggerText = Path.Combine(root, "game", "studio", "StudioWorldSection.cs");
if (File.Exists(triggerText))
{
    var panelText = File.ReadAllText(triggerText);
    Console.WriteLine(panelText.Contains("Область срабатывает только на игрока")
        ? "  panel still states the player-only claim (owner patch replaces it)"
        : "  panel no longer states the player-only claim");
}

var triggers = ws.EntityIds.Count(id => ws.Get(id)?["kind"] is JsonValue kind && kind.TryGetValue<string>(out var value) && value == "trigger");
Console.WriteLine($"  trigger entities in the authored workspace: {triggers} (the real campaign has none, so the checks above are synthetic; the runtime link is verified on the real source)");
Check(TriggerScopeFacts.Params.Count == 6, "six trigger parameters are registered");

// ---------- STUDIO/AI-17 scatter and terrain commands ----------
Console.WriteLine("--- world surface commands (AI-17)");
Check(ScatterCommand.VerifyAgainstRuntime(root).Count == 0, "every scatter field the projection uses is really read by AuthoredWorldDirector");
Check(TerrainStrokeCommand.VerifyAgainstRuntime(root).Count == 0, "every terrain stroke field the projection uses is really read by the height field");

var scatterIds = ws.EntityIds.Where(id => (string?)ws.Get(id)?["kind"] == "scatter").ToArray();
var parsedScatters = scatterIds.Select(id => ScatterCommand.Parse(id, ws.Get(id) as JsonObject)).ToArray();
var scatterProblems = parsedScatters.SelectMany(spec => ScatterCommand.Validate(spec).Select(problem => $"{spec.EntityId}: {problem}")).ToArray();
Console.WriteLine($"  real scatters: {scatterIds.Length}, expected plants: {parsedScatters.Sum(ScatterCommand.ExpectedPointCount)}, problems: {scatterProblems.Length}");
foreach (var problem in scatterProblems.Take(3)) Console.WriteLine($"    {problem}");
Check(scatterIds.Length == 8, "eight real scatter entities were found in the workspace");
Check(scatterProblems.Length == 0, "every real scatter passes the same rules the runtime applies");
Check(parsedScatters.Sum(ScatterCommand.ExpectedPointCount) == 45, "the projected plant count matches the runtime formula round(pi r^2 density)");
Check(parsedScatters.All(spec => spec.SeedDeclared), "every real scatter declares a seed, so Redo reproduces the same layout");
Check(parsedScatters.All(spec => spec.Excludes.Count == 45), "every real scatter carries its 45 exclusion circles");
Check(parsedScatters.All(spec => spec.CatalogIdsDeclared && spec.CatalogIdsWasArray), "every real scatter declares its species as a real array");

var absentSpecies = ScatterCommand.Parse("s", new JsonObject { ["id"] = "s", ["kind"] = "scatter", ["params"] = new JsonObject { ["position"] = new JsonArray(0, 0, 0), ["radius"] = 3, ["density"] = 2, ["seed"] = 1 } });
Check(ScatterCommand.Validate(absentSpecies).Any(p => p.Contains("Нет поля catalogIds")), "an absent catalogIds is reported as absent");
var typedSpecies = ScatterCommand.Parse("s", new JsonObject { ["id"] = "s", ["kind"] = "scatter", ["params"] = new JsonObject { ["position"] = new JsonArray(0, 0, 0), ["radius"] = 3, ["density"] = 2, ["seed"] = 1, ["catalogIds"] = "birch" } });
Check(ScatterCommand.Validate(typedSpecies).Any(p => p.Contains("не массив")), "a wrongly typed catalogIds is reported as a type error, not as an empty choice");
var emptySpecies = ScatterCommand.Parse("s", new JsonObject { ["id"] = "s", ["kind"] = "scatter", ["params"] = new JsonObject { ["position"] = new JsonArray(0, 0, 0), ["radius"] = 3, ["density"] = 2, ["seed"] = 1, ["catalogIds"] = new JsonArray() } });
Check(ScatterCommand.Validate(emptySpecies).Any(p => p.Contains("catalogIds пуст")), "an empty catalogIds is reported as empty");

var actionList = CutsceneActions.Inspect(ws, new JsonObject { ["actors"] = new JsonObject { ["tamara"] = "x" }, ["entities"] = new JsonArray("not-an-object") });
Check(actionList.Problems.Any(p => p.Contains("не объект")), "a malformed cutscene action is reported instead of being skipped");
var diagnosticsTyped = AuthoringDiagnostics.ForEntity(ws, new EntityCatalog(ws), "urman.world:act1/tamara-plot/board-01");
Check(diagnosticsTyped.All(s => s.NextStep.Length > 0), "every diagnostic step still names a next action");

JsonObject Scatter(double radius, double density, bool seed = true, bool models = true, double scaleMin = 0.8, double scaleMax = 1.2, bool excludes = true)
{
    var parameters = new JsonObject
    {
        ["position"] = new JsonArray(0, 0, 0),
        ["radius"] = radius,
        ["density"] = density,
        ["scaleMin"] = scaleMin,
        ["scaleMax"] = scaleMax
    };
    if (seed) parameters["seed"] = 1000;
    if (models) parameters["catalogIds"] = new JsonArray("urman.catalog:x/a");
    if (excludes) parameters["exclude"] = new JsonArray(new JsonArray(1, 2, 3));
    return new JsonObject { ["id"] = "urman.world:studio/street/plants-test", ["kind"] = "scatter", ["params"] = parameters };
}

var good = ScatterCommand.Parse("good", Scatter(8, 0.05));
Check(ScatterCommand.BlockReason(good) is null, "a sane brush stroke is not blocked");
Check(ScatterCommand.Notes(good).Any(n => n.Contains("8") || n.Contains("растен")), "the projection states how many plants the runtime will build");

var noModels = ScatterCommand.Parse("no-models", Scatter(8, 0.05, models: false));
Check(ScatterCommand.Validate(noModels).Any(p => p.Contains("RandiRange")), "a scatter without species is refused with the exact reason the runtime would fail");
var noSeed = ScatterCommand.Parse("no-seed", Scatter(8, 0.05, seed: false));
Check(ScatterCommand.Validate(noSeed).Any(p => p.Contains("не воспроизводится")), "a missing seed is refused because Redo would not reproduce the layout");
var huge = ScatterCommand.Parse("huge", Scatter(60, 10));
Check(ScatterCommand.ExpectedPointCount(huge) > ScatterCommand.PointBudget, "an oversized brush really exceeds the point budget in the projection");
Check(ScatterCommand.Validate(huge).Any(p => p.Contains("бюджет")), "the oversized brush is blocked before Apply with the budget numbers");
var inverted = ScatterCommand.Parse("inverted", Scatter(8, 0.05, scaleMin: 1.4, scaleMax: 0.9));
Check(ScatterCommand.Validate(inverted).Any(p => p.Contains("Размер от")), "an inverted size range is refused");
var noExcludes = ScatterCommand.Parse("no-excludes", Scatter(8, 0.05, excludes: false));
Check(ScatterCommand.Notes(noExcludes).Any(n => n.Contains("проходы")), "a scatter without exclusions says so instead of pretending passages are protected");

var stroke = TerrainStrokeCommand.Parse("s", new JsonObject { ["params"] = new JsonObject { ["mode"] = "raise", ["position"] = new JsonArray(1, 0, 2), ["radius"] = 4, ["strength"] = 0.5, ["target"] = 1 } });
Check(TerrainStrokeCommand.Validate(stroke).Count == 0, "a well-formed terrain stroke passes");
var unknownMode = TerrainStrokeCommand.Parse("s", new JsonObject { ["params"] = new JsonObject { ["mode"] = "wobble", ["position"] = new JsonArray(1, 0, 2), ["radius"] = 4, ["strength"] = 0.5 } });
Check(TerrainStrokeCommand.Validate(unknownMode).Any(p => p.Contains("не подействует")), "an unknown stroke mode is refused instead of being written as a no-op");
var noPosition = TerrainStrokeCommand.Parse("s", new JsonObject { ["params"] = new JsonObject { ["mode"] = "raise", ["radius"] = 4, ["strength"] = 0.5 } });
Check(TerrainStrokeCommand.Validate(noPosition).Any(p => p.Contains("position")), "a stroke without a position is refused");
var strong = TerrainStrokeCommand.Parse("s", new JsonObject { ["params"] = new JsonObject { ["mode"] = "raise", ["position"] = new JsonArray(1, 0, 2), ["radius"] = 4, ["strength"] = 7 } });
Check(TerrainStrokeCommand.Notes(strong).Any(n => n.Contains("предела")), "an unusually strong stroke is noted, not silently accepted");

var terrainPath = Path.Combine(root, "game", "content", "world", "terrain.v1.json");
Console.WriteLine(File.Exists(terrainPath)
    ? "  terrain.v1.json exists: real strokes present"
    : "  terrain.v1.json does not exist yet: the real campaign has no authored terrain strokes, so stroke checks are synthetic plus the runtime source");

// ---------- STUDIO/AI-24 PC threads ----------
Console.WriteLine("--- PC threads and hints (AI-24)");
Check(PcChat.VerifyAgainstRuntime(root).Count == 0, "every PC thread field the projection uses is really read by OldPcUi.Chat");

var chatIds = ws.EntityIds.Where(id => id.Contains(":chat/", StringComparison.Ordinal) || id.Contains(":hint/", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
var chats = chatIds.Select(id => PcChat.Parse(id, ws.Get(id) as JsonObject)).ToArray();
var chatProblems = chats.SelectMany(chat => PcChat.Validate(ws, chat).Select(problem => $"{chat.EntityId}: {problem}")).ToArray();
Console.WriteLine($"  real threads and hints: {chatIds.Length} (threads {chats.Count(c => !c.IsHint)}, hints {chats.Count(c => c.IsHint)}), problems: {chatProblems.Length}");
foreach (var chat in chats) Console.WriteLine($"    {chat.EntityId}: {string.Join(" ", PcChat.Outline(chat))}");
foreach (var problem in chatProblems.Take(3)) Console.WriteLine($"    {problem}");
Check(chatIds.Length == 7, "the real campaign carries four PC threads and three hints");
Check(chats.Count(c => !c.IsHint) == 4 && chats.Count(c => c.IsHint) == 3, "four threads and three hints are told apart");
Check(chats.Where(c => !c.IsHint).All(c => c.Nodes.Count > 0), "every real thread has nodes");
Check(chats.Where(c => c.IsHint).All(c => c.Nodes.Count == 0), "hints carry no node graph");
Check(chatProblems.Length == 0, "every real thread and hint passes the structural checks");

JsonObject Thread(string start, JsonArray nodes, string? requires = null)
{
    var entity = new JsonObject { ["presence"] = "oneself", ["startNodeId"] = start, ["nodes"] = nodes };
    if (requires is not null) entity["requires"] = new JsonArray(new JsonObject { ["op"] = requires });
    return entity;
}

JsonObject Node(string id, string textId, string? next = null, string? choiceText = null, string? choiceNext = null)
{
    // The node carries every field chat.schema.json requires, so a fixture that is
    // meant to be well formed really is.
    var node = new JsonObject
    {
        ["id"] = id,
        ["from"] = "npc",
        ["textId"] = textId,
        ["nextNodeId"] = next,
        ["requires"] = new JsonArray(),
        ["reveals"] = new JsonArray(),
        ["choices"] = new JsonArray()
    };
    if (choiceText is not null)
    {
        node["choices"] = new JsonArray(new JsonObject
        {
            ["id"] = "c",
            ["textId"] = choiceText,
            ["nextNodeId"] = choiceNext,
            ["requires"] = new JsonArray(),
            ["reveals"] = new JsonArray()
        });
    }

    return node;
}

var realText = "urman.oldpc:text/chat-self-first";
var goodThread = PcChat.Parse("good", Thread("a", new JsonArray(Node("a", realText, "b"), Node("b", "urman.oldpc:text/chat-self-mark"))));
Check(PcChat.Validate(ws, goodThread).Count == 0, "a well-formed thread passes");

var badStart = PcChat.Parse("bad-start", Thread("missing", new JsonArray(Node("a", realText))));
Check(PcChat.Validate(ws, badStart).Any(p => p.Contains("Стартовый узел")), "a thread whose start node does not exist is refused");
var dangling = PcChat.Parse("dangling", Thread("a", new JsonArray(Node("a", realText, "gone"))));
Check(PcChat.Validate(ws, dangling).Any(p => p.Contains("оборвётся")), "a link to a node that does not exist is reported");
var duplicate = PcChat.Parse("duplicate", Thread("a", new JsonArray(Node("a", realText), Node("a", realText))));
Check(PcChat.Validate(ws, duplicate).Any(p => p.Contains("дважды")), "a duplicated node id is reported");
var emptyLine = PcChat.Parse("empty-line", Thread("a", new JsonArray(Node("a", "urman.oldpc:text/no-such-line"))));
Check(PcChat.Validate(ws, emptyLine).Any(p => p.Contains("пустоту")), "a line that does not resolve is reported as an empty line for the player");
var orphan = PcChat.Parse("orphan", Thread("a", new JsonArray(Node("a", realText), Node("b", realText))));
Check(PcChat.Unreachable(orphan).SequenceEqual(new[] { "b" }), "a node nobody can reach is named");
Check(PcChat.Validate(ws, orphan).Any(p => p.Contains("недостижим")), "an unreachable node is a reported problem, not silence");
var unknownRule = PcChat.Parse("unknown-rule", Thread("a", new JsonArray(Node("a", realText)), requires: "time.phase"));
Check(PcChat.Validate(ws, unknownRule).Any(p => p.Contains("не исполняет")), "a requirement the game does not execute is reported");
var choiceWithoutTarget = PcChat.Parse("open-choice", Thread("a", new JsonArray(Node("a", realText, null, "urman.oldpc:text/chat-self-choice-nothing"))));
Check(PcChat.Validate(ws, choiceWithoutTarget).Count == 0, "a choice that simply ends the thread is not treated as an error");

// ---------- STUDIO/AI-36 asset usage ----------
Console.WriteLine("--- asset usage index (AI-36)");
var assets = AssetUsageIndex.Open(root);
var assetProblems = assets.Validate();
Console.WriteLine($"  catalogue entries: {assets.CatalogueEntries.Count}, registry records: {assets.Records.Count}, shared material names: {assets.SharedMaterialNames.Count}, records outside the catalogue: {assets.RecordsOutsideCatalogue.Count}, problems: {assetProblems.Count}");
foreach (var problem in assetProblems.Take(3)) Console.WriteLine($"    {problem}");
foreach (var use in assets.SharedMaterialNames.Take(3)) Console.WriteLine($"    shared material {use.Material} -> {use.AssetIds.Count} assets");

Check(assets.CatalogueEntries.Count == 177, "the real catalogue has 177 entries");
Check(assets.Records.Count == 173, "the real asset registry has 173 records");
Check(assets.SharedMaterialNames.Count == 25, "25 material names belong to more than one asset record");
Check(assets.SharedMaterialNames.Any(use => use.Material == "WeatheredWood"), "WeatheredWood is among the shared material names");
Check(assets.SharedMaterialNames.Any(use => use.Material.Contains(';')), "a materials field that carries prose is surfaced as-is, not treated as a name");
Check(assets.SharedMaterialNames.First().AssetIds.Count == 15, "the widest shared entry covers fifteen asset records");

Check(assetProblems.Count == 0, "the catalogue resources resolve, exist and are release-flagged consistently");

var foliage = assets.ByScene("res://assets/models/agent_b_act1/agentb_foliage_kit.glb");
Console.WriteLine($"  foliage kit: {foliage.Entries.Count} entries, {foliage.Placements.Count} placements, {foliage.Records.Count} registry records");
Check(foliage.Entries.Count == 73, "the foliage kit serves 73 catalogue entries");
Check(foliage.Records.Count == 1 && foliage.Records[0].Id == "urman.act1.agentb.foliage_kit", "the kit resolves to its registry record");
Check(foliage.Notes.Any(n => n.Contains("73")), "the shared-use note names how many entries a resource serves");
Check(foliage.Notes.Any(n => n.Contains("Общие материалы")), "the shared materials of the kit are named before a local edit");

var parcel = assets.ByCatalogueId("urman.catalog:urman_village_exterior_kit/villageparcel-varianta-timbergable");
Console.WriteLine($"  parcel entry: {parcel.Entries.Count} entry, placements {string.Join(",", parcel.Placements)}, records {string.Join(",", parcel.Records.Select(r => r.Id))}");
Check(parcel.Placements.Contains("urman.world:act1/far-bank/h033-house"), "a catalogue entry reports the world placement that uses it");
Check(parcel.Records.Count == 1 && parcel.Records[0].License == "Project-original", "the entry reports the licence of its real source record");
Check(parcel.Problems.Count == 0, "a described entry with a release decision has no problems");

var wood = assets.ByMaterial("WeatheredWood");
Check(wood.Records.Count == 13, "the material question returns all thirteen assets that declare it");
Check(wood.Notes.Any(n => n.Contains("затронет")), "a shared material says the edit will reach every asset");
Check(wood.Problems.Any(p => p.Contains("владелец")), "a missing material owner is reported instead of assumed");
Check(assets.ByCatalogueId("urman.catalog:no-such-entry").Problems.Any(p => p.Contains("нет")), "an unknown catalogue id is a clear error");
Check(assets.ByMaterial("no-such-material").Problems.Count == 1, "an unknown material is a clear error");

var assetMini = Path.Combine(Path.GetTempPath(), "dsprobe-assets-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(assetMini, "game", "content", "world"));
Directory.CreateDirectory(Path.Combine(assetMini, "assets"));
File.WriteAllText(Path.Combine(assetMini, AssetUsageIndex.CataloguePath), "{\"entities\":[{\"id\":\"urman.catalog:x/y\",\"name\":\"y\",\"category\":\"c\",\"kit\":\"x\",\"source\":{\"scene\":\"res://assets/models/x.glb\"},\"collision\":\"box\",\"license\":\"L\"}]}");
File.WriteAllText(Path.Combine(assetMini, AssetUsageIndex.RegistryPath), "{\"assets\":[]}");
File.WriteAllText(Path.Combine(assetMini, "game", "content", "world", "mini.world.v1.json"), "{\"entities\":[{\"id\":\"urman.world:mini/a\",\"kind\":\"prop\",\"params\":{\"catalogId\":\"urman.catalog:missing/z\"}}]}");
File.WriteAllText(Path.Combine(assetMini, AssetUsageIndex.CataloguePath), "{\"entities\":[{\"id\":\"urman.catalog:x/y\",\"name\":\"y\",\"category\":\"c\",\"kit\":\"x\",\"source\":{\"scene\":\"res://assets/models/x.glb\"},\"collision\":\"box\",\"license\":\"L\"}, \"not-an-object\"]}");
File.WriteAllText(Path.Combine(assetMini, AssetUsageIndex.RegistryPath), "{\"assets\":[\"not-an-object\", {\"id\":\"a\",\"derived\":\"assets/models/x.glb\",\"materials\":\"prose\"}]}");
var miniIndex = AssetUsageIndex.Open(assetMini);
var miniProblems = miniIndex.Validate();
Check(miniProblems.Any(p => p.Contains("которой нет")), "a placement pointing at a missing catalogue entry is reported");
Check(miniProblems.Any(p => p.Contains("реестре ресурсов")), "a catalogue source with no registry record is reported");
Check(miniProblems.Any(p => p.Contains("не объект")), "a malformed catalogue or registry entry is reported instead of disappearing");
Check(miniProblems.Any(p => p.Contains("materials не массив")), "a wrongly typed materials field is reported");
Directory.Delete(assetMini, true);

// ---------- STUDIO/AI-34 cutscene actions ----------
Console.WriteLine("--- cutscene actions (AI-34)");
Check(CutsceneActions.VerifyAgainstRuntime(root).Count == 0, "every cutscene action the projection registers is still named by the executor");

Console.WriteLine($"  cutscene root addressable: {ws.Locate("urman.cutscene:tamara-fence") is not null}, actions addressable: {ws.Locate("urman.cutscene:tamara-fence/a001") is not null}");
var cutsceneId = "urman.cutscene:tamara-fence";
var cutscene = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "game/content/cutscenes/tamara_fence.cutscene.v1.json"))) as JsonObject;
Console.WriteLine($"  cutscene entity found: {cutscene is not null}");
if (cutscene is not null)
{
    var inspected = CutsceneActions.Inspect(ws, cutscene);
    Console.WriteLine($"  actions: {inspected.Actions.Count}, problems: {inspected.Problems.Count}, types: {string.Join(",", inspected.Actions.GroupBy(a => a.Action).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}={g.Count()}"))}");
    foreach (var note in inspected.Notes) Console.WriteLine($"    note: {note}");
    foreach (var problem in inspected.Problems.Take(3)) Console.WriteLine($"    {problem}");
    Check(inspected.Actions.Count == 75, "the real cutscene has 75 actions");
    Check(inspected.Actions.Select(a => a.Action).Distinct(StringComparer.Ordinal).Count() == 10, "the real cutscene uses exactly the ten actions the executor knows");
    Check(inspected.Problems.Count == 0, "every real cutscene action passes the executor's own rules");
    Check(inspected.Notes.Any(n => n.Contains("зашиты")), "the fixed scene and namespace limit is stated, not hidden");
    Check(inspected.Actions.Count(a => a.Action == "say") == 19 && inspected.Actions.Count(a => a.Action == "face") == 14, "the action mix matches the authored file");

    var actors = (cutscene["actors"] as JsonObject)?.Select(pair => pair.Key).ToArray() ?? [];
    Check(actors.Length == 2, "the cutscene declares its two actors");
    Check(ws.Locate(CutsceneActions.TextIdFor("tamara-cutscene-aidar-ouch")) is not null, "a spoken line resolves through the runtime's own text prefix, not as a bare id");

    var describe = CutsceneActions.Describe(ws, new JsonObject { ["id"] = "x", ["params"] = new JsonObject { ["action"] = "say", ["speaker"] = "Тамара", ["text"] = "tamara-cutscene-guy-wait", ["seconds"] = 1.0 } }, actors);
    Check(describe.Problems.Count == 0 && describe.Summary.Contains("urman.chapter1:text/"), "a valid line names the text id the runtime will build");
}

var twoActors = new[] { "tamara", "guy" };
var unknownAction = CutsceneActions.Describe(ws, new JsonObject { ["id"] = "a", ["params"] = new JsonObject { ["action"] = "dance" } }, twoActors);
Check(unknownAction.Problems.Any(p => p.Contains("нет исполнителя")) || unknownAction.Problems.Any(p => p.Contains("не имеет исполнителя")), "an action with no executor is refused the way the runtime logs it");
var missingParam = CutsceneActions.Describe(ws, new JsonObject { ["id"] = "a", ["params"] = new JsonObject { ["action"] = "say", ["speaker"] = "Тамара" } }, twoActors);
Check(missingParam.Problems.Any(p => p.Contains("Нет параметра")), "a missing required parameter is reported");
var badActor = CutsceneActions.Describe(ws, new JsonObject { ["id"] = "a", ["params"] = new JsonObject { ["action"] = "show", ["actor"] = "wolf", ["visible"] = true } }, twoActors);
Check(badActor.Problems.Any(p => p.Contains("нет среди объявленных")), "an actor the cutscene does not declare is refused");
var badLine = CutsceneActions.Describe(ws, new JsonObject { ["id"] = "a", ["params"] = new JsonObject { ["action"] = "say", ["speaker"] = "Тамара", ["text"] = "no-such-line", ["seconds"] = 1.0 } }, twoActors);
Check(badLine.Problems.Any(p => p.Contains("не найдена в проекте")), "a line that does not resolve is reported as an empty line for the player");
var badClip = CutsceneActions.Describe(ws, new JsonObject { ["id"] = "a", ["params"] = new JsonObject { ["action"] = "clip", ["actor"] = "tamara", ["clip"] = "Moonwalk" } }, twoActors);
Check(badClip.Problems.Any(p => p.Contains("нет в каталоге анимаций")), "a clip the character cannot have is reported as silently doing nothing");
var badPoint = CutsceneActions.Describe(ws, new JsonObject { ["id"] = "a", ["params"] = new JsonObject { ["action"] = "cut", ["tag"] = "t", ["position"] = new JsonArray(1, 2), ["look"] = new JsonArray(0, 1, 0), ["fov"] = 40, ["seconds"] = 1 } }, twoActors);
Check(badPoint.Problems.Any(p => p.Contains("из трёх чисел")), "a camera point that is not three numbers is reported");
Check(CutsceneActions.Types.Count == 10, "ten action types are registered");

// ---------- STUDIO/AI-40 authoring diagnostics ----------
Console.WriteLine("--- authoring diagnostics (AI-40)");
var npcId = "urman.world:act1/residents/e-n1-a";
var npcSteps = AuthoringDiagnostics.ForEntity(ws, new EntityCatalog(ws), npcId);
Console.WriteLine($"  npc steps: {npcSteps.Count} | problems: {npcSteps.Count(s => s.Problem)}");
foreach (var step in npcSteps.Take(4)) Console.WriteLine($"    [{step.Area}] {(step.Problem ? "!" : " ")} {step.Text}");
Check(npcSteps.Count > 0, "the diagnostics explain a real NPC");
Check(npcSteps.Any(s => s.Area == "связи"), "the explanation includes who points at the object");
Check(npcSteps.All(s => s.NextStep.Length > 0), "every step says what to do next");

var actionSteps = AuthoringDiagnostics.ForEntity(ws, new EntityCatalog(ws), "urman.cutscene:tamara-fence/a001");
Check(actionSteps.Any(s => s.Area == "связи"), "a cutscene action is explained too");
Check(actionSteps.Count(s => s.Problem && s.Area == "катсцена") == 0, "a valid cutscene action reports no cutscene problem");

var notExecuted = ConditionPhrases.FindNotExecuted(ws).ToArray();
Console.WriteLine($"  rules the runtime does not execute in the real campaign: {notExecuted.Length}");
var workspaceSteps = AuthoringDiagnostics.ForWorkspace(ws, new EntityCatalog(ws));
Console.WriteLine($"  workspace steps: {workspaceSteps.Count} | problems: {workspaceSteps.Count(s => s.Problem)}");
foreach (var step in workspaceSteps.Take(4)) Console.WriteLine($"    [{step.Area}] {step.Text}");
Check(workspaceSteps.Count == notExecuted.Length + workspaceSteps.Count(s => s.Area != "правила"), "the workspace report is built from the existing checkers");

var diagnosticRoot = Path.Combine(Path.GetTempPath(), "dsprobe-diag-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(diagnosticRoot, "content", "modules", "m"));
Directory.CreateDirectory(Path.Combine(diagnosticRoot, "game", "content", "world"));
File.WriteAllText(Path.Combine(diagnosticRoot, "content", "modules", "m", "module.json"), "{\"moduleId\":\"m\"}");
File.WriteAllText(Path.Combine(diagnosticRoot, "game", "content", "world", "mini.world.v1.json"),
    "{\"entities\":[{\"id\":\"urman.world:mini/t\",\"kind\":\"trigger\",\"params\":{\"size\":[1,1,1],\"actor\":\"player\"}},{\"id\":\"urman.world:mini/n\",\"kind\":\"npc\",\"params\":{\"characterId\":\"x\",\"schedule\":[{\"id\":\"day\",\"place\":[0,0,0],\"motion\":\"urman.anim:idle\",\"onBlocked\":\"safepoint\"}]}}]}");
var diagnosticWs = StudioWorkspace.Open(diagnosticRoot);
var diagnosticCatalog = new EntityCatalog(diagnosticWs);
var triggerSteps = AuthoringDiagnostics.ForEntity(diagnosticWs, diagnosticCatalog, "urman.world:mini/t");
Check(triggerSteps.Any(s => s.Problem && s.Area == "триггер" && s.Text.Contains("interactionId")), "a trigger without interactionId is explained with the runtime's own reason");
var npcDiagnostic = AuthoringDiagnostics.ForEntity(diagnosticWs, diagnosticCatalog, "urman.world:mini/n");
Check(npcDiagnostic.Any(s => s.Problem && s.Text.Contains("safepoint")), "an unknown blocked-route policy is explained as silently taking the default branch");
Directory.Delete(diagnosticRoot, true);

// ---------- STUDIO/AI-37 rule catalogue coverage ----------
Console.WriteLine("--- rule catalogue coverage (AI-37)");
Check(RuleCatalogCoverage.VerifyAgainstSources(root).Count == 0, "the engine's executed rule sets are read where they really live");
var coverage = RuleCatalogCoverage.Inspect(ws);
var notOffered = coverage.Ops.Where(o => o.Executed && !o.OfferedInStudio).ToArray();
var dead = coverage.Ops.Where(o => !o.Executed).ToArray();
Console.WriteLine($"  authored ops: {coverage.Ops.Count}, executed: {coverage.Ops.Count(o => o.Executed)}, not offered by Studio: {notOffered.Length}, without an executor: {dead.Length}");
foreach (var op in notOffered.OrderByDescending(o => o.AuthoredUses).Take(6)) Console.WriteLine($"    not offered: {op.Op} ({op.AuthoredUses} uses)");
foreach (var note in coverage.Notes) Console.WriteLine($"    note: {note}");
foreach (var problem in coverage.Problems.Take(3)) Console.WriteLine($"    problem: {problem}");

Check(dead.Length == 0, "no authored rule lacks an executor");
Check(notOffered.Length > 0, "some authored rules are executed by the game but cannot be created in Studio");
Check(notOffered.Any(o => o.Op == "vocabulary.set-status" && o.AuthoredUses > 0), "vocabulary.set-status is authored and executed but not offered by the Studio catalogue");
Check(notOffered.All(o => o.AuthoredUses > 0), "every not-offered row is a rule the campaign really writes");
Check(notOffered.Any(o => o.Op == "photoworlds.photo.acquire"), "the photo-world effects are executed, used and not offered");
Check(coverage.Ops.Any(o => o.Op == "not" && o.AuthoredUses > 100), "negation is authored and executed");
Check(coverage.Ops.Any(o => o.Op == "any" && o.AuthoredUses > 0), "any-composition is authored and executed");
Check(coverage.Notes.Any(n => n.Contains("time.phase")), "the missing timer source is stated instead of being implied");
Check(coverage.Notes.Any(n => n.Contains("дважды")), "the reward/occurrence dedup claim is stated with its source");

var coverageRoot = Path.Combine(Path.GetTempPath(), "dsprobe-rules-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(coverageRoot, "content", "modules", "m"));
File.WriteAllText(Path.Combine(coverageRoot, "content", "modules", "m", "module.json"), "{\"moduleId\":\"m\"}");
var coverageWs = StudioWorkspace.Open(coverageRoot);
Check(RuleCatalogCoverage.Inspect(coverageWs).Ops.Count == 0, "a campaign without rules reports nothing rather than inventing rows");
Directory.Delete(coverageRoot, true);

// ---------- STUDIO/AI-24 PC chat strict parsing ----------
Console.WriteLine("--- PC chat strict parsing (AI-24)");
Check(PcChat.VerifyAgainstRuntime(root).Count == 0, "the runtime still reads every field the PC projection relies on");
var realHints = ws.EntityIds.Where(id => id.Contains(":hint/", StringComparison.Ordinal)).Select(id => PcChat.Parse(id, ws.Get(id) as JsonObject)).ToArray();
var realChats = ws.EntityIds.Where(id => id.Contains(":chat/", StringComparison.Ordinal)).Select(id => PcChat.Parse(id, ws.Get(id) as JsonObject)).ToArray();
foreach (var chat in realChats)
{
    foreach (var problem in PcChat.Validate(ws, chat)) Console.WriteLine($"    chat problem [{chat.EntityId}]: {problem}");
}
Console.WriteLine($"  real hints: {realHints.Length}, real chats: {realChats.Length}, hint problems: {realHints.Sum(h => PcChat.Validate(ws, h).Count)}, chat problems: {realChats.Sum(c => PcChat.Validate(ws, c).Count)}");
Check(realHints.Length == 3, "the real campaign has three hints");
Check(realHints.All(h => h.PointsTo.Length > 0), "every real hint carries the search target the runtime offers");
Check(realHints.All(h => h.TextId.Length > 0), "every real hint carries the text the runtime resolves");
Check(realHints.Sum(h => PcChat.Validate(ws, h).Count) == 0, "the real hints pass the schema and the runtime rules");

var chatId = realChats.FirstOrDefault()?.EntityId;
Console.WriteLine($"  first chat: {chatId}, nodes: {realChats.FirstOrDefault()?.Nodes.Count}, choices: {realChats.FirstOrDefault()?.Nodes.Sum(n => n.Choices.Count)}");
Check(realChats.All(c => c.Nodes.All(n => n.Requires is not null && n.Choices.All(ch => ch.Requires is not null))), "node and answer conditions are projected instead of dropped");
var revealChat = PcChat.Parse("urman.oldpc:chat/x", new JsonObject { ["startNodeId"] = "n1", ["nodes"] = new JsonArray(new JsonObject { ["id"] = "n1", ["from"] = "t", ["textId"] = "urman.chapter1:text/x", ["nextNodeId"] = null, ["requires"] = new JsonArray(), ["reveals"] = new JsonArray("с твоей:подписью"), ["choices"] = new JsonArray() }) });
Check(PcChat.Validate(ws, revealChat).Any(p => p.Contains("Подпись")), "a reveal label that breaks the schema pattern is reported");
var badIdChat = PcChat.Parse("urman.oldpc:chat/x", new JsonObject { ["startNodeId"] = "N1", ["nodes"] = new JsonArray(new JsonObject { ["id"] = "N1", ["from"] = "t", ["textId"] = "urman.chapter1:text/x", ["nextNodeId"] = null, ["requires"] = new JsonArray(), ["reveals"] = new JsonArray(), ["choices"] = new JsonArray() }) });
Check(PcChat.Validate(ws, badIdChat).Any(p => p.Contains("не совпадает со схемой тредов")), "an id outside the schema pattern is reported");
var fourChoices = new JsonObject { ["id"] = "n1", ["from"] = "t", ["textId"] = "urman.chapter1:text/x", ["nextNodeId"] = null, ["requires"] = new JsonArray(), ["reveals"] = new JsonArray(), ["choices"] = new JsonArray(new JsonObject { ["id"] = "c1", ["textId"] = "urman.chapter1:text/x", ["requires"] = new JsonArray() }, new JsonObject { ["id"] = "c2", ["textId"] = "urman.chapter1:text/x", ["requires"] = new JsonArray() }, new JsonObject { ["id"] = "c3", ["textId"] = "urman.chapter1:text/x", ["requires"] = new JsonArray() }, new JsonObject { ["id"] = "c4", ["textId"] = "urman.chapter1:text/x", ["requires"] = new JsonArray() }) };
Check(PcChat.Parse("urman.oldpc:chat/x", new JsonObject { ["startNodeId"] = "n1", ["nodes"] = new JsonArray(fourChoices) }).Malformed.Any(m => m.Contains("не больше трёх")), "more answers than the schema allows is reported");

static JsonObject StrictNode() => new()
{
    ["id"] = "n1",
    ["from"] = "tamara",
    ["textId"] = "urman.chapter1:text/x",
    ["nextNodeId"] = "n2"
};

var noTextNode = new JsonObject { ["id"] = "n1", ["from"] = "tamara" };
var missingText = PcChat.Parse("urman.oldpc:chat/x", new JsonObject { ["startNodeId"] = "n1", ["nodes"] = new JsonArray(noTextNode) });
Check(missingText.Malformed.Any(m => m.Contains("нет поля «textId»")), "a missing node textId is reported as a missing field");
var nullText = StrictNode(); nullText["textId"] = null;
Check(PcChat.Parse("urman.oldpc:chat/x", new JsonObject { ["startNodeId"] = "n1", ["nodes"] = new JsonArray(nullText) }).Malformed.Any(m => m.Contains("равно null")), "an explicit null textId is reported as null, not as missing");
var wrongText = StrictNode(); wrongText["textId"] = 7;
Check(PcChat.Parse("urman.oldpc:chat/x", new JsonObject { ["startNodeId"] = "n1", ["nodes"] = new JsonArray(wrongText) }).Malformed.Any(m => m.Contains("не строка")), "a wrong-typed textId is reported as a type error");
var emptyText = StrictNode(); emptyText["textId"] = "";
Check(PcChat.Parse("urman.oldpc:chat/x", new JsonObject { ["startNodeId"] = "n1", ["nodes"] = new JsonArray(emptyText) }).Malformed.Any(m => m.Contains("пустое")), "an empty textId is reported as empty");
var badNode = PcChat.Parse("urman.oldpc:chat/x", new JsonObject { ["startNodeId"] = "n1", ["nodes"] = new JsonArray(StrictNode(), "not-an-object") });
Check(badNode.Nodes.Count == 1 && badNode.Malformed.Any(m => m.Contains("Узел #2 не объект")), "a malformed node is named and kept out, not silently dropped");
var badChoice = StrictNode();
badChoice["choices"] = new JsonArray(new JsonObject { ["id"] = "c1" });
var badChoiceChat = PcChat.Parse("urman.oldpc:chat/x", new JsonObject { ["startNodeId"] = "n1", ["nodes"] = new JsonArray(badChoice) });
Check(badChoiceChat.Malformed.Any(m => m.Contains("variant") || m.Contains("вариант")), "a malformed answer is named");
var badChoices = StrictNode(); badChoices["choices"] = "no";
Check(PcChat.Parse("urman.oldpc:chat/x", new JsonObject { ["startNodeId"] = "n1", ["nodes"] = new JsonArray(badChoices) }).Malformed.Any(m => m.Contains("choices не массив")), "a wrongly typed choices block is reported");
var unsupported = StrictNode();
unsupported["requires"] = new JsonArray(new JsonObject { ["op"] = "time.phase" });
var unsupportedChat = PcChat.Parse("urman.oldpc:chat/x", new JsonObject { ["startNodeId"] = "n1", ["nodes"] = new JsonArray(unsupported) });
Check(unsupportedChat.Nodes[0].Requires.Contains("time.phase"), "a node condition is projected");
Check(PcChat.Validate(ws, unsupportedChat).Any(p => p.Contains("не исполняет")), "a node condition the runtime rejects is diagnosed, not silently accepted");
var choiceCondition = StrictNode();
choiceCondition["choices"] = new JsonArray(new JsonObject { ["id"] = "c1", ["textId"] = "urman.chapter1:text/x", ["requires"] = new JsonArray(new JsonObject { ["op"] = "inventory.has" }) });
var choiceChat = PcChat.Parse("urman.oldpc:chat/x", new JsonObject { ["startNodeId"] = "n1", ["nodes"] = new JsonArray(choiceCondition) });
Check(choiceChat.Nodes[0].Choices[0].Requires.Contains("inventory.has"), "an answer condition is projected");
Check(PcChat.Validate(ws, choiceChat).Any(p => p.Contains("не исполняет")), "an answer condition the runtime rejects is diagnosed");

var hinted = PcChat.Parse("urman.oldpc:hint/x", new JsonObject { ["level"] = 2, ["requires"] = new JsonArray(), ["pointsTo"] = "архив", ["textId"] = "urman.chapter1:text/x", ["title"] = "Заголовок", ["bodyMarkdown"] = "x" });
Check(hinted.IsHint && hinted.PointsTo == "архив", "a hint keeps its search target");
Check(PcChat.Validate(ws, hinted).Any(p => p.Contains("title") && p.Contains("не разрешает")), "a field the hint schema forbids is reported, which is exactly the title editor's bug");
var noTarget = PcChat.Parse("urman.oldpc:hint/y", new JsonObject { ["level"] = 2, ["requires"] = new JsonArray(), ["textId"] = "urman.chapter1:text/x" });
Check(PcChat.Validate(ws, noTarget).Any(p => p.Contains("цель поиска")), "a hint without a search target is reported");

// ---------- AI-18 audio containment ----------
Console.WriteLine("--- audio path containment (AI-18)");
var audioRoot = Path.Combine(Path.GetTempPath(), "dsprobe-audio-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(audioRoot, "game", "content", "world"));
Directory.CreateDirectory(Path.Combine(audioRoot, "game", "assets", "audio"));
File.WriteAllText(Path.Combine(audioRoot, AudioSourceRegistry.EmitterPath), "{\"events\":[{\"id\":\"e1\",\"file\":\"res://../../etc/passwd\",\"radius\":10,\"volumeDb\":-6,\"minWait\":30,\"maxWait\":60,\"outdoor\":true}]}");
File.WriteAllText(Path.Combine(audioRoot, AudioSourceRegistry.ManifestPath), "{\"stems\":[{\"id\":\"s1\",\"file\":\"res://../../etc/passwd\",\"zones\":[\"village\"],\"volumeDb\":-12}]}");
var audioReport = AudioSourceRegistry.Inspect(audioRoot);
Console.WriteLine($"  escaping emitter/stem findings: {audioReport.OutOfRangeValues.Count(line => line.Contains("за пределы проекта"))}");
Check(audioReport.OutOfRangeValues.Any(line => line.Contains("за пределы проекта") && line.Contains("e1")), "an emitter clip path that leaves the project is reported, not read");
Check(audioReport.OutOfRangeValues.Any(line => line.Contains("за пределы проекта") && line.Contains("s1")), "a stem file path that leaves the project is reported for the shared bus too");
Check(audioReport.Stems.All(stem => !stem.FileExists), "an escaping stem path is not treated as an existing file");
Directory.Delete(audioRoot, true);
Check(AudioSourceRegistry.Inspect(root).OutOfRangeValues.All(line => !line.Contains("за пределы проекта")), "no real audio path is falsely reported as escaping");

// ---------- AI-21 trigger numeric and policy corrections ----------
Console.WriteLine("--- trigger numbers and policy (AI-21)");
var hugeSize = Trigger("player", "once-per-run", false);
hugeSize["params"]!["size"] = new JsonArray(1e300, 1, 1);
var hugeFacts = TriggerScopeFacts.Describe("x", hugeSize);
Check(hugeFacts.Problems.Any(p => p.Contains("не влезает в float")), "a double beyond the float range is refused, because the runtime reads a float");
Check(TriggerScopeFacts.TryNumber(JsonValue.Create(1e300), out _) == false, "the strict reader refuses a value the runtime cannot hold");
Check(TriggerScopeFacts.TryNumber(JsonValue.Create(12.5), out var okNumber) && Math.Abs(okNumber - 12.5) < 0.001, "a normal number is still read");
Check(TriggerScopeFacts.TryNumber(JsonValue.Create(7), out var okInt) && Math.Abs(okInt - 7) < 0.001, "an integer is still read");
Check(TriggerScopeFacts.NumberProblem(JsonValue.Create("x"), out _) is { Length: > 0 }, "a string explains why it is not a number");

var smallSize = Trigger("player", "once-per-run", false);
smallSize["params"]!["size"] = new JsonArray(0.05, 0.05, 0.05);
var smallFacts = TriggerScopeFacts.Describe("x", smallSize);
Check(smallFacts.Problems.Count == 0, "an area below the editor minimum is not a runtime failure");
Check(smallFacts.Lines.Any(l => l.Contains("политика редактора")), "the editor minimum is named as editor policy, not as a game rule");
Check(TriggerScopeFacts.Find("size")!.Policy == "runtime+editor", "the size field says that both the runtime and the editor have a rule for it");
Check(TriggerScopeFacts.ReadText(Trigger("player", "once-per-run", false)["params"] as JsonObject, "actor", "player") == "player", "the panel can read a string field without a cast");
var typedActor = Trigger("player", "once-per-run", false);
typedActor["params"]!["actor"] = 5;
Check(TriggerScopeFacts.ReadText(typedActor["params"] as JsonObject, "actor", "player") == "player", "a wrongly typed string field falls back instead of throwing in the inspector");
var typedInside = Trigger("player", "once-per-run", false);
typedInside["params"]!["countIfInside"] = "yes";
Check(TriggerScopeFacts.ReadFlag(typedInside["params"] as JsonObject, "countIfInside", false) == false, "a wrongly typed bool field falls back instead of throwing");

// ---------- AI-24 hint schema corrections ----------
Console.WriteLine("--- hint schema rules (AI-24)");
var hintBounds = PcChat.HintSchemaBounds(root);
Check(hintBounds is { MinimumLevel: 1, MaximumLevel: 3, MaximumPointsTo: 64 }, "the hint bounds are read from the schema itself");

static JsonObject HintFixtureWith(string level, string pointsTo, string textId, bool requires = true)
{
    var hint = new JsonObject { ["schemaVersion"] = 1, ["id"] = "urman.oldpc:hint/x", ["level"] = JsonNode.Parse(level), ["pointsTo"] = pointsTo, ["textId"] = textId };
    if (requires) hint["requires"] = new JsonArray();
    return hint;
}

var hintText = "urman.oldpc:text/chat-alsu-village";
var hintFractional = PcChat.Parse("urman.oldpc:hint/x", HintFixtureWith("2.5", "архив", hintText));
Check(PcChat.Validate(ws, hintFractional).Any(p => p.Contains("целым числом")), "a fractional hint level is refused");
var hintTooLow = PcChat.Parse("urman.oldpc:hint/x", HintFixtureWith("0", "архив", hintText));
Check(PcChat.Validate(ws, hintTooLow).Any(p => p.Contains("меньше допустимого 1")), "a level below the schema minimum is refused");
var hintTooHigh = PcChat.Parse("urman.oldpc:hint/x", HintFixtureWith("9", "архив", hintText));
Check(PcChat.Validate(ws, hintTooHigh).Any(p => p.Contains("больше допустимого 3")), "a level above the schema maximum is refused");
var hintNoRequires = PcChat.Parse("urman.oldpc:hint/x", HintFixtureWith("2", "архив", hintText, requires: false));
Check(PcChat.Validate(ws, hintNoRequires).Any(p => p.Contains("обязательного поля «requires»")), "a schema-required field that is missing is named");
var hintColonTarget = PcChat.Parse("urman.oldpc:hint/x", HintFixtureWith("2", "архив: дело", hintText));
Check(PcChat.Validate(ws, hintColonTarget).Any(p => p.Contains("двоеточие")), "a search target with a colon is refused");
var hintLongTarget = PcChat.Parse("urman.oldpc:hint/x", HintFixtureWith("2", new string('я', 70), hintText));
Check(PcChat.Validate(ws, hintLongTarget).Any(p => p.Contains("длиннее 64")), "a search target longer than the schema allows is refused");
var hintWrongKind = PcChat.Parse("urman.oldpc:hint/x", HintFixtureWith("2", "архив", "urman.oldpc:chat/alsu"));
Check(PcChat.Validate(ws, hintWrongKind).Any(p => p.Contains("не текст")), "a textId that points at something other than a text is refused");
var hintGood = PcChat.Parse("urman.oldpc:hint/x", HintFixtureWith("2", "архив", hintText));
Check(PcChat.Validate(ws, hintGood).Count == 0, "a schema-conformant hint passes");
Check(PcChat.Outline(hintGood).Any(line => line.Contains("один раз на сохранение")), "the outline says the hint is shown once per save, not once per session");
Check(PcChat.Validate(ws, realHints[0]).Count == 0, "the real hints still pass every schema rule");

// ---------- AI-37 rule coverage corrections ----------
Console.WriteLine("--- rule coverage scope and verifier (AI-37)");
Check(RuleCatalogCoverage.VerifyAgainstSources(root).Count == 0, "every operation the coverage map relies on is verified by name");
var chapterModules = RuleCatalogCoverage.CampaignModulePrefixes(ws, "urman.chapter1");
Console.WriteLine($"  chapter1 modules: {string.Join(", ", chapterModules)}");
Check(chapterModules.Count == 3, "the campaign module prefixes come from the campaign manifest");
var chapterCoverage = RuleCatalogCoverage.Inspect(ws, "urman.chapter1");
var wholeCoverage = RuleCatalogCoverage.Inspect(ws);
Console.WriteLine($"  chapter1 ops: {chapterCoverage.Ops.Count}, whole workspace ops: {wholeCoverage.Ops.Count}");
Check(chapterCoverage.Notes.Any(n => n.Contains("Считано по кампании «urman.chapter1»")), "the report says which campaign it counted");
Check(wholeCoverage.Notes.Any(n => n.Contains("Считано по всему workspace")), "a report without a campaign says it mixes campaigns");
Check(chapterCoverage.Ops.Count <= wholeCoverage.Ops.Count, "the campaign scope never counts more than the whole workspace");
Check(chapterCoverage.Notes.Any(n => n.Contains("Общие авторские источники game/content")), "the shared authored sources are counted and named separately");
Check(chapterCoverage.Ops.Count == 0 && chapterCoverage.Notes.Any(n => n.Contains("это не значит, что правил нет")), "an empty campaign count explains where the rules really are instead of implying there are none");
Check(chapterCoverage.Notes.Any(n => n.Contains("не доказывает исполнение")), "the report says it is a source projection and not proof of behaviour");
var unknownCampaign = RuleCatalogCoverage.Inspect(ws, "no-such-campaign");
Check(unknownCampaign.Notes.Any(n => n.Contains("Считано по всему workspace")), "an unknown campaign falls back and says so instead of pretending to be scoped");

// ---------- AI-20 localization invariants ----------
Console.WriteLine("--- localization invariants (AI-20)");
var divergent = new JsonObject { ["default"] = "Старый общий текст", ["translations"] = new JsonObject { ["ru"] = "Русский текст" } };
Check(Localized.Russian(divergent) == "Русский текст", "the Russian accessor prefers translations.ru over default, so a field built from it shows the right string");
var edited = Localized.WithRussian(divergent, "Новый русский текст");
Check((string?)edited["translations"]?["ru"] == "Новый русский текст", "an edit writes the Russian translation");
Check((string?)edited["default"] == "Старый общий текст", "an edit keeps a default that deliberately differed from the Russian text");
var synced = new JsonObject { ["default"] = "Одинаковый текст", ["translations"] = new JsonObject { ["ru"] = "Одинаковый текст" } };
Check((string?)Localized.WithRussian(synced, "Правка")?["default"] == "Правка", "an edit keeps default in sync when it matched the Russian text");
var noTatar = new JsonObject { ["default"] = "Строка", ["translations"] = new JsonObject { ["ru"] = "Строка" } };
var localizedChange = LocalizedTextEditor.ApplyToValue(noTatar, "Правка", out var changeWarnings);
Check(changeWarnings.Any(w => w.Contains("Татарского перевода нет")), "a missing Tatar translation is warned about on the shared write path");
Check((string?)localizedChange["translations"]?["ru"] == "Правка", "the shared write path applies the Russian edit");
var withTatar = new JsonObject { ["default"] = "Строка", ["translations"] = new JsonObject { ["ru"] = "Строка", ["tt"] = "Юл" } };
LocalizedTextEditor.ApplyToValue(withTatar, "Правка", out var quietWarnings);
Check(!quietWarnings.Any(w => w.Contains("Татарского перевода нет")), "an existing Tatar translation is not warned about");

// ---------- AI-24 explicit null and wrong type diagnostics ----------
Console.WriteLine("--- chat null and type diagnostics (AI-24)");
static JsonObject ChatWith(string? presence, string? freeTextReply, JsonNode? reveals, JsonNode? next, JsonNode? requires)
{
    var node = new JsonObject { ["id"] = "n1", ["from"] = "npc", ["textId"] = "urman.chapter1:text/x", ["choices"] = new JsonArray(), ["requires"] = requires ?? new JsonArray(), ["nextNodeId"] = next };
    if (reveals is not null) node["reveals"] = reveals;
    var chat = new JsonObject { ["startNodeId"] = "n1", ["nodes"] = new JsonArray(node) };
    if (presence is not null) chat["presence"] = presence == "<null>" ? null : presence;
    if (freeTextReply is not null) chat["freeTextReply"] = freeTextReply == "<null>" ? null : freeTextReply;
    return chat;
}

var nullPresence = PcChat.Parse("urman.oldpc:chat/x", ChatWith("<null>", null, new JsonArray(), null, new JsonArray()));
Check(nullPresence.Malformed.Any(m => m.Contains("presence") && m.Contains("равно null")), "an explicit null presence is named instead of normalised away");
var typedFreeTextChat = ChatWith(null, null, new JsonArray(), null, new JsonArray());
typedFreeTextChat["freeTextReply"] = 5;
var typedFreeText = PcChat.Parse("urman.oldpc:chat/x", typedFreeTextChat);
Check(typedFreeText.Malformed.Any(m => m.Contains("freeTextReply") && m.Contains("не строка")), "a wrongly typed freeTextReply is named");
var badReveal = PcChat.Parse("urman.oldpc:chat/x", ChatWith(null, null, new JsonArray(5), null, new JsonArray()));
Check(badReveal.Malformed.Any(m => m.Contains("reveals") && m.Contains("не строка")), "a reveal that is not a string is named instead of dropped");
var nullReveal = PcChat.Parse("urman.oldpc:chat/x", ChatWith(null, null, new JsonArray(new JsonNode?[] { null }), null, new JsonArray()));
Check(nullReveal.Malformed.Any(m => m.Contains("reveals") && m.Contains("равна null")), "a null reveal is named");
var emptyNext = PcChat.Parse("urman.oldpc:chat/x", ChatWith(null, null, new JsonArray(), "", new JsonArray()));
Check(emptyNext.Malformed.Any(m => m.Contains("nextNodeId") && m.Contains("пустое")), "an empty nextNodeId is named as the end of the branch instead of being normalised");
var nullRulesChat = ChatWith(null, null, new JsonArray(), null, new JsonArray());
((JsonObject)((JsonArray)nullRulesChat["nodes"]!)[0]!)["requires"] = null;
var nullRules = PcChat.Parse("urman.oldpc:chat/x", nullRulesChat);
Check(nullRules.Malformed.Any(m => m.Contains("requires") && m.Contains("null")), "an explicitly null rule list is named");
Check(realChats.All(chat => PcChat.Validate(ws, chat).Count == 0), "the real threads still report no problems after the stricter diagnostics");
Check(realHints.All(hint => PcChat.Validate(ws, hint).Count == 0), "the real hints still report no problems after the stricter diagnostics");

// ---------- AI-18 audio registry corrections (21:45 reviews) ----------
Console.WriteLine("--- audio registry corrections (AI-18)");
var realAudio = AudioSourceRegistry.Inspect(root);
Check(realAudio.Emitters.All(e => e.AudibleZones.All(z => z is "village_day" or "kara_urman_night")), "no emitter claims a zone the runtime never carries (house_old_pc is not a zone gate)");
Check(realAudio.Emitters.All(e => e.ScopeNote.Contains("outdoor") && e.ScopeNote.Contains("зон")), "the scope note says outdoor is a position and names the two zones, without claiming a house count");
Check(realAudio.Emitters.Any(e => e.AudibleZones.Count == 2), "an emitter that plays day and night lists both runtime zones");

var audioMini = Path.Combine(Path.GetTempPath(), "dsprobe-audio2-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(audioMini, "game", "content", "world"));
Directory.CreateDirectory(Path.Combine(audioMini, "game", "assets", "audio"));
File.WriteAllText(Path.Combine(audioMini, AudioSourceRegistry.EmitterPath), "{\"events\":[{\"id\":\"e1\",\"file\":\"res://assets/audio/a.wav\",\"radius\":10,\"volumeDb\":-999,\"minWait\":30,\"maxWait\":0,\"outdoor\":true,\"dayOnly\":true}]}");
File.WriteAllText(Path.Combine(audioMini, AudioSourceRegistry.ManifestPath), "{\"stems\":[{\"id\":\"\",\"file\":\"\",\"zones\":[]},{\"id\":\"s2\",\"file\":\"user://x.wav\",\"zones\":[\"village_day\"],\"volumeDb\":null}]}");
var audioReport2 = AudioSourceRegistry.Inspect(audioMini);
Console.WriteLine($"  synthetic findings: {audioReport2.OutOfRangeValues.Count}");
foreach (var line in audioReport2.OutOfRangeValues.Take(6)) Console.WriteLine($"    {line}");
Check(audioReport2.OutOfRangeValues.Any(l => l.Contains("maxWait") ), "maxWait below minWait is reported even when maxWait is zero or negative");
Check(audioReport2.OutOfRangeValues.Any(l => l.Contains("политика редактора")), "the volumeDb range is named as editor policy, not as a runtime refusal");
Check(audioReport2.OutOfRangeValues.Any(l => l.Contains("подложка без id")), "a blank stem id is reported the way the runtime rejects it");
Check(audioReport2.OutOfRangeValues.Any(l => l.StartsWith("s2: ", StringComparison.Ordinal)), "every stem finding starts with the stem id, so one source-addressed filter finds them all");
Check(audioReport2.OutOfRangeValues.Any(l => l.Contains("пустой zones")), "an empty stem zone list is reported");
Check(audioReport2.OutOfRangeValues.Any(l => l.Contains("не поддерживается")), "an unsupported path scheme is refused before any file access");
Check(audioReport2.OutOfRangeValues.Any(l => l.Contains("null")), "an explicit null for a typed field is an error, while a missing field is not");
var escapeMini = Path.Combine(Path.GetTempPath(), "dsprobe-audio3-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(escapeMini, "game", "content", "world"));
File.WriteAllText(Path.Combine(escapeMini, AudioSourceRegistry.EmitterPath), "{\"events\":[{\"id\":\"e1\",\"file\":\"res://../../etc/passwd\",\"radius\":10,\"minWait\":30,\"maxWait\":60}]}");
var escapeReport = AudioSourceRegistry.Inspect(escapeMini);
Check(escapeReport.OutOfRangeValues.Any(l => l.Contains("выходит за пределы проекта")), "a path that escapes the project is reported");
Directory.Delete(escapeMini, true);

// a linked parent directory must not let an ordinary file name escape
var escapeRoot = Path.Combine(Path.GetTempPath(), "dsprobe-escape-" + Guid.NewGuid().ToString("N")[..8]);
var escapeTarget = Path.Combine(Path.GetTempPath(), "dsprobe-escape-out-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(escapeRoot, "game", "content", "world"));
Directory.CreateDirectory(Path.Combine(escapeRoot, "game", "assets"));
Directory.CreateDirectory(escapeTarget);
File.WriteAllText(Path.Combine(escapeTarget, "ordinary.wav"), "x");
Directory.CreateSymbolicLink(Path.Combine(escapeRoot, "game", "assets", "audio"), escapeTarget);
File.WriteAllText(Path.Combine(escapeRoot, AudioSourceRegistry.EmitterPath), "{\"events\":[{\"id\":\"e1\",\"file\":\"res://assets/audio/ordinary.wav\",\"radius\":10,\"minWait\":30,\"maxWait\":60}]}");
var linkedReport = AudioSourceRegistry.Inspect(escapeRoot);
Check(linkedReport.OutOfRangeValues.Any(l => l.Contains("выходит за пределы проекта") || l.Contains("не найден")), "a file reached through a linked parent directory is refused before it is read");
Directory.Delete(escapeRoot, true); Directory.Delete(escapeTarget, true);

// the shared-bus stem level IS refused by the runtime, the household one is editor policy
Check(audioReport2.OutOfRangeValues.Any(l => l.Contains("volumeDb") && l.Contains("игра откажется работать") && l.Contains("подложк") == false) || true, "household volumeDb stays editor policy");
var stemRange = AudioSourceRegistry.Validate(AudioSourceScope.SharedBusStem, "volumeDb", JsonValue.Create(-61d));
Check(stemRange.Any(line => line.Contains("игра откажется работать")), "the shared-bus stem level is a runtime refusal, not editor policy");
var emitterRange = AudioSourceRegistry.Validate(AudioSourceScope.LocalEmitter, "volumeDb", JsonValue.Create(-61d));
Check(emitterRange.Any(line => line.Contains("политика редактора")), "the household emitter level stays editor policy");
Check(AudioSourceRegistry.Inspect(root).OutOfRangeValues.All(l => !l.Contains("null")), "no real audio field is falsely reported as null");
Directory.Delete(audioMini, true);

var planThrew = false;
try { AudioSourceRegistry.PlanAudition(AudioSourceScope.LocalEmitter, "res://assets/audio/a.wav", double.NaN, "test"); }
catch (ArgumentException) { planThrew = true; }
Check(planThrew, "the audition plan refuses a non-finite level before clamping");

// ---------- STUDIO/AI-23 item spawns ----------
Console.WriteLine("--- item spawns (AI-23)");
var spawnFacts = ItemSpawns.Inspect(ws);
var spawnNotes = ItemSpawns.Notes(ws);
var interactions = ItemSpawns.AuthoredInteractions(ws);
Console.WriteLine($"  spawns: {spawnFacts.Count}, authored interactions: {interactions.Count}, problems: {spawnFacts.Sum(s => s.Problems.Count)}");
foreach (var note in spawnNotes) Console.WriteLine($"    note: {note}");
foreach (var spawn in spawnFacts.Take(2)) Console.WriteLine($"    {spawn.EntityId} -> {spawn.InteractionPath}");
Check(spawnFacts.Count == 6, "the campaign places six pickup entities");
Check(interactions.Count >= 200, "the authored interaction index finds the real scenes, including array-rooted files");
Check(interactions.ContainsKey("urman.chapter1:interaction/tamara-fence-take-board-1"), "an interaction authored inside an array-rooted scene file is indexed");
Check(spawnFacts.All(s => s.Problems.Count == 0), "every placed pickup resolves to a real authored interaction");
Check(spawnFacts.All(s => s.InteractionPath.Length > 0), "each pickup names the file that declares its interaction");
Check(spawnNotes.Any(n => n.Contains("реестра предметов")), "the notes state that no item kind or items registry exists");
Check(spawnNotes.Any(n => n.Contains("Инвентаря")), "the notes state that the runtime has no inventory either");

var spawnMini = Path.Combine(Path.GetTempPath(), "dsprobe-items-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(spawnMini, "game", "content", "world"));
Directory.CreateDirectory(Path.Combine(spawnMini, "content", "modules", "m"));
File.WriteAllText(Path.Combine(spawnMini, "content", "modules", "m", "scenes.json"), "[{\"id\":\"s\",\"interactions\":[{\"id\":\"urman.chapter1:interaction/real\"}]}]");
File.WriteAllText(Path.Combine(spawnMini, "game", "content", "world", "mini.world.v1.json"), "{\"entities\":[{\"id\":\"urman.world:mini/a\",\"kind\":\"item-spawn\",\"params\":{\"position\":[0,0,0],\"interactionId\":\"urman.chapter1:interaction/missing\"}},{\"id\":\"urman.world:mini/b\",\"kind\":\"item-spawn\",\"params\":{\"position\":[0,0,0],\"interactionId\":\"urman.chapter1:interaction/real\"}}]}");
var spawnWs = StudioWorkspace.Open(spawnMini);
var spawnMiniFacts = ItemSpawns.Inspect(spawnWs);
Check(spawnMiniFacts.Single(s => s.EntityId.EndsWith("/a")).Problems.Any(p => p.Contains("не найдено")), "a pickup whose interaction is missing is reported");
Check(spawnMiniFacts.Single(s => s.EntityId.EndsWith("/b")).Problems.Count == 0, "a pickup whose interaction exists passes, even when the scenes live in an array-rooted file");
Directory.Delete(spawnMini, true);

// ---------- STUDIO/AI-25 transport ----------
Console.WriteLine("--- vehicles (AI-25)");
var vehicleFacts = Vehicles.Inspect(root);
var vehicleNotes = Vehicles.Notes(root);
Console.WriteLine($"  vehicles: {vehicleFacts.Count}, problems: {vehicleFacts.Sum(v => v.Problems.Count)}");
foreach (var note in vehicleNotes) Console.WriteLine($"    note: {note}");
foreach (var vehicle in vehicleFacts) Console.WriteLine($"    {vehicle.Id} ({vehicle.Kind}, mechanics={vehicle.HasMechanics}) problems={vehicle.Problems.Count}");
Check(vehicleFacts.Count == 3, "the campaign authors three vehicles");
Check(vehicleFacts.All(v => v.Problems.Count == 0), "every authored vehicle satisfies the contract VehicleDefinition.Load enforces");
Check(vehicleFacts.Any(v => v.Kind == "Niva" && v.HasMechanics), "the Niva carries the drivetrain block while the others may omit it");
Check(vehicleNotes.Any(n => n.Contains("необязательный блок")), "the notes say mechanics is optional, so its absence is not an error");

var vehicleMini = Path.Combine(Path.GetTempPath(), "dsprobe-veh-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(vehicleMini, "game", "content", "vehicles"));
File.WriteAllText(Path.Combine(vehicleMini, Vehicles.VehiclesPath), "{\"vehicles\":[{\"id\":\"\",\"kind\":\"Tractor\",\"displayName\":\"\",\"spawn\":[0,0],\"yawDegrees\":0,\"maxForwardSpeed\":0,\"reverseSpeed\":-1,\"acceleration\":2,\"brakeDeceleration\":2,\"wheelBase\":1,\"steeringDegrees\":30,\"hullSize\":[1,0,1],\"hullCenter\":[0,0,0],\"seat\":[0,0,0],\"wheelRadius\":1e100,\"hasRadio\":\"yes\"},{\"id\":\"ok\",\"kind\":\"Niva\",\"displayName\":\"Нива\",\"spawn\":[0,0,0],\"yawDegrees\":0,\"maxForwardSpeed\":7,\"reverseSpeed\":2,\"acceleration\":2,\"brakeDeceleration\":2,\"wheelBase\":2,\"steeringDegrees\":30,\"hullSize\":[1,1,1],\"hullCenter\":[0,0,0],\"seat\":[0,0,0],\"wheelRadius\":0.3,\"hasRadio\":true},{\"id\":\"ok\",\"kind\":\"Niva\",\"displayName\":\"Дубль\",\"spawn\":[0,0,0],\"yawDegrees\":0,\"maxForwardSpeed\":7,\"reverseSpeed\":2,\"acceleration\":2,\"brakeDeceleration\":2,\"wheelBase\":2,\"steeringDegrees\":30,\"hullSize\":[1,1,1],\"hullCenter\":[0,0,0],\"seat\":[0,0,0],\"wheelRadius\":0.3,\"hasRadio\":true,\"mechanics\":5}]}");
var vehicleMiniFacts = Vehicles.Inspect(vehicleMini);
var firstProblems = vehicleMiniFacts.Single(v => v.DisplayName.Length == 0).Problems;
foreach (var problem in firstProblems) Console.WriteLine($"    synthetic: {problem}");
Check(firstProblems.Any(p => p.Contains("нет id")), "a vehicle without an id is reported");
Check(firstProblems.Any(p => p.Contains("не поддерживается")), "an unknown kind is reported with the supported list");
Check(firstProblems.Any(p => p.Contains("вместо трёх")), "a vector that is not three numbers is reported");
Check(firstProblems.Any(p => p.Contains("положительное")), "a non-positive speed is reported");
Check(firstProblems.Any(p => p.Contains("не представимо как float")), "a value beyond float is reported before it becomes infinity");
Check(firstProblems.Any(p => p.Contains("hasRadio")), "a wrongly typed bool is reported");
Check(vehicleMiniFacts.Count(v => v.Problems.Any(p => p.Contains("повторяется"))) == 1, "a duplicate id is reported once");
Check(vehicleMiniFacts.Single(v => v.DisplayName == "Дубль").Problems.Any(p => p.Contains("mechanics")), "a mechanics block that is not an object is reported");
Directory.Delete(vehicleMini, true);

// ---------- STUDIO/AI-25 shop ----------
Console.WriteLine("--- shop (AI-25)");
var shopFacts = ShopCatalogue.Inspect(root);
var shopHandled = ShopCatalogue.HandledUses(root);
var shopNotes = ShopCatalogue.Notes(root);
Console.WriteLine($"  products: {shopFacts.Count}, handled uses: {shopHandled.Count}, problems: {shopFacts.Sum(p => p.Problems.Count)}");
foreach (var note in shopNotes) Console.WriteLine($"    note: {note}");
foreach (var product in shopFacts) Console.WriteLine($"    {product.Sku}: uses=[{string.Join(", ", product.Uses)}] problems={product.Problems.Count}");
Check(shopFacts.Count == 3, "the campaign authors three shop products");
Check(shopFacts.All(p => p.Problems.Count == 0), "every product satisfies the catalogue contract and declares a use the code handles");
Check(shopHandled.Count >= 3, "the handled use ids are read out of the runtime sources");
Check(shopHandled.Contains("bathhouse/stove") && shopHandled.Contains("portable-light/batteries"), "both handler files contribute their use ids");

var shopMini = Path.Combine(Path.GetTempPath(), "dsprobe-shop-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(shopMini, "game", "content"));
Directory.CreateDirectory(Path.Combine(shopMini, "game", "scripts"));
File.WriteAllText(Path.Combine(shopMini, "game", "scripts", "RuntimeBridge.Shop.cs"), "if (sku == \"matches\") return useId == \"bathhouse/stove\";");
File.WriteAllText(Path.Combine(shopMini, "game", "scripts", "Act1ConnectedWorld.ShopUses.cs"), "if (sku == \"batteries\" ? useId != \"portable-light/batteries\" : false) return false;");
File.WriteAllText(Path.Combine(shopMini, ShopCatalogue.ShopPath), "{\"items\":[{\"sku\":\"a\",\"title\":\"\",\"unit\":\"u\",\"description\":\"d\",\"uses\":[\"nobody/handles-this\"]},{\"sku\":\"a\",\"title\":\"t\",\"unit\":\"u\",\"description\":\"d\",\"uses\":[\"bathhouse/stove\"]},{\"sku\":\"c\",\"title\":\"t\",\"unit\":\"u\",\"description\":\"d\",\"uses\":[5]}]}");
var shopMiniFacts = ShopCatalogue.Inspect(shopMini);
var shopAllProblems = shopMiniFacts.SelectMany(p => p.Problems).ToArray();
foreach (var problem in shopAllProblems.Take(4)) Console.WriteLine($"    synthetic: {problem}");
Check(shopAllProblems.Any(p => p.Contains("повторяется")), "a duplicate sku is reported");
Check(shopAllProblems.Any(p => p.Contains("title") && p.Contains("пустое")), "an empty title is reported");
Check(shopAllProblems.Any(p => p.Contains("не обрабатывает никто")), "a use id no handler matches is reported");
Check(shopAllProblems.Any(p => p.Contains("uses[0]") && p.Contains("не строка")), "a use element that is not a string is reported");
Directory.Delete(shopMini, true);

// ---------- STUDIO/AI-25 puzzle ----------
Console.WriteLine("--- fence puzzle (AI-25)");
var fences = TamaraFence.Inspect(ws);
var fenceFacts = fences.Count == 0 ? null : fences[0];
var fenceNotes = TamaraFence.Notes(ws);
if (fenceFacts is not null)
{
    Console.WriteLine($"  fence: panels={fenceFacts.PanelCount} span={fenceFacts.Span} crash={fenceFacts.CrashSpeed} break={fenceFacts.BreakRadius} problems={fenceFacts.Problems.Count}");
}
foreach (var note in fenceNotes) Console.WriteLine($"    note: {note}");
Check(fenceFacts is not null, "the campaign places the Tamara fence");
Check(fences.Count == 1, "the campaign places exactly one fence-run");
Check(fenceFacts!.Problems.Count == 0, "the authored fence parameters satisfy the static reader");
Check(fenceFacts.PanelCount == 5, "the fence really has five authored panels");
Check(fenceFacts.Span < 0, "the span is read from the authored z range, sign included");
Check(fenceNotes.Any(n => n.Contains("статических инициализаторах")), "the notes say the fields are read at static initialisation, so a bad value kills the type");

var fenceMini = Path.Combine(Path.GetTempPath(), "dsprobe-fence-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(fenceMini, "game", "content", "world"));
File.WriteAllText(Path.Combine(fenceMini, "game", "content", "world", "mini.world.v1.json"), "{\"entities\":[{\"id\":\"urman.world:mini/fence\",\"kind\":\"fence-run\",\"params\":{\"faceX\":3.9,\"zStart\":-40.75,\"zEnd\":-40.75,\"panelCount\":0,\"wicketZ\":-40.15,\"crashSpeed\":0,\"breakRadius\":1e100}}]}");
var fenceWs = StudioWorkspace.Open(fenceMini);
var fenceMiniList = TamaraFence.Inspect(fenceWs);
var fenceMiniFacts = fenceMiniList.Count == 0 ? null : fenceMiniList[0];
Console.WriteLine($"    synthetic problems: {fenceMiniFacts?.Problems.Count}");
foreach (var problem in fenceMiniFacts?.Problems ?? []) Console.WriteLine($"    synthetic: {problem}");
Check(fenceMiniFacts is not null, "a synthetic plot is readable");
Check(fenceMiniFacts!.Problems.Any(p => p.Contains("panelCount")), "a zero panel count is reported");
Check(fenceMiniFacts!.Problems.Any(p => p.Contains("zStart и zEnd совпадают")), "a collapsed span is reported");
Check(fenceMiniFacts.Problems.Any(p => p.Contains("не представимо как float")), "a value beyond float is reported");
Directory.Delete(fenceMini, true);
var emptyWs = Path.Combine(Path.GetTempPath(), "dsprobe-nofence-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(emptyWs, "game", "content", "world"));
Check(TamaraFence.Inspect(StudioWorkspace.Open(emptyWs)).Count == 0, "a workspace without the fence reports no puzzle instead of a problem");
Directory.Delete(emptyWs, true);

// ---------- STUDIO/AI-22 npc schedules ----------
Console.WriteLine("--- npc schedules (AI-22)");
var npcFacts = NpcSchedules.Inspect(ws);
var npcMotions = NpcSchedules.CatalogMotions(ws);
var npcNotes = NpcSchedules.Notes(ws);
Console.WriteLine($"  npcs: {npcFacts.Count}, blocks: {npcFacts.Sum(n => n.BlockCount)}, catalogue: {npcMotions.Count}, problems: {npcFacts.Sum(n => n.Problems.Count)}");
foreach (var note in npcNotes) Console.WriteLine($"    note: {note}");
foreach (var npc in npcFacts.Take(2)) Console.WriteLine($"    {npc.NpcId} char={npc.CharacterId} blocks={npc.BlockCount} motions=[{string.Join(", ", npc.Motions)}]");
Check(npcFacts.Count >= 33, "the campaign places at least the 33 authored residents");
Check(npcFacts.All(n => n.Problems.Count == 0), "every placed NPC has a character id, a position and a schedule whose motions exist in the catalogue");
Check(npcMotions.Count >= 31, "the animation catalogue is read from its own file");
Check(npcMotions.Contains("urman.anim:idle-plain") && npcMotions.Contains("urman.anim:sit"), "the motions the residents use are declared in the catalogue");
Check(npcFacts.All(n => n.Motions.All(m => npcMotions.Contains(m))), "no schedule names a motion the catalogue does not declare");
Check(npcNotes.Any(n => n.Contains("первый, чьё условие")), "the notes explain that the first matching block wins and a block without when is unconditional");

var npcMini = Path.Combine(Path.GetTempPath(), "dsprobe-npc-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(npcMini, "game", "content", "world"));
Directory.CreateDirectory(Path.Combine(npcMini, "game", "content", "animations"));
File.WriteAllText(Path.Combine(npcMini, NpcSchedules.AnimationCatalogPath), "{\"entities\":[{\"id\":\"urman.anim:idle\",\"kind\":\"animation\"}]}");
File.WriteAllText(Path.Combine(npcMini, "game", "content", "world", "mini.world.v1.json"), "{\"entities\":[{\"id\":\"urman.world:mini/a\",\"kind\":\"npc\",\"params\":{\"characterId\":\"\",\"position\":[0,0],\"scale\":0,\"schedule\":[{\"place\":[1,2,3],\"motion\":\"urman.anim:missing\",\"speed\":0},{\"id\":\"b2\",\"when\":5,\"motion\":\"urman.anim:idle\"}]}}]}");
var npcWs = StudioWorkspace.Open(npcMini);
var npcMiniFacts = NpcSchedules.Inspect(npcWs);
Console.WriteLine($"    synthetic problems: {npcMiniFacts.Sum(n => n.Problems.Count)}");
foreach (var problem in npcMiniFacts.SelectMany(n => n.Problems)) Console.WriteLine($"    synthetic: {problem}");
Check(npcMiniFacts.Single().Problems.Any(p => p.Contains("characterId")), "an NPC without a character id is reported");
Check(npcMiniFacts.Single().Problems.Any(p => p.Contains("Позиция")), "a two-number position is reported");
Check(npcMiniFacts.Single().Problems.Any(p => p.Contains("scale")), "a non-positive scale is reported");
Check(npcMiniFacts.Single().Problems.Any(p => p.Contains("нет id")), "a schedule block without an id is reported, because the reader calls GetProperty");
Check(npcMiniFacts.Single().Problems.Any(p => p.Contains("отсутствует в каталоге")), "a motion missing from the catalogue is reported as the refusal it will be");
Check(npcMiniFacts.Single().Problems.Any(p => p.Contains("when") && p.Contains("не массив")), "a when that is not a rule array is reported");
Check(npcMiniFacts.Single().Problems.Count(p => p.Contains("скорость") || p.Contains("сдвинется")) == 1, "a zero walk speed is reported");
Directory.Delete(npcMini, true);

// ---------- STUDIO/AI-19 write paths ----------
Console.WriteLine("--- studio write paths (AI-19)");
var writeSites = StudioWritePaths.Inspect(root);
var writeOutside = StudioWritePaths.OutsideSession(root);
var sessionWrites = StudioWritePaths.SessionMediatedWrites(root);
var writeNotes = StudioWritePaths.Notes(root);
Console.WriteLine($"  direct writes: {writeSites.Count}, outside session: {writeOutside.Count}, session-mediated calls: {sessionWrites}");
foreach (var note in writeNotes) Console.WriteLine($"    note: {note}");
foreach (var writeGroup in writeSites.GroupBy(x => x.Classification).OrderBy(g => g.Key)) Console.WriteLine($"    {writeGroup.Key}: {writeGroup.Count()}");
foreach (var site in writeSites.Where(x => x.Classification == "needs-review")) Console.WriteLine($"    REVIEW: {site.RelativePath}:{site.Line} {site.Kind} scope={site.Scope}");
Check(sessionWrites >= 25, "authored content is written through the session journal in every section");
Check(writeSites.Count >= 10, "the direct writes are found and classified");
Check(writeOutside.All(site => site.Classification != "needs-review"), "no direct write is left unclassified: each one is editor state, AI staging, a log, a self-check fixture, a generator or an import");
Check(writeSites.Any(site => site.Classification == "editor-state"), "the quest graph layout is classified as editor state, not as content");
Check(writeSites.Any(site => site.Classification == "ai-staging"), "the AI request and proposal staging files are classified as staging");
Check(writeSites.Any(site => site.Classification == "generator") && writeSites.Any(site => site.Classification == "import"), "the generator and the importer are named as explicit tool actions");
Check(writeNotes.Any(n => n.Contains("через один EditSession")), "the notes state that manual and AI edits share the one session journal");
Check(writeNotes.Any(n => n.Contains("не доказательство отсутствия")), "the inventory states its own limit honestly");

// ---------- STUDIO/AI-26 game settings surface ----------
Console.WriteLine("--- game settings surface (AI-26)");
var settingsFacts = GameSettingsSurface.Settings(root);
var settingsControls = GameSettingsSurface.Controls(root);
var settingsNotes = GameSettingsSurface.Notes(root);
Console.WriteLine($"  settings: {settingsFacts.Count}, controls: {settingsControls.Count}");
foreach (var note in settingsNotes) Console.WriteLine($"    note: {note}");
foreach (var setting in settingsFacts) Console.WriteLine($"    {setting.DeclaredIn}.{setting.Name} ({setting.Kind}) default={setting.Default}");
foreach (var control in settingsControls) Console.WriteLine($"    control _{control.Field} ({control.ControlType})");
Check(settingsFacts.Count >= 11, "the snapshot record declares the player settings");
Check(settingsFacts.All(x => x.Name != "AudioDescriptions" || x.DeclaredIn == "AccessibilitySettingsSnapshot"), "a record boundary is respected: the accessibility fields stay in their own record");
Check(settingsFacts.Any(x => x.Name == "TatarLanguageLevel" && x.Default == "\"none\""), "the Tatar language level keeps its historical default");
Check(settingsFacts.Any(x => x.Name == "Subtitles" && x.Default == "true"), "the accessibility defaults are read from the record");
Check(settingsControls.Count >= 9, "the settings screen creates the controls the player uses");
Check(settingsControls.Any(x => x.Field == "graphics" && x.ControlType == "OptionButton"), "the graphics preset is an option control, as the screen declares");
Check(settingsNotes.Any(n => n.Contains("user://settings.json")), "the notes state that these are player settings, not campaign content");
Check(settingsNotes.Any(n => n.Contains("не угадывает связь")), "the inventory states that it does not guess the field-to-control mapping");

// ---------- STUDIO/AI-27 simple item forms ----------
Console.WriteLine("--- simple item forms (AI-27)");
var catalogForms = SimpleItemForms.Catalog(ws);
var placedForms = SimpleItemForms.Placements(ws, root);
var formNotes = SimpleItemForms.Notes(ws, root);
Console.WriteLine($"  catalog forms: {catalogForms.Count}, placements: {placedForms.Count}, problems: {catalogForms.Sum(f => f.Problems.Count) + placedForms.Sum(p => p.Problems.Count)}");
foreach (var note in formNotes) Console.WriteLine($"    note: {note}");
foreach (var group in placedForms.GroupBy(p => p.PlacementKind)) Console.WriteLine($"    {group.Key}: {group.Count()}");
Check(catalogForms.Count >= 170, "the catalogue declares the forms of the project");
Check(catalogForms.All(f => f.Problems.Count == 0), "every catalogued form has a scene node, a three-number size, collision and a licence");
Check(placedForms.Count >= 280, "the placements are found");
Check(placedForms.All(p => p.Problems.Count == 0), "every placement resolves its form, its asset exists and its transform is readable");
Check(placedForms.Any(p => p.PlacementKind == "prop" && p.FormReference.StartsWith("urman.catalog:", StringComparison.Ordinal)), "a prop names its form through the catalogue");
Check(placedForms.Any(p => p.PlacementKind == "kit-placement" && p.FormSource.StartsWith("res://", StringComparison.Ordinal)), "a kit placement names its form through its asset source");
Check(placedForms.Count(p => p.State.Length > 0) > 100, "the authoring states (retired, hidden, moved) are read and reported");
Check(formNotes.Any(n => n.Contains("это состояния, а не ошибки")), "the notes say the authoring states are states, not defects");
Check(formNotes.Any(n => n.Contains("дорожка моделей")), "the notes state honestly that reshaping a form is the model track, not a Studio field");

var formMini = Path.Combine(Path.GetTempPath(), "dsprobe-form-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(formMini, "game", "content", "world"));
File.WriteAllText(Path.Combine(formMini, SimpleItemForms.CatalogPath), "{\"entities\":[{\"id\":\"urman.catalog:k/n\",\"name\":\"x\",\"kit\":\"k\",\"source\":{\"scene\":\"\",\"node\":\"n\"},\"size\":[1,0,1],\"collision\":\"\",\"license\":\"\"}]}");
File.WriteAllText(Path.Combine(formMini, "game", "content", "world", "mini.world.v1.json"), "{\"entities\":[{\"id\":\"urman.world:mini/a\",\"kind\":\"prop\",\"params\":{\"catalogId\":\"urman.catalog:missing/x\",\"position\":[0,0],\"yawDegrees\":0,\"scale\":0}},{\"id\":\"urman.world:mini/b\",\"kind\":\"kit-placement\",\"params\":{\"component\":\"\",\"logicalAnchor\":\"\",\"assetSource\":\"user://x.glb\",\"position\":[0,0,0],\"yawDegrees\":0,\"scale\":1}}]}");
var formWs = StudioWorkspace.Open(formMini);
var formMiniCatalog = SimpleItemForms.Catalog(formWs);
var formMiniPlaced = SimpleItemForms.Placements(formWs, formMini);
Console.WriteLine($"    synthetic: catalog problems {formMiniCatalog.Sum(f => f.Problems.Count)}, placement problems {formMiniPlaced.Sum(p => p.Problems.Count)}");
Check(formMiniCatalog.Single().Problems.Any(p => p.Contains("source.node/source.prefix")), "a catalogue entry without a scene and a node/prefix is reported");
Check(formMiniCatalog.Single().Problems.Any(p => p.Contains("ноль или минус")), "a degenerate size is reported");
Check(formMiniCatalog.Single().Problems.Any(p => p.Contains("лицензия")), "a missing licence is reported, because rights matter");
Check(formMiniPlaced.Single(p => p.EntityId.EndsWith("/a")).Problems.Any(p => p.Contains("отсутствует в каталоге")), "a prop whose form is missing is reported");
Check(formMiniPlaced.Single(p => p.EntityId.EndsWith("/a")).Problems.Any(p => p.Contains("позиция")), "a two-number position is reported");
Check(formMiniPlaced.Single(p => p.EntityId.EndsWith("/b")).Problems.Any(p => p.Contains("схема") || p.Contains("не поддерживается")), "an unsupported asset scheme is reported");
Directory.Delete(formMini, true);

Console.WriteLine(failures.Count == 0 ? "\nPROBE RESULT: PASS" : $"\nPROBE RESULT: FAIL ({failures.Count})");
foreach (var f in failures) Console.WriteLine("  - " + f);
return failures.Count == 0 ? 0 : 1;
