using FileAccess = Godot.FileAccess;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    /// <summary>
    /// Reread the open-part and far-bank road sources before a Studio full-world
    /// rebuild. Nothing is published until both source documents parse and all
    /// road axes validate. The returned action restores all replaced static caches
    /// if the Studio rebuild has to reattach its previous world.
    /// </summary>
    internal static System.Action ReloadRoadSourcesForStudio()
    {
        var openPartPath = AgentBAct1Layout.OpenPartPlotPath;
        var farBankPath = AgentBAct1HeightField.FarBankPlotPath;
        if (!FileAccess.FileExists(openPartPath) || !FileAccess.FileExists(farBankPath))
            throw new InvalidDataException("Both village road source files must exist before a Studio world reload.");

        var openPartJson = FileAccess.GetFileAsString(openPartPath);
        var farBankJson = FileAccess.GetFileAsString(farBankPath);
        var nextOpenPart = ParseOpenPartPlotJson(openPartJson);
        var nextFarBank = ParseFarBankPlotJson(farBankJson);
        ValidateRoadSources(nextOpenPart.Roads, nextFarBank.Roads);

        // Keep the height field's original double-width parser so an unchanged
        // JSON source remains bit-for-bit equal to its startup axes.
        var nextRoadAxes = AgentBAct1HeightField.PlotRoadsFromJson(farBankJson)
            .Concat(AgentBAct1HeightField.PlotRoadsFromJson(openPartJson)).ToArray();
        var previousOpenPart = _openPart;
        var previousFarBank = _farBank;
        System.Action? rollbackHeightField = null;
        var published = false;
        var rolledBack = false;
        System.Action rollback = () =>
        {
            if (!published || rolledBack) return;
            _openPart = previousOpenPart;
            _farBank = previousFarBank;
            rollbackHeightField!();
            rolledBack = true;
        };

        rollbackHeightField = AgentBAct1HeightField.ReplacePlotRoadAxesForStudio(nextRoadAxes);
        _openPart = nextOpenPart;
        _farBank = nextFarBank;
        published = true;
        return rollback;
    }

    private static void ValidateRoadSources(FarBankRoad[] openPartRoads, FarBankRoad[] farBankRoads)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var road in openPartRoads.Concat(farBankRoads))
        {
            if (string.IsNullOrWhiteSpace(road.Id) || road.Street is null || !ids.Add(road.Id)
                || road.Points is null || road.Points.Length < 2
                || !float.IsFinite(road.Width) || road.Width <= 0
                || road.Points.Any(point => !point.IsFinite()))
                throw new InvalidDataException("Village road sources contain an invalid or duplicate road.");
        }
    }
}
