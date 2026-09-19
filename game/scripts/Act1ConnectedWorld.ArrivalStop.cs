using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string ArrivalTimetableDocument = "urman.chapter1:document/arrival-stop-timetable";
    private Label3D? _arrivalTimetable;

    private void BuildArrivalBusStop()
    {
        var arrival = GetNode<Node3D>("Act1CoreWorldGreybox/Arrival");
        var stop = new Node3D { Name = "ArrivalBusStop",
            Position = new(3.70f, AgentBAct1HeightField.CollisionGround(3.70f, 6.35f), 6.35f) };
        arrival.AddChild(stop);
        stop.SetMeta("settlementPoiId", "POI-ARRIVAL-STOP");
        stop.SetMeta("sourceKey", "act1/arrival/bus-stop");
        stop.SetMeta("presentationRole", "ordinary rural stop beside the existing arrival bench");
        stop.SetMeta("documentId", ArrivalTimetableDocument);
        FacilitySolid(stop, "GalvanizedPost", new(.085f, 2.76f, .085f), new(0, 1.38f, -.055f), "79837f", "metal");
        AddVisualBox(stop, "PostCap", new(.10f, .025f, .10f), new(0, 2.775f, -.055f), "b0b5ae", "metal");
        FacilitySolid(stop, "BusSign", new(.56f, .68f, .035f), new(0, 2.42f, .012f), "294d72", "metal");
        AddVisualBox(stop, "WhiteSignField", new(.44f, .53f, .009f), new(0, 2.43f, .035f), "e1e0cb", "metal");
        AddVisualBox(stop, "BusSilhouette", new(.29f, .19f, .006f), new(0, 2.43f, .042f), "263b44", "metal");
        for (var window = 0; window < 3; window++)
            AddVisualBox(stop, "BusWindow" + window, new(.063f, .073f, .006f), new(-.09f + window * .09f, 2.46f, .048f), "dedeca", "metal");
        foreach (var x in new[] { -.09f, .09f })
        {
            var wheel = DiscoveryCylinder(stop, "BusWheel" + x, .036f, .036f, .007f, new(x, 2.315f, .045f), "263b44");
            wheel.RotationDegrees = new(90, 0, 0);
        }
        FacilitySolid(stop, "SettlementNameBoard", new(1.48f, .245f, .035f), new(0, 1.94f, .012f), "31534d", "metal");
        stop.AddChild(new Label3D { Name = "SettlementName", Text = SettlementRegistry.VillageName,
            Position = new(0, 1.94f, .034f), FontSize = 46, PixelSize = .0027f,
            OutlineSize = 0, Shaded = true, DoubleSided = false, Modulate = Color.FromHtml("eae4d2") });
        FacilitySolid(stop, "TimetableCase", new(.76f, 1.09f, .058f), new(0, 1.235f, .012f), "666c62", "metal");
        AddVisualBox(stop, "TimetablePaper", new(.69f, 1.02f, .012f), new(0, 1.235f, .047f), "d8cfb4", "paper");
        _arrivalTimetable = new Label3D { Name = "PrintedTimetable", Position = new(0, 1.235f, .056f),
            FontSize = 25, PixelSize = .00093f, Width = 680, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            OutlineSize = 0, Shaded = true, DoubleSided = false, Modulate = Color.FromHtml("353e38") };
        stop.AddChild(_arrivalTimetable);
        foreach (var x in new[] { -.313f, .313f }) foreach (var y in new[] { .78f, 1.69f })
            AddVisualBox(stop, "PaperPin" + x + "_" + y, new(.019f, .019f, .014f), new(x, y, .061f), "6c786e", "metal");
        AddVisualBox(stop, "CaseSnowLip", new(.80f, .036f, .10f), new(0, 1.795f, .020f), "e8edf0", "snow_ground");
        var target = FacilityTarget("ArrivalStopTimetable", "urman.chapter1:interaction/arrival-stop-timetable",
            "Прочитать расписание и объявления", stop, new(0, 1.235f, .075f), new(.72f, 1.02f, .025f));
        target.DocumentId = ArrivalTimetableDocument;
    }

    private void FinalizeArrivalBusStop()
    {
        if (_arrivalTimetable is null || _runtimeBridge is null) return;
        _arrivalTimetable.Text = _runtimeBridge.RequireDocument(ArrivalTimetableDocument).BodyMarkdown
            .Replace("**", string.Empty, StringComparison.Ordinal).Replace("#", string.Empty, StringComparison.Ordinal).Trim();
        _arrivalTimetable.SetMeta("contentOwner", ArrivalTimetableDocument);
    }
}
