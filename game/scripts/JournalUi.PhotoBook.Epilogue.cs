using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

public partial class JournalUi
{
    private const string EpilogueKeepsakePropId = "photobook/family-epilogue-keepsake";
    private VBoxContainer? _epilogueKeepsakeSection;
    private Label? _epilogueKeepsakePrompt, _epilogueKeepsakeCaption, _epilogueKeepsakeStatus;
    private TextureRect? _epilogueFieldPrintPreview;
    private Button? _epilogueKeepPhotoButton, _epilogueKeepCaptionButton, _epilogueKeepFieldButton, _epilogueKeepRetryButton;
    private string? _epiloguePendingPhotoId, _epiloguePendingKind;
    private object? _epilogueSelectionSession;
    private bool _epiloguePropsCommitted, _epilogueSelectionPending;

    private void RefreshPhotoBookEpilogueResult(RuntimeBridge.PhotoBookPhoto photo)
    {
        if (_bridge is not { } bridge || !PhotoWorldEpiloguePresence.HasRecordedBookResult(bridge))
        {
            if (_epilogueKeepsakeSection is not null) _epilogueKeepsakeSection.Visible = false;
            return;
        }

        EnsureEpilogueKeepsakeSection();
        if (!ReferenceEquals(_epilogueSelectionSession, bridge.SessionIdentity))
        {
            _epilogueSelectionSession = bridge.SessionIdentity;
            _epilogueSelectionPending = false;
            _epiloguePropsCommitted = false;
            _epiloguePendingPhotoId = _epiloguePendingKind = null;
        }
        RefreshEpilogueKeepsakeChoice(bridge, photo);
        if (photo.PhotoId != "S-NOW") return;

        var prepared = bridge.PreparedCommonCaptionText();
        var final = bridge.CommonFrameNoteText();
        var result = "Итоговый разворот\n\nДо общего снимка:\n"
            + (string.IsNullOrWhiteSpace(prepared) ? "Подписи нет." : prepared)
            + "\n\nПосле возвращения:\n"
            + (string.IsNullOrWhiteSpace(final) ? "Пометы нет." : final);
        if (!_epilogueSelectionPending && TryReadEpilogueKeepsake(bridge,
                out var label, out var texture, out var caption, out var response))
        {
            _photoBookComparisonHeading.Text = "Сегодня и выбранное";
            _photoBookComparisonHeading.Visible = true;
            _photoBookComparisonRow.Visible = true;
            _photoBookSelectedPhotoLabel.Text = "Сегодняшний снимок";
            _photoBookSelectedPhotoLabel.Visible = true;
            _photoBookComparisonPhotoColumn.Visible = true;
            _photoBookComparisonPhotoLabel.Text = label;
            _photoBookComparisonPhotoLabel.Visible = true;
            _photoBookComparisonImage.Texture = texture;
            _photoBookComparisonImage.Visible = true;
            _photoBookSharedLandmark.Visible = false;
            result += "\n\nРешение Айдара: оставить рядом «" + label + "»."
                + "\nГөлсинә: " + response;
            if (!string.IsNullOrWhiteSpace(caption)) result += "\n\nОставленная подпись:\n" + caption;
        }
        else ClearEpiloguePhotoPair();

        result += "\n\nСегодняшний снимок добавлен в семейную книгу. О смерти Марата новых сведений не нашлось.";
        _photoBookCommonNote.Text = result;
        _photoBookCommonNote.Visible = true;
    }

