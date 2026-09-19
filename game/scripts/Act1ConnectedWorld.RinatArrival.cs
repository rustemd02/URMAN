using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private RinatPresencePresentation? _rinatPresence;

    private static Vector3 RinatRoadsideAnchor => new(2f,
        AgentBAct1HeightField.CollisionGround(2f, -84.5f), -84.5f);

    // The voice starts after the physical approach at (.6, -121), not at the
    // zone-entry spawn. His lamp is ahead on that same traversable road edge.
    private static Vector3 RinatForestAnchor => new(2.1f,
        AgentBAct1HeightField.CollisionGround(2.1f, -122.4f), -122.4f);

    private void BuildRinatRoadsidePresentation()
    {
        if (_rinatNpc is null) throw new InvalidOperationException("Rinat staging must precede his roadside presentation.");
        var zirat = (StyleBenchmarkZone)_zoneInstances["zirat_road"];
        _rinatPresence = RinatPresencePresentation.AttachConnected(this, _rinatNpc, zirat,
            RinatRoadsideAnchor, new(0f, AgentBAct1HeightField.CollisionGround(0f, -82.5f) + 1.62f, -82.5f));
        var endpoint = FindChild("KaraForestApproachEndpoint", true, false) as InteractionTarget
            ?? throw new InvalidOperationException("The physical forest approach endpoint is missing.");
        endpoint.SetMeta("observationReferenceEye", new Vector3(.6f,
            AgentBAct1HeightField.CollisionGround(.6f, -121f) + 1.7f, -121f));
        endpoint.SetMeta("observationLookAt", endpoint.GlobalPosition);
    }

    public Task<bool> PresentRinatInterventionAsync(object sessionIdentity) =>
        _rinatPresence?.PresentRinatInterventionAsync(sessionIdentity) ?? Task.FromResult(false);
}
