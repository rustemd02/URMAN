using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    public SettlementRegistry? NotebookSettlement =>
        (GetTree().GetFirstNodeInGroup("act1_connected_world") as Act1ConnectedWorld)?.AddressRegistry;

    public IReadOnlyList<string> KnownAddressIds()
    {
        if (_kernel is null || NotebookSettlement is not { } registry) return [];
        var state = _kernel.SelectState();
        var knowledge = state.GetProperty("knowledge");
        var known = new HashSet<string>(LocatedAddressIds(), StringComparer.Ordinal);
        const string prefix = "urman.chapter1:knowledge/address-";
        foreach (var fact in knowledge.EnumerateObject())
        {
            if (fact.Name.StartsWith(prefix, StringComparison.Ordinal)
                && fact.Value.TryGetProperty("status", out var status) && status.GetString() == "confirmed"
                && registry.CanonicalAddressId("ADR-" + fact.Name[prefix.Length..].ToUpperInvariant()) is { } id) known.Add(id);
        }
        return known.OrderBy(id => registry.FormatAddress(id), StringComparer.CurrentCulture).ToArray();
    }

    public IReadOnlyList<string> LocatedAddressIds()
    {
        if (_kernel is null || NotebookSettlement is not { } registry) return [];
        // Hearing an address gives a lead. Its exact house and access point are
        // added to the sketch only after the player reads the physical plate.
        return SelectWorldProps().EnumerateObject()
            .Where(prop => prop.Name.StartsWith("address/", StringComparison.Ordinal)
                && prop.Value.TryGetProperty("read", out var read) && read.ValueKind == JsonValueKind.True)
            .Select(prop => registry.CanonicalAddressId(prop.Name[8..]))
            .Where(id => id is not null).Select(id => id!)
            .Distinct(StringComparer.Ordinal).OrderBy(id => registry.FormatAddress(id), StringComparer.CurrentCulture).ToArray();
    }

    public IReadOnlyList<ResolvedJournalEntry> NotebookAddresses()
    {
        if (NotebookSettlement is not { } registry) return [];
        var located = LocatedAddressIds().ToHashSet(StringComparer.Ordinal);
        var sources = JournalEntries();
        return KnownAddressIds().Select(id =>
        {
            var address = registry.Addresses[id];
            var street = registry.Streets[address.StreetId];
            var read = located.Contains(id);
            const string prefix = "urman.chapter1:knowledge/address-";
            var source = sources.FirstOrDefault(entry => entry.EntryId.StartsWith(prefix, StringComparison.Ordinal)
                && registry.CanonicalAddressId("ADR-" + entry.EntryId[prefix.Length..].ToUpperInvariant()) == id);
            var body = registry.FormatAddress(id) + "\n\n" + (source?.Body
                ?? (read ? "Прочитал адрес на табличке у этого дома." : "Адрес записан со слов собеседника."));
            body += "\n\nУлица: " + street.Tatar + " / " + street.Russian;
            return new ResolvedJournalEntry("notebook/address/" + id, "notebook/address/" + id,
                registry.FormatAddress(id), body, read ? "Адресная табличка" : source?.SourceTitle ?? "Разговор");
        }).ToArray();
    }

    public IReadOnlyList<ResolvedJournalEntry> NotebookPeople()
    {
        if (_kernel is null) return [];
        var found = new Dictionary<string, (string Dialogue, string Node)>(StringComparer.Ordinal);
        var props = SelectWorldProps();
        foreach (var prop in props.EnumerateObject())
        {
            if (!prop.Name.StartsWith("notebook/person/", StringComparison.Ordinal)
                || !prop.Value.TryGetProperty("dialogueId", out var dialogue)
                || !prop.Value.TryGetProperty("nodeId", out var node)) continue;
            found.TryAdd(prop.Name["notebook/person/".Length..], (dialogue.GetString()!, node.GetString()!));
        }
        // Old saves already contain actual answered lines. Project those without
        // migrating them into a second conversation log or granting new knowledge.
        var state = _kernel.SelectState();
        if (state.TryGetProperty("dialogueChoices", out var choices))
        foreach (var row in choices.EnumerateArray())
        {
            var dialogueId = row.GetProperty("dialogueId").GetString()!;
            var nodeId = row.GetProperty("nodeId").GetString()!;
            if (!_content.RequireDialogue(dialogueId).Nodes.TryGetValue(nodeId, out var node)) continue;
            if (node.SpeakerRole is not ("aidar" or "narrator" or "")) found.TryAdd(node.SpeakerRole, (dialogueId, nodeId));
        }
        return found.Select(pair =>
        {
            var node = _content.RequireDialogue(pair.Value.Dialogue).Nodes[pair.Value.Node];
            return new ResolvedJournalEntry("notebook/person/" + pair.Key, "notebook/person/" + pair.Key,
                DialogueUi.SpeakerName(pair.Key), "Из нашего разговора:\n\n" + ResolveText(node.TextId), "Личный разговор");
        }).ToArray();
    }

    private async Task RememberDialogueSpeakerAsync(string dialogueId, CompiledDialogueNodeContent node)
    {
        if (_kernel is null || node.SpeakerRole is "aidar" or "narrator" or "") return;
        var key = "notebook/person/" + node.SpeakerRole;
        if (SelectWorldProps().TryGetProperty(key, out _)) return;
        await DispatchWorldPropsAsync(new JsonArray(new JsonObject
        {
            ["propId"] = key, ["dialogueId"] = dialogueId, ["nodeId"] = node.Id
        }));
    }

    public async Task<bool> RememberAddressAsync(string addressId)
    {
        if (_kernel is null || NotebookSettlement is not { } registry
            || !registry.Addresses.ContainsKey(addressId)
            || FindPlayer() is not { } player || player.ModalOpen) return false;
        // A plate may be on the street-facing wall while the gate is around the
        // corner. Reading follows the actual focused plate, not the access point.
        var ray = player.GetNodeOrNull<RayCast3D>("Head/Camera3D/InteractionRay");
        if (ray is null) return false;
        ray.ForceRaycastUpdate();
        if (ray.GetCollider() is not InteractionTarget target
            || target.GetParent() is not AddressSignVisualComponent sign
            || sign.AddressId != addressId) return false;
        var camera = player.GetNodeOrNull<Camera3D>("Head/Camera3D");
        if (camera is null || !sign.IsVisibleInTree()
            || sign.GlobalBasis.Z.Normalized().Dot(camera.GlobalPosition - sign.GlobalPosition) <= .02f)
            return false;
        var props = SelectWorldProps();
        if (props.TryGetProperty("address/" + addressId, out var prior)
            && prior.TryGetProperty("read", out var read) && read.ValueKind == JsonValueKind.True) return true;
        if (!await DispatchWorldPropsAsync(new JsonArray(new JsonObject
            { ["propId"] = "address/" + addressId, ["read"] = true }))) return false;
        await SaveCheckpointAsync(force: true);
        return true;
    }

    public string PersonalNotebookText()
    {
        if (_kernel is null) return string.Empty;
        var props = SelectWorldProps();
        return props.TryGetProperty("notebook/personal", out var page) && page.TryGetProperty("text", out var text)
            ? text.GetString() ?? string.Empty : string.Empty;
    }

    public async Task<bool> SavePersonalNotebookAsync(string text)
    {
        if (_kernel is null || text.Length > 12000) return false;
        var session = _kernel;
        if (PersonalNotebookText() != text && !await DispatchWorldPropsAsync(new JsonArray(new JsonObject
            { ["propId"] = "notebook/personal", ["text"] = text }))) return false;
        if (!ReferenceEquals(session, _kernel)) return false;
        // An explicit write must report disk failure and permit retry even when
        // the previous attempt already committed the text to the runtime.
        return await SaveSlotAsync(CheckpointSlot) && ReferenceEquals(session, _kernel);
    }
}
