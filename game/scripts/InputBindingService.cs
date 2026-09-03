using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public static class InputBindingService
{
    public static readonly string[] RemappableActions =
    [
        "move_forward",
        "move_backward",
        "move_left",
        "move_right",
        "interact",
        "crouch",
        "journal",
        "pause",
        "quick_save",
        "quick_load"
    ];

    public static IReadOnlyList<InputBindingSnapshot> Capture() => RemappableActions
        .Select(CaptureAction)
        .ToArray();

    public static void Apply(IEnumerable<InputBindingSnapshot> bindings)
    {
        var byAction = bindings.ToDictionary(binding => binding.Action, StringComparer.Ordinal);
        foreach (var action in RemappableActions)
        {
            if (!byAction.TryGetValue(action, out var binding))
            {
                throw new InvalidDataException($"Saved input bindings are missing action {action}.");
            }

            RebindKeyboard(action, (Key)binding.KeyboardPhysicalKeycode);
            ClearGamepadEvents(action);
            if (binding.GamepadButton is { } button)
            {
                AddGamepadButton(action, (JoyButton)button);
            }
            if (binding.GamepadAxis is { } axis && binding.GamepadAxisSign is { } sign)
            {
                AddGamepadAxis(action, axis, (float)sign);
            }
        }
    }

    public static void RebindKeyboard(string action, Key physicalKeycode)
    {
        RequireAction(action);
        foreach (var inputEvent in InputMap.ActionGetEvents(action).OfType<InputEventKey>().ToArray())
        {
            InputMap.ActionEraseEvent(action, inputEvent);
        }

        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = physicalKeycode });
    }

    public static void RebindGamepad(string action, JoyButton button)
    {
        RequireAction(action);
        ClearGamepadEvents(action);
        AddGamepadButton(action, button);
    }

    public static void RebindGamepadAxis(string action, int axis, float sign)
    {
        RequireAction(action);
        if (axis is < 0 or > 7 || Math.Abs(Math.Abs(sign) - 1f) > 0.0001f)
        {
            throw new ArgumentOutOfRangeException(nameof(axis), "A gamepad axis must be 0..7 with a sign of -1 or 1.");
        }

        ClearGamepadEvents(action);
        AddGamepadAxis(action, axis, sign);
    }

    public static string Label(string action)
    {
        var binding = CaptureAction(action);
        var keyboard = OS.GetKeycodeString((Key)binding.KeyboardPhysicalKeycode);
        if (binding.GamepadButton is { } button)
        {
            return $"{keyboard} / {(JoyButton)button}";
        }

        return binding.GamepadAxis is { } axis && binding.GamepadAxisSign is { } sign
            ? $"{keyboard} / Axis {axis} {(sign < 0 ? "−" : "+")}"
            : keyboard;
    }

    private static InputBindingSnapshot CaptureAction(string action)
    {
        RequireAction(action);
        var events = InputMap.ActionGetEvents(action);
        var keyboard = events.OfType<InputEventKey>().FirstOrDefault()
            ?? throw new InvalidOperationException($"Input action {action} has no keyboard binding.");
        var gamepad = events.OfType<InputEventJoypadButton>().FirstOrDefault();
        var axis = events.OfType<InputEventJoypadMotion>().FirstOrDefault();
        return new(
            action,
            (long)keyboard.PhysicalKeycode,
            gamepad is null ? null : (int)gamepad.ButtonIndex,
            axis is null ? null : (int)axis.Axis,
            axis is null ? null : Math.Sign(axis.AxisValue));
    }

    private static void ClearGamepadEvents(string action)
    {
        foreach (var inputEvent in InputMap.ActionGetEvents(action)
                     .Where(inputEvent => inputEvent is InputEventJoypadButton or InputEventJoypadMotion)
                     .ToArray())
        {
            InputMap.ActionEraseEvent(action, inputEvent);
        }
    }

    private static void AddGamepadButton(string action, JoyButton button) =>
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button });

    private static void AddGamepadAxis(string action, int axis, float sign) =>
        InputMap.ActionAddEvent(action, new InputEventJoypadMotion
        {
            Axis = (JoyAxis)axis,
            AxisValue = sign
        });

    private static void RequireAction(string action)
    {
        if (!RemappableActions.Contains(action, StringComparer.Ordinal) || !InputMap.HasAction(action))
        {
            throw new KeyNotFoundException($"Unknown remappable input action {action}.");
        }
    }
}
