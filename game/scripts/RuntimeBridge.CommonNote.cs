using System.Text.Json;
using Urman.Core.Contracts;
using Urman.Core.Narrative;
using Urman.Core.Runtime;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    private const string CommonNoteCommand = "photoworlds.common-note.confirm";
    private const string CommonNoteInteraction = "urman.fullgame:interaction/pw-common-note";
    private const string CommonNoteScene = "urman.fullgame:scene/pw-common";
    private const string CommonNoteProp = "photoworlds/W04/final-note";
    private const string CommonNoteFact = "common_note_finished";

    internal enum CommonNoteSaveResult { Rejected, Saved, CommittedNotSaved, Stale }

    internal static bool TryNormalizeCommonNote(string text, out string normalized)
    {
        normalized = text.Trim();
        return normalized.Length is >= 1 and <= 120 && !normalized.Any(char.IsControl);
    }

    internal string CommonFrameNoteText() => SessionIdentity is { } session
        ? ReadCommonFrameNote(session.SelectState()) : string.Empty;

    private static string ReadCommonFrameNote(JsonElement state) =>
        CommonNoteConfirmed(state)
        && state.TryGetProperty(WorldPropsStateKey, out var props) && props.ValueKind == JsonValueKind.Object
        && props.TryGetProperty(CommonNoteProp, out var note)
        && note.ValueKind == JsonValueKind.Object
        && note.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
        && TryNormalizeCommonNote(text.GetString()!, out var normalized)
        ? normalized : string.Empty;

    private static bool CommonNoteConfirmed(JsonElement state) => ContentRuleEngine.Evaluate(
        JsonSerializer.SerializeToElement(new { op = "photoworlds.fact.observed", factId = CommonNoteFact }), state);

    internal string PreparedCommonCaptionText() => SessionIdentity is { } session
        && session.SelectState().GetProperty("photoworlds").GetProperty("captions")
            .TryGetProperty("P-COMMON", out var caption)
        && caption.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
        ? text.GetString() ?? string.Empty : string.Empty;

    internal bool TryGetCommonNoteVisit(out RuntimeKernel session, out string? visitId)
    {
        session = SessionIdentity!;
        visitId = null;
        return session is not null && !_loadPreparing && _photoVisitCompletion is null
            && ActiveSceneId == CommonNoteScene
            && TryReadCommonNoteVisit(session.SelectState(), out visitId);
    }

    internal bool IsCommonNoteVisitCurrent(RuntimeKernel session, string? visitId) =>
        TryGetCommonNoteVisit(out var current, out var currentVisit)
        && ReferenceEquals(session, current) && StringComparer.Ordinal.Equals(visitId, currentVisit);

    private static bool TryReadCommonNoteVisit(JsonElement state, out string? visitId)
    {
        visitId = null;
        if (state.GetProperty("activeScene").GetString() != CommonNoteScene) return false;
        var active = state.GetProperty("photoworlds").GetProperty("visits").GetProperty("active");
        // Older supported checkpoints have no visit ticket; do not invent one.
        if (active.ValueKind == JsonValueKind.Null) return true;
        if (active.ValueKind != JsonValueKind.Object
            || active.GetProperty("worldId").GetString() != "W04"
            || active.GetProperty("status").GetString() != "active") return false;
        visitId = active.GetProperty("visitId").GetString();
        return !string.IsNullOrEmpty(visitId);
    }

    private void RegisterCommonNoteHandler(Dictionary<string, RuntimeCommandHandler> handlers)
    {
        var observe = handlers[PhotoWorldCommandHandler.ContextObserve];
        handlers.Add(CommonNoteCommand, (command, context) => PlanCommonNote(command, context, observe));
    }

    private CommandPlan PlanCommonNote(GameCommand command, RuntimeCommandContext context, RuntimeCommandHandler observe)
    {
        var entered = command.Payload.GetProperty("text").GetString() ?? string.Empty;
        if (!TryNormalizeCommonNote(entered, out var text)
            || _loadPreparing || _loadingSlot || NeedsPhysicalRecovery
            || !TryReadCommonNoteVisit(context.State, out var currentVisit)
            || !StringComparer.Ordinal.Equals(command.Payload.GetProperty("visitId").GetString(), currentVisit)
            || !_content.TryGetInteraction(CommonNoteInteraction, out var interaction)
            || !OwnsInteraction(interaction, context.State))
            return new(Rejection: new("CommonNoteRejected", "Помету можно записать только в текущем общем кадре."));
        if (CommonNoteConfirmed(context.State))
            return new(Rejection: new("CommonNoteAlreadyConfirmed", "Помета уже записана."));

        var conditions = Elements(interaction.Conditions).Append(JsonSerializer.SerializeToElement(new
            { op = "location.is", locationId = CommonNoteScene }));
        var effects = Elements(_content.RequireScene(CommonNoteScene).OnExit).Concat(Elements(interaction.Effects));
        var envelope = JsonSerializer.SerializeToElement(new
        {
            namespaceId = PhotoWorldState.NamespaceId,
            campaignId = _content.CampaignId,
            campaignExactVersion = _content.CampaignExactVersion,
            conditions, effects
        });
        var plan = observe(command with { Type = PhotoWorldCommandHandler.ContextObserve, Payload = envelope }, context);
        if (plan.Rejection is not null || plan.Effects is null) return plan;
        var placement = JsonSerializer.SerializeToElement(new[] { new
        {
            propId = CommonNoteProp, text, photoId = "P-COMMON", sourceId = CommonNoteInteraction,
            preparedById = _content.PhotoWorldCatalog.GetProperty("playerId").GetString()
        } });
        return plan with { Effects = plan.Effects.Append(new StateEffect(StateEffectOperation.Set,
            WorldPropsStateKey, WritePlacement(context.State, placement))).ToArray() };
    }

    internal async Task<CommonNoteSaveResult> ConfirmCommonFrameNoteAsync(
        RuntimeKernel session, string? visitId, string entered)
    {
        if (!TryNormalizeCommonNote(entered, out var text)) return CommonNoteSaveResult.Rejected;
        if (!IsCommonNoteVisitCurrent(session, visitId)) return CommonNoteSaveResult.Stale;
        var committedText = CommonFrameNoteText();
        if (PhotoWorldFactObserved(CommonNoteFact))
        {
            if (!StringComparer.Ordinal.Equals(committedText, text)) return CommonNoteSaveResult.Rejected;
        }
        else
        {
            var result = await session.DispatchAsync(new GameCommand(
                $"{CommonNoteCommand}:{Interlocked.Increment(ref _interactionSequence):D8}", CommonNoteCommand,
                JsonSerializer.SerializeToElement(new { text, visitId })));
            if (!IsCommonNoteVisitCurrent(session, visitId)) return CommonNoteSaveResult.Stale;
            if (result.Status != CommandStatus.Committed) return CommonNoteSaveResult.Rejected;
        }
        await ReconcileQuestsAsync(session);
        if (!IsCommonNoteVisitCurrent(session, visitId)) return CommonNoteSaveResult.Stale;
        QueueRuntimeStateChanged();
        var saved = await SaveCheckpointAsync(force: true);
        if (!IsCommonNoteVisitCurrent(session, visitId)) return CommonNoteSaveResult.Stale;
        return saved ? CommonNoteSaveResult.Saved : CommonNoteSaveResult.CommittedNotSaved;
    }
}
