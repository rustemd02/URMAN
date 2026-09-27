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
        "jump",
        "sprint",
        "carry_rotate",
        "carry_place",
        "carry_use",
        "radio_station",
        "journal",
        "pause",
        "quick_save",
        "quick_load"
    ];

    private static IReadOnlyList<InputBindingSnapshot>? _defaults;

    /// <summary>
    /// UIUX-008: capture the pristine project-default bindings once, before
    /// any rebind or saved-settings apply can mutate the input map.
    /// </summary>
    public static void InitializeDefaults() => _defaults ??= Capture();

    public static IReadOnlyList<InputBindingSnapshot> Capture() => RemappableActions
        .Select(CaptureAction)
        .ToArray();

    public static string ActionHint(string action, bool gamepad)
    {
        if (!InputMap.HasAction(action)) return "[—]";
        var events = InputMap.ActionGetEvents(action);
        if (gamepad)
        {
            var button = events.OfType<InputEventJoypadButton>().FirstOrDefault();
            if (button is not null)
            {
                var label = button.ButtonIndex switch
                {
                    JoyButton.A or JoyButton.B or JoyButton.X or JoyButton.Y => button.ButtonIndex.ToString(),
                    JoyButton.LeftShoulder => "LB", JoyButton.RightShoulder => "RB",
                    JoyButton.LeftStick => "L3", JoyButton.RightStick => "R3",
                    JoyButton.DpadUp => "Крестовина ↑", JoyButton.DpadDown => "Крестовина ↓",
                    JoyButton.DpadLeft => "Крестовина ←", JoyButton.DpadRight => "Крестовина →",
                    JoyButton.Start => "Menu", JoyButton.Back => "View", JoyButton.Guide => "Главная",
                    JoyButton.Touchpad => "Тачпад",
                    JoyButton.Paddle1 => "P1", JoyButton.Paddle2 => "P2",
                    JoyButton.Paddle3 => "P3", JoyButton.Paddle4 => "P4",
                    _ => $"Кнопка {(int)button.ButtonIndex + 1}"
                };
                return $"[{label}]";
            }
            var axis = events.OfType<InputEventJoypadMotion>().FirstOrDefault();
            if (axis is null) return "[Gamepad]";
            return axis.Axis switch
            {
                JoyAxis.LeftX => axis.AxisValue < 0 ? "[Левый стик ←]" : "[Левый стик →]",
                JoyAxis.LeftY => axis.AxisValue < 0 ? "[Левый стик ↑]" : "[Левый стик ↓]",
                JoyAxis.RightX => axis.AxisValue < 0 ? "[Правый стик ←]" : "[Правый стик →]",
                JoyAxis.RightY => axis.AxisValue < 0 ? "[Правый стик ↑]" : "[Правый стик ↓]",
                _ => $"[{axis.Axis} {(axis.AxisValue < 0 ? "−" : "+")}]"
            };
        }
        var key = events.OfType<InputEventKey>().FirstOrDefault();
        return key is null ? "[Key]" : $"[{OS.GetKeycodeString(key.PhysicalKeycode)}]";
    }

    public static void Apply(IEnumerable<InputBindingSnapshot> bindings)
    {
        var byAction = bindings.ToDictionary(binding => binding.Action, StringComparer.Ordinal);
        foreach (var action in RemappableActions)
        {
            // Actions absent from an older save keep their project-default
            // binding: throwing here would crash every existing player's
            // settings load the moment a new remappable action ships.
            if (!byAction.TryGetValue(action, out var binding))
            {
                continue;
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

    /// <summary>
    /// UIUX-008: remappable actions already bound to the same physical key
    /// (excluding the action being rebound). Empty means no conflict.
    /// </summary>
    public static IReadOnlyList<string> FindKeyboardConflicts(Key physicalKeycode, string excludingAction)
    {
        InitializeDefaults();
        return RemappableActions
            .Where(action => !string.Equals(action, excludingAction, StringComparison.Ordinal))
            .Where(action => InputMap.ActionGetEvents(action)
                .OfType<InputEventKey>()
                .Any(keyEvent => keyEvent.PhysicalKeycode == physicalKeycode))
            .ToArray();
    }

    /// <summary>UIUX-008: restore the pristine project-default bindings.</summary>
    public static void RestoreDefaults()
    {
        _defaults ??= Capture();
        Apply(_defaults);
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
        return binding.GamepadButton is not null || binding.GamepadAxis is not null
            ? $"{keyboard} / {ActionHint(action, true).Trim('[', ']')}"
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
