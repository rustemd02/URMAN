using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Shared boot helper for smokes that consume the real demo entrypoint: they
/// must pass the UIUX-001 main menu by pressing the production New Game
/// button (never by injecting intro/gameplay state directly).
/// </summary>
public static class Act1MainMenuTestSupport
{
    public static async Task<bool> StartThroughMainMenuAsync(
        this Node context,
        Act1DemoRoot demo,
        int timeoutFrames = 900)
    {
        var frames = timeoutFrames;
        while (!demo.MainMenuVisible && frames-- > 0)
        {
            await context.ToSignal(context.GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (!demo.MainMenuVisible || demo.MainMenu?.NewGameButton is not { } button)
        {
            GD.PushError("Act 1 main menu did not appear with a New Game button.");
            return false;
        }

        button.EmitSignal(BaseButton.SignalName.Pressed);
        if (button.Text == "Начать новую игру") button.EmitSignal(BaseButton.SignalName.Pressed);
        frames = timeoutFrames;
        while (!demo.IntroVisible && frames-- > 0)
        {
            await context.ToSignal(context.GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (!demo.IntroVisible)
        {
            GD.PushError("Act 1 intro did not appear after the main menu choice.");
            return false;
        }

        return true;
    }
}
