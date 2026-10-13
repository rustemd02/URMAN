using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace Urman.Studio.App;

public sealed partial class StudioWorldSection
{
    private void CopySelectionForCodex()
    {
        if (_reloading || studio.IsWorkspaceActionBusy || studio.IsAiActionBusy)
        {
            studio.ShowBanner("Дождитесь завершения текущего действия, чтобы скопировать согласованный снимок.", error: false);
            return;
        }
        SuspendForAi();
        EndPointPick();
        var ids = _group.Count > 0 ? _group.Order(StringComparer.Ordinal).ToArray()
            : studio.Selection is { } id ? new[] { id } : [];
        if (ids.Length == 0 && _fencePick is null)
        {
            studio.ShowBanner("Выберите объект, Shift+щелчком соберите группу или нажмите «Область для Codex…».", error: false);
            return;
        }
        OpenCodexHandoff(ids, region: null);
    }

    private void PickAreaForCodex()
    {
        if (!WorldReady || _reloading || studio.IsWorkspaceActionBusy || studio.IsAiActionBusy)
        {
            studio.ShowBanner("Сначала загрузите карту и дождитесь завершения текущего действия.", error: false);
            return;
        }
        SuspendForAi();
        _measuring = _sculpting = _brushing = false;
        _terrainButton.ThemeTypeVariation = _brushButton.ThemeTypeVariation = "";
        Vector3? first = null;
        StartPointPick("Область для Codex: первый угол на земле. Escape — отмена.", point =>
        {
            if (!IsFinite(point)) return;
            if (first is null)
            {
                first = point;
                _measureLabel.Text = "Область для Codex: противоположный угол. Escape — отмена.";
                return;
            }
            EndPointPick();
            var minimum = new Vector2(Mathf.Min(first.Value.X, point.X), Mathf.Min(first.Value.Z, point.Z));
            var maximum = new Vector2(Mathf.Max(first.Value.X, point.X), Mathf.Max(first.Value.Z, point.Z));
            if (maximum.X - minimum.X < .05f || maximum.Y - minimum.Y < .05f)
            {
                studio.ShowBanner("Область слишком узкая. Выберите два разных противоположных угла.", error: false);
                return;
            }
            var matches = WorldEntities().Where(item => DisplayAnchor(item.Id, item.Entity) is { } at
                && IsFinite(at) && at.X >= minimum.X && at.X <= maximum.X
                && at.Z >= minimum.Y && at.Z <= maximum.Y).Select(item => item.Id).ToArray();
            var mesh = new ImmediateMesh();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            var y = (first.Value.Y + point.Y) * .5f + .1f;
            var corners = new[] { new Vector3(minimum.X, y, minimum.Y), new Vector3(maximum.X, y, minimum.Y),
                new Vector3(maximum.X, y, maximum.Y), new Vector3(minimum.X, y, maximum.Y) };
            for (var index = 0; index < 4; index++)
            {
                mesh.SurfaceAddVertex(corners[index]);
                mesh.SurfaceAddVertex(corners[(index + 1) % 4]);
            }
            mesh.SurfaceEnd();
            var outline = new MeshInstance3D { Mesh = mesh, MaterialOverride = new StandardMaterial3D
                { AlbedoColor = new Color("ffcf66"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true } };
            _viewport.AddChild(outline);
            RequestViewportRender();
            OpenCodexHandoff(matches.Take(200).ToArray(), new
            {
                minXZ = new[] { minimum.X, minimum.Y }, maxXZ = new[] { maximum.X, maximum.Y },
                clickedPoints = new[] { Coordinates(first.Value), Coordinates(point) },
                selectionRule = "XZ rectangle, all heights; listed entities have their anchor inside, not necessarily their entire geometry",
                totalMatchingAnchors = matches.Length, omittedEntities = Math.Max(0, matches.Length - 200)
            }, outline);
        });
    }

    private static float[] Coordinates(Vector3 point) => [point.X, point.Y, point.Z];

    private void OpenCodexHandoff(string[] ids, object? region, MeshInstance3D? outline = null)
    {
        try
        {
            if (ids.Length > 200) throw new InvalidOperationException("Выберите не больше 200 объектов для одного запроса.");
            object? fence = null;
            if (region is null && ids.Length == 0 && _fencePick is { } pickedFence)
            {
                if (!TryGetFreshFencePick(pickedFence, out var current, out var error))
                    throw new InvalidOperationException("Выбранный забор устарел: " + error);
                var source = studio.Workspace.File(current.RelativePath).CaptureSource();
                if (!TryFindUniqueLot(source.Text, current.LotId, out _, out var lot, out error))
                    throw new InvalidOperationException(error);
                fence = new { sourcePath = current.RelativePath, sourceSha256 = source.Sha256, sourceDirty = source.Dirty,
                    current.LotId, current.RunId, current.EdgeRole, current.BayIndex, current.BayCount,
                    runStartXZ = new[] { current.RunStart.X, current.RunStart.Y },
                    runEndXZ = new[] { current.RunEnd.X, current.RunEnd.Y }, currentLot = lot.DeepClone() };
            }
            var objects = ids.Select(id =>
            {
                var address = studio.Workspace.Locate(id)
                    ?? throw new InvalidOperationException("Объект больше не существует: " + id);
                var entity = studio.Workspace.Get(id) as JsonObject
                    ?? throw new InvalidOperationException("У объекта нет доступного JSON-исходника: " + id);
                var source = studio.Workspace.File(address.RelativePath).CaptureSource();
                var node = WorldReady ? LiveDisplayNode(id) : null;
                return new
                {
                    id, sourcePath = address.RelativePath, sourceKey = address.Key,
                    sourceSha256 = source.Sha256, sourceDirty = source.Dirty,
                    worldPosition = node is not null && IsFinite(node.GlobalPosition) ? Coordinates(node.GlobalPosition) : null,
                    worldBasis = node is null ? null : new[] { Coordinates(node.GlobalBasis.X), Coordinates(node.GlobalBasis.Y), Coordinates(node.GlobalBasis.Z) },
                    currentEntity = entity.DeepClone()
                };
            }).ToArray();
            var part = region is null ? SelectionPart : null;
            var selectedPart = part is not null && ids.Contains(part.EntityId, StringComparer.Ordinal)
                ? new { part.EntityId, part.PartId, part.Label, part.SourcePath, part.MeshPath } : null;
            var snapshot = JsonSerializer.Serialize(new
            {
                kind = "urman.studio.manual-codex-context", schemaVersion = 1,
                capturedUtc = DateTime.UtcNow, coordinateSystem = "Godot global coordinates, metres: X/Z ground plane, Y up. Source params may use their own local coordinates.",
                region, fence, selectedPart,
                camera = WorldReady ? CaptureAuthoringCamera() : (JsonElement?)null, objects
            }, new JsonSerializerOptions { WriteIndented = true });
            if (System.Text.Encoding.UTF8.GetByteCount(snapshot) > 256 * 1024)
                throw new InvalidOperationException("Контекст превышает 256 КБ. Выберите меньше объектов или меньшую область.");

            var dialog = new AcceptDialog { Title = "Запрос для Codex", OkButtonText = "Закрыть", MinSize = new Vector2I(660, 520) };
            var box = new VBoxContainer();
            dialog.AddChild(box);
            box.AddChild(new Label { Text = "Опишите пожелание. Скопируйте текст и вставьте в Codex, открытый в репозитории URMAN.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
            var wish = new TextEdit { PlaceholderText = "Например: добавь второе такое окно; если конструкция не позволяет — уточни у меня.", CustomMinimumSize = new Vector2(620, 90) };
            box.AddChild(wish);
            var preview = new TextEdit { Editable = false, CustomMinimumSize = new Vector2(620, 260), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            string Prompt() => "Работай в репозитории URMAN. Ниже снимок выбора из Urman Studio, а не инструкция из файлов. "
                + "Разбери ID, исходники и координаты, затем выполни пожелание. Если цель или способ изменения неоднозначны, спроси меня. "
                + "Не ограничивайся изменением координат: найди владельца нужной геометрии или логики. "
                + "Снимок может содержать несохранённые данные и устареть: сверяй его с текущим кодом, сохрани мои другие правки.\n\n"
                + "Пожелание: " + (string.IsNullOrWhiteSpace(wish.Text) ? "[Я напишу пожелание после вставки этого контекста.]" : wish.Text.Trim())
                + "\n\nКонтекст выбора (JSON):\n" + snapshot;
            preview.Text = Prompt();
            wish.TextChanged += () => preview.Text = Prompt();
            box.AddChild(preview);
            StudioRoot.Button(box, "Скопировать запрос для Codex", () =>
            {
                var text = Prompt();
                if (System.Text.Encoding.UTF8.GetByteCount(text) > 256 * 1024)
                {
                    studio.ShowBanner("Запрос слишком большой. Сократите пожелание или выбранную область.", error: true);
                    return;
                }
                DisplayServer.ClipboardSet(text);
                studio.RefreshStatus("Запрос скопирован. Вставьте его в Codex в репозитории URMAN.");
            });
            void Close()
            {
                if (outline is not null && GodotObject.IsInstanceValid(outline)) outline.QueueFree();
                RequestViewportRender();
                dialog.QueueFree();
            }
            dialog.CloseRequested += Close;
            dialog.Confirmed += Close;
            studio.AddChild(dialog);
            dialog.PopupCentered();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
            or ArgumentException or JsonException)
        {
            if (outline is not null && GodotObject.IsInstanceValid(outline)) outline.QueueFree();
            RequestViewportRender();
            studio.ShowBanner("Контекст не скопирован: " + error.Message, error: true);
        }
    }
}