    private void EnsureEpilogueKeepsakeSection()
    {
        if (_epilogueKeepsakeSection is not null) return;
        _epilogueKeepsakeSection = new VBoxContainer
        {
            Name = "FamilyEpilogueKeepsakeChoice", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Visible = false
        };
        _epilogueKeepsakeSection.AddThemeConstantOverride("separation", 8);
        _photoBookSpread.AddChild(new HSeparator { Name = "FamilyEpilogueKeepsakeSeparator" });
        _photoBookSpread.AddChild(_epilogueKeepsakeSection);
        _epilogueKeepsakeSection.AddChild(new Label { Text = "Семейный альбом", ThemeTypeVariation = UrmanUiTheme.Heading });
        _epilogueKeepsakePrompt = new Label
        {
            Name = "KeepsakePrompt", AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _epilogueKeepsakeSection.AddChild(_epilogueKeepsakePrompt);
        _epilogueFieldPrintPreview = new TextureRect
        {
            Name = "ForestFieldPrintPreview", CustomMinimumSize = new Vector2(0f, 140f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false
        };
        _epilogueKeepsakeSection.AddChild(_epilogueFieldPrintPreview);
        _epilogueKeepFieldButton = new Button { Text = "Положить мой снимок из леса рядом" };
        _epilogueKeepFieldButton.Pressed += () => _ = SaveEpilogueKeepsakeAsync("forest-field", "photo");
        _epilogueKeepsakeSection.AddChild(_epilogueKeepFieldButton);
        _epilogueKeepPhotoButton = new Button { Text = "Положить этот снимок рядом" };
        _epilogueKeepPhotoButton.Pressed += () =>
        {
            if (_photoBookSelectedId is { } photoId) _ = SaveEpilogueKeepsakeAsync(photoId, "photo");
        };
        _epilogueKeepsakeSection.AddChild(_epilogueKeepPhotoButton);
        _epilogueKeepsakeCaption = new Label
        {
            Name = "PreparedCaptionPreview", AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false
        };
        _epilogueKeepsakeSection.AddChild(_epilogueKeepsakeCaption);
        _epilogueKeepCaptionButton = new Button { Text = "Положить мою подпись рядом" };
        _epilogueKeepCaptionButton.Pressed += () => _ = SaveEpilogueKeepsakeAsync("P-COMMON", "caption");
        _epilogueKeepsakeSection.AddChild(_epilogueKeepCaptionButton);
        _epilogueKeepsakeStatus = new Label
        {
            Name = "KeepsakeStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false
        };
        _epilogueKeepsakeSection.AddChild(_epilogueKeepsakeStatus);
        _epilogueKeepRetryButton = new Button { Text = "Повторить сохранение", Visible = false };
        _epilogueKeepRetryButton.Pressed += RetryEpilogueKeepsakeSave;
        _epilogueKeepsakeSection.AddChild(_epilogueKeepRetryButton);
    }

    private void ClearEpiloguePhotoPair()
    {
        _photoBookComparisonHeading.Visible = false;
        var hasTodayPhoto = _photoBookFrontImage.Visible && _photoBookFrontImage.Texture is not null;
        _photoBookComparisonRow.Visible = hasTodayPhoto;
        _photoBookSelectedPhotoLabel.Text = hasTodayPhoto ? "Сегодняшний снимок" : string.Empty;
        _photoBookSelectedPhotoLabel.Visible = hasTodayPhoto;
        _photoBookComparisonPhotoColumn.Visible = false;
        _photoBookComparisonPhotoLabel.Text = string.Empty;
        _photoBookComparisonPhotoLabel.Visible = false;
        _photoBookComparisonImage.Texture = null;
        _photoBookComparisonImage.Visible = false;
        _photoBookSharedLandmark.Visible = false;
    }

    private void RefreshEpilogueKeepsakeChoice(RuntimeBridge bridge, RuntimeBridge.PhotoBookPhoto photo)
    {
        _epilogueKeepsakeSection!.Visible = true;
        _epilogueKeepPhotoButton!.Visible = photo.PhotoId != "S-NOW"
            && PhotoBookFrontTexture(bridge, photo.PhotoId) is not null;
        var caption = photo.PhotoId == "P-COMMON" ? bridge.PreparedCommonCaptionText() : string.Empty;
        _epilogueKeepsakeCaption!.Text = string.IsNullOrWhiteSpace(caption)
            ? string.Empty : "Моя подпись к этому снимку:\n" + caption;
        _epilogueKeepsakeCaption.Visible = photo.PhotoId == "P-COMMON" && !string.IsNullOrWhiteSpace(caption);
        _epilogueKeepCaptionButton!.Visible = photo.PhotoId == "P-COMMON"
            && !string.IsNullOrWhiteSpace(caption) && PhotoBookFrontTexture("P-COMMON") is not null;

        var fieldPrint = photo.PhotoId == "S-NOW" && bridge.HasForestFieldPrint
            ? bridge.DevelopForestFieldPrint() : null;
        _epilogueFieldPrintPreview!.Texture = fieldPrint;
        _epilogueFieldPrintPreview.Visible = fieldPrint is not null;
        _epilogueKeepFieldButton!.Visible = fieldPrint is not null;

        if (photo.PhotoId == "S-NOW")
        {
            var currentLabel = string.Empty;
            var hasSelection = !_epilogueSelectionPending
                && TryReadEpilogueKeepsake(bridge, out currentLabel, out _, out _, out _);
            var hasOtherPhoto = bridge.PhotoBookPhotos().Any(item => item.PhotoId != "S-NOW"
                && PhotoBookFrontTexture(bridge, item.PhotoId) is not null);
            var hasFieldPrint = bridge.HasForestFieldPrint && bridge.DevelopForestFieldPrint() is not null;
            _epilogueKeepsakePrompt!.Text = hasSelection
                ? $"Сейчас рядом: {currentLabel}. Выберите другой отпечаток слева, чтобы заменить его."
                : hasOtherPhoto || hasFieldPrint
                    ? "Выберите снимок слева, чтобы оставить его рядом с сегодняшним. Свой лесной кадр можно положить здесь."
                    : "Сегодняшний снимок останется в альбоме.";
        }
        else _epilogueKeepsakePrompt!.Text = $"Выбран «{photo.Title}». Положить рядом с сегодняшним снимком?";

        _epilogueKeepRetryButton!.Visible = _epiloguePendingPhotoId is not null && !_photoBookActionBusy;
        if (!_photoBookActionBusy && !_epilogueKeepRetryButton.Visible) _epilogueKeepsakeStatus!.Visible = false;
        SetEpilogueKeepsakeButtonsDisabled(_photoBookActionBusy);
    }

    private async Task SaveEpilogueKeepsakeAsync(string photoId, string kind)
    {
        if (_photoBookActionBusy || _bridge is not { } bridge || bridge.SessionIdentity is not { } session
            || !IsCurrentPhotoBookOperation(bridge, session) || !_screen.Visible
            || _tabs.CurrentTab != PhotoBookTabIndex || !_photoBookPage.Visible
            || !PhotoWorldEpiloguePresence.HasRecordedBookResult(bridge)
            || kind == "photo" && photoId != "forest-field" && _photoBookSelectedId != photoId
            || !CanSelectEpilogueKeepsake(bridge, photoId, kind)) return;

        _epilogueSelectionSession = session;
        _epiloguePendingPhotoId = photoId;
        _epiloguePendingKind = kind;
        _epiloguePropsCommitted = false;
        _epilogueSelectionPending = false;
        _photoBookActionBusy = true;
        _epilogueKeepsakeStatus!.Text = "Сохраняю семейный разворот…";
        _epilogueKeepsakeStatus.Visible = true;
        _epilogueKeepRetryButton!.Visible = false;
        SetEpilogueKeepsakeButtonsDisabled(true);
        RefreshPhotoBookActionButtons();
        try { await PersistEpilogueKeepsakeAsync(bridge, session); }
        catch (Exception error)
        {
            GD.PushWarning($"Family album selection failed: {error.Message}");
            if (ReferenceEquals(session, bridge.SessionIdentity)) ShowEpilogueKeepsakeSaveError("Выбор не сохранился. Можно попробовать ещё раз.");
        }
        finally
        {
            _photoBookActionBusy = false;
            if (IsInsideTree() && GodotObject.IsInstanceValid(this)) RefreshPhotoBookActionButtons();
            SetEpilogueKeepsakeButtonsDisabled(false);
        }
    }

    private async Task PersistEpilogueKeepsakeAsync(RuntimeBridge bridge, object session)
    {
        if (_epiloguePendingPhotoId is not { } photoId || _epiloguePendingKind is not { } kind
            || !ReferenceEquals(session, bridge.SessionIdentity)) return;
        if (!_epiloguePropsCommitted)
        {
            var placement = new JsonArray(new JsonObject
            {
                ["propId"] = EpilogueKeepsakePropId, ["photoId"] = photoId, ["kind"] = kind
            });
            var dispatched = await bridge.DispatchWorldPropsAsync(placement);
            if (!ReferenceEquals(session, bridge.SessionIdentity)) return;
            if (!dispatched)
            {
                ShowEpilogueKeepsakeSaveError("Не удалось записать выбор. Можно повторить сохранение.");
                return;
            }
            _epiloguePropsCommitted = true;
            _epilogueSelectionPending = true;
        }
        var saved = await bridge.SaveCheckpointAsync(force: true);
        if (!ReferenceEquals(session, bridge.SessionIdentity)) return;
        if (!saved)
        {
            ShowEpilogueKeepsakeSaveError("Выбор записан, но автосохранение не удалось. Повторите сохранение.");
            return;
        }

        _epilogueSelectionPending = _epiloguePropsCommitted = false;
        _epiloguePendingPhotoId = _epiloguePendingKind = null;
        if (_screen.Visible && ReferenceEquals(_bridge, bridge))
        {
            _photoBookSelectedId = "S-NOW";
            RefreshPhotoBookPage();
            _photoBookActionStatus.Text = "Выбор сохранён в семейной книге.";
        }
    }

    private async void RetryEpilogueKeepsakeSave()
    {
        if (_photoBookActionBusy || _epiloguePendingPhotoId is null || _bridge is not { } bridge
            || bridge.SessionIdentity is not { } session || !IsCurrentPhotoBookOperation(bridge, session)) return;
        _photoBookActionBusy = true;
        _epilogueKeepsakeStatus!.Text = "Повторяю сохранение…";
        SetEpilogueKeepsakeButtonsDisabled(true);
        try { await PersistEpilogueKeepsakeAsync(bridge, session); }
        catch (Exception error)
        {
            GD.PushWarning($"Family album selection retry failed: {error.Message}");
            if (ReferenceEquals(session, bridge.SessionIdentity)) ShowEpilogueKeepsakeSaveError("Автосохранение не удалось. Можно попробовать ещё раз.");
        }
        finally
        {
            _photoBookActionBusy = false;
            if (IsInsideTree() && GodotObject.IsInstanceValid(this)) RefreshPhotoBookActionButtons();
            SetEpilogueKeepsakeButtonsDisabled(false);
        }
    }

    private void ShowEpilogueKeepsakeSaveError(string message)
    {
        _epilogueKeepsakeStatus!.Text = message;
        _epilogueKeepsakeStatus.Visible = true;
        _epilogueKeepRetryButton!.Visible = true;
    }

    private void SetEpilogueKeepsakeButtonsDisabled(bool disabled)
    {
        if (_epilogueKeepPhotoButton is not null) _epilogueKeepPhotoButton.Disabled = disabled;
        if (_epilogueKeepCaptionButton is not null) _epilogueKeepCaptionButton.Disabled = disabled;
        if (_epilogueKeepFieldButton is not null) _epilogueKeepFieldButton.Disabled = disabled;
        if (_epilogueKeepRetryButton is not null) _epilogueKeepRetryButton.Disabled = disabled;
    }

    private static bool CanSelectEpilogueKeepsake(RuntimeBridge bridge, string photoId, string kind) => kind switch
    {
        "caption" => photoId == "P-COMMON" && !string.IsNullOrWhiteSpace(bridge.PreparedCommonCaptionText())
            && bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == "P-COMMON")
            && PhotoBookFrontTexture("P-COMMON") is not null,
        "photo" when photoId == "forest-field" => bridge.HasForestFieldPrint && bridge.DevelopForestFieldPrint() is not null,
        "photo" => photoId != "S-NOW" && bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == photoId)
            && PhotoBookFrontTexture(bridge, photoId) is not null,
        _ => false
    };

