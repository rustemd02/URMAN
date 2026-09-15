using Godot;

namespace Urman.Godot;

/// <summary>The actual carried tool and its authored, individually aimed uses.</summary>
public partial class YardTool : Node3D
{
    private readonly List<YardUseTarget> _uses = new();
    public string ToolId { get; private set; } = string.Empty;
    public string ToolName { get; private set; } = string.Empty;
    public CarryableProp Prop { get; private set; } = null!;
    public IReadOnlyList<YardUseTarget> Targets => _uses;
    public IReadOnlyList<string> UseIds => _uses.Select(use => use.UseId).ToArray();

    public static YardTool Create(string toolId, string toolName, Vector3 position, float yawDegrees, bool shovel)
    {
        var tool = new YardTool { Name = $"YardTool_{toolId}", ToolId = toolId, ToolName = toolName };
        tool.Prop = CarryableProp.Create($"carry-tool-{toolId}", toolName,
            shovel ? CarryableProp.ItemClass.Medium : CarryableProp.ItemClass.Bulky,
            position, yawDegrees, "8a6b50", "wood",
            shovel ? CarryableProp.ItemKind.Shovel : CarryableProp.ItemKind.Pole);
        tool.Prop.ToolId = toolId;
        tool.AddChild(tool.Prop);
        return tool;
    }

    public void AddUse(string useId, string prompt, Node3D effect, Action<bool>? apply = null)
    {
        var target = YardUseTarget.Create(this, useId, prompt, effect, apply);
        _uses.Add(target);
        AddChild(target);
    }

    public void RestoreResults(System.Text.Json.JsonElement props)
    {
        foreach (var target in _uses)
        {
            var visible = props.TryGetProperty(target.StateKey, out var record)
                && record.TryGetProperty("visible", out var value) && value.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False
                    ? value.GetBoolean() : target.InitialVisible;
            target.ApplyResult(visible);
        }
    }
}
