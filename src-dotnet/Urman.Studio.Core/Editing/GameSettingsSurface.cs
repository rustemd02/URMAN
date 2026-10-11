using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>One player-visible setting as the code declares it.</summary>
public sealed record GameSetting(string Name, string Kind, string Default, string DeclaredIn, string SourcePath, int Line);

/// <summary>One control the game settings screen really exposes.</summary>
public sealed record GameSettingControl(string Field, string ControlType, int Line);

/// <summary>
/// The first slice of the spec AI-26 slice: the settings surface of the game, taken from
/// the code that really owns it. The snapshot record declares the shape of what is
/// persisted, and the settings screen declares the controls the player can change; this
/// inventory reports both without guessing a mapping between them.
///
/// The important boundary for the editor is that these are PLAYER settings: they live in
/// user://settings.json, never in the authored campaign, so a Studio preview may render
/// this surface but must not write it.
/// </summary>
public static class GameSettingsSurface
{
    public const string SnapshotPath = "src-dotnet/Urman.Core/Persistence/SaveGameV3.cs";

    public const string ScreenPath = "game/scripts/SettingsUi.cs";

    private static readonly string[] Records =
    [
        "GameSettingsSnapshot", "AccessibilitySettingsSnapshot"
    ];

    private static readonly Regex Setting = new(
        "^\\s*(?:public\\s+)?(bool|double|string|float|int|IReadOnlyList<[^>]+>)\\s+(\\w+)\\s*(?:=\\s*([^,;)]+))?",
        RegexOptions.CultureInvariant);

    private static readonly Regex InitProperty = new(
        "^\\s*(?:public\\s+)?(bool|double|string|float|int)\\s+(\\w+)\\s*\\{\\s*get;\\s*init;\\s*\\}\\s*(?:=\\s*([^;]+))?;",
        RegexOptions.CultureInvariant);

    private static readonly Regex Control = new(
        "^\\s*private\\s+(HSlider|CheckBox|OptionButton|LineEdit|SpinBox)\\s+_(\\w+)\\s*[;=]",
        RegexOptions.CultureInvariant);

    /// <summary>The player settings the snapshot record declares, with their defaults.</summary>
    public static IReadOnlyList<GameSetting> Settings(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var path = Path.Combine(Path.GetFullPath(root), SnapshotPath.Replace('/', Path.DirectorySeparatorChar));
        var settings = new List<GameSetting>();
        if (!File.Exists(path))
        {
            return settings;
        }

        var lines = File.ReadAllLines(path);
        for (var index = 0; index < lines.Length; index++)
        {
            var declaration = lines[index];
            var record = Records.FirstOrDefault(name => declaration.Contains("record " + name, StringComparison.Ordinal));
            if (record is null)
            {
                continue;
            }

            // A record runs from its parameter list into its body: the list ends with a
            // line containing the closing parenthesis, and the body ends with the brace
            // that closes the record. Both a parameter with a trailing parenthesis and an
            // init-only property in the body are captured.
            var inParameters = true;
            for (var inner = index + 1; inner < lines.Length; inner++)
            {
                var candidate = lines[inner];
                var trimmed = candidate.TrimStart();
                if (trimmed.StartsWith('{'))
                {
                    inParameters = false;
                    continue;
                }

                if (!inParameters && trimmed.StartsWith('}'))
                {
                    break;
                }

                if (inParameters && candidate.Contains(')'))
                {
                    // The last parameter and the closing parenthesis share a line.
                    Capture(settings, candidate, record, inner + 1);
                    inParameters = false;
                    continue;
                }

                if (!inParameters && trimmed.StartsWith("public") && trimmed.Contains("record ", StringComparison.Ordinal))
                {
                    break;
                }

                // An init-only property carries its own default outside the parameter list,
                // which is how TatarLanguageLevel keeps the historical "none".
                var property = InitProperty.Match(candidate);
                if (property.Success)
                {
                    settings.Add(new GameSetting(
                        property.Groups[2].Value,
                        property.Groups[1].Value,
                        property.Groups[3].Success ? property.Groups[3].Value.Trim() : "значение по умолчанию типа",
                        record,
                        SnapshotPath,
                        inner + 1));
                    continue;
                }

                Capture(settings, candidate, record, inner + 1);
            }
        }

        return settings;
    }

    /// <summary>Adds one scalar setting when the line declares one.</summary>
    private static void Capture(List<GameSetting> settings, string line, string record, int lineNumber)
    {
        var match = Setting.Match(line);
        if (match.Success)
        {
            settings.Add(new GameSetting(
                match.Groups[2].Value,
                match.Groups[1].Value,
                match.Groups[3].Success ? match.Groups[3].Value.Trim() : "обязательное поле записи",
                record,
                SnapshotPath,
                lineNumber));
        }
    }

    /// <summary>The controls the settings screen actually creates.</summary>
    public static IReadOnlyList<GameSettingControl> Controls(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var path = Path.Combine(Path.GetFullPath(root), ScreenPath.Replace('/', Path.DirectorySeparatorChar));
        var controls = new List<GameSettingControl>();
        if (!File.Exists(path))
        {
            return controls;
        }

        var lines = File.ReadAllLines(path);
        for (var index = 0; index < lines.Length; index++)
        {
            var match = Control.Match(lines[index]);
            if (match.Success)
            {
                controls.Add(new GameSettingControl(match.Groups[2].Value, match.Groups[1].Value, index + 1));
            }
        }

        return controls;
    }

    public static IReadOnlyList<string> Notes(string root)
    {
        var settings = Settings(root);
        var controls = Controls(root);
        var accessibility = settings.Count(setting => setting.DeclaredIn == "AccessibilitySettingsSnapshot");
        return
        [
            $"Настроек в записи: {settings.Count} (из них доступность: {accessibility}); элементов управления на экране настроек: {controls.Count}.",
            "Это НАСТРОЙКИ ИГРОКА: они живут в user://settings.json и никогда в авторской кампании, поэтому превью в Studio может показывать этот набор, но не имеет права его записывать и не должно подменять его своим форматом.",
            "Соответствие между полем записи и элементом управления задаёт сам экран: инвентарь намеренно не угадывает связь по имени (например _fov и FieldOfView текстуально не совпадают), а показывает оба списка как есть.",
            "TatarLanguageLevel сохраняет историческое поведение: отсутствие поля в старых файлах означает «none»."
        ];
    }
}
