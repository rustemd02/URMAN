using System.Text.Json.Nodes;
using Godot;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

/// <summary>What an import check found; problems block the import, warnings are shown and allowed.</summary>
public sealed record ImportReport(
    string SourcePath, string Kind, string SuggestedName, Vector3 Size, int Meshes, int Materials, int MissingTextures,
    int Bones, IReadOnlyList<string> Problems, IReadOnlyList<string> Warnings, int ExistingUses, string TargetPath)
{
    public bool CanImport => Problems.Count == 0;
}

/// <summary>
/// Import of the author's own files (spec WORLD05, WORLD06): glTF/GLB models,
/// PNG/JPEG images, WAV/OGG sounds. A model is opened in a sandbox (Godot's
/// glTF reader; nothing in the file is executed), measured and checked for
/// scale, axes, materials, missing textures, skeleton and empty geometry;
/// the report is shown before anything is copied. A re-import shows how many
/// placed objects will change. Imported files go to game/assets/imported/.
/// </summary>
public static class StudioImporter
{
    public const string ImportedDirectory = "game/assets/imported";
    private static readonly string[] ModelExtensions = [".glb", ".gltf"];
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg"];
    private static readonly string[] SoundExtensions = [".wav", ".ogg"];
    private const long MaxBytes = 200L * 1024 * 1024;

    public static ImportReport Check(StudioRoot studio, string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var name = Path.GetFileNameWithoutExtension(path);
        var problems = new List<string>();
        var warnings = new List<string>();
        var kind = ModelExtensions.Contains(extension) ? "model" : ImageExtensions.Contains(extension) ? "image" : SoundExtensions.Contains(extension) ? "sound" : "unknown";
        var slug = Slug(name);
        var target = $"{ImportedDirectory}/{kind}s/{slug}{extension}";
        if (kind == "unknown")
        {
            problems.Add($"Формат «{extension}» не поддерживается. Модели — glTF/GLB, изображения — PNG/JPEG, звук — WAV/OGG.");
            return new(path, kind, name, Vector3.Zero, 0, 0, 0, 0, problems, warnings, 0, target);
        }

        var info = new FileInfo(path);
        if (!info.Exists) problems.Add("Файл не найден.");
        else if (info.Length > MaxBytes) problems.Add($"Файл слишком большой: {info.Length / 1048576} МБ (предел 200 МБ).");
        else if (info.Length == 0) problems.Add("Файл пустой.");
        var uses = UsesOf(studio, $"res://{target["game/".Length..]}");
        if (problems.Count > 0) return new(path, kind, name, Vector3.Zero, 0, 0, 0, 0, problems, warnings, uses, target);

        if (kind == "image")
        {
            var image = Image.LoadFromFile(path);
            if (image is null || image.IsEmpty()) problems.Add("Изображение не читается.");
            else if (image.GetWidth() > 4096 || image.GetHeight() > 4096) warnings.Add($"Большое изображение {image.GetWidth()}×{image.GetHeight()}: в игре будет уменьшено.");
            return new(path, kind, name, Vector3.Zero, 0, 0, 0, 0, problems, warnings, uses, target);
        }

        if (kind == "sound")
        {
            var header = File.ReadAllBytes(path).Take(12).ToArray();
            var riff = header.Length >= 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WAVE";
            var ogg = header.Length >= 4 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "OggS";
            if (extension == ".wav" && !riff || extension == ".ogg" && !ogg) problems.Add("Содержимое не похоже на заявленный звуковой формат.");
            return new(path, kind, name, Vector3.Zero, 0, 0, 0, 0, problems, warnings, uses, target);
        }

        var document = new GltfDocument();
        var state = new GltfState();
        var error = document.AppendFromFile(path, state);
        if (error != Error.Ok)
        {
            problems.Add($"Модель не читается ({error}). Возможно, файл повреждён или ссылается на отсутствующие части.");
            return new(path, kind, name, Vector3.Zero, 0, 0, 0, 0, problems, warnings, uses, target);
        }

        var scene = document.GenerateScene(state) as Node3D;
        if (scene is null)
        {
            problems.Add("В файле нет сцены с объектами.");
            return new(path, kind, name, Vector3.Zero, 0, 0, 0, 0, problems, warnings, uses, target);
        }

        var meshes = scene.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToArray();
        var materials = meshes.SelectMany(mesh => Enumerable.Range(0, mesh.Mesh?.GetSurfaceCount() ?? 0).Select(surface => mesh.Mesh!.SurfaceGetMaterial(surface))).Where(material => material is not null).Distinct().ToArray();
        var missingTextures = 0;
        if (extension == ".gltf" && JsonNode.Parse(File.ReadAllText(path))?["images"] is JsonArray images)
        {
            foreach (var image in images.OfType<JsonObject>())
            {
                if (image["uri"] is JsonValue uri && !((string)uri!).StartsWith("data:", StringComparison.Ordinal)
                    && !File.Exists(Path.Combine(Path.GetDirectoryName(path)!, Uri.UnescapeDataString((string)uri!))))
                {
                    missingTextures++;
                }
            }
        }

        var skeleton = scene.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().FirstOrDefault();
        Aabb? bounds = null;
        foreach (var mesh in meshes)
        {
            if (mesh.Mesh is null) continue;
            var box = mesh.GlobalTransform * mesh.Mesh.GetAabb();
            bounds = bounds is { } total ? total.Merge(box) : box;
        }

        var size = bounds?.Size ?? Vector3.Zero;
        if (meshes.Length == 0) problems.Add("В модели нет видимой геометрии.");
        if (missingTextures > 0) problems.Add($"Не найдено текстур: {missingTextures}. Положите их рядом с файлом .gltf или используйте .glb.");
        if (materials.Length == 0 && meshes.Length > 0) warnings.Add("У модели нет материалов: она будет серой.");
        if (size.Length() > 200) warnings.Add($"Очень крупная модель ({size.X:0.#}×{size.Y:0.#}×{size.Z:0.#} м). Проверьте единицы: в URMAN 1 единица = 1 метр.");
        if (size.Length() is > 0 and < .02f) warnings.Add("Очень маленькая модель: возможно, она сохранена в сантиметрах или миллиметрах.");
        if (size.Y > 0 && size.Z > size.Y * 8 && size.X > size.Y * 8 && size.Y < .3f) warnings.Add("Модель почти плоская — это нормально для покрытия, но проверьте оси, если это стоящий предмет.");
        if (size.Z > 0 && size.Y > size.Z * 6 && size.Y > size.X * 6) warnings.Add("Модель вытянута по высоте: если это лежащий предмет, возможно, перепутаны оси (Y вверх в glTF).");
        if (skeleton is not null && skeleton.GetBoneCount() == 0) problems.Add("Скелет пустой.");
        if (File.Exists(Path.Combine(studio.Workspace.Root, target)))
        {
            warnings.Add($"Файл с таким именем уже есть и будет заменён; экземпляров в мире изменится: {uses}.");
        }
        scene.Free();
        return new(path, kind, name, size, meshes.Length, materials.Length, missingTextures, skeleton?.GetBoneCount() ?? 0, problems, warnings, uses, target);
    }

