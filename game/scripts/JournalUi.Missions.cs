using System;
using System.Linq;
using System.Text.Json;
using Urman.Core.Narrative;

namespace Urman.Godot;

public partial class JournalUi
{
    private const string MissionHomeZone = "house_old_pc";
    private const string MissionStreetZone = "village_day";
    private const string MissionPcAnchor = "ZoneHost/Act1ConnectedWorld/babay-abi-house/OldPc";
    private const string MissionBookAnchor = "ZoneHost/Act1ConnectedWorld/babay-abi-house/PhotoWorldFamilyBookTarget";

    internal static PlayerMission? CurrentPlayerMission(RuntimeBridge bridge)
    {
        if (bridge.SessionIdentity is null || bridge.IsPhotoBookProjectionDeferred) return null;
        var mainMission = ArrivalMission(bridge) ?? PhotoWorldMission(bridge) ?? PhotoWorldAftermathMission(bridge)
            ?? PhotoBookPreparationMission(bridge);
        if (mainMission is not null) return mainMission;
        return bridge.IsPhotoWorldsCampaign && bridge.CanInspectReceivedPhotoBook
            && bridge.ActiveSceneId == "urman.fullgame:scene/pw-home"
            && (bridge.PhotoWorldFactObserved("family_box_opened") || bridge.PhotoWorldFactObserved("cellar_photo_found"))
                ? PhotoBookCellarMission(bridge) : null;
    }

    // The selected page may explain an unfinished forest visit while the default
    // campaign route offers the river. Both views use the same authored gates.
    internal static string? PhotoBookPreparationObjectiveText(RuntimeBridge? bridge, string? selectedPhotoId = null) =>
        bridge is null ? null : PhotoBookPreparationMission(bridge, selectedPhotoId)?.Action;

    internal static bool WaterHomeResultPending(RuntimeBridge bridge)
        => HomeWorldResultPending(bridge, "urman.fullgame:quest/pw-w02",
            "urman.fullgame:knowledge/pw-water-home-account");

    internal static bool ForestHomeResultPending(RuntimeBridge bridge)
        => HomeWorldResultPending(bridge, "urman.fullgame:quest/pw-w01",
            "urman.fullgame:knowledge/pw-forest-inside");

    private static bool HomeWorldResultPending(RuntimeBridge bridge, string questId, string knowledgeId)
    {
        if (!bridge.IsPhotoWorldsCampaign || !bridge.CanInspectReceivedPhotoBook
            || bridge.SessionIdentity is not { } session)
            return false;

        var state = session.SelectState();
        var pending = PhotoWorldQuestStatus(state, questId) == "completed"
            && PhotoWorldQuestStatus(state, "urman.fullgame:quest/pw-w04") != "completed"
            && !MissionKnowledgeConfirmed(state, knowledgeId);
        return pending && ReferenceEquals(session, bridge.SessionIdentity);
    }

