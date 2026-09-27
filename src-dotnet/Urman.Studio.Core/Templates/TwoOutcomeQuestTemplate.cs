using System.Text.Json.Nodes;
using Urman.Studio.Core.Editing;

namespace Urman.Studio.Core.Templates;

/// <summary>What the author fills in on the "choice with two outcomes" template form.</summary>
public sealed record TwoOutcomeQuestRequest(
    string Namespace,
    string ModuleDirectory,
    string CampaignPath,
    string QuestTitle,
    string NpcName,
    string NpcKitPrefix,
    double[] NpcPosition,
    double NpcYaw,
    string Request,
    string AcceptText,
    string RefuseText,
    string ItemName,
    double[] ItemPosition,
    string ItemCatalogId,
    string GateCatalogId,
    double[] GatePosition,
    double GateYaw,
    double[] TriggerPosition,
    double[] TriggerSize);

/// <summary>IDs of everything the template created, for navigation, tests and the developer context.</summary>
public sealed record TwoOutcomeQuestIds(
    string QuestId, string CharacterId, string DialogueId, string SceneId,
    string TalkInteractionId, string PickupInteractionId, string TriggerInteractionId,
    string NpcEntityId, string PickupEntityId, string GateEntityId, string TriggerEntityId,
    string ModuleFile, string WorldFile, string RoleKey);

