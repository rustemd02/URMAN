using Godot;

namespace Urman.Godot;

public partial class W05Controller
{
    private Transform3D _responseInitialPose;
    private bool _responsePoseCaptured;
    private bool _missingBichuraReported;

    // This gate is intentionally closed until the original imported A-BICHURA,
    // its real hand attachment and path/carry/place clips are supplied together.
    // A moved cup by itself would falsely claim the world's central encounter.
    private bool CellarResponseReady => false;

    private bool InspectCellarResponse()
    {
        if (!CellarResponseReady || !CanUseCellarAction(ResponseInteractionId, out _, out _)) return false;
        return false;
    }

    private void ProcessCellarResponse(double delta, RuntimeBridge bridge, object session)
    {
        if (_missingBichuraReported || bridge.PhotoWorldFactObserved(ResponseFactId)) return;
        _missingBichuraReported = true;
        GD.PushWarning("W05 source candidate: original imported A-BICHURA, canonical load path, rig/hand attachment and real path/carry/place clips are missing. Response remains unavailable; no creature substitution or fact commit.");
    }

    private void CancelCellarResponse()
    {
        _missingBichuraReported = false;
    }

    private void ApplyCommittedCellarPoses(RuntimeBridge? bridge)
    {
        if (!_responsePoseCaptured)
        {
            _responseInitialPose = _responseObject.GlobalTransform;
            _responsePoseCaptured = true;
        }
        // A loaded authored fact has a stable result pose. Unfinished encounters
        // restore the initial cup; the carry system alone owns the family box.
        _responseObject.GlobalTransform = bridge?.PhotoWorldFactObserved(ResponseFactId) == true
            ? _responseRest.GlobalTransform : _responseInitialPose;
    }
}
