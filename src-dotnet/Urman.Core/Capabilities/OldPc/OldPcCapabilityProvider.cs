using System.Text.Json;
using Urman.Core.Contracts;

namespace Urman.Core.Capabilities.OldPc;

public sealed record OldPcDocumentDescriptor(string Id, string Section);

public sealed class OldPcCapabilityProvider : ICapabilityProvider
{
    public const string Protocol = "urman.oldpc:capability/archive-hub";
    public const string ModuleId = "urman.oldpc";

    private readonly IReadOnlyList<OldPcDocumentDescriptor> _documents;

    public OldPcCapabilityProvider(IEnumerable<OldPcDocumentDescriptor> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        _documents = documents
            .OrderBy(document => document.Id, StringComparer.Ordinal)
            .ToArray();
        if (_documents.Count == 0 ||
            _documents.Any(document => !document.Id.StartsWith($"{ModuleId}:document/", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(document.Section)) ||
            _documents.Select(document => document.Id).Distinct(StringComparer.Ordinal).Count() != _documents.Count)
        {
            throw new ArgumentException("Old PC provider requires unique urman.oldpc documents with sections.", nameof(documents));
        }
    }

    public string ProtocolId => Protocol;

    public string ExactVersion => "1.0.0";

    public int StateSchemaVersion => 1;

    public bool ValidateConfig(JsonElement config) =>
        config.ValueKind == JsonValueKind.Object &&
        config.EnumerateObject().Select(property => property.Name).SequenceEqual(["moduleId"]) &&
        config.GetProperty("moduleId").GetString() == ModuleId;

    public ICapabilitySession CreateSession(string capabilityInstanceId, JsonElement config) =>
        new OldPcSession(_documents);

    private sealed class OldPcSession(IReadOnlyList<OldPcDocumentDescriptor> documents) : ICapabilitySession
    {
        private static readonly string[] SnapshotKeys =
        [
            "activeDocumentId",
            "activeSection",
            "nextActionSequence",
            "query",
            "savedDocumentIds"
        ];

        private readonly IReadOnlyDictionary<string, OldPcDocumentDescriptor> _documents =
            documents.ToDictionary(document => document.Id, StringComparer.Ordinal);
        private readonly HashSet<string> _sections = documents
            .Select(document => document.Section)
            .Append("archive_search")
            .ToHashSet(StringComparer.Ordinal);
        private readonly HashSet<string> _savedDocumentIds = new(StringComparer.Ordinal);
        private OldPcDesktopSnapshot _desktop = new();
        private string? _activeDocumentId;
        private string _activeSection = "archive_search";
        private string _query = string.Empty;
        private long _nextActionSequence = 1;
        private bool _started;
        private bool _disposed;

        public void Restore(JsonElement state)
        {
            EnsureNotStarted();
            if (state.ValueKind != JsonValueKind.Object ||
                !state.EnumerateObject().Select(property => property.Name).Where(name => name != "desktop")
                    .Order(StringComparer.Ordinal).SequenceEqual(SnapshotKeys)
                || state.EnumerateObject().Count(property => property.Name == "desktop") > 1)
            {
                throw new InvalidDataException("Old PC snapshot has an invalid shape.");
            }

            _activeDocumentId = state.GetProperty("activeDocumentId").ValueKind == JsonValueKind.Null
                ? null
                : RequiredDocumentId(state.GetProperty("activeDocumentId").GetString());
            _desktop = state.TryGetProperty("desktop", out var desktop)
                ? OldPcDesktopSnapshot.Restore(desktop, _documents.Keys.ToHashSet(StringComparer.Ordinal), _sections)
                : new OldPcDesktopSnapshot();
            _activeSection = state.GetProperty("activeSection").GetString()
                ?? throw new InvalidDataException("Old PC snapshot section is missing.");
            if (!_sections.Contains(_activeSection))
            {
                throw new InvalidDataException("Old PC snapshot section is unknown.");
            }

            _query = state.GetProperty("query").GetString()
                ?? throw new InvalidDataException("Old PC snapshot query is invalid.");
            if (!state.GetProperty("nextActionSequence").TryGetInt64(out _nextActionSequence) || _nextActionSequence < 1)
            {
                throw new InvalidDataException("Old PC snapshot action sequence is invalid.");
            }

            _savedDocumentIds.Clear();
            foreach (var item in state.GetProperty("savedDocumentIds").EnumerateArray())
            {
                var id = RequiredDocumentId(item.GetString());
                if (!_savedDocumentIds.Add(id))
                {
                    throw new InvalidDataException("Old PC snapshot contains a duplicate saved document.");
                }
            }
        }

        public void Start()
        {
            EnsureNotStarted();
            _started = true;
        }

        public JsonElement Handle(JsonElement input)
        {
            EnsureActive();
            var type = input.GetProperty("type").GetString();
            switch (type)
            {
                case "desktop":
                    // Presentation state is captured with the same capability as
                    // the archive. It cannot open/save a source or advance a clue.
                    _desktop = OldPcDesktopSnapshot.Restore(input.GetProperty("desktop"),
                        _documents.Keys.ToHashSet(StringComparer.Ordinal), _sections);
                    break;
                case "search":
                    _query = input.GetProperty("query").GetString()
                        ?? throw new ArgumentException("Old PC search query must be a string.");
                    _activeSection = "archive_search";
                    _activeDocumentId = null;
                    AdvanceActionSequence();
                    break;
                case "open":
                    _activeDocumentId = RequiredDocumentId(input.GetProperty("documentId").GetString());
                    AdvanceActionSequence();
                    break;
                case "save":
                    _savedDocumentIds.Add(RequiredDocumentId(input.GetProperty("documentId").GetString()));
                    AdvanceActionSequence();
                    break;
                case "section":
                    var section = input.GetProperty("section").GetString()
                        ?? throw new ArgumentException("Old PC section must be a string.");
                    if (!_sections.Contains(section))
                    {
                        throw new KeyNotFoundException($"Unknown old PC section {section}.");
                    }

                    _activeSection = section;
                    _activeDocumentId = null;
                    _query = string.Empty;
                    break;
                default:
                    throw new ArgumentException($"Unknown old PC input {type}.");
            }

            return CaptureState();
        }

        public JsonElement CaptureState()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return JsonSerializer.SerializeToElement(new
            {
                activeDocumentId = _activeDocumentId,
                activeSection = _activeSection,
                desktop = JsonSerializer.SerializeToElement(_desktop, OldPcDesktopSnapshot.JsonOptions),
                nextActionSequence = _nextActionSequence,
                query = _query,
                savedDocumentIds = _savedDocumentIds.Order(StringComparer.Ordinal).ToArray()
            });
        }

        public void Stop()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _started = false;
        }

        public void Dispose()
        {
            _disposed = true;
            _started = false;
        }

        private string RequiredDocumentId(string? id)
        {
            if (id is null || !_documents.ContainsKey(id))
            {
                throw new KeyNotFoundException($"Unknown old PC document {id}.");
            }

            return id;
        }

        private void AdvanceActionSequence() => _nextActionSequence = checked(_nextActionSequence + 1);

        private void EnsureNotStarted()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
            {
                throw new InvalidOperationException("Old PC session is already started.");
            }
        }

        private void EnsureActive()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_started)
            {
                throw new InvalidOperationException("Old PC session is not active.");
            }
        }
    }
}
