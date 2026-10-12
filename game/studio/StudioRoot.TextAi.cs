using System.Text.Json.Nodes;
using Godot;
using Urman.Studio.Core.Editing;

namespace Urman.Studio.App;

public partial class StudioRoot
{
    private StudioTextAiPanel? _textAiPanel;

    internal bool IsTextAiBusy => _textAiPanel is { } panel
        && GodotObject.IsInstanceValid(panel)
        && (panel.IsActionBusy || panel.HasActiveOperation);

    public bool CanOpenTextAi(string? textId)
    {
        if (_closing || string.IsNullOrWhiteSpace(textId) || Workspace is null
            || Workspace.Locate(textId) is not { } address
            || !StudioWorkspace.IsCanonicalAuthoredJsonSourcePath(address.RelativePath)
            || Workspace.Get(textId) is not JsonObject entity
            || EntityCatalog.KindOf(textId, entity) != "text")
            return false;

        return StudioTextAiPanel.HasSupportedRussianText(entity);
    }

    public void OpenTextAi(string textId)
    {
        if (!CanOpenTextAi(textId) || IsWorkspaceActionBusy || IsTextAiBusy) return;
        if (_textAiPanel is null || !GodotObject.IsInstanceValid(_textAiPanel))
            _textAiPanel = new StudioTextAiPanel(this);
        if (_textAiPanel.GetParent() is null) AddChild(_textAiPanel);
        _textAiPanel.Open(textId);
    }
}
