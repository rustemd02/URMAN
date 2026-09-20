using Godot;
using Urman.Core.Capabilities.OldPc;

namespace Urman.Godot;

// Contextual hints (03_oldpc_full_system.md §4): authored data with ordinary
// conditions, two carriers - the archive line on an empty search and the
// «Заметки Айдара» thread - and one rule the data cannot express: a level-3
// hint waits until the player has really read at least two archive documents.
// A hint never creates knowledge, never writes a journal entry and never
// quotes a closed record: it only points at a term or a place.
public partial class OldPcUi
{
    public IReadOnlyList<string> SeenHintIds => _desktop.HintsSeen;
    public int FruitlessSearches { get; private set; }

    // Carrier (a): the gentle notes arrive in the system thread when the
    // computer opens. The stronger levels surface where the player is stuck.
    private void RefreshHints()
    {
        if (_bridge is not { } bridge) return;
        foreach (var hint in bridge.OldPcHints.Where(hint => hint.Level is 1))
        {
            if (_desktop.HintsSeen.Contains(hint.Id, StringComparer.Ordinal)) continue;
            if (!HintAvailable(hint)) continue;
            RecordHint(hint);
        }
    }

    private bool HintAvailable(CompiledHintContent hint)
    {
        if (_bridge is not { } bridge) return false;
        if (hint.Level >= 3 && !OldPcDesktopSnapshot.DirectHintAllowed(OpenedDocumentCount(bridge))) return false;
        return bridge.EvaluateConditions(hint.Requires);
    }

    // The mildest unseen hint whose conditions already hold; the archive shows
    // it on an empty search and records it in the same notes thread.
    private CompiledHintContent? NextHint() =>
        _bridge is null
            ? null
            : _bridge.OldPcHints.FirstOrDefault(hint =>
                !_desktop.HintsSeen.Contains(hint.Id, StringComparer.Ordinal) && HintAvailable(hint));

    private string? SurfaceNextHint()
    {
        if (NextHint() is not { } hint) return null;
        RecordHint(hint);
        return _bridge!.ResolveText(hint.TextId);
    }

    private void RecordHint(CompiledHintContent hint)
    {
        _desktop.HintsSeen.Add(hint.Id);
        if (_desktop.HintsSeen.Count > OldPcDesktopSnapshot.MaximumSeenHints)
            _desktop.HintsSeen.RemoveAt(0);
        AppendChatNote("self", _bridge!.ResolveText(hint.TextId));
        MarkDesktopChanged();
    }

    private static int OpenedDocumentCount(RuntimeBridge bridge) =>
        bridge.SelectRuntimeState().GetProperty("presentation").GetProperty("openedDocumentIds").GetArrayLength();
}
