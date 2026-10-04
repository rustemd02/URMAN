using Godot;
using System.Text.RegularExpressions;

namespace Urman.Godot;

public partial class OldPcUi
{
    // The social page splits and re-scans document markdown on every render; the
    // patterns are parsed and compiled once instead of through the static Regex
    // cache and its lock per call. Same patterns and same options as before.
    private static readonly Regex SocialParagraphSplit = new(@"\r?\n\s*\r?\n", RegexOptions.Compiled);
    private static readonly Regex SocialAuthorLine = new(@"^([\p{L}\p{M}][\p{L}\p{M} .’'-]{0,41}):\s*(.+)$",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private ScrollContainer _socialScroll = null!;
    private VBoxContainer _socialFeed = null!;
    internal bool SocialPageVisible => _socialScroll is not null && _socialScroll.IsVisibleInTree();

    private void BuildSocialBrowser(Control parent)
    {
        _socialScroll = new ScrollContainer { Name = "SocialPage", Visible = false,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All };
        parent.AddChild(_socialScroll);
        _socialFeed = new VBoxContainer { Name = "Feed", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _socialFeed.AddThemeConstantOverride("separation", 12);
        _socialScroll.AddChild(_socialFeed);
    }

    private void ResetSocialPage()
    {
        if (_socialScroll is null) return;
        _socialScroll.Hide();
        foreach (var child in _socialFeed.GetChildren()) { _socialFeed.RemoveChild(child); child.QueueFree(); }
        _browserReader.Show();
    }

    private void BeginSocialPage()
    {
        ResetSocialPage();
        _socialScroll.Show();
        _socialScroll.ScrollVertical = 0;
        _browserReader.Hide();
        var brand = new PanelContainer { Name = "Masthead" };
        brand.SetMeta("socialRole", "brand");
        _socialFeed.AddChild(brand);
        var row = new HBoxContainer();
        brand.AddChild(row);
        row.AddChild(new Label { Name = "Brand", Text = "ЯЛКЫН", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        row.AddChild(new Label { Text = "Люди рядом · Кешеләр" });
    }

    private bool RenderSocialDocument(OldPcDocumentContent document)
    {
        if (!document.SearchTerms.Any(term => term is "view:profile" or "view:community" or "view:post"))
            return false;
        BeginSocialPage();
        var profile = document.SearchTerms.Contains("view:profile");
        var section = "";
        var entryInSection = 0;
        foreach (var block in SocialParagraphSplit.Split(document.BodyMarkdown.Trim()))
        {
            var text = block.Trim();
            if (text.Length == 0) continue;
            if (text.StartsWith("# ", StringComparison.Ordinal))
            {
                var newline = text.IndexOf('\n');
                var name = (newline < 0 ? text[2..] : text[2..newline]).Trim();
                var subtitle = newline < 0 ? "" : text[(newline + 1)..].Trim();
                var panel = SocialPanel(profile ? "profile" : "community");
                var row = new HBoxContainer { Name = "Identity" };
                panel.AddChild(row);
                row.AddChild(new OldPcDesktopIcon { Name = "Avatar", Kind = profile ? "person" : "pictures",
                    CustomMinimumSize = new(70, 70), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin });
                var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddChild(words);
                words.AddChild(new Label { Name = "IdentityName", Text = name, AutowrapMode = TextServer.AutowrapMode.WordSmart });
                if (subtitle.Length > 0) words.AddChild(new Label { Text = subtitle, AutowrapMode = TextServer.AutowrapMode.WordSmart });
                continue;
            }
            if (text.StartsWith("## ", StringComparison.Ordinal))
            {
                var newline = text.IndexOf('\n');
                section = (newline < 0 ? text[3..] : text[3..newline]).Trim();
                entryInSection = 0;
                _socialFeed.AddChild(new Label { Name = "Section", Text = section, AutowrapMode = TextServer.AutowrapMode.WordSmart });
                text = newline < 0 ? "" : text[(newline + 1)..].Trim();
                if (text.Length == 0) continue;
            }
            // A source link contains a colon too. Only a human name/label can
            // introduce an author; keep a complete Markdown link for the shared
            // source-link renderer and its access conditions.
            var author = SocialAuthorLine.Match(text);
            var isComment = section == "Комментарии" || author.Success && entryInSection > 0;
            var role = isComment ? "comment" : section.Length == 0 && profile ? "bio" : "post";
            var card = SocialPanel(role);
            var content = new VBoxContainer { Name = "Content" };
            card.AddChild(content);
            if (author.Success)
            {
                content.AddChild(new Label { Name = "Author", Text = author.Groups[1].Value,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart });
                text = author.Groups[2].Value;
            }
            var reader = SocialText(content, text);
            reader.SetMeta("sourceDocumentId", document.Id);
            entryInSection++;
        }
        if (document.Images is { Count: > 0 })
        {
            var card = SocialPanel("photo");
            var imageArea = new VBoxContainer { CustomMinimumSize = new(0, 340),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            card.AddChild(imageArea);
            var description = SocialText(imageArea, "Фотография из этой записи.");
            var images = DocumentImageReader.Attach(description);
            images.SetImages(document.Images);
            images.ShowPicture(true);
        }
        ApplySocialPresentation();
        return true;
    }

    private void RenderSocialIndex()
    {
        if (_bridge is null) return;
        BeginSocialPage();
        _socialFeed.AddChild(new Label { Text = "Страницы жителей и старые сообщества",
            AutowrapMode = TextServer.AutowrapMode.WordSmart });
        foreach (var document in _bridge.OldPcDocuments.Where(document => _bridge.IsOldPcDocumentAccessible(document.Id)
                     && (document.SearchTerms.Contains("site:yalkyn") || document.Section == "saved_messages"
                         && !document.SearchTerms.Contains("site:mail"))))
        {
            var card = SocialPanel(document.SearchTerms.Contains("view:profile") ? "profile" : "post");
            SocialText(card, "[" + document.Title + "](doc:" + document.Id + ")");
        }
        ApplySocialPresentation();
    }

    private PanelContainer SocialPanel(string role)
    {
        var wrapper = new MarginContainer();
        if (role == "comment") wrapper.AddThemeConstantOverride("margin_left", 42);
        _socialFeed.AddChild(wrapper);
        var panel = new PanelContainer { Name = role == "comment" ? "Comment" : "Card",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.SetMeta("socialRole", role);
        wrapper.AddChild(panel);
        return panel;
    }

    private RichTextLabel SocialText(Node parent, string text)
    {
        var reader = new RichTextLabel { Name = "Text", Text = RenderLinkedSource(text),
            BbcodeEnabled = true, FitContent = true, ScrollActive = false, SelectionEnabled = true,
            FocusMode = Control.FocusModeEnum.All, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        parent.AddChild(reader);
        reader.MetaClicked += meta => ShowBrowserAddress(meta.AsString());
        return reader;
    }

    private void ApplySocialPresentation()
    {
        if (_socialScroll is null) return;
        var high = _accessibility.HighContrast;
        var scale = (float)_accessibility.TextScale;
        var ink = high ? Colors.White : new Color("293d4d");
        foreach (var node in _socialFeed.FindChildren("*", "Control", true, false))
        {
            if (node is PanelContainer panel && panel.HasMeta("socialRole"))
            {
                var role = panel.GetMeta("socialRole").AsString();
                var color = role == "brand" ? "456d87" : high ? "172430"
                    : role == "comment" ? "e7eef2" : role is "profile" or "community" ? "d9e5ed" : "fffef8";
                panel.AddThemeStyleboxOverride("panel", DesktopStyle(color, high ? "91a9bc" : "b9c9d2", 1, 16));
            }
            if (node is Label label)
            {
                var brand = label.Name == "Brand" || label.GetParent().GetParent() is PanelContainer { Name: var name } && name == "Masthead";
                label.AddThemeColorOverride("font_color", brand ? Colors.White : ink);
                label.AddThemeFontSizeOverride("font_size", (int)((label.Name == "IdentityName" || label.Name == "Brand" ? 28 : 22) * scale));
                label.AddThemeConstantOverride("shadow_offset_x", 0);
                label.AddThemeConstantOverride("shadow_offset_y", 0);
            }
            if (node is RichTextLabel rich)
            {
                rich.AddThemeColorOverride("default_color", ink);
                rich.AddThemeColorOverride("font_selected_color", high ? Colors.White : new Color("183c5d"));
                rich.AddThemeColorOverride("selection_color", new Color("7faacb99"));
                foreach (var metric in new[] { "normal_font_size", "bold_font_size", "italics_font_size", "bold_italics_font_size", "mono_font_size" })
                    rich.AddThemeFontSizeOverride(metric, (int)(24 * scale));
                rich.AddThemeConstantOverride("line_separation", 4);
                rich.AddThemeStyleboxOverride("normal", DesktopStyle("00000000", "00000000", 0, 0));
                rich.AddThemeStyleboxOverride("focus", DesktopFocus(high));
            }
            if (node is Button button)
            {
                button.AddThemeFontSizeOverride("font_size", (int)(22 * scale));
                button.AddThemeColorOverride("font_color", ink);
                button.AddThemeColorOverride("font_hover_color", ink);
                button.AddThemeColorOverride("font_pressed_color", ink);
                button.AddThemeColorOverride("font_focus_color", ink);
                button.AddThemeConstantOverride("outline_size", 0);
                button.AddThemeStyleboxOverride("normal", DesktopStyle(high ? "172430" : "eeeadd", "91a9bc", 1, 6));
                button.AddThemeStyleboxOverride("hover", DesktopStyle(high ? "254461" : "dae5ed", "5685ad", 1, 6));
                button.AddThemeStyleboxOverride("pressed", DesktopStyle(high ? "254461" : "bed3e6", "366995", 1, 6));
                button.AddThemeStyleboxOverride("focus", DesktopFocus(high));
            }
        }
    }
}