    private static bool TryReadEpilogueKeepsake(RuntimeBridge bridge, out string label,
        out Texture2D texture, out string caption, out string response)
    {
        label = caption = response = string.Empty;
        texture = null!;
        var props = bridge.SelectWorldProps();
        if (!props.TryGetProperty(EpilogueKeepsakePropId, out var record)
            || record.ValueKind != JsonValueKind.Object
            || !record.TryGetProperty("photoId", out var photo)
            || photo.ValueKind != JsonValueKind.String
            || !record.TryGetProperty("kind", out var kind)
            || kind.ValueKind != JsonValueKind.String) return false;
        var photoId = photo.GetString() ?? string.Empty;
        var type = kind.GetString() ?? string.Empty;
        if (!CanSelectEpilogueKeepsake(bridge, photoId, type)) return false;
        if (photoId == "forest-field")
        {
            label = "Мой снимок из леса";
            texture = bridge.DevelopForestFieldPrint()!;
            response = "Ты сам снял это место. Пусть твой кадр останется в книге.";
        }
        else if (type == "caption")
        {
            label = "Подпись к снимку двора";
            texture = PhotoBookFrontTexture("P-COMMON")!;
            caption = bridge.PreparedCommonCaptionText();
            response = "Эти слова твои. Оставим их рядом с отпечатком.";
        }
        else
        {
            var selected = bridge.PhotoBookPhotos().FirstOrDefault(item => item.PhotoId == photoId);
            label = string.IsNullOrWhiteSpace(selected?.Title) ? "Снимок из семейной книги" : selected.Title;
            texture = PhotoBookFrontTexture(bridge, photoId)!;
            response = photoId switch
            {
                "P-FOREST" => "Лесной снимок оставим рядом с сегодняшним.",
                "P-COMMON" => "Этот отпечаток оставим рядом с сегодняшним.",
                _ => "Хорошо, этот снимок останется рядом."
            };
        }
        return true;
    }
}
