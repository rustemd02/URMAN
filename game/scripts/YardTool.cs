using Godot;

namespace Urman.Godot;

/// <summary>
/// EX03: a yard tool the player can use, with two distinct uses per tool. The
/// uses cycle on the existing interact input; each use produces a visible change
/// in the yard (a snow pile cleared, a vessel filled, a bed opened). No new
/// mechanic and no narrative interaction - the tool is presentation-only, and
/// the same focus/input path as the carry system drives it.
/// </summary>
public partial class YardTool : Node3D
{
    private readonly List<(string Prompt, Node3D Effect)> _uses = new();
    private int _useIndex = -1;

    public string ToolId { get; private set; } = string.Empty;
    public string ToolName { get; private set; } = string.Empty;
    public float Reach { get; private set; } = 2.2f;

    public static YardTool Create(string toolId, string toolName, Vector3 position, float yawDegrees,
        bool shovel)
    {
        var tool = new YardTool
        {
            ToolId = toolId,
            ToolName = toolName,
            Position = position,
            RotationDegrees = new Vector3(0, yawDegrees, 0)
        };
        tool.Name = $"YardTool_{toolId}";
        if (shovel)
        {
            // Shaft and blade, leaned against the wall.
            tool.AddChild(new MeshInstance3D
            {
                Name = "Shaft",
                Position = new(0, 0.55f, 0),
                RotationDegrees = new(0, 0, -14),
                Mesh = new BoxMesh { Size = new(0.05f, 1.1f, 0.05f) },
                MaterialOverride = PainterlyMaterialLibrary.ForColor("8a6b50", "wood")
            });
            tool.AddChild(new MeshInstance3D
            {
                Name = "Blade",
                Position = new(0.13f, 0.03f, 0),
                RotationDegrees = new(0, 0, -14),
                Mesh = new BoxMesh { Size = new(0.22f, 0.26f, 0.03f) },
                MaterialOverride = PainterlyMaterialLibrary.ForColor("6f6d61", "metal")
            });
        }
        else
        {
            tool.AddChild(new MeshInstance3D
            {
                Name = "Pail",
                Position = new(0, 0.14f, 0),
                Mesh = new CylinderMesh { TopRadius = 0.13f, BottomRadius = 0.11f, Height = 0.28f },
                MaterialOverride = PainterlyMaterialLibrary.ForColor("6f6d61", "metal")
            });
            tool.AddChild(new MeshInstance3D
            {
                Name = "Handle",
                Position = new(0, 0.3f, 0),
                Mesh = new BoxMesh { Size = new(0.26f, 0.025f, 0.025f) },
                MaterialOverride = PainterlyMaterialLibrary.ForColor("4c4c48", "metal")
            });
        }

        tool.SetMeta("presentationOnly", true);
        tool.SetMeta("yardToolId", toolId);
        return tool;
    }

    /// <summary>
    /// Registers one use. The effect node's visibility toggles when the use
    /// cycles, so the result is a visible change in the yard rather than text.
    /// </summary>
    public void AddUse(string prompt, Node3D effect)
    {
        _uses.Add((prompt, effect));
    }

    public bool HasUses => _uses.Count > 0;

    public string NextPrompt() =>
        _uses.Count == 0 ? $"Использовать: {ToolName}" : _uses[(_useIndex + 1) % _uses.Count].Prompt;

    public string Use()
    {
        if (_uses.Count == 0)
        {
            return string.Empty;
        }

        _useIndex = (_useIndex + 1) % _uses.Count;
        var (prompt, effect) = _uses[_useIndex];
        effect.Visible = !effect.Visible;
        SetMeta("lastUse", prompt);
        return prompt;
    }
}
