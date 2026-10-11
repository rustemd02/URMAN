using System.Text.RegularExpressions;

namespace Urman.Studio.Core.Editing;

/// <summary>One thing a character change could mean, and what the runtime really offers for it.</summary>
public sealed record CharacterChangeRow(
    string Capability,
    string Kind,
    string Evidence,
    bool DataDriven);

/// <summary>
/// The shared honesty surface behind AI-41 (face sculpting), AI-42 (a new garment), AI-43 (a
/// new character) and AI-28 (the end-to-end character change): what the runtime can actually
/// change today, and what it cannot. The brief forbids replacing a sculpted face with a
/// ready-made one and forbids passing a colour change off as a new garment, so the useful
/// engineering answer is a measured split:
///
///   * DATA: the appearance fields the runtime reads (colours, silhouette, profile);
///   * MODEL/ART: the kit model itself, where a garment or a face would have to be authored;
///   * NONE: a path that does not exist in this revision.
///
/// The negative rows are stated as what was searched and where, because a file scan is
/// evidence about the code that was read, not a proof about the whole engine.
/// </summary>
public static class CharacterChangeSurface
{
    public const string DressingPath = "game/scripts/GeneratedCharacterKitDressing.cs";
    public const string ResidentsPath = "game/scripts/AuthoredWorldDirector.Residents.cs";

    public static IReadOnlyList<CharacterChangeRow> Rows(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        var dressing = Read(full, DressingPath);
        var residents = Read(full, ResidentsPath);
        var fields = CharacterAppearance.ReadFields(root);
        var meshCreations = MeshCreationSites(dressing);
        var kit = File.Exists(Path.Combine(full, "game", "assets", "generated", "urman_character_kit.glb"));
        var kitV2 = File.Exists(Path.Combine(full, "game", "assets", "generated", "urman_character_kit_v2.glb"));

        return
        [
            new("Цвет одежды (пальто, головное, акцент)", "data",
                $"рантайм читает {string.Join(", ", fields.Where(field => field.EndsWith("Color", StringComparison.Ordinal)).Order(StringComparer.Ordinal))}", true),
            new("Силуэт (ширина и глубина)", "data",
                "width и depth, рантайм зажимает их в .94–1.06 и .95–1.05", true),
            new("Описание образа", "data", "profile уходит в метаданные жителя", true),
            new("Рост", "data", "scale", true),
            new("Новая деталь одежды как меш", "model-or-art",
                meshCreations == 0
                    ? $"{DressingPath} не создаёт мешей: искал SetMesh( и new MeshInstance3D — {meshCreations} совпадений, значит готовая деталь может появиться только из набора {KitRef(kit, kitV2)}"
                    : $"{DressingPath}: найдено мест создания меша — {meshCreations}; проверьте их", false),
            new("Скульптированная форма лица", "none",
                residents.Contains("unchanged face and bones", StringComparison.Ordinal)
                    ? "политика самого рантайма: «unchanged face and bones» — лицо и кости остаются как в наборе"
                    : "строка политики не найдена: проверить вручную", false),
            new("Новый скелет, веса и сокеты", "model-or-art",
                $"тела и кости берутся из набора ({KitRef(kit, kitV2)}); в этом срезе путей замены скелета в рантайме не искали", false)
        ];
    }

    public static IReadOnlyList<string> Notes(string root)
    {
        var rows = Rows(root);
        var data = rows.Count(row => row.DataDriven);
        return
        [
            $"Изменяемых из данных возможностей: {data}; требующих модели/арта: {rows.Count(row => row.Kind == "model-or-art")}; отсутствующих путей: {rows.Count(row => row.Kind == "none")}.",
            "Это честная граница, а не отказ: цвет и силуэт действительно меняются данными, а новая деталь одежды и форма лица требуют работы с набором, поэтому их нельзя выдать за готовую возможность редактора.",
            "Отрицательные строки говорят, что именно искали и где: скан файла — это факт о прочитанном коде, а не доказательство отсутствия во всём движке.",
            $"Визуальный и художественный результат остаётся внешней приёмкой (Blender и глаза), здесь его нет."
        ];
    }

    /// <summary>Where the dressing code creates a mesh, if it does.</summary>
    public static int MeshCreationSites(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Regex.Matches(source, "SetMesh\\(|new MeshInstance3D").Count;
    }

    private static string KitRef(bool kit, bool kitV2) => (kit, kitV2) switch
    {
        (true, true) => "urman_character_kit.glb и urman_character_kit_v2.glb",
        (true, false) => "urman_character_kit.glb",
        (false, true) => "urman_character_kit_v2.glb",
        _ => "набор, которого нет на диске"
    };

    private static string Read(string full, string relative)
    {
        var path = Path.Combine(full, relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }
}
