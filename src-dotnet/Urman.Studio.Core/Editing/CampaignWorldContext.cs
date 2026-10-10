namespace Urman.Studio.Core.Editing;

/// <summary>
/// How far the authored data of one world actually goes. Spec AI-13/AI-34 require
/// a scene without authoring capability to be named instead of being shown as
/// editable, so the value travels with every authoring context.
/// </summary>
public enum WorldAuthoringCapability
{
    /// <summary>Authored world plots for this world exist in the checkout and Studio can edit them.</summary>
    AuthoredWorldPlots,

    /// <summary>The runtime scene exists, but its world data is produced by code rather than authored plots.</summary>
    PresentationOnly,

    /// <summary>Only a specification exists; there is no scene an author could open.</summary>
    SpecifiedNotImplemented
}

/// <summary>
/// One real campaign manifest: <c>content/campaigns/&lt;selection&gt;/campaign.json</c>.
/// <see cref="Selection"/> is the folder an author compiles, <see cref="Id"/> is
/// the id inside the file; they differ for the legacy examples on purpose, so
/// both are kept.
/// </summary>
public sealed record CampaignRef(
    string Selection,
    string Id,
    string ExactVersion,
    string Entrypoint,
    string RelativePath,
    IReadOnlyList<string> ModuleIds,
    string CompiledPackPath,
    bool CompiledPackExists,
    bool IsArchive,
    string ArchiveReason)
{
    /// <summary>Archive examples never start by default (spec AI-13 done_when).</summary>
    public bool SelectableByDefault => !IsArchive;
}

/// <summary>
/// One world an author can open: a real entry scene of the game plus the zone it
/// starts in. <see cref="ZoneId"/> and <see cref="SpawnPointId"/> stay empty when
/// the scene does not declare them and the value is owned by code.
/// </summary>
public sealed record WorldRef(
    string CampaignSelection,
    string WorldId,
    string Title,
    string ScenePath,
    string ZoneId,
    string SpawnPointId,
    string? DeclaredCampaignResource,
    WorldAuthoringCapability Capability,
    IReadOnlyList<string> CapabilityEvidence)
{
    /// <summary>Taken from the scene declaration, so it is empty for code-bound entry zones.</summary>
    public bool ZoneDeclaredByScene => !string.IsNullOrEmpty(ZoneId);
}

/// <summary>
/// The campaign and world Studio is authoring right now (spec AI-13 contract
/// <c>CampaignWorldContext</c>). Every field is read from the checkout's real
/// sources and the same value can travel into a request and its receipt, so one
/// saved authoring change stays tied to one campaign and one world.
/// </summary>
public sealed record CampaignWorldContext(
    CampaignRef Campaign,
    WorldRef World,
    string SourceRevision,
    string PackPath,
    string CampaignBinding = CampaignWorldContext.CampaignBindingExplicit)
{
    /// <summary>The entry scene itself declared this campaign (real link in the files).</summary>
    public const string CampaignBindingDeclared = "declared-by-scene";

    /// <summary>The author paired this campaign with a scene that does not declare one; the pair must be recorded.</summary>
    public const string CampaignBindingExplicit = "explicit-selection";

    /// <summary>
    /// Stable id of this campaign+version+world combination. Per-world authoring
    /// state is keyed by it, so switching worlds never mixes the two.
    /// </summary>
    public string StateId => $"{Campaign.Id}@{Campaign.ExactVersion}/{World.WorldId}";

    /// <summary>Short author-facing label for banners and run logs.</summary>
    public string Describe() => $"{Campaign.Id} · {World.Title}";
}
