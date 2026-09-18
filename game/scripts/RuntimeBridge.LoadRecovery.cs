using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class RuntimeBridge
{
    // The ordinary SaveGameV3 captured before loading remains the sole snapshot.
    // This reference is transient; a failed physical projection never writes it
    // over a user's slot or grants control inside a rejected vehicle placement.
    private SaveGameV3? _physicalRecoverySave;
    internal bool NeedsPhysicalRecovery { get; private set; }

    private void ShowPhysicalRecoveryMenu()
    {
        if (GetParent()?.GetParent() is Act1DemoRoot demo) demo.ShowLoadPlacementFailure();
        else FindPlayer()?.NotifyTraversal("Не удалось найти свободное место. Загрузите сохранение или начните новую игру.");
    }

    private async Task<bool> StartRecoverySessionAsync(bool debugSession, FirstPersonController player)
    {
        var previousSave = _physicalRecoverySave;
        var previousDebugSession = IsDebugSession;
        var success = false;
        _loadPreparing = true;
        _loadingTimeSampleUsec = Time.GetTicksUsec();
        player.SetSessionTransition(true);
        PlayTimeBoundary?.Invoke("load-start");
        try
        {
            // The new session's normal initialization is allowed to dispatch;
            // the menu and transition gate continue to own the player's input.
            NeedsPhysicalRecovery = false;
            await CreateFreshSessionAsync(debugSession, player);
            _loadingSlot = true;
            _loadPreparing = false;
            // CreateFreshSessionAsync completed the same ordered physical
            // projection used by loading; do not reset it a second time here.
            if (!player.CanStandAt(player.GlobalPosition))
                throw new InvalidDataException("The new session's standing position is blocked.");
            _physicalRecoverySave = null;
            success = true;
            return true;
        }
        catch (Exception error)
        {
            NeedsPhysicalRecovery = true;
            _physicalRecoverySave = previousSave;
            GD.PushError("SaveGameV3 recovery new game failed: " + error.Message);
            if (previousSave is not null)
            {
                try
                {
                    await RestoreLoadedWorldAsync(previousSave, previousDebugSession, player);
                    NeedsPhysicalRecovery = false;
                    _physicalRecoverySave = null;
                }
                catch (Exception restoreError)
                {
                    GD.PushError("SaveGameV3 recovery retained the previous snapshot: " + restoreError.Message);
                }
            }
            return false;
        }
        finally
        {
            SampleLoadingTime();
            _loadingSlot = false;
            _loadPreparing = false;
            player.SetSessionTransition(NeedsPhysicalRecovery);
            QueueRuntimeStateChanged();
            PlayTimeBoundary?.Invoke(success ? "load-restored" : "load-failed");
            if (NeedsPhysicalRecovery) ShowPhysicalRecoveryMenu();
        }
    }
}
