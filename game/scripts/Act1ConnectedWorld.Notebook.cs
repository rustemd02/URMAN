using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private async void RememberReadAddress(string addressId)
    {
        if (GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge) return;
        try
        {
            if (!await bridge.RememberAddressAsync(addressId)) return;
            (GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)
                ?.NotifyTraversal(AddressRegistry!.FormatAddress(addressId) + " — записано в книжку");
        }
        catch (System.Exception exception)
        {
            GD.PushError("Address notebook write failed: " + exception.Message);
        }
    }
}