    private static PlayerMission? ArrivalMission(RuntimeBridge bridge)
    {
        if (bridge.ActiveSceneId == "urman.chapter1:scene/arrival_vehicle_dusk")
            return new("arrival-home", "Домой к семье", "Зайдите к бабаю и әби: они ждут к чаю.",
                "urman.chapter1:interaction/arrival-enter-house", TargetZoneId: bridge.CurrentZoneId, TargetLabel: "Дом бабая и әби");
        if (!bridge.IsPhotoWorldsCampaign || !bridge.CanShowFamilyBookOnTable) return null;
        if (bridge.ActiveSceneId == "urman.fullgame:scene/pw-niva-arrival")
            return bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-niva-stop")
                ? new("arrival-niva", "Разговор с бабаем", "Закончите разговор у Нивы.",
                    "urman.fullgame:interaction/pw-niva-stop", TargetLabel: "Нива") : null;
        if (bridge.ActiveSceneId != "urman.fullgame:scene/pw-home") return null;
        if (bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-book-handoff"))
            return HomeMission(bridge, "arrival-book", "Семейная книга", "Возьмите семейную книгу со стола.", "pw-book-handoff", "Семейная книга");
        if (bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-arrival-tea-talk"))
            return HomeMission(bridge, "arrival-tea", "Чай с семьёй", "Поговорите с Гөлсинә за чайным столом.", "pw-arrival-tea-talk", "Гөлсинә");
        return null;
    }

    private static PlayerMission? PhotoBookPreparationMission(RuntimeBridge bridge, string? selectedPhotoId = null)
    {
        if (!bridge.IsPhotoWorldsCampaign || !bridge.CanInspectReceivedPhotoBook
            || bridge.ActiveSceneId is not { } activeSceneId
            || (activeSceneId.StartsWith("urman.fullgame:scene/pw-", StringComparison.Ordinal)
                && activeSceneId is not ("urman.fullgame:scene/pw-home" or "urman.fullgame:scene/pw-niva-arrival")))
            return null;

        if (ForestHomeResultPending(bridge))
            return HomeMission(bridge, "first-return-talk", "Рассказать о лесном снимке",
                "Расскажите Гөлсинә, что нашли за корнями.",
                "pw-first-world-home-talk", "Гөлсинә");

        if (WaterHomeResultPending(bridge))
            return HomeMission(bridge, "water-return-talk", "Рассказать о старом спуске",
                "Расскажите Гөлсинә, где нашли старый спуск к мосткам.",
                "pw-first-world-home-talk", "Гөлсинә");

        var state = bridge.SelectRuntimeState();
        var forestQuestStatus = PhotoWorldQuestStatus(state, "urman.fullgame:quest/pw-w01");
        var waterQuestStatus = PhotoWorldQuestStatus(state, "urman.fullgame:quest/pw-w02");
        var houseQuestStatus = PhotoWorldQuestStatus(state, "urman.fullgame:quest/pw-w03");
        var activeObjectives = bridge.ActiveObjectives();
        var forestObjectiveIsActive = activeObjectives.Any(objective => objective.QuestId == "urman.fullgame:quest/pw-w01");
        var waterObjectiveIsActive = activeObjectives.Any(objective => objective.QuestId == "urman.fullgame:quest/pw-w02");
        var houseObjectiveIsActive = activeObjectives.Any(objective => objective.QuestId == "urman.fullgame:quest/pw-w03");
        var photos = bridge.PhotoBookPhotos();
        var forestPhoto = photos.FirstOrDefault(photo => photo.PhotoId == "P-FOREST");
        var schoolPhoto = photos.FirstOrDefault(photo => photo.PhotoId == "S-SCHOOL");
        var waterPhoto = photos.FirstOrDefault(photo => photo.PhotoId == "P-WATER");
        var waterRouteHasProgress = waterPhoto is not null
            || bridge.PhotoWorldFactObserved("home_water_place_told")
            || bridge.PhotoWorldFactObserved("water_photo_found");
        var selectedForestPhotoIsIncomplete = selectedPhotoId == "P-FOREST"
            && forestPhoto is { WorldCompleted: false };
        var selectedForestPhotoCanResume = selectedForestPhotoIsIncomplete
            && forestPhoto is { Mounted: true, HasResumePoint: true } && schoolPhoto is not null;
        if (selectedPhotoId == "P-CELLAR") return PhotoBookCellarMission(bridge);
        if (!selectedForestPhotoIsIncomplete && waterQuestStatus != "completed"
            && (waterObjectiveIsActive || waterRouteHasProgress))
            return PhotoBookWaterMission(bridge, requireFirstWorldHomeTalk: forestQuestStatus == "completed");
        if (selectedForestPhotoCanResume || (!selectedForestPhotoIsIncomplete && forestObjectiveIsActive))
            return ForestEntryMission(bridge, forestPhoto?.HasResumePoint == true);
        if (!selectedForestPhotoIsIncomplete && houseQuestStatus == "completed"
            && forestQuestStatus == "completed" && waterQuestStatus == "completed")
            return LaterCampaignMission(bridge);
        if (!selectedForestPhotoIsIncomplete && houseQuestStatus == "completed" && forestQuestStatus == "completed")
            return PhotoBookWaterMission(bridge);
        if (!selectedForestPhotoIsIncomplete && houseQuestStatus != "completed" && (houseObjectiveIsActive
            || bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-enter-house")))
            return PhotoBookHouseMission(bridge);
        if (!selectedForestPhotoIsIncomplete && houseQuestStatus != "completed" && waterQuestStatus == "completed")
            return PhotoBookHouseMission(bridge);
        if (!selectedForestPhotoIsIncomplete && forestQuestStatus == "completed")
            return PhotoBookWaterMission(bridge);
        if (forestPhoto is null) return null;
        if (schoolPhoto is null)
            return bridge.PhotoWorldFactObserved("school_copy_identified")
                ? StreetMission(bridge, "forest-school-copy", "Получить копию снимка", "В учительской попросите у сотрудницы копию лесного снимка.", "pw-school-copy-permission", "Сотрудница школы")
                : StreetMission(bridge, "forest-school-album", "Открыть школьный альбом", "Откройте альбом в учительской старой школы: Лесная, 12.", "pw-school-copy-source", "Школьный альбом");
        var canEnterForest = bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-enter-forest");
        if (forestPhoto.Mounted && canEnterForest) return ForestEntryMission(bridge, forestPhoto.HasResumePoint);
        if (!forestPhoto.Mounted && forestPhoto.CanMount)
            return new("forest-mount", "Лесной снимок", "Вклейте снимок в книгу.", BookPhotoId: forestPhoto.PhotoId);
        if (!forestPhoto.BackRead && forestPhoto.CanReadBack)
            return new("forest-back", "Оборот лесного снимка", "Прочитайте оборот снимка.", BookPhotoId: forestPhoto.PhotoId);
        if (bridge.CurrentZoneId != MissionHomeZone)
            return HomeMission(bridge, "forest-compare", "Сверить два снимка", "Дома сравните школьную копию с лесным снимком в семейном альбоме.", anchor: MissionBookAnchor);
        if (!forestPhoto.Mounted) return null;
        return canEnterForest ? ForestEntryMission(bridge, forestPhoto.HasResumePoint)
            : HomeMission(bridge, "forest-compare", "Сверить два снимка", "Сравните школьную копию с лесным снимком в семейном альбоме.", anchor: MissionBookAnchor);
    }

    private static PlayerMission ForestEntryMission(RuntimeBridge bridge, bool resume) =>
        StreetMission(bridge, "forest-enter", "Лесной снимок",
            $"У трёх стволов откройте книгу и выберите «{(resume ? "Продолжить снимок" : "Войти в снимок")}».",
            "pw-enter-forest", "Три ствола на опушке",
            "ZoneHost/Act1ConnectedWorld/Act1CoreWorldGreybox/PhotoForestEntry/ForestPhotoEntryAnchor",
            bookPhotoId: "P-FOREST");

    private static PlayerMission EntryMission(RuntimeBridge bridge, string id, string title,
        RuntimeBridge.PhotoBookPhoto photo, string interaction, string destination) =>
        StreetMission(bridge, id, title,
            $"Подойдите к {destination}. В книге выберите «{(photo.HasResumePoint ? "Продолжить снимок" : "Войти в снимок")}».",
            interaction, title, interaction switch
            {
                "pw-enter-river" => "ZoneHost/Act1ConnectedWorld/Act1CoreWorldGreybox/PhotoBankApproach/BankEntry",
                "pw-enter-common" => "ZoneHost/Act1ConnectedWorld/Act1CoreWorldGreybox/PhotoBankApproach/BankReturnWitness",
                _ => null
            }, bookPhotoId: photo.PhotoId);

    private static PlayerMission? PhotoBookWaterMission(RuntimeBridge bridge, bool requireFirstWorldHomeTalk = true)
    {
        var atHome = bridge.ActiveSceneId == "urman.fullgame:scene/pw-home" && bridge.CurrentZoneId == MissionHomeZone;
        if (requireFirstWorldHomeTalk && !bridge.PhotoWorldFactObserved("first_world_home_talk"))
            return !atHome || bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-first-world-home-talk")
                ? HomeMission(bridge, "first-return-talk", "Рассказать о лесном снимке", "Дома поговорите с Гөлсинә о том, что удалось увидеть в лесном снимке.", "pw-first-world-home-talk", "Гөлсинә") : null;
        var photos = bridge.PhotoBookPhotos();
        var waterPhoto = photos.FirstOrDefault(photo => photo.PhotoId == "P-WATER");
        var housePhoto = photos.FirstOrDefault(photo => photo.PhotoId == "P-HOUSE");
        var waterFound = bridge.PhotoWorldFactObserved("water_photo_found");
        var placeTold = bridge.PhotoWorldFactObserved("home_water_place_told");
        var housePhotoPending = bridge.PhotoWorldFactObserved("house_print_found") && housePhoto is null;
        var housePermissionPending = atHome && housePhotoPending
            && bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-house-photo-permission");
        PlayerMission HousePermission() => HomeMission(bridge, "house-permission", "Семейная копия", "Попросите у Гөлсинә копию снимка двора.", "pw-house-photo-permission", "Гөлсинә");
        PlayerMission WaterSource() => HomeMission(bridge, "water-source", "Снимок у мостков", "Найдите снимок у мостков в семейном конверте на столе.", "pw-water-photo-source", "Семейный конверт");
        PlayerMission PlaceTalk() => HomeMission(bridge, "water-place", "Где были мостки", "Спросите Гөлсинә, где находились старые мостки.", "pw-water-place-talk", "Гөлсинә");
        PlayerMission WaterPermission() => HomeMission(bridge, "water-permission", "Копия снимка у мостков", "Попросите у Гөлсинә разрешение на копию снимка у мостков.", "pw-water-photo-permission", "Гөлсинә");
        PlayerMission Mount() => new("water-mount", "Снимок у мостков", "Вклейте снимок в книгу.", BookPhotoId: waterPhoto!.PhotoId);
        PlayerMission Back() => new("water-back", "Оборот снимка у мостков", "Прочитайте оборот снимка.", BookPhotoId: waterPhoto!.PhotoId);
        if (placeTold && waterPhoto is { Mounted: true, BackRead: true })
            return EntryMission(bridge, "water-enter", "Старые мостки", waterPhoto, "pw-enter-river", "старому подходу к мосткам на берегу");
        if (!atHome)
        {
            if (housePhotoPending && waterFound) return HousePermission();
            if (housePhotoPending && !waterFound) return WaterSource();
            if (!placeTold) return PlaceTalk();
            if (!waterFound) return WaterSource();
            if (waterPhoto is null) return WaterPermission();
            if (!waterPhoto.Mounted) return Mount();
            return !waterPhoto.BackRead ? Back() : null;
        }
        if (!placeTold && !housePermissionPending && bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-water-place-talk")) return PlaceTalk();
        if (!waterFound && bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-water-photo-source")) return WaterSource();
        if (!placeTold) return housePermissionPending ? HousePermission() : null;
        if (waterPhoto is null)
            return housePermissionPending ? HousePermission()
                : bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-water-photo-permission") ? WaterPermission() : null;
        if (!waterPhoto.Mounted) return waterPhoto.CanMount ? Mount() : null;
        return !waterPhoto.BackRead && waterPhoto.CanReadBack ? Back() : null;
    }

    private static PlayerMission? PhotoBookHouseMission(RuntimeBridge bridge)
    {
        var atHome = bridge.ActiveSceneId == "urman.fullgame:scene/pw-home" && bridge.CurrentZoneId == MissionHomeZone;
        if (!bridge.PhotoWorldFactObserved("first_world_home_talk"))
        {
            var state = bridge.SelectRuntimeState();
            var waterOnlyCompleted = PhotoWorldQuestStatus(state, "urman.fullgame:quest/pw-w02") == "completed"
                && PhotoWorldQuestStatus(state, "urman.fullgame:quest/pw-w01") != "completed";
            return !atHome || bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-first-world-home-talk")
                ? HomeMission(bridge, "first-return-talk", waterOnlyCompleted ? "Старый спуск найден" : "Рассказать о лесном снимке",
                    waterOnlyCompleted ? "Найден старый спуск за мостками. Расскажите Гөлсинә, что удалось увидеть."
                        : "Дома поговорите с Гөлсинә о том, что удалось увидеть в лесном снимке.", "pw-first-world-home-talk", "Гөлсинә") : null;
        }
        var photo = bridge.PhotoBookPhotos().FirstOrDefault(photo => photo.PhotoId == "P-HOUSE");
        if (!bridge.PhotoWorldFactObserved("house_print_found"))
            return !atHome || bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-house-photo-source")
                ? HomeMission(bridge, "house-source", "Снимок семейного двора", "Найдите снимок двора в семейном конверте на столе.", "pw-house-photo-source", "Семейный конверт") : null;
        if (photo is null)
            return !atHome || bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-house-photo-permission")
                ? HomeMission(bridge, "house-permission", "Семейная копия", "Попросите у Гөлсинә копию снимка двора.", "pw-house-photo-permission", "Гөлсинә") : null;
        if (!photo.Mounted)
            return !atHome || photo.CanMount
                ? new("house-mount", "Снимок двора", "Вклейте снимок в книгу.", BookPhotoId: photo.PhotoId) : null;
        if (!photo.BackRead)
            return photo.CanReadBack ? new("house-back", "Оборот снимка двора", "Прочитайте оборот снимка.", BookPhotoId: photo.PhotoId) : null;
        return bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-enter-house")
            ? EntryMission(bridge, "house-enter", "Двор на старом снимке", photo, "pw-enter-house", "наружному окну семейного дома") : null;
    }

    private static PlayerMission? LaterCampaignMission(RuntimeBridge bridge)
    {
        if (PhotoWorldQuestStatus(bridge.SelectRuntimeState(), "urman.fullgame:quest/pw-w04") == "completed")
            return FinaleMission(bridge);
        var photos = bridge.PhotoBookPhotos();
        var common = photos.FirstOrDefault(photo => photo.PhotoId == "P-COMMON");
        // The two outside confirmations are compulsory inputs to the common file.
        // Evaluate their authored condition, rather than inferring from a visit.
        var state = bridge.SelectRuntimeState();
        if (!MissionEvidenceConfirmed(state, "E-BANK-OUTSIDE"))
            return BankEvidenceMission(bridge);
        if (!MissionEvidenceConfirmed(state, "E-PHOTOGRAPHER-OUTSIDE"))
        {
            var photographer = photos.FirstOrDefault(photo => photo.PhotoId == "S-PHOTOGRAPHER");
            if (!bridge.PhotoWorldFactObserved("photographer_copy_found"))
                return HomeMission(bridge, "photographer-source", "Кто снимал двор", "Найдите снимок с подписью фотографа в семейном альбоме.", "pw-photographer-photo-source", "Семейный альбом");
            if (photographer is null)
                return HomeMission(bridge, "photographer-permission", "Копия из альбома", "Попросите у Гөлсинә копию снимка с подписью фотографа.", "pw-photographer-photo-permission", "Гөлсинә");
            if (!photographer.BackRead)
                return photographer.CanReadBack
                    ? new("photographer-back", "Подпись фотографа", "Прочитайте оборот снимка.", BookPhotoId: photographer.PhotoId) : null;
            if (!bridge.PhotoWorldFactObserved("golsina_author_conversation"))
                return HomeMission(bridge, "photographer-talk", "Кто стоял за камерой", "Спросите Гөлсинә, кто снимал семейный двор.", "pw-gulsina-author-talk", "Гөлсинә");
            return HomeMission(bridge, "photographer-verify", "Сверить рассказ и снимок", "Сверьте рассказ Гөлсинә с подписью фотографа на снимке.", "pw-verify-photographer-evidence", "Семейный альбом");
        }
        if (!photos.Any(photo => photo.PhotoId == "S-PRINT"))
        {
            if (!bridge.PhotoWorldFactObserved("contact_sheet_found"))
                return HomeMission(bridge, "common-contact-sheet", "Контактный лист", "На ПК откройте «Семейные снимки» → «Контактный лист».", "pw-contact-sheet-source", "Домашний ПК", MissionPcAnchor);
            return HomeMission(bridge, "common-contact-copy", "Копия контактного листа", "На ПК сохраните копию контактного листа в книгу.", "pw-contact-sheet-permission", "Домашний ПК", MissionPcAnchor);
        }
        if (!bridge.PhotoWorldFactObserved("common_marat_file_found"))
            return HomeMission(bridge, "common-file", "Общий кадр Марата", "В папке «Семейные снимки» на ПК найдите «общий_кадр.jpg».", "pw-common-file-source", "Домашний ПК", MissionPcAnchor);
        if (common is null)
            return HomeMission(bridge, "common-permission", "Разрешение на копию", "Попросите у Гөлсинә разрешение скопировать общий кадр Марата.", "pw-common-file-permission", "Гөлсинә");
        if (!common.Mounted)
        {
            if (!MissionCommonCaptionPrepared(state))
                return bridge.CanPreparePhotoWorldCaption("P-COMMON")
                    ? HomeMission(bridge, "common-caption", "Своя подпись", "На ПК откройте «Блокнот», напишите подпись и нажмите «Составить подпись».", anchor: MissionPcAnchor) : null;
            if (!bridge.HasPrintedCommonPhoto)
                return HomeMission(bridge, "common-print", "Бумажный снимок", "В «Блокноте» на ПК нажмите «Напечатать подписанный снимок для книги».", "pw-print-common-photo", "Домашний ПК", MissionPcAnchor);
            if (bridge.CurrentZoneId != MissionHomeZone || common.CanMount)
                return new("common-mount", "Общий снимок", "Вклейте снимок в книгу.", BookPhotoId: common.PhotoId);
            return null;
        }
        if (!bridge.PhotoWorldFactObserved("alsu_bank_meeting_planned")
            && bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-alsu-before-w04-talk"))
            return StreetMission(bridge, "common-alsu", "Встреча с Алсу", "Поговорите с Алсу перед входом в общий снимок.", "pw-alsu-before-w04-talk", "Алсу");
        return bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-enter-common")
            ? EntryMission(bridge, "common-enter", "Общий снимок", common, "pw-enter-common", "месту общего снимка у берега") : null;
    }

    private static PlayerMission? BankEvidenceMission(RuntimeBridge bridge)
    {
        if (bridge.ActiveSceneId != "urman.fullgame:scene/pw-bank-approach")
            return StreetMission(bridge, "bank-visit", "Проверить старые мостки", "На берегу осмотрите старый подход к мосткам.", "pw-enter-bank-approach", "Подход к мосткам");
        if (!bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == "S-BANK"))
            return bridge.PhotoWorldFactObserved("bank_copy_found")
                ? StreetMission(bridge, "bank-copy", "Копия схемы берега", "В сельсовете попросите копию схемы старых мостков.", "pw-bank-copy-permission", "Схема в сельсовете")
                : StreetMission(bridge, "bank-source", "Схема старых мостков", "Найдите схему старых мостков на столе в сельсовете.", "pw-bank-copy-source", "Схема в сельсовете");
        if (!bridge.PhotoWorldFactObserved("winter_piles_inspected"))
            return StreetMission(bridge, "bank-piles", "Старые сваи", "Осмотрите старые сваи на зимнем берегу.", "pw-winter-piles-inspected", "Старые сваи");
        return StreetMission(bridge, "bank-verify", "Мостки на схеме и на берегу", "Сверьте старые сваи со схемой мостков.", "pw-verify-bank-evidence", "Старые сваи");
    }

    private static PlayerMission? PhotoWorldAftermathMission(RuntimeBridge bridge)
    {
        if (!bridge.IsPhotoWorldsCampaign || !bridge.CanInspectReceivedPhotoBook) return null;
        if (bridge.ActiveSceneId == "urman.fullgame:scene/pw-epilogue")
            return FinaleMission(bridge);
        if (bridge.ActiveSceneId != "urman.fullgame:scene/pw-bank-approach") return null;
        if (PhotoWorldQuestStatus(bridge.SelectRuntimeState(), "urman.fullgame:quest/pw-w04") == "completed")
            return FinaleMission(bridge);
        if (!MissionEvidenceConfirmed(bridge.SelectRuntimeState(), "E-BANK-OUTSIDE"))
            return BankEvidenceMission(bridge);
        return StreetMission(bridge, "bank-return", "Вернуться домой", "Вернитесь домой с берега. Там нужно сверить семейные снимки.", "pw-bank-return", "Дорога домой");
    }

    private static PlayerMission? FinaleMission(RuntimeBridge bridge)
    {
        if (bridge.ActiveSceneId == "urman.fullgame:scene/pw-epilogue")
        {
            if (!bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == "S-NOW"))
                return HomeMission(bridge, "epilogue-photo", "Новый семейный снимок", "Встаньте у чайного стола и снимите Гөлсинә, Мансура и чай.", "pw-take-now-photo", "Чайный стол",
                    "ZoneHost/Act1ConnectedWorld/babay-abi-house/PhotoWorldEpiloguePresence/SNowCameraStance");
            if (!MissionKnowledgeConfirmed(bridge.SelectRuntimeState(), "urman.fullgame:knowledge/pw-marat-death-question-remains-open"))
                return HomeMission(bridge, "epilogue-talk", "Разговор дома", "Расскажите Гөлсинә, что удалось узнать.", "pw-home-epilogue-talk", "Гөлсинә");
            return new("campaign-finished", "Семейная книга дополнена", "Новый семейный снимок сохранён в книге. Можно продолжить прогулку.");
        }
        if (bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == "S-NOW")
            && MissionKnowledgeConfirmed(bridge.SelectRuntimeState(), "urman.fullgame:knowledge/pw-marat-death-question-remains-open"))
            return new("campaign-finished", "Семейная книга дополнена", "Новый семейный снимок сохранён в книге. Можно продолжить прогулку.");
        if (bridge.ActiveSceneId != "urman.fullgame:scene/pw-bank-approach")
            return StreetMission(bridge, "finale-bank", "Встреча у берега", "Вернитесь к месту общего снимка на берегу.", "pw-enter-bank-approach", "Берег у мостков");
        if (!bridge.PhotoWorldFactObserved("outside_leaf_witnessed"))
            return StreetMission(bridge, "finale-leaf", "Летний лист на берегу", "Осмотрите летний лист на зимнем берегу.", "pw-witness-summer-leaf", "Летний лист");
        if (!bridge.PhotoWorldFactObserved("alsu_meeting_agreed"))
            return StreetMission(bridge, "finale-alsu", "Показать находку Алсу", "Покажите Алсу летний лист и договоритесь вернуться вместе.", "pw-alsu-leaf-talk", "Алсу");
        return StreetMission(bridge, "finale-home", "Дом после возвращения", "Вернитесь домой с берега, чтобы сделать новый семейный снимок.", "pw-bank-to-epilogue", "Дорога домой");
    }

    private static PlayerMission? PhotoBookCellarMission(RuntimeBridge bridge)
    {
        var photos = bridge.PhotoBookPhotos();
        var photo = photos.FirstOrDefault(photo => photo.PhotoId == "P-CELLAR");
        if (photo?.WorldCompleted == true) return null;
        if (!bridge.PhotoWorldFactObserved("family_box_opened"))
            return HomeMission(bridge, "cellar-family-box", "Семейный ящик", "Откройте семейный ящик со снимками на полке.", "pw-family-box-source", "Семейный ящик");
        if (!photos.Any(photo => photo.PhotoId == "S-HAND"))
            return HomeMission(bridge, "cellar-family-permission", "Снимок из ящика", "Попросите у Гөлсинә разрешение сохранить семейный снимок.", "pw-family-box-permission", "Гөлсинә");
        if (!bridge.PhotoWorldFactObserved("cellar_photo_found"))
            return HomeMission(bridge, "cellar-source", "Снимок перед уборкой", "Найдите снимок сеней в семейном ящике.", "pw-cellar-photo-source", "Семейный ящик");
        if (photo is null)
            return HomeMission(bridge, "cellar-permission", "Копия снимка сеней", "Попросите у Гөлсинә копию снимка сеней.", "pw-cellar-photo-permission", "Гөлсинә");
        if (!photo.Mounted)
            return bridge.CurrentZoneId != MissionHomeZone || photo.CanMount ? new("cellar-mount", "Снимок сеней", "Вклейте снимок в книгу.", BookPhotoId: photo.PhotoId) : null;
        return bridge.IsInteractionAvailable("urman.fullgame:interaction/pw-enter-cellar")
            ? EntryMission(bridge, "cellar-enter", "Сухие сени", photo, "pw-enter-cellar", "двери сухих сеней семейного дома") : null;
    }

    private static bool MissionEvidenceConfirmed(JsonElement state, string evidenceId) =>
        PhotoWorldState.EvaluateCondition(JsonSerializer.SerializeToElement(new { op = "photoworlds.evidence.confirmed", evidenceId }), state);

    private static bool MissionCommonCaptionPrepared(JsonElement state) => PhotoWorldState.EvaluateCondition(
        JsonSerializer.SerializeToElement(new { op = "photoworlds.caption.prepared", photoId = "P-COMMON", sourceAuthorId = "urman.chapter1:character/marat" }), state);

    private static bool MissionKnowledgeConfirmed(JsonElement state, string knowledgeId) =>
        state.TryGetProperty("knowledge", out var knowledge) && knowledge.ValueKind == JsonValueKind.Object
        && knowledge.TryGetProperty(knowledgeId, out var item) && item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty("status", out var status) && status.GetString() == "confirmed";

    private static PlayerMission HomeMission(RuntimeBridge bridge, string id, string title, string action,
        string? interaction = null, string? label = null, string? anchor = null) =>
        MissionInZone(bridge, id, title, action, MissionHomeZone, interaction, label, anchor);

    private static PlayerMission StreetMission(RuntimeBridge bridge, string id, string title, string action,
        string interaction, string label, string? anchor = null, string? bookPhotoId = null) =>
        MissionInZone(bridge, id, title, action, MissionStreetZone, interaction, label, anchor, bookPhotoId);

    private static PlayerMission MissionInZone(RuntimeBridge bridge, string id, string title, string action,
        string zone, string? interaction, string? label, string? anchor = null, string? bookPhotoId = null)
    {
        if (bridge.CurrentZoneId == MissionHomeZone && zone == MissionStreetZone)
            return new(id, title, "Выйдите во двор через входную дверь.", "urman.fullgame:interaction/pw-home-exit-house",
                TargetZoneId: bridge.CurrentZoneId, TargetLabel: "Выход во двор");
        if (bridge.CurrentZoneId != MissionHomeZone && zone == MissionHomeZone)
            return new(id, title, id == "arrival-tea"
                    ? "Зайдите в дом Гөлсинә и Мансура на чай."
                    : "Вернитесь в дом Гөлсинә и Мансура.",
                "urman.fullgame:interaction/pw-home-enter-house", TargetZoneId: bridge.CurrentZoneId, TargetLabel: "Дом Гөлсинә и Мансура");
        return new(id, title, action, interaction is null ? null : "urman.fullgame:interaction/" + interaction,
            anchor, zone, label ?? title, bookPhotoId);
    }

    private static PlayerMission? PhotoWorldMission(RuntimeBridge bridge)
    {
        if (bridge.CurrentPhotoWorldHint() is not { } hint) return null;
        // Keys come from the bridge's current earned hint. This table contains
        // presentation and authored spatial references, never progress rules.
        (string Title, string Action, string Interaction, string? Anchor, string Label)? target =
            (bridge.ActiveSceneId, hint.Key) switch
            {
                ("urman.fullgame:scene/pw-prologue", "prologue-branch") =>
                    ("Ветка поперёк тропы", "Подойдите к ветке и отведите её, чтобы пройти дальше.", "pw-prologue-branch-move", "ZoneHost/W01/Anchors/F01", "Ветка на тропе"),
                ("urman.fullgame:scene/pw-prologue", "prologue-exit") =>
                    ("Выход из леса", "Идите по тропе к светлой поляне.", "pw-prologue-f03-end", "ZoneHost/W01/Anchors/F03", "Светлая поляна"),
                ("urman.fullgame:scene/pw-forest", "camera") =>
                    ("Три ориентира на опушке", "У трёх стволов повернитесь так, чтобы видеть берёзу, перекладину и дугу корня сразу.", "pw-forest-group", "ZoneHost/W01/Anchors/F11", "За тремя стволами"),
                ("urman.fullgame:scene/pw-forest", "path") =>
                    ("Три ствола за корнем", "Пройдите под корнем, обернитесь и осмотрите три ствола по очереди.", "pw-forest-photographer", "ZoneHost/W01/Anchors/F13", "Поворот у корней"),
                ("urman.fullgame:scene/pw-forest", "talk") =>
                    ("Поговорить у корней", "Поговорите с незнакомцем у корня.", "pw-forest-shurale", "ZoneHost/W01/Anchors/F14/ShuraleRoot", "Незнакомец у корней"),
                ("urman.fullgame:scene/pw-forest", "bag") =>
                    ("Освободить сумку", "Снимите ремень с корня и поставьте сумку на землю.", "pw-forest-bag-release", "ZoneHost/W01/Anchors/F14/BagSnagContact", "Застрявшая сумка"),
                ("urman.fullgame:scene/pw-forest", "exit") =>
                    ("Вернуться к семье", "Идите по тропе к выходу из снимка.", "pw-forest-return", "ZoneHost/W01/Anchors/F16", "Выход из снимка"),
                ("urman.fullgame:scene/pw-river", "river-reflection") =>
                    ("Отражение мостков", "С берега посмотрите на отражение мостков.", "pw-water-reflection", "ZoneHost/W02/Anchors/R03", "Окно отражения"),
                ("urman.fullgame:scene/pw-river", "river-reed") =>
                    ("Проход в камыше", "Раздвиньте камыш у поворота, чтобы открыть берег.", "pw-water-bypass", null, "Камыш у поворота"),
                ("urman.fullgame:scene/pw-river", "river-approach") =>
                    ("Старый подход к мосткам", "Осмотрите подход к мосткам за камышом.", "pw-water-old-approach", null, "Старый подход к мосткам"),
                ("urman.fullgame:scene/pw-river", "river-encounter") =>
                    ("Дощечка у воды", RuntimeBridge.WaterEncounterActionCue, "pw-water-encounter", "ZoneHost/W02/W02World/FarPierLooseBoard", "Конец дальних мостков"),
                ("urman.fullgame:scene/pw-river", "river-damp-print") =>
                    ("Сырой снимок у берега", "Осмотрите снимок у выхода на летний берег.", "pw-water-damp-print", null, "Сырой снимок"),
                ("urman.fullgame:scene/pw-river", "river-exit") =>
                    ("Вернуться к семье", "Вернитесь домой через выход на летнем берегу.", "pw-water-return", null, "Выход с берега"),
                ("urman.fullgame:scene/pw-house", "house-shutter") =>
                    ("Закрытая ставня", "Откройте ставню у окна.", "pw-house-shutter", null, "Ставня"),
                ("urman.fullgame:scene/pw-house", "house-door-left") =>
                    ("Левый проход", "Пройдите через левую дверь и осмотрите двор с другой стороны.", "pw-house-door-left", "ZoneHost/W03/W03World/H02PassageFrame", "Левая дверь"),
                ("urman.fullgame:scene/pw-house", "house-door-right") =>
                    ("Правый проход", "Пройдите через правую дверь и осмотрите двор с другой стороны.", "pw-house-door-right", "ZoneHost/W03/W03World/H03PassageFrame", "Правая дверь"),
                ("urman.fullgame:scene/pw-house", "house-two-views") =>
                    ("Яблоня и садовый стул", "Осмотрите яблоню и садовый стул из обоих проходов.", "pw-house-two-views", "ZoneHost/W03/Anchors/H04", "Яблоня и садовый стул"),
                ("urman.fullgame:scene/pw-house", "house-camera") =>
                    ("Место фотографа", RuntimeBridge.HouseCameraActionCue, "pw-house-camera-position", "ZoneHost/W03/W03World/H05PhotographerWitness", "Место фотографа"),
                ("urman.fullgame:scene/pw-house", "house-cloth") =>
                    ("Ткань на окне", "Снимите ткань с окна и положите её на полку.", "pw-house-cloth", null, "Ткань на окне"),
                ("urman.fullgame:scene/pw-house", "house-frame") =>
                    ("Оконная рама", "Осмотрите раму после того, как снимете ткань.", "pw-house-frame", null, "Оконная рама"),
                ("urman.fullgame:scene/pw-house", "house-exit") =>
                    ("Вернуться к семье", "Вернитесь домой через выход из двора.", "pw-house-return", null, "Выход из снимка"),
                ("urman.fullgame:scene/pw-common", "common-places") =>
                    ("Лес, река и семейный дом", "С места общего снимка найдите лес, реку и семейный дом.", "pw-common-three-places", "ZoneHost/W04/Anchors/C03", "Место общего снимка"),
                ("urman.fullgame:scene/pw-common", "common-cloth") =>
                    ("Открыть вид на берег", "Отведите ткань, чтобы увидеть берег.", "pw-common-cloth", null, "Ткань перед берегом"),
                ("urman.fullgame:scene/pw-common", "common-horizon") =>
                    ("Линия горизонта", "Подойдите к открытому виду и посмотрите на горизонт.", "pw-common-horizon", "ZoneHost/W04/Anchors/C05", "Открытый вид"),
                ("urman.fullgame:scene/pw-common", "common-note") =>
                    ("Подпись к общему снимку", "У стола закончите подпись к общему снимку.", "pw-common-note", null, "Стол с подписью"),
                ("urman.fullgame:scene/pw-common", "common-exit") =>
                    ("Вернуться к семье", "Вернитесь домой через выход из общего снимка.", "pw-common-return", null, "Выход из снимка"),
                ("urman.fullgame:scene/pw-cellar", "cellar-order") =>
                    ("Следы на семейном столе", "Осмотрите сухой круг, затёртый край стола и сложенную ткань.", "pw-cellar-order", "ZoneHost/W05/W05World/B01HouseholdTable", "Стол со следами"),
                ("urman.fullgame:scene/pw-cellar", "cellar-object") =>
                    ("Вернуть семейную коробку", "Перенесите коробку с лавки на пустую подставку у полки.", "pw-cellar-object", "ZoneHost/W05/W05World/FamilyBoxReturnedPose", "Подставка для семейной коробки"),
                ("urman.fullgame:scene/pw-cellar", "cellar-response") =>
                    ("Чашка на столике", "Осмотрите переставленную чашку.", "pw-cellar-response", null, "Переставленная чашка"),
                ("urman.fullgame:scene/pw-cellar", "cellar-niche") =>
                    ("Ниша под полкой", "Осмотрите нишу под нижней полкой.", "pw-cellar-niche", null, "Ниша под полкой"),
                ("urman.fullgame:scene/pw-cellar", "cellar-exit") =>
                    ("Вернуться к семье", "Вернитесь к семье через дальний порог сеней.", "pw-cellar-return", null, "Дальний порог сеней"),
                _ => null
            };
        return target is { } step
            ? new("world-" + hint.Key, step.Title, step.Action, "urman.fullgame:interaction/" + step.Interaction,
                step.Anchor, TargetLabel: step.Label)
            : null;
    }

    private static string? PhotoWorldQuestStatus(JsonElement state, string questId)
    {
        if (!state.TryGetProperty("quests", out var quests) || quests.ValueKind != JsonValueKind.Object
            || !quests.TryGetProperty(questId, out var quest) || quest.ValueKind != JsonValueKind.Object
            || !quest.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String) return null;
        return status.GetString();
    }
}
