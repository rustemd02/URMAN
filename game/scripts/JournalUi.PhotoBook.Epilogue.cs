namespace Urman.Godot;

public partial class JournalUi
{
    private void RefreshPhotoBookEpilogueResult(RuntimeBridge.PhotoBookPhoto photo)
    {
        if (photo.PhotoId != "S-NOW" || _bridge is not { } bridge
            || !PhotoWorldEpiloguePresence.HasRecordedBookResult(bridge)) return;

        var prepared = bridge.PreparedCommonCaptionText();
        var final = bridge.CommonFrameNoteText();
        _photoBookCommonNote.Text = "Итоговый разворот\n\nДо общего снимка:\n"
            + (string.IsNullOrWhiteSpace(prepared) ? "Подписи нет." : prepared)
            + "\n\nПосле возвращения:\n"
            + (string.IsNullOrWhiteSpace(final) ? "Пометы нет." : final)
            + "\n\nСегодняшний снимок добавлен в семейную книгу. О смерти Марата новых сведений не нашлось.";
        _photoBookCommonNote.Visible = true;
    }
}
