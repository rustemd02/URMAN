using System.Text.Json;
using System.Text.Json.Serialization;

namespace Urman.Core.Capabilities.OldPc;

/// <summary>
/// A bounded projection of the simulated desktop. This belongs to the existing
/// OldPc capability snapshot; it never grants knowledge or writes host files.
/// </summary>
public sealed class OldPcDesktopSnapshot
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static readonly string[] Applications =
        ["archive", "files", "browser", "notepad", "writer", "pictures", "trash"];

    public List<OldPcWindowSnapshot> Windows { get; set; } = [];
    public string? ActiveWindowId { get; set; }
    public List<OldPcNoteSnapshot> Files { get; set; } = [];
    public List<string> BrowserHistory { get; set; } = [];
    public int BrowserIndex { get; set; } = -1;
    public List<string> VisitedDocumentIds { get; set; } = [];
    public string Folder { get; set; } = "computer";
    public string? NoteId { get; set; }
    public string? WriterId { get; set; }
    public double ElapsedSeconds { get; set; }

    public static OldPcDesktopSnapshot Restore(JsonElement json, IReadOnlySet<string> documents,
        IReadOnlySet<string> sections)
    {
        OldPcDesktopSnapshot state;
        try
        {
            state = json.Deserialize<OldPcDesktopSnapshot>(JsonOptions)
                ?? throw new InvalidDataException("Old PC desktop snapshot is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Old PC desktop snapshot has an invalid shape.", error);
        }
        state.Validate(documents, sections);
        return state;
    }

    public void Validate(IReadOnlySet<string> documents, IReadOnlySet<string> sections)
    {
        if (Windows is null || Files is null || BrowserHistory is null || VisitedDocumentIds is null
            || Windows.Count > Applications.Length || Files.Count > 32
            || BrowserHistory.Count > 64 || VisitedDocumentIds.Count > documents.Count)
            throw new InvalidDataException("Old PC desktop snapshot exceeds its bounds.");
        var windowIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var window in Windows)
        {
            if (window is null || !Applications.Contains(window.Id, StringComparer.Ordinal)
                || !windowIds.Add(window.Id)
                || !FiniteRange(window.X, 0, 1) || !FiniteRange(window.Y, 0, 1)
                || !FiniteRange(window.Width, .2, 1) || !FiniteRange(window.Height, .2, 1))
                throw new InvalidDataException("Old PC desktop window is invalid.");
        }
        if (ActiveWindowId is not null && !windowIds.Contains(ActiveWindowId))
            throw new InvalidDataException("Old PC active window is missing.");
        var noteIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var note in Files)
        {
            if (note is null || note.Id is null || note.Title is null || note.Text is null
                || !note.Id.StartsWith("note-", StringComparison.Ordinal)
                || note.Id.Length > 64 || !noteIds.Add(note.Id)
                || note.Title.Length is < 1 or > 96 || note.Text.Length > 32768)
                throw new InvalidDataException("Old PC personal file is invalid.");
        }
        if ((NoteId is not null && !noteIds.Contains(NoteId))
            || (WriterId is not null && !noteIds.Contains(WriterId)))
            throw new InvalidDataException("Old PC editor file is missing.");
        foreach (var address in BrowserHistory)
        {
            if (address is null || address.Length > 192
                || !(address is "home" or "tatwiki" or "yalkyn" or "village" or "mail"
                    || address.StartsWith("doc:", StringComparison.Ordinal) && documents.Contains(address[4..])))
                throw new InvalidDataException("Old PC browser history contains an unknown page.");
        }
        if (BrowserIndex < -1 || BrowserIndex >= BrowserHistory.Count
            || (BrowserHistory.Count > 0 && BrowserIndex < 0))
            throw new InvalidDataException("Old PC browser cursor is invalid.");
        var visited = new HashSet<string>(StringComparer.Ordinal);
        if (VisitedDocumentIds.Any(id => id is null || !documents.Contains(id) || !visited.Add(id)))
            throw new InvalidDataException("Old PC visited documents are invalid.");
        if (Folder is null || !(Folder is "computer" or "notes" or "trash" or "pictures" || sections.Contains(Folder))
            || !FiniteRange(ElapsedSeconds, 0, 315360000))
            throw new InvalidDataException("Old PC desktop folder or clock is invalid.");
    }

    private static bool FiniteRange(double value, double minimum, double maximum) =>
        double.IsFinite(value) && value >= minimum && value <= maximum;
}

public sealed class OldPcWindowSnapshot
{
    [JsonRequired]
    public string Id { get; set; } = string.Empty;
    [JsonRequired]
    public float X { get; set; } = .1f;
    [JsonRequired]
    public float Y { get; set; } = .1f;
    [JsonRequired]
    public float Width { get; set; } = .8f;
    [JsonRequired]
    public float Height { get; set; } = .8f;
    [JsonRequired]
    public bool Minimized { get; set; }
    [JsonRequired]
    public bool Maximized { get; set; }
}

public sealed class OldPcNoteSnapshot
{
    [JsonRequired]
    public string Id { get; set; } = string.Empty;
    [JsonRequired]
    public string Title { get; set; } = "Без названия.txt";
    [JsonRequired]
    public string Text { get; set; } = string.Empty;
    [JsonRequired]
    public bool RichText { get; set; }
    [JsonRequired]
    public bool Deleted { get; set; }
}
