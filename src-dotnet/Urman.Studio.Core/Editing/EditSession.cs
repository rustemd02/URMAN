using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>One entity before and after an edit. A null value means "absent" (created or deleted).</summary>
public sealed record EntityChange(string RelativePath, string Key, JsonNode? Before, JsonNode? After);

/// <summary>A user-visible undo step: a drag, a brush stroke or a composite action is one command (SAVE01).</summary>
public sealed record EditCommand(string Label, IReadOnlyList<EntityChange> Changes);

/// <summary>
/// Undo/redo would overwrite something that changed since this command — most
/// often another author's merged work (SAVE05). Nothing was applied.
/// </summary>
public sealed class UndoConflictException(string label, IReadOnlyList<EntityChange> blocked)
    : InvalidOperationException($"«{label}» нельзя отменить автоматически: эти объекты уже изменены после неё.")
{
    public IReadOnlyList<EntityChange> Blocked { get; } = blocked;
}

/// <summary>
/// Typed edits over a <see cref="StudioWorkspace"/> with undo and redo. Every
/// edit is recorded as entity snapshots; undo only reverts an entity that still
/// holds the value this command produced, so undoing your own step never rolls
/// back someone else's work that arrived afterwards.
/// </summary>
public sealed class EditSession(StudioWorkspace workspace)
{
    private readonly Stack<EditCommand> _undo = new();
    private readonly Stack<EditCommand> _redo = new();
    private List<EntityChange>? _open;
    private string? _openLabel;

    public StudioWorkspace Workspace { get; } = workspace;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoLabel => _undo.TryPeek(out var command) ? command.Label : null;
    public string? RedoLabel => _redo.TryPeek(out var command) ? command.Label : null;
    public event Action? Changed;

    /// <summary>Group several edits (a drag, a template) into one undo step.</summary>
    public IDisposable Begin(string label)
    {
        if (_open is not null)
        {
            return new Scope(() => { });
        }

        _open = [];
        _openLabel = label;
        return new Scope(() =>
        {
            var changes = Coalesce(_open!);
            _open = null;
            if (changes.Count > 0)
            {
                Push(new(_openLabel!, changes));
            }
        });
    }

    /// <summary>Replace (or create) an entity by ID in its file.</summary>
    public void Set(string relativePath, string key, JsonNode? value, string label) => Apply(relativePath, key, value, remove: false, label);

    public void Delete(string relativePath, string key, string label) => Apply(relativePath, key, null, remove: true, label);

    public const string OrderKey = "#order";

    /// <summary>Change the order of a file's entities (a list order is behaviour: cutscene actions, stages). One undo step.</summary>
    public void SetOrder(string relativePath, IReadOnlyList<string> keys, string label)
    {
        var file = Workspace.File(relativePath);
        var before = new JsonArray(file.Keys().Select(key => (JsonNode?)key).ToArray());
        var after = new JsonArray(keys.Select(key => (JsonNode?)key).ToArray());
        if (JsonNode.DeepEquals(before, after)) return;
        file.Reorder(keys);
        var change = new EntityChange(relativePath, OrderKey, before, after);
        if (_open is not null) _open.Add(change); else Push(new(label, [change]));
    }

    /// <summary>Change one field of an entity addressed by its ID; the path is a list of property names.</summary>
    public void SetField(string id, IReadOnlyList<string> path, JsonNode? value, string label)
    {
        var address = Workspace.Locate(id) ?? throw new KeyNotFoundException($"Нет объекта с ID {id}.");
        var entity = Workspace.File(address.RelativePath).Get(address.Key)!.AsObject();
        JsonObject parent = entity;
        foreach (var name in path.Take(path.Count - 1))
        {
            parent = parent[name] as JsonObject ?? throw new KeyNotFoundException($"У {id} нет поля {string.Join('.', path)}.");
        }

        parent[path[^1]] = value?.DeepClone();
        Set(address.RelativePath, address.Key, entity, label);
    }

    public void Undo()
    {
        if (!_undo.TryPop(out var command))
        {
            return;
        }

        try
        {
            Revert(command, forward: false);
            _redo.Push(command);
        }
        catch
        {
            _undo.Push(command);
            throw;
        }

        Changed?.Invoke();
    }

    public void Redo()
    {
        if (!_redo.TryPop(out var command))
        {
            return;
        }

        try
        {
            Revert(command, forward: true);
            _undo.Push(command);
        }
        catch
        {
            _redo.Push(command);
            throw;
        }

        Changed?.Invoke();
    }

    private void Apply(string relativePath, string key, JsonNode? value, bool remove, string label)
    {
        var file = Workspace.File(relativePath);
        var before = file.Get(key);
        var after = remove ? null : value?.DeepClone();
        if (JsonNode.DeepEquals(before, after))
        {
            return;
        }

        file.Put(key, after, remove);
        Workspace.Reindex();
        var change = new EntityChange(relativePath, key, before, after);
        if (_open is not null)
        {
            _open.Add(change);
        }
        else
        {
            Push(new(label, [change]));
        }
    }

    private void Push(EditCommand command)
    {
        _undo.Push(command);
        _redo.Clear();
        Changed?.Invoke();
    }

    private void Revert(EditCommand command, bool forward)
    {
        JsonNode? Current(EntityChange change) => change.Key == OrderKey
            ? new JsonArray(Workspace.File(change.RelativePath).Keys().Select(key => (JsonNode?)key).ToArray())
            : Workspace.File(change.RelativePath).Get(change.Key);
        var blocked = command.Changes
            .Where(change => !JsonNode.DeepEquals(Current(change), forward ? change.Before : change.After))
            .ToArray();
        if (blocked.Length > 0)
        {
            throw new UndoConflictException(command.Label, blocked);
        }

        foreach (var change in forward ? command.Changes : command.Changes.Reverse())
        {
            var target = forward ? change.After : change.Before;
            if (change.Key == OrderKey)
            {
                Workspace.File(change.RelativePath).Reorder(target!.AsArray().Select(key => (string)key!).ToArray());
                continue;
            }

            Workspace.File(change.RelativePath).Put(change.Key, target, remove: target is null);
        }

        Workspace.Reindex();
    }

    // Several edits of the same entity inside one command collapse to its
    // first "before" and last "after" (a whole drag is one change).
    private static List<EntityChange> Coalesce(List<EntityChange> changes) =>
        changes
            .GroupBy(change => (change.RelativePath, change.Key))
            .Select(group => new EntityChange(group.Key.RelativePath, group.Key.Key, group.First().Before, group.Last().After))
            .Where(change => !JsonNode.DeepEquals(change.Before, change.After))
            .ToList();

    private sealed class Scope(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
        }
    }
}
