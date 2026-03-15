using System.Numerics;
using Silk.NET.Input;

namespace EmberVox.Engine;

public static class InputManager
{
    public static event EventHandler<Vector2>? MouseMoved;
    public static event EventHandler<ScrollWheel>? MouseScrolled;
    public static event EventHandler<Key>? KeyPressed;

    private static IInputContext _inputContext = null!;
    private static IKeyboard _mainKeyboard = null!;
    private static IMouse _mainMouse = null!;

    public static void Initialize(IInputContext inputContext)
    {
        _inputContext = inputContext;

        _mainKeyboard = _inputContext.Keyboards[0];
        _mainMouse = _inputContext.Mice[0];

        _mainMouse.MouseMove += MainMouseOnMouseMove;
        _mainMouse.Scroll += MainMouseOnScroll;
        _mainKeyboard.KeyDown += MainKeyboardOnKeyDown;

        _mainMouse.Cursor.CursorMode = CursorMode.Raw;
    }

    private static void MainMouseOnMouseMove(IMouse mouse, Vector2 position)
    {
        MouseMoved?.Invoke(null, position);
    }

    private static void MainMouseOnScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        MouseScrolled?.Invoke(null, scrollWheel);
    }

    private static void MainKeyboardOnKeyDown(IKeyboard keyboard, Key key, int strength)
    {
        KeyPressed?.Invoke(null, key);
    }

    public static bool IsKeyPressed(Key key)
    {
        return _mainKeyboard.IsKeyPressed(key);
    }

    public static float GetInputKeyStrength(Key key2)
    {
        return IsKeyPressed(key2) ? 1 : 0;
    }

    public static float GetInputKeysAxis(Key negativeInput, Key positiveInput)
    {
        float negativeStrength = -GetInputKeyStrength(negativeInput);
        float positiveStrength = GetInputKeyStrength(positiveInput);

        return negativeStrength + positiveStrength;
    }

    public static Vector2 GetInputKeysVector(
        Key negativeInputX,
        Key positiveInputX,
        Key negativeInputY,
        Key positiveInputY
    )
    {
        return new Vector2(
            GetInputKeysAxis(negativeInputX, positiveInputX),
            GetInputKeysAxis(negativeInputY, positiveInputY)
        );
    }
}
