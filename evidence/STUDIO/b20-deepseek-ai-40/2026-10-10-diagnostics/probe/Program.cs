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
Check(TriggerScopeFacts.Describe("x", shortSize).Problems.Any(p => p.Contains("трёх конечных чисел")), "a size that is not three finite numbers is reported");
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
    var node = new JsonObject { ["id"] = id, ["from"] = "npc", ["textId"] = textId };
    if (next is not null) node["nextNodeId"] = next;
    if (choiceText is not null)
    {
        var choice = new JsonObject { ["id"] = "c", ["textId"] = choiceText };
        if (choiceNext is not null) choice["nextNodeId"] = choiceNext;
        node["choices"] = new JsonArray(choice);
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
var miniIndex = AssetUsageIndex.Open(assetMini);
var miniProblems = miniIndex.Validate();
Check(miniProblems.Any(p => p.Contains("которой нет")), "a placement pointing at a missing catalogue entry is reported");
Check(miniProblems.Any(p => p.Contains("реестре ресурсов")), "a catalogue source with no registry record is reported");
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

Console.WriteLine(failures.Count == 0 ? "\nPROBE RESULT: PASS" : $"\nPROBE RESULT: FAIL ({failures.Count})");
foreach (var f in failures) Console.WriteLine("  - " + f);
return failures.Count == 0 ? 0 : 1;
