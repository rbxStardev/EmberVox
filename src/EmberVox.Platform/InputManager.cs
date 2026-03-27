using System.Numerics;
using Silk.NET.Input;

namespace EmberVox.Engine;

public class InputManager
{
    private IInputContext _inputContext;
    private readonly IKeyboard _mainKeyboard;
    private readonly IMouse _mainMouse;
    public event EventHandler<Vector2>? MouseMoved;
    public event EventHandler<ScrollWheel>? MouseScrolled;
    public event EventHandler<Key>? KeyPressed;

    public InputManager(IInputContext inputContext)
    {
        _inputContext = inputContext;

        _mainKeyboard = _inputContext.Keyboards[0];
        _mainMouse = _inputContext.Mice[0];

        _mainMouse.MouseMove += MainMouseOnMouseMove;
        _mainMouse.Scroll += MainMouseOnScroll;
        _mainKeyboard.KeyDown += MainKeyboardOnKeyDown;

        _mainMouse.Cursor.CursorMode = CursorMode.Raw;
    }

    private void MainMouseOnMouseMove(IMouse mouse, Vector2 position)
    {
        MouseMoved?.Invoke(this, position);
    }

    private void MainMouseOnScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        MouseScrolled?.Invoke(this, scrollWheel);
    }

    private void MainKeyboardOnKeyDown(IKeyboard keyboard, Key key, int strength)
    {
        KeyPressed?.Invoke(this, key);
    }

    public bool IsKeyPressed(Key key)
    {
        return _mainKeyboard.IsKeyPressed(key);
    }

    public float GetInputKeyStrength(Key key2)
    {
        return IsKeyPressed(key2) ? 1 : 0;
    }

    public float GetInputKeysAxis(Key negativeInput, Key positiveInput)
    {
        float negativeStrength = -GetInputKeyStrength(negativeInput);
        float positiveStrength = GetInputKeyStrength(positiveInput);

        return negativeStrength + positiveStrength;
    }

    public Vector2 GetInputKeysVector(
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
