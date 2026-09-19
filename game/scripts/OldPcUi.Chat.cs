using Godot;
using Urman.Core.Capabilities.OldPc;

namespace Urman.Godot;

// «Ялкын · Сообщения»: thread list, delivered log, authored answers and the
// honest free-text stub. The script lives in authored chat content; this class
// owns only presentation state and never writes knowledge.
public partial class OldPcUi
{
    private ItemList _chatThreadList = null!;
    private RichTextLabel _chatLog = null!;
    private VBoxContainer _chatChoiceRow = null!;
    private LineEdit _chatInput = null!;
    private Label _chatStatus = null!;
    private string _chatThreadId = string.Empty;

    public string? ChatThreadId => string.IsNullOrEmpty(_chatThreadId) ? null : _chatThreadId;
    public IReadOnlyList<OldPcChatThreadSnapshot> ChatThreads => _desktop.Chat;
    public int ChatUnreadCount => _desktop.Chat.Count(thread => thread.Unread);
    public IReadOnlyList<string> VisibleChatIds => _bridge is null
        ? []
        : _bridge.OldPcChats.Where(chat => _bridge.EvaluateConditions(chat.Requires))
            .Select(chat => chat.Id).ToArray();

    // The snapshot stores the short thread id the state schema enumerates
    // ("alsu", "mansur", "self"), while the compiled script keeps its content
    // id; the mapping lives here so the capability never sees a content id.
    private static string SnapshotThreadId(CompiledChatContent chat) =>
        chat.Id[(chat.Id.LastIndexOf('/') + 1)..];