    /// <summary>Copy the checked file into the project and, for a model, add its catalogue entry (one undo step for the entry).</summary>
    public static string? Apply(StudioRoot studio, ImportReport report, string name, string category)
    {
        if (!report.CanImport) return null;
        var target = Path.Combine(studio.Workspace.Root, report.TargetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        AtomicFile.WriteAllBytes(target, File.ReadAllBytes(report.SourcePath));
        if (report.Kind != "model") return report.TargetPath;

        const string catalogPath = "game/content/world/catalog.v1.json";
        var id = $"urman.catalog:imported/{Slug(Path.GetFileNameWithoutExtension(report.TargetPath))}";
        var entry = new JsonObject
        {
            ["id"] = id,
            ["name"] = name,
            ["category"] = category,
            ["source"] = new JsonObject { ["scene"] = "res://" + report.TargetPath["game/".Length..], ["whole"] = true },
            ["size"] = new JsonArray(Math.Round(report.Size.X, 2), Math.Round(report.Size.Y, 2), Math.Round(report.Size.Z, 2)),
            ["origin"] = new JsonArray(0.0, 0.0, 0.0),
            ["collision"] = "box",
            ["kit"] = "импорт",
            ["license"] = "импортировано автором; источник и лицензию укажите в свойствах",
            ["authored"] = new JsonArray("name", "category")
        };
        studio.Session.Set(catalogPath, id, entry, $"импорт «{name}»");
        return id;
    }

    private static int UsesOf(StudioRoot studio, string resourcePath) =>
        studio.Workspace.EntityIds
            .Where(id => id.StartsWith("urman.catalog:", StringComparison.Ordinal) && (string?)studio.Workspace.Get(id)?["source"]?["scene"] == resourcePath)
            .Sum(catalogId => studio.Workspace.EntityIds.Count(entity => (string?)studio.Workspace.Get(entity)?["params"]?["catalogId"] == catalogId));

    private static string Slug(string text)
    {
        var slug = System.Text.RegularExpressions.Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length == 0 ? "import-" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(3)) : slug;
    }
}