/// <summary>
/// Template "Выбор с двумя исходами" (spec QUEST13): a character asks for
/// help; accepting sends the player for an item and repairs a gate, refusing
/// fails the quest with a named outcome and leaves the gate broken. Every
/// created thing is ordinary editable data with fresh IDs, made through one
/// undoable command: characters, texts, dialogue, scene interactions, the
/// quest with its branch, a world plot (NPC, pickup, gate with story states,
/// trigger area), the module manifest and the campaign role binding. The game
/// executes it with the existing runtime; nothing here is template-specific
/// at run time.
/// </summary>
public static class TwoOutcomeQuestTemplate
{
    public static TwoOutcomeQuestIds Create(EditSession session, TwoOutcomeQuestRequest request)
    {
        var workspace = session.Workspace;
        var ns = request.Namespace;
        string Id(string kind, string hint) => workspace.NewId(ns, kind, hint);
        var characterId = Id("character", "npc");
        var questId = Id("quest", "side");
        var dialogueId = Id("dialogue", "side");
        var sceneId = Id("scene", "side");
        var talkId = Id("interaction", "talk");
        var pickupId = Id("interaction", "take");
        var triggerId = Id("interaction", "notice");
        var slug = questId[(questId.LastIndexOf('/') + 1)..];
        var moduleFile = $"{request.ModuleDirectory}/{slug}.json";
        var worldFile = $"game/content/world/{slug}.world.v1.json";
        var plotId = $"urman.world:studio/{slug}";
        var roleKey = slug.Replace('.', '-');

        JsonObject State(string key, JsonNode value) => new()
        {
            ["op"] = "npc.state", ["characterId"] = characterId, ["stateKey"] = key, ["value"] = value
        };
        JsonObject SetState(string key, JsonNode value) => new()
        {
            ["op"] = "npc.set-state", ["characterId"] = characterId, ["stateKey"] = key, ["value"] = value
        };
        JsonObject Not(JsonObject condition) => new() { ["op"] = "not", ["condition"] = condition };

        using var command = session.Begin($"новый квест «{request.QuestTitle}»");
        workspace.CreateFile(moduleFile, "[\n]\n");
        workspace.CreateFile(worldFile, "{\n  \"schemaVersion\": 1,\n  \"kind\": \"urman.world-plot\",\n  \"id\": \"" + plotId + "\",\n  \"name\": \"" +
            request.QuestTitle.Replace("\"", "'") + "\",\n  \"executor\": \"generic\",\n  \"entities\": [\n  ]\n}\n");
        var created = new List<string>();
        void Put(JsonObject entity)
        {
            session.Set(moduleFile, (string)entity["id"]!, entity, "шаблон");
            created.Add((string)entity["id"]!);
        }

        string Text(string hint, string value, string purpose = "ui")
        {
            var id = Id("text", hint);
            Put(new JsonObject
            {
                ["schemaVersion"] = 1, ["id"] = id,
                ["value"] = new JsonObject { ["default"] = value, ["translations"] = new JsonObject { ["ru"] = value } },
                ["purpose"] = purpose
            });
            return id;
        }

        Put(new JsonObject
        {
            ["schemaVersion"] = 1, ["id"] = characterId,
            ["displayName"] = new JsonObject { ["default"] = request.NpcName, ["translations"] = new JsonObject { ["ru"] = request.NpcName } },
            ["roleTags"] = new JsonArray("villager"), ["assetIds"] = new JsonArray()
        });
        var title = Text("quest-title", request.QuestTitle);
        var talkObjective = Text("objective-talk", $"Ответить: {request.NpcName}");
        var refuseObjective = Text("objective-refuse", "Отказаться");
        var fetchObjective = Text("objective-fetch", $"Найти: {request.ItemName}");
        var returnObjective = Text("objective-return", $"Вернуться: {request.NpcName}");
        var askLine = Text("line-ask", request.Request, "dialogue");
        var acceptChoice = Text("choice-accept", request.AcceptText, "dialogue");
        var refuseChoice = Text("choice-refuse", request.RefuseText, "dialogue");
        var waitLine = Text("line-wait", "Ну что, нашёл?", "dialogue");
        var giveChoice = Text("choice-give", $"Отдать: {request.ItemName}", "dialogue");
        var thanksLine = Text("line-thanks", "Вот спасибо.", "dialogue");
        var refusedLine = Text("line-refused", "Ладно. Сам как-нибудь.", "dialogue");
        var talkLabel = Text("label-talk", "Поговорить");
        var takeLabel = Text("label-take", $"Взять: {request.ItemName}");
        var noticeLabel = Text("label-notice", "Осмотреться");

        static JsonObject Choice(string id, string textId, JsonArray conditions, JsonArray effects, string? next) => new()
        {
            ["id"] = id, ["textId"] = textId, ["conditions"] = conditions, ["effects"] = effects, ["nextNodeId"] = next
        };
        JsonObject Node(string id, string textId, JsonArray choices) => new()
        {
            ["id"] = id, ["speakerRole"] = roleKey, ["textId"] = textId, ["conditions"] = new JsonArray(), ["effects"] = new JsonArray(), ["choices"] = choices
        };
        Put(new JsonObject
        {
            ["schemaVersion"] = 1, ["id"] = dialogueId, ["participantRoles"] = new JsonArray("aidar", roleKey), ["startNodeId"] = "ask",
            ["entryRoutes"] = new JsonArray(
                new JsonObject { ["nodeId"] = "thanks", ["conditions"] = new JsonArray(State("delivered", true)) },
                new JsonObject { ["nodeId"] = "refused", ["conditions"] = new JsonArray(State("refused", true)) },
                new JsonObject { ["nodeId"] = "wait", ["conditions"] = new JsonArray(State("accepted", true)) }),
            ["nodes"] = new JsonArray(
                Node("ask", askLine, new JsonArray(
                    Choice("accept", acceptChoice, new JsonArray(), new JsonArray(SetState("accepted", true)), null),
                    Choice("refuse", refuseChoice, new JsonArray(), new JsonArray(SetState("refused", true)), null))),
                Node("wait", waitLine, new JsonArray(
                    Choice("give", giveChoice, new JsonArray(State("item_taken", true), Not(State("delivered", true))), new JsonArray(SetState("delivered", true)), "thanks"))),
                Node("thanks", thanksLine, new JsonArray()),
                Node("refused", refusedLine, new JsonArray()))
        });

        JsonObject Interaction(string id, string label, JsonArray conditions, JsonArray effects, string? dialogue = null)
        {
            var interaction = new JsonObject
            {
                ["id"] = id, ["labelTextId"] = label, ["conditions"] = conditions, ["effects"] = effects,
                ["worldLocations"] = new JsonArray("village_day")
            };
            if (dialogue is not null) interaction["targetDialogueId"] = dialogue;
            return interaction;
        }

        Put(new JsonObject
        {
            ["schemaVersion"] = 1, ["id"] = sceneId, ["sceneType"] = "presentation",
            ["title"] = new JsonObject { ["default"] = request.QuestTitle, ["translations"] = new JsonObject { ["ru"] = request.QuestTitle } },
            ["assetRefs"] = new JsonArray(), ["textRefs"] = new JsonArray(), ["entryConditions"] = new JsonArray(), ["onEnter"] = new JsonArray(), ["onExit"] = new JsonArray(),
            ["interactions"] = new JsonArray(
                Interaction(talkId, talkLabel, new JsonArray(), new JsonArray(), dialogueId),
                Interaction(pickupId, takeLabel, new JsonArray(State("accepted", true), Not(State("item_taken", true))), new JsonArray(SetState("item_taken", true))),
                Interaction(triggerId, noticeLabel, new JsonArray(Not(State("noticed", true))), new JsonArray(SetState("noticed", true))))
        });

        static JsonObject Objective(string id, string titleTextId, JsonObject completion, JsonArray? effects = null) => new()
        {
            ["id"] = id, ["titleTextId"] = titleTextId, ["optional"] = false,
            ["startConditions"] = new JsonArray(), ["completionConditions"] = new JsonArray(completion),
            ["completionEffects"] = effects ?? new JsonArray(), ["failureEffects"] = new JsonArray()
        };
        static JsonObject Transition(string id, JsonObject on, JsonObject target, JsonArray? effects = null) => new()
        {
            ["id"] = id, ["on"] = on, ["conditions"] = new JsonArray(), ["effects"] = effects ?? new JsonArray(), ["target"] = target
        };
        Put(new JsonObject
        {
            ["schemaVersion"] = 1, ["id"] = questId, ["titleTextId"] = title,
            ["stages"] = new JsonArray(
                new JsonObject
                {
                    ["id"] = "talk",
                    ["objectives"] = new JsonArray(Objective("accept", talkObjective, State("accepted", true)), Objective("refuse", refuseObjective, State("refused", true))),
                    ["composition"] = new JsonObject { ["mode"] = "any", ["objectiveIds"] = new JsonArray("accept", "refuse") },
                    ["transitions"] = new JsonArray(
                        Transition("accepted", new JsonObject { ["kind"] = "objective-complete", ["objectiveId"] = "accept" }, new JsonObject { ["stageId"] = "fetch" }),
                        Transition("refused", new JsonObject { ["kind"] = "objective-complete", ["objectiveId"] = "refuse" },
                            new JsonObject { ["end"] = "failed", ["outcomeId"] = "refused" }, new JsonArray(SetState("gate_broken", true))))
                },
                new JsonObject
                {
                    ["id"] = "fetch",
                    ["objectives"] = new JsonArray(Objective("take", fetchObjective, State("item_taken", true))),
                    ["composition"] = new JsonObject { ["mode"] = "all", ["objectiveIds"] = new JsonArray("take") }
                },
                new JsonObject
                {
                    ["id"] = "return",
                    ["objectives"] = new JsonArray(Objective("deliver", returnObjective, State("delivered", true), new JsonArray(SetState("gate_repaired", true)))),
                    ["composition"] = new JsonObject { ["mode"] = "all", ["objectiveIds"] = new JsonArray("deliver") }
                }),
            ["outcomes"] = new JsonObject { ["success"] = new JsonArray(), ["optional"] = new JsonArray(), ["failure"] = new JsonArray() },
            ["retryPolicy"] = new JsonObject { ["mode"] = "none", ["maximumAttempts"] = 1 },
            ["checkpointPolicy"] = new JsonObject { ["mode"] = "objective" },
            ["cancelPolicy"] = new JsonObject { ["allowed"] = false, ["effects"] = new JsonArray() }
        });

        // World plot: executed by the game's generic AuthoredWorldDirector.
        JsonArray Point(double[] value) => new(value.Select(number => (JsonNode?)number).ToArray());
        var npcEntity = $"{plotId}/npc";
        var pickupEntity = $"{plotId}/item";
        var gateEntity = $"{plotId}/gate";
        var triggerEntity = $"{plotId}/notice-area";
        void World(string id, string kind, string name, JsonObject parameters) =>
            session.Set(worldFile, id, new JsonObject { ["id"] = id, ["kind"] = kind, ["name"] = name, ["params"] = parameters }, "шаблон");
        World(npcEntity, "npc", request.NpcName, new JsonObject
        {
            ["characterId"] = characterId, ["kitPrefix"] = request.NpcKitPrefix, ["position"] = Point(request.NpcPosition), ["yawDegrees"] = request.NpcYaw, ["clip"] = "Idle",
            ["talk"] = new JsonObject { ["interactionId"] = talkId, ["dialogueId"] = dialogueId, ["prompt"] = $"Поговорить: {request.NpcName}" }
        });
        World(pickupEntity, "pickup", request.ItemName, new JsonObject
        {
            ["catalogId"] = request.ItemCatalogId, ["position"] = Point(request.ItemPosition), ["yawDegrees"] = 0, ["interactionId"] = pickupId, ["prompt"] = $"Взять: {request.ItemName}",
            ["states"] = new JsonArray(
                new JsonObject { ["id"] = "taken", ["name"] = "Взят", ["when"] = new JsonArray(State("item_taken", true)), ["visible"] = false },
                new JsonObject { ["id"] = "not-yet", ["name"] = "Ещё не нужен", ["when"] = new JsonArray(Not(State("accepted", true))), ["visible"] = false })
        });
        World(gateEntity, "prop", "Калитка", new JsonObject
        {
            ["catalogId"] = request.GateCatalogId, ["position"] = Point(request.GatePosition), ["yawDegrees"] = request.GateYaw, ["collision"] = "box",
            ["states"] = new JsonArray(
                new JsonObject { ["id"] = "repaired", ["name"] = "Починена", ["when"] = new JsonArray(State("gate_repaired", true)), ["visible"] = true, ["collision"] = true },
                new JsonObject { ["id"] = "broken", ["name"] = "Сломана", ["when"] = new JsonArray(State("gate_broken", true)), ["visible"] = false, ["collision"] = false })
        });
        World(triggerEntity, "trigger", "Место, где стоит осмотреться", new JsonObject
        {
            ["position"] = Point(request.TriggerPosition), ["size"] = Point(request.TriggerSize), ["actor"] = "player",
            ["repeat"] = "every-entry", ["countIfInside"] = true, ["interactionId"] = triggerId
        });

        foreach (var (key, label) in new[] { ("accepted", "согласился помочь"), ("refused", "отказался помогать"), ("item_taken", $"взял: {request.ItemName}"),
                     ("delivered", $"отдал: {request.ItemName}"), ("gate_repaired", "калитка починена"), ("gate_broken", "калитка сломана"), ("noticed", "осмотрелся на месте") })
        {
            ConditionPhrases.NameFact(session, characterId, key, label);
        }

        var outcomeLabel = $"{questId}#outcome:refused";
        session.Set(workspace.FactLabels().RelativePath, outcomeLabel, new JsonObject { ["id"] = outcomeLabel, ["questId"] = questId, ["outcomeId"] = "refused", ["label"] = "отказ помогать" }, "шаблон");

        // Manifest and role binding: the module lists its new file and IDs, the
        // campaign binds the dialogue role to the new character.
        var manifestPath = $"{request.ModuleDirectory}/module.json";
        var manifest = workspace.File(manifestPath);
        var sources = manifest.Get("sourceFiles")!.AsArray();
        sources.Add($"{slug}.json");
        session.Set(manifestPath, "sourceFiles", sources, "шаблон");
        var provides = manifest.Get("provides")!.AsArray();
        foreach (var id in created) provides.Add(id);
        session.Set(manifestPath, "provides", provides, "шаблон");
        var bindings = workspace.File(request.CampaignPath).Get("roleBindings")!.AsObject();
        bindings[roleKey] = characterId;
        session.Set(request.CampaignPath, "roleBindings", bindings, "шаблон");

        return new(questId, characterId, dialogueId, sceneId, talkId, pickupId, triggerId, npcEntity, pickupEntity, gateEntity, triggerEntity, moduleFile, worldFile, roleKey);
    }
}
