using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Handoff;

namespace Urman.Studio.Core.Storage;

public sealed record StudioCheckpointMetadata(string Id, string Name, string Author, DateTimeOffset CreatedAt,
    string Revision, int FileCount, int ResourceCount, string ResourceCapture);

public sealed record StudioCheckpointRestorePlan(StudioCheckpointMetadata Metadata,
    IReadOnlyList<StudioFileWrite> Writes, IReadOnlySet<string> AllowedPaths, IReadOnlyList<string> ChangedPaths,
    IReadOnlyDictionary<string, string> WorkspaceHashes, IReadOnlyDictionary<string, string?> ExpectedDiskHashes,
    IReadOnlyDictionary<string, string> CurrentTexts);

public sealed record StudioCheckpointCatalogImage(string DiskSha256, byte[]? DraftBytes);
public sealed record StudioCheckpointCatalogSource(string Owner, string Path, Func<StudioCheckpointCatalogImage> Capture,
    Action<byte[]> Validate);

/// <summary>Named authored-text checkpoints. Resources are receipts, not copied assets or a typed closure.</summary>
public sealed class StudioCheckpointStore(StudioWorkspace workspace,
    Func<IReadOnlyList<StudioCheckpointCatalogSource>>? catalogSources = null,
    Func<string>? revision = null)
{
    private const string DirectoryPath = ".urman-studio/checkpoints";
    private const string ResourceCapture = "literal-res-paths-record-only";
    private const string HeroRecipePath = "game/content/studio/hero_house.recipe.json";
    private static readonly string[] BasisPaths = ["assets/source/blender/act1/urman_village_exterior_kit.py",
        "assets/source/blender/act1/urman_village_exterior_kit.blend", "game/assets/models/act1/urman_village_exterior_kit.glb"];
    private const int MaxFiles = 2000;
    private const int MaxBytes = 8 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly string _root = workspace.Root;

    public StudioCheckpointMetadata Create(string name, string author)
    {
        ValidateLabel(name);
        ValidateLabel(author);
        if (workspace.IdentityConflicts.Count != 0) throw new InvalidOperationException("Исправьте повторные ID перед созданием checkpoint.");
        var ownedSources = CurrentCatalogSources();
        var ownedByPath = ownedSources.ToDictionary(source => source.Path, StringComparer.Ordinal);
        var catalogImages = CaptureCatalogImages(ownedSources);
        var paths = CurrentSourcePaths(ownedSources);
        if (paths.Length is < 1 or > MaxFiles) throw new InvalidOperationException("Checkpoint допускает от 1 до 2000 исходных файлов.");
        var images = new List<byte[]>();
        var sources = new List<SourceReceipt>();
        var resourcePaths = BasisPaths.ToHashSet(StringComparer.Ordinal);
        long total = 0;
        foreach (var path in paths)
        {
            ValidateSourcePath(path);
            Guard(path);
            var bytes = CurrentSourceBytes(path, catalogImages, includeDrafts: true);
            var text = Utf8.GetString(bytes);
            if (ownedByPath.TryGetValue(path, out var ownedSource)) ownedSource.Validate(bytes);
            total += bytes.LongLength;
            if (total > MaxBytes) throw BudgetFailure();
            var parsed = AuthoredFile.CreateNew(_root, path, text);
            foreach (var value in Strings(parsed.Snapshot()))
                if (value.StartsWith("res://", StringComparison.Ordinal)) resourcePaths.Add(ResourcePath(value));
            var image = $"source-{sources.Count:D4}.txt";
            sources.Add(new(path, image, bytes.Length, AtomicFile.Sha256(bytes)));
            images.Add(bytes);
        }
        if (resourcePaths.Count > MaxFiles) throw new InvalidOperationException("Слишком много ссылок на ресурсы для checkpoint.");
        var resources = resourcePaths.Order(StringComparer.Ordinal).Select(path =>
        {
            Guard(path);
            var full = Path.Combine(_root, path);
            if (BasisPaths.Contains(path, StringComparer.Ordinal) && !File.Exists(full))
                throw new InvalidOperationException("Основа генерации дома отсутствует; checkpoint не создан.");
            return new ResourceReceipt(path, File.Exists(full) ? DiskHash(path) : null);
        }).ToArray();
        var snapshotRevision = revision?.Invoke() ?? DeveloperContext.ResolveRevision(_root);
        if (!IsRevision(snapshotRevision)) throw new InvalidOperationException("Нет полной Git-ревизии для checkpoint.");
        var metadata = new StudioCheckpointMetadata(Guid.NewGuid().ToString("N"), name, author,
            DateTimeOffset.UtcNow, snapshotRevision, paths.Length, resources.Length, ResourceCapture);
        var manifest = new Manifest(1, RootHash(), metadata, sources.ToArray(), resources);
        var manifestBytes = Utf8.GetBytes(JsonSerializer.Serialize(manifest));
        if (total + manifestBytes.LongLength > MaxBytes) throw BudgetFailure();
        // Validate and capture everything before creating any persistent artifact.
        var directory = CheckpointDirectory(metadata.Id);
        Guard(directory, metadata: true);
        foreach (var source in sources) Guard(directory + "/" + source.Image, metadata: true);
        Guard(directory + "/manifest.json", metadata: true);
        if (System.IO.Directory.Exists(Path.Combine(_root, directory))) throw new IOException("Checkpoint уже существует.");
        System.IO.Directory.CreateDirectory(Path.Combine(_root, directory));
        for (var index = 0; index < sources.Count; index++)
            AtomicFile.WriteAllBytes(Path.Combine(_root, directory, sources[index].Image), images[index], overwrite: false);
        // Manifest is the completion marker; incomplete directories are preserved, never listed as valid.
        AtomicFile.WriteAllBytes(Path.Combine(_root, directory, "manifest.json"), manifestBytes, overwrite: false);
        return metadata;
    }

    public IReadOnlyList<StudioCheckpointMetadata> List(out IReadOnlyList<string> warnings)
    {
        var problems = new List<string>();
        warnings = problems;
        var result = new List<StudioCheckpointMetadata>();
        try
        {
            Guard(DirectoryPath, metadata: true);
            var full = Path.Combine(_root, DirectoryPath);
            if (!System.IO.Directory.Exists(full)) return result;
            foreach (var directory in System.IO.Directory.EnumerateDirectories(full).Order(StringComparer.Ordinal))
            {
                try { result.Add(Read(Path.GetFileName(directory)).Metadata); }
                catch (Exception error) when (IsReadFailure(error))
                { problems.Add("Один checkpoint недоступен или повреждён; его артефакты сохранены."); }
            }
        }
        catch (Exception error) when (IsReadFailure(error))
        { problems.Add("Список checkpoint недоступен; артефакты сохранены."); }
        return result.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    public StudioCheckpointRestorePlan PrepareRestore(string id)
    {
        var ownedSources = CurrentCatalogSources();
        var catalogImages = CaptureCatalogImages(ownedSources);
        RefuseCatalogDrafts(catalogImages);
        var checkpoint = Read(id);
        ValidateMembership(checkpoint, ownedSources);
        ValidateResources(checkpoint);
        if (workspace.IdentityConflicts.Count != 0) throw new InvalidOperationException("Исправьте повторные ID перед восстановлением.");
        var writes = new List<StudioFileWrite>();
        var workspaceHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var diskHashes = new Dictionary<string, string?>(StringComparer.Ordinal);
        var currentTexts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in checkpoint.Sources)
        {
            Guard(source.Path);
            var full = Path.Combine(_root, source.Path);
            if (!File.Exists(full)) throw new InvalidOperationException("Исходник checkpoint отсутствует. Создание и удаление файлов при восстановлении не поддерживается.");
            var diskBytes = ReadBounded(full, MaxBytes);
            var diskHash = AtomicFile.Sha256(diskBytes);
            ValidateCatalogDiskHash(source.Path, diskBytes, catalogImages);
            var currentText = catalogImages.ContainsKey(source.Path) ? Utf8.GetString(diskBytes) : CurrentText(source.Path);
            var bytes = ReadImage(checkpoint, source);
            var parsed = AuthoredFile.CreateNew(_root, source.Path, Utf8.GetString(bytes));
            if (parsed.RootIsArray != AuthoredFile.CreateNew(_root, source.Path, currentText).RootIsArray)
                throw new InvalidDataException("Тип исходника checkpoint не совпадает с текущим.");
            workspaceHashes.Add(source.Path, AtomicFile.Sha256(Utf8.GetBytes(currentText)));
            diskHashes.Add(source.Path, diskHash);
            currentTexts.Add(source.Path, Utf8.GetString(diskBytes));
            if (diskHash != source.Sha256) writes.Add(new(source.Path, diskHash, bytes));
        }
        ValidateTargetIdentities(checkpoint);
        return new(checkpoint.Metadata, writes, checkpoint.Sources.Select(source => source.Path).ToHashSet(StringComparer.Ordinal),
            writes.Select(write => write.RelativePath).Order(StringComparer.Ordinal).ToArray(), workspaceHashes, diskHashes, currentTexts);
    }

    /// <summary>Repeat immediately before ApplyFilesAsync; this method never writes project files.</summary>
    public void ValidateRestore(StudioCheckpointRestorePlan plan)
    {
        var ownedSources = CurrentCatalogSources();
        var catalogImages = CaptureCatalogImages(ownedSources);
        RefuseCatalogDrafts(catalogImages);
        var checkpoint = Read(plan.Metadata.Id);
        ValidateMembership(checkpoint, ownedSources);
        ValidateResources(checkpoint);
        if (plan.Metadata != checkpoint.Metadata || workspace.IdentityConflicts.Count != 0
            || !plan.AllowedPaths.SetEquals(checkpoint.Sources.Select(source => source.Path))
            || plan.WorkspaceHashes.Count != checkpoint.Sources.Length || plan.ExpectedDiskHashes.Count != checkpoint.Sources.Length
            || plan.CurrentTexts.Count != checkpoint.Sources.Length)
            throw new InvalidOperationException("Checkpoint или контекст восстановления изменился. Откройте новый preview.");
        var expectedWrites = new List<string>();
        foreach (var source in checkpoint.Sources)
        {
            Guard(source.Path);
            var full = Path.Combine(_root, source.Path);
            if (!File.Exists(full)) throw new InvalidOperationException("Исходники изменились после preview. Откройте новый preview.");
            var diskBytes = ReadBounded(full, MaxBytes);
            ValidateCatalogDiskHash(source.Path, diskBytes, catalogImages);
            var currentText = catalogImages.ContainsKey(source.Path) ? Utf8.GetString(diskBytes) : CurrentText(source.Path);
            if (!plan.WorkspaceHashes.TryGetValue(source.Path, out var workspaceHash)
                || AtomicFile.Sha256(Utf8.GetBytes(currentText)) != workspaceHash
                || !plan.ExpectedDiskHashes.TryGetValue(source.Path, out var expectedHash)
                || !plan.CurrentTexts.TryGetValue(source.Path, out var planCurrentText)
                || AtomicFile.Sha256(Utf8.GetBytes(planCurrentText)) != expectedHash
                || AtomicFile.Sha256(diskBytes) != expectedHash)
                throw new InvalidOperationException("Исходники изменились после preview. Откройте новый preview.");
            if (expectedHash == source.Sha256) continue;
            expectedWrites.Add(source.Path);
            var write = plan.Writes.SingleOrDefault(write => write.RelativePath == source.Path);
            if (write?.Bytes is null || write.ExpectedSha256 != expectedHash || AtomicFile.Sha256(write.Bytes) != source.Sha256)
                throw new InvalidDataException("Данные плана восстановления не совпадают с checkpoint.");
        }
        if (plan.Writes.Count != expectedWrites.Count || !plan.ChangedPaths.SequenceEqual(expectedWrites.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Набор изменений checkpoint не совпадает с preview.");
        ValidateTargetIdentities(checkpoint);
    }

    public Task RestoreAsync(EditSession session, StudioCheckpointRestorePlan plan,
        Action nativeBarrier, Func<Task> afterPromotionAsync)
    {
        if (!ReferenceEquals(session.Workspace, workspace) || session.IsBusy
            || workspace.Files.Any(file => file.Dirty || file.IsNew))
            throw new InvalidOperationException("Сессия восстановления недоступна или содержит черновики.");
        ValidateRestore(plan);
        if (plan.Writes.Count == 0) return Task.CompletedTask;
        // Startup hydrates only pilot history; a later checkpoint must fence that partial history.
        StudioFileTransaction.FenceIncompleteHistory(_root);
        return session.ApplyFilesAsync("восстановить «" + plan.Metadata.Name + "»", "urman.studio:checkpoint/" + plan.Metadata.Id,
            plan.Writes, plan.AllowedPaths, nativeBarrier, afterPromotionAsync);
    }

    private Manifest Read(string id)
    {
        var directory = CheckpointDirectory(id);
        var path = directory + "/manifest.json";
        Guard(path, metadata: true);
        var bytes = ReadBounded(Path.Combine(_root, path), MaxBytes);
        Manifest manifest;
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
            RefuseDuplicates(document.RootElement);
            manifest = JsonSerializer.Deserialize<Manifest>(Utf8.GetString(bytes)) ?? throw new InvalidDataException();
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException or InvalidOperationException)
        { throw new InvalidDataException("Метаданные checkpoint некорректны.", error); }
        var meta = manifest.Metadata;
        if (manifest.Version != 1 || manifest.RootHash != RootHash() || meta is null || meta.Id != id
            || manifest.Sources is null || manifest.Resources is null || manifest.Sources.Length is < 1 or > MaxFiles
            || manifest.Resources.Length > MaxFiles || meta.FileCount != manifest.Sources.Length
            || meta.ResourceCount != manifest.Resources.Length || meta.ResourceCapture != ResourceCapture
            || !IsRevision(meta.Revision) || meta.CreatedAt.Offset != TimeSpan.Zero || meta.CreatedAt == default)
            throw new InvalidDataException("Checkpoint не соответствует рабочей области.");
        ValidateLabel(meta.Name);
        ValidateLabel(meta.Author);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ownedSources = CurrentCatalogSources();
        var ownedByPath = ownedSources.ToDictionary(source => source.Path, StringComparer.Ordinal);
        var currentPaths = CurrentSourcePaths(ownedSources).ToHashSet(StringComparer.Ordinal);
        var discoveredResources = BasisPaths.ToHashSet(StringComparer.Ordinal);
        long total = bytes.LongLength;
        for (var index = 0; index < manifest.Sources.Length; index++)
        {
            var source = manifest.Sources[index];
            if (source is null) throw new InvalidDataException();
            ValidateSourcePath(source.Path);
            if (!currentPaths.Contains(source.Path)
                || !seen.Add(source.Path) || source.Image != $"source-{index:D4}.txt" || source.Bytes < 0 || source.Bytes > MaxBytes || !IsSha(source.Sha256))
                throw new InvalidDataException("Список исходников checkpoint некорректен.");
            total += source.Bytes;
            if (total > MaxBytes) throw new InvalidDataException("Checkpoint превышает бюджет.");
            var image = ReadImage(manifest, source);
            if (ownedByPath.TryGetValue(source.Path, out var ownedSource)) ownedSource.Validate(image);
            foreach (var value in Strings(AuthoredFile.CreateNew(_root, source.Path, Utf8.GetString(image)).Snapshot()))
                if (value.StartsWith("res://", StringComparison.Ordinal)) discoveredResources.Add(ResourcePath(value));
        }
        seen.Clear();
        foreach (var resource in manifest.Resources)
        {
            if (resource is null) throw new InvalidDataException();
            ValidateSourcePath(resource.Path);
            if ((!resource.Path.StartsWith("game/", StringComparison.Ordinal) && !BasisPaths.Contains(resource.Path, StringComparer.Ordinal)) || !seen.Add(resource.Path)
                || resource.Sha256 is not null && !IsSha(resource.Sha256)) throw new InvalidDataException("Ссылки checkpoint некорректны.");
        }
        if (BasisPaths.Any(path => !manifest.Resources.Any(resource => resource.Path == path && resource.Sha256 is not null)))
            throw new InvalidDataException("Receipts основы генерации отсутствуют.");
        if (!seen.SetEquals(discoveredResources)) throw new InvalidDataException("Receipts ссылок checkpoint не соответствуют snapshots.");
        return manifest;
    }

    private byte[] ReadImage(Manifest checkpoint, SourceReceipt source)
    {
        var relative = CheckpointDirectory(checkpoint.Metadata.Id) + "/" + source.Image;
        Guard(relative, metadata: true);
        var bytes = ReadBounded(Path.Combine(_root, relative), source.Bytes);
        if (bytes.LongLength != source.Bytes || AtomicFile.Sha256(bytes) != source.Sha256) throw new InvalidDataException("Snapshot checkpoint повреждён.");
        _ = AuthoredFile.CreateNew(_root, source.Path, Utf8.GetString(bytes)).Snapshot();
        return bytes;
    }

    private void ValidateMembership(Manifest checkpoint, IReadOnlyList<StudioCheckpointCatalogSource>? ownedSources = null)
    {
        if (!CurrentSourcePaths(ownedSources).ToHashSet(StringComparer.Ordinal)
            .SetEquals(checkpoint.Sources.Select(source => source.Path)))
            throw new InvalidOperationException("Состав исходников изменился. Неизвестные, новые или отсутствующие файлы не восстанавливаются автоматически.");
    }

    private void ValidateResources(Manifest checkpoint)
    {
        foreach (var resource in checkpoint.Resources)
        {
            Guard(resource.Path);
            var path = Path.Combine(_root, resource.Path);
            if (resource.Sha256 is null ? File.Exists(path) : !File.Exists(path) || DiskHash(resource.Path) != resource.Sha256)
                throw new InvalidOperationException("Связанный ресурс отсутствует или изменился. Checkpoint хранит только receipts ресурсов, не их версии.");
        }
    }

    private void ValidateTargetIdentities(Manifest checkpoint)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in checkpoint.Sources)
        {
            var file = AuthoredFile.CreateNew(_root, source.Path, Utf8.GetString(ReadImage(checkpoint, source)));
            if (!file.RootIsArray) continue;
            foreach (var key in file.Keys())
                if (!ids.Add(key) || key == EditSession.OrderKey || file.Get(key) is not JsonObject entity
                    || entity["id"] is not JsonValue value || !value.TryGetValue<string>(out var id) || string.IsNullOrWhiteSpace(id) || id != key)
                    throw new InvalidDataException("Snapshot содержит неоднозначные или некорректные ID.");
        }
    }

    private void Guard(string relative, bool metadata = false) => StudioFileTransaction.EnsureNoLinks(_root, relative, allowStudioMetadata: metadata);
    private string[] CurrentSourcePaths(IReadOnlyList<StudioCheckpointCatalogSource>? ownedSources = null)
    {
        ownedSources ??= CurrentCatalogSources();
        Guard(HeroRecipePath);
        return workspace.Files.Select(file => file.RelativePath)
            .Concat(File.Exists(Path.Combine(_root, HeroRecipePath)) ? [HeroRecipePath] : Array.Empty<string>())
            .Concat(ownedSources.Select(source => source.Path))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private StudioCheckpointCatalogSource[] CurrentCatalogSources()
    {
        var sources = catalogSources?.Invoke() ?? [];
        if (catalogSources is not null && sources.Count != 2)
            throw new InvalidDataException("Checkpoint должен получить оба каталога с точным владельцем источника.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (source is null || source.Capture is null || source.Validate is null
                || workspace.HasFile(source.Path) || !StudioFileTransaction.IsOwnedCatalogPath(source.Owner, source.Path)
                || !seen.Add(source.Path))
                throw new InvalidDataException("Checkpoint получил неизвестный или повторный каталог.");
            ValidateSourcePath(source.Path);
            Guard(source.Path);
        }
        return sources.OrderBy(source => source.Path, StringComparer.Ordinal).ToArray();
    }

    private Dictionary<string, StudioCheckpointCatalogImage> CaptureCatalogImages(
        IReadOnlyList<StudioCheckpointCatalogSource> ownedSources)
    {
        var images = new Dictionary<string, StudioCheckpointCatalogImage>(StringComparer.Ordinal);
        foreach (var source in ownedSources)
        {
            Guard(source.Path);
            var image = source.Capture();
            if (image is null || !IsSha(image.DiskSha256) || image.DraftBytes is { Length: > MaxBytes })
                throw new InvalidDataException("Снимок каталога некорректен или превышает бюджет.");
            if (image.DraftBytes is { } draftBytes) _ = Utf8.GetString(draftBytes);
            images.Add(source.Path, image);
        }
        return images;
    }

    private static void RefuseCatalogDrafts(IReadOnlyDictionary<string, StudioCheckpointCatalogImage> images)
    {
        if (images.Values.Any(image => image.DraftBytes is not null))
            throw new InvalidOperationException("Сначала сохраните или отмените черновики каталогов перед восстановлением.");
    }

    private byte[] CurrentSourceBytes(string path, IReadOnlyDictionary<string, StudioCheckpointCatalogImage> catalogImages,
        bool includeDrafts)
    {
        Guard(path);
        if (!catalogImages.TryGetValue(path, out var catalogImage)) return Utf8.GetBytes(CurrentText(path));
        var diskBytes = ReadBounded(Path.Combine(_root, path), MaxBytes);
        ValidateCatalogDiskHash(path, diskBytes, catalogImages);
        return includeDrafts ? catalogImage.DraftBytes ?? diskBytes : diskBytes;
    }

    private static void ValidateCatalogDiskHash(string path, byte[] diskBytes,
        IReadOnlyDictionary<string, StudioCheckpointCatalogImage> catalogImages)
    {
        if (catalogImages.TryGetValue(path, out var image) && AtomicFile.Sha256(diskBytes) != image.DiskSha256)
            throw new InvalidOperationException("Каталог изменился во время снимка. Перечитайте его перед продолжением.");
    }
    private string CurrentText(string path)
    {
        Guard(path);
        return workspace.HasFile(path) ? workspace.File(path).Text
            : path == HeroRecipePath ? Utf8.GetString(ReadBounded(Path.Combine(_root, path), MaxBytes))
            : throw new InvalidDataException("Неизвестный источник checkpoint.");
    }
    private string DiskHash(string relative)
    {
        Guard(relative);
        using var stream = new FileStream(Path.Combine(_root, relative), FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
    private string RootHash() => AtomicFile.Sha256(Utf8.GetBytes(Path.GetFullPath(_root)));
    private static string CheckpointDirectory(string id) => Guid.TryParseExact(id, "N", out var guid) && guid.ToString("N") == id
        ? DirectoryPath + "/" + id : throw new ArgumentException("Некорректный ID checkpoint.", nameof(id));
    private static void ValidateLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(char.IsControl))
            throw new ArgumentException("Название и автор checkpoint должны содержать от 1 до 128 символов без управляющих знаков.");
    }
    private static void ValidateSourcePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':')
            || path.Any(char.IsControl) || path.Split('/').Any(part => part.Length == 0 || part is "." or "..")
            || path.Split('/')[0].StartsWith('.')) throw new InvalidDataException("Некорректный относительный путь checkpoint.");
    }
    private static string ResourcePath(string resource)
    {
        var path = "game/" + resource["res://".Length..];
        ValidateSourcePath(path);
        return path;
    }
    private static bool IsSha(string value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool IsRevision(string value) => value is { Length: 40 or 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static InvalidOperationException BudgetFailure() => new("Checkpoint превышает бюджет исходных текстов 8 МиБ. Данные не обрезаны; checkpoint не записан.");
    private static bool IsReadFailure(Exception error) => error is IOException or UnauthorizedAccessException or InvalidDataException
        or ArgumentException or InvalidOperationException or FormatException or JsonException;
    private static byte[] ReadBounded(string path, long limit)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > limit) throw new InvalidDataException("Snapshot превышает объявленный размер.");
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        int count;
        while ((count = input.Read(buffer)) > 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Snapshot превышает объявленный размер.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    private static IEnumerable<string> Strings(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text)) yield return text;
        else if (node is JsonObject obj)
            foreach (var pair in obj) foreach (var textValue in Strings(pair.Value)) yield return textValue;
        else if (node is JsonArray array)
            foreach (var item in array) foreach (var textValue in Strings(item)) yield return textValue;
    }
    private static void RefuseDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Повторное поле checkpoint.");
                RefuseDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RefuseDuplicates(item);
    }
    private sealed record SourceReceipt(string Path, string Image, long Bytes, string Sha256);
    private sealed record ResourceReceipt(string Path, string? Sha256);
    private sealed record Manifest(int Version, string RootHash, StudioCheckpointMetadata Metadata, SourceReceipt[] Sources, ResourceReceipt[] Resources);
}
