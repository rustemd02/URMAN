using Godot;

namespace Urman.Godot;

/// <summary>
/// EX03/EX05: a yard tool the player can use, with two distinct uses per tool.
/// The uses cycle on the existing interact input; each use produces a visible
/// change in the yard (a snow pile cleared, a vessel filled, a bed opened). No
/// new mechanic and no narrative interaction - the tool is presentation-only,
/// and the same focus/input path as the carry system drives it. Every use has a
/// stable id so its result can be written to the world state and restored after
/// a load (EX05.4: a cleared step stays cleared while the scenario needs it,
/// while the cosmetic snow dust still resets by its own session-only contract).
/// </summary>
public partial class YardTool : Node3D
{
    private readonly List<(string UseId, string Prompt, Node3D Effect)> _uses = new();
    private int _useIndex = -1;

    public string ToolId { get; private set; } = string.Empty;
    /// <summary>Id of the use performed by the last <see cref="Use"/> call.</summary>
    public string LastUseId { get; private set; } = string.Empty;
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
    public void AddUse(string useId, string prompt, Node3D effect)
    {
        _uses.Add((useId, prompt, effect));
    }

    public bool HasUses => _uses.Count > 0;

    /// <summary>Stable ids of every registered use, in registration order.</summary>
    public IReadOnlyList<string> UseIds => _uses.Select(use => use.UseId).ToArray();

    /// <summary>
    /// The result recorded in the world state for one use, or null when the use
    /// has never been performed in this save.
    /// </summary>
    public bool? ResultOf(string useId)
    {
        var use = _uses.FirstOrDefault(candidate => candidate.UseId == useId);
        return use.Effect is null ? null : use.Effect.Visible;
    }

    /// <summary>
    /// Re-applies a persisted result without going through the use cycle, so a
    /// loaded session shows the same yard the player left.
    /// </summary>
    public void ApplyResult(string useId, bool visible)
    {
        var use = _uses.FirstOrDefault(candidate => candidate.UseId == useId);
        if (use.Effect is not null)
        {
            use.Effect.Visible = visible;
        }
    }

    public string NextPrompt() =>
        _uses.Count == 0 ? $"Использовать: {ToolName}" : _uses[(_useIndex + 1) % _uses.Count].Prompt;

    public string Use()
    {
        if (_uses.Count == 0)
        {
            return string.Empty;
        }

        _useIndex = (_useIndex + 1) % _uses.Count;
        var (useId, prompt, effect) = _uses[_useIndex];
        effect.Visible = !effect.Visible;
        SetMeta("lastUse", prompt);
        LastUseId = useId;
        return prompt;
    }
}