    private void BuildChat()
    {
        var body = CreateDesktopWindow("chat", "Ялкын · Сообщения", out _);
        var columns = new HBoxContainer { Name = "Columns", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddChild(columns);
        _chatThreadList = new ItemList { Name = "Threads", CustomMinimumSize = new Vector2(260, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddChild(_chatThreadList);
        _chatThreadList.ItemSelected += _ => SelectChatThread();
        var conversation = new VBoxContainer { Name = "Conversation", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddChild(conversation);
        _chatLog = new RichTextLabel { Name = "Log", SizeFlagsVertical = Control.SizeFlags.ExpandFill, ScrollFollowing = true };
        conversation.AddChild(_chatLog);
        _chatChoiceRow = new VBoxContainer { Name = "Choices" };
        conversation.AddChild(_chatChoiceRow);
        var input = new HBoxContainer { Name = "Input" };
        conversation.AddChild(input);
        _chatInput = new LineEdit { Name = "Text", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            PlaceholderText = "Написать…" };
        input.AddChild(_chatInput);
        _chatInput.TextSubmitted += _ => SendChatFreeText();
        DesktopButton(input, "Send", "Отправить", SendChatFreeText);
        _chatStatus = new Label { Name = "Status", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        conversation.AddChild(_chatStatus);
    }

    // Called when the computer opens, when the chat window opens and after every
    // answer: triggers are ordinary conditions, so a thread can receive a line
    // while the player is looking at another one - that is what marks it unread.
    private void RefreshChat()
    {
        if (_chatThreadList is null || _bridge is not { } bridge) return;
        foreach (var chat in bridge.OldPcChats.Where(chat => bridge.EvaluateConditions(chat.Requires)))
            DeliverChat(chat, active: chat.Id == _chatThreadId);
        if (string.IsNullOrEmpty(_chatThreadId) || !VisibleChatIds.Contains(_chatThreadId, StringComparer.Ordinal))
            _chatThreadId = VisibleChatIds.FirstOrDefault() ?? string.Empty;
        RebuildChatThreadList();
        ShowChatThread();
    }

    private void RebuildChatThreadList()
    {
        _chatThreadList.Clear();
        if (_bridge is not { } bridge) return;
        foreach (var chat in bridge.OldPcChats.Where(chat => bridge.EvaluateConditions(chat.Requires)))
        {
            var unread = Thread(SnapshotThreadId(chat), create: false)?.Unread == true;
            _chatThreadList.AddItem(chat.Title + (unread ? "  ●" : string.Empty));
            var index = _chatThreadList.ItemCount - 1;
            _chatThreadList.SetItemMetadata(index, chat.Id);
            _chatThreadList.SetItemTooltip(index, PresenceLabel(chat.Presence));
            if (chat.Id == _chatThreadId) _chatThreadList.Select(index);
        }
        var badge = ChatUnreadCount > 0 ? $" ●{ChatUnreadCount}" : string.Empty;
        (_shortcuts.GetNodeOrNull<Button>("Icon_chat"))?.SetDeferred("text", "Сообщения" + badge);
        if (_windows.TryGetValue("chat", out var window)) window.Task.Text = "Ялкын · Сообщения" + badge;
    }

    private static string PresenceLabel(string presence) => presence switch
    {
        "recently" => "был(а) недавно",
        "offline" => "не в сети",
        _ => "записи Айдара"
    };

    private void SelectChatThread()
    {
        var selected = _chatThreadList.GetSelectedItems();
        if (selected.Length == 0) return;
        _chatThreadId = _chatThreadList.GetItemMetadata(selected[0]).AsString();
        if (_bridge?.OldPcChats.FirstOrDefault(item => item.Id == _chatThreadId) is { } selectedChat)
            Thread(SnapshotThreadId(selectedChat), create: false)?.Unread = false;
        MarkDesktopChanged();
        RefreshChat();
    }

    private void ShowChatThread()
    {
        _chatLog.Text = string.Empty;
        foreach (var child in _chatChoiceRow.GetChildren()) { _chatChoiceRow.RemoveChild(child); child.QueueFree(); }
        if (_bridge is not { } bridge || string.IsNullOrEmpty(_chatThreadId))
        {
            _chatStatus.Text = "Выберите тред.";
            return;
        }
        var chat = bridge.OldPcChats.FirstOrDefault(item => item.Id == _chatThreadId);
        var thread = chat is null ? null : Thread(SnapshotThreadId(chat), create: false);
        if (chat is null || thread is null)
        {
            _chatStatus.Text = "Тред пуст.";
            return;
        }
        _chatStatus.Text = $"{PresenceLabel(chat.Presence)} · {thread.Messages.Count}/{OldPcDesktopSnapshot.MaximumChatMessages} сообщений";
        foreach (var message in thread.Messages)
            _chatLog.AppendText($"[b]{(message.From == "player" ? "Айдар" : chat.Title)}:[/b] {message.Text}\n");
        if (PendingChoiceNode(chat, thread) is not { } pending) return;
        foreach (var choice in pending.Choices.Where(choice => bridge.EvaluateConditions(choice.Requires)))
        {
            var answer = choice;
            DesktopButton(_chatChoiceRow, "Choice_" + answer.Id, bridge.ResolveText(answer.TextId),
                () => ChooseChatAnswer(chat, pending, answer));
        }
    }

    private CompiledChatNodeContent? PendingChoiceNode(CompiledChatContent chat, OldPcChatThreadSnapshot thread)
    {
        if (thread.NodeId is not { Length: > 0 } nodeId || !chat.Nodes.TryGetValue(nodeId, out var node)) return null;
        if (node.From != "npc" || node.Choices.Count == 0) return null;
        return thread.Messages.Any(message => message.NodeId == node.Id) ? node : null;
    }

    private void ChooseChatAnswer(CompiledChatContent chat, CompiledChatNodeContent node, CompiledChatChoiceContent choice)
    {
        if (_bridge is not { } bridge) return;
        var thread = Thread(SnapshotThreadId(chat), create: true)!;
        AppendChatMessage(thread, "player", bridge.ResolveText(choice.TextId), null);
        thread.NodeId = choice.NextNodeId ?? string.Empty;
        thread.Unread = false;
        MarkDesktopChanged();
        RefreshChat();
    }

    private void SendChatFreeText()
    {
        if (_bridge is not { } bridge || string.IsNullOrEmpty(_chatThreadId)) return;
        var text = _chatInput.Text.Trim();
        _chatInput.Text = string.Empty;
        if (text.Length == 0) return;
        if (bridge.OldPcChats.FirstOrDefault(item => item.Id == _chatThreadId) is not { } chat) return;
        var thread = Thread(SnapshotThreadId(chat), create: true)!;
        AppendChatMessage(thread, "player", text.Length > 500 ? text[..500] : text, null);
        // Free text never moves the script: it only earns the authored stub.
        AppendChatMessage(thread, "npc", bridge.ResolveText(chat.FreeTextReply), null);
        MarkDesktopChanged();
        RefreshChat();
    }

    private void DeliverChat(CompiledChatContent chat, bool active)
    {
        if (_bridge is not { } bridge) return;
        var thread = Thread(SnapshotThreadId(chat), create: false);
        var cursor = thread?.NodeId ?? chat.StartNodeId;
        for (var guard = 0; guard < 16; guard++)
        {
            if (cursor.Length == 0 || !chat.Nodes.TryGetValue(cursor, out var node)) return;
            if (thread is not null && thread.Messages.Any(message => message.NodeId == node.Id)) return;
            if (!bridge.EvaluateConditions(node.Requires)) return;
            thread = Thread(SnapshotThreadId(chat), create: true)!;
            AppendChatMessage(thread, node.From, bridge.ResolveText(node.TextId), node.Id);
            if (!active) thread.Unread = true;
            if (node.Choices.Count > 0)
            {
                thread.NodeId = node.Id;
                return;
            }
            cursor = node.NextNodeId ?? string.Empty;
            thread.NodeId = cursor;
        }
    }

    private OldPcChatThreadSnapshot? Thread(string id, bool create)
    {
        var thread = _desktop.Chat.FirstOrDefault(item => item.Id == id);
        if (thread is null && create)
        {
            thread = new OldPcChatThreadSnapshot { Id = id };
            _desktop.Chat.Add(thread);
        }
        return thread;
    }

    private static void AppendChatMessage(OldPcChatThreadSnapshot thread, string from, string text, string? nodeId)
    {
        if (thread.Messages.Count >= OldPcDesktopSnapshot.MaximumChatMessages) thread.Messages.RemoveAt(0);
        thread.Messages.Add(new OldPcChatMessageSnapshot { From = from, Text = text, NodeId = nodeId });
    }
}
