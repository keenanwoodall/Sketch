using System;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using Shapes;

using static Unity.Mathematics.math;

public abstract class Sketch : MonoBehaviour
{
    private Camera _targetCamera;

    private double _startTime;
    private double _lastDrawTime;
    private double _minTimeStep;
    private int _subFrames;
    private bool _isSubFrame;
    private ShutterProfile _shutterProfile;
    private InputSettings.UpdateMode _lastUpdateMode;

    protected void OnEnable() => StartSketch();
    protected void OnDisable() => StopSketch();

    protected void Update()
    {
        if (!_targetCamera) 
            return;
        
        _targetCamera.enabled = false;

        using (Draw.Command(_targetCamera))
        {
            var currentTime = UnityEngine.Time.timeAsDouble - _startTime;
            var deltaTime   = currentTime - _lastDrawTime;

            if (deltaTime < _minTimeStep)
                return;

            _targetCamera.enabled   = true;

            var cachedSubFrames     = _subFrames;
            var subFrameDeltaTime   = deltaTime / (cachedSubFrames + 1f);

            PreciseDeltaTime        = deltaTime;
            DeltaTime               = (float)PreciseDeltaTime;

            PreciseTime             = currentTime;
            Time                    = (float)PreciseTime;

            FrameCount++;

            UpdateInput();
            PrepareDrawing();

            _drawAlpha = 1f;
            OnDrawBackground();

            PreciseTime = _lastDrawTime;
            Time = (float)PreciseTime;

            for (int i = 0; i <= cachedSubFrames; i++)
            {
                var shutterTime     = (i + 1f) / (cachedSubFrames + 1f);
                var subFrameAlpha   = 1f / (cachedSubFrames + 1f);

                _drawAlpha          = _shutterProfile.Profile.Evaluate(shutterTime) * subFrameAlpha / _shutterProfile.Area;
                _isSubFrame         = i != cachedSubFrames;

                PreciseDeltaTime    = subFrameDeltaTime;
                DeltaTime           = (float)PreciseDeltaTime;

                PreciseTime += subFrameDeltaTime;
                Time = (float)PreciseTime;

                PrepareDrawing();
                OnDraw();
            }

            if (MotionBlurSubFrames() > 0 && Draw.BlendMode != ShapesBlendMode.Additive)
                Debug.LogWarning("Motion Blur enabled without additive blending. Colors may not look correct.");

            _lastDrawTime = UnityEngine.Time.timeAsDouble - _startTime;
        }
    }

    public void StartSketch()
    {
        _targetCamera               = gameObject.AddComponent<Camera>();
        _targetCamera.cullingMask   = 0;
        _targetCamera.enabled       = false;
        _targetCamera.orthographic  = true;
        _targetCamera.clearFlags    = CameraClearFlags.Nothing;
        _targetCamera.hideFlags     = HideFlags.NotEditable;

        var seed = (uint)UnityEngine.Random.Range(0, 4294967295);
        Random = new Unity.Mathematics.Random(seed);

        _lastUpdateMode = InputSystem.settings.updateMode;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;

        InitializeTime();
        FrameRate(60f);
        MotionBlur(0, UniformShutter);
        PrepareDrawing();
        UpdateInput();
        OnStart();  
    }

    public void StopSketch(bool immediate = false)
    {
        try { OnStop(); }
        finally
        {
            InputSystem.settings.updateMode = _lastUpdateMode;
            if (_targetCamera && _targetCamera.gameObject)
            {
                if (immediate)
                {
                    DestroyImmediate(_targetCamera.GetUniversalAdditionalCameraData());
                    DestroyImmediate(_targetCamera);
                }
                else
                {
                    Destroy(_targetCamera.GetUniversalAdditionalCameraData());
                    Destroy(_targetCamera);
                }
            }
        }
    }

    public void RestartSketch()
    {
        StopSketch(immediate: true);
        StartSketch();
    }

    private void InitializeTime()
    {
        MotionBlur(0, UniformShutter);

        _startTime          = UnityEngine.Time.timeAsDouble;
        _lastDrawTime       = 0d;

        Time                = 0f;
        PreciseTime         = 0d;
        DeltaTime           = 0f;
        PreciseDeltaTime    = 0d;
        FrameCount = 0;
    }

    private void PrepareDrawing()
    {
        Width   = _targetCamera.scaledPixelWidth;
        Height  = _targetCamera.scaledPixelHeight;
        Size    = float2(Width, Height);

        Color(WHITE);
        StrokeWeight(1);
        TransparentBlend();

        Draw.ThicknessSpace = ThicknessSpace.Meters;
        Draw.Matrix         = SketchUtils.GetScreenToWorldMatrix(_targetCamera);
    }

    private void UpdateInput()
    {
        InputSystem.Update();
        
        var focused = Application.isFocused;
        if (focused)
        {
            var mousePosition           = Mouse.current?.position.value ?? Vector2.zero;
            var viewportMousePosition   = _targetCamera.ScreenToViewportPoint(mousePosition);
            var pixelMousePosition      = Vector2.Scale(viewportMousePosition, new Vector2(_targetCamera.pixelWidth, _targetCamera.pixelHeight));

            MousePosition               = pixelMousePosition;
            MouseX                      = pixelMousePosition.x;
            MouseY                      = pixelMousePosition.y;
            MouseHeld                   = Mouse.current?.leftButton.isPressed ?? false && Application.isFocused;
        }

        if (focused && Keyboard.current.anyKey.wasPressedThisFrame)
            OnKeyPress();
        if (focused && Keyboard.current.anyKey.isPressed)
            OnKeyHeld();
        if (focused && Keyboard.current.anyKey.wasReleasedThisFrame)
            OnKeyRelease();

        if (focused && Mouse.current.leftButton.wasPressedThisFrame)
            OnMousePress();
        if (focused && Mouse.current.leftButton.isPressed)
            OnMouseHeld();
        if (focused && Mouse.current.leftButton.wasReleasedThisFrame)
            OnMouseRelease();
    }

    protected virtual void OnStart(){}
    protected virtual void OnStop(){}
    protected virtual void OnDrawBackground(){}
    protected virtual void OnDraw(){}

    [NonSerialized] public float Time;
    [NonSerialized] public double PreciseTime;
    [NonSerialized] public float DeltaTime;
    [NonSerialized] public double PreciseDeltaTime;
    [NonSerialized] public int FrameCount;
    public float FrameRate() => (float)(1d / _minTimeStep);
    public void FrameRate(float newFrameRate) => _minTimeStep = 1f / newFrameRate;
    public void MotionBlur(int subFrames, ShutterProfile shutterProfile)
    {
        _subFrames = Mathf.Max(0, subFrames);
        _shutterProfile = shutterProfile;
    }
    public void MotionBlur(int subFrames)
    {
        _subFrames = Mathf.Max(0, subFrames);
        _shutterProfile = UniformShutter;
    }
    public int MotionBlurSubFrames() => _subFrames;
    public readonly ShutterProfile UniformShutter = new ShutterProfile(AnimationCurve.Constant(0f, 1f, 1f));
    public readonly ShutterProfile SmoothShutter = new ShutterProfile(shutterOpen: 0.25f, shutterClose: 0.75f);

    protected virtual void OnMousePress(){}
    protected virtual void OnMouseRelease(){}
    protected virtual void OnMouseHeld(){}
    protected virtual void OnKeyPress(){}
    protected virtual void OnKeyRelease(){}
    protected virtual void OnKeyHeld(){}

    protected readonly float4 WHITE = new float4(1f, 1f, 1f, 1f);
    protected readonly float4 BLACK = new float4(0f, 0f, 0f, 1f);
    protected readonly float4 RED = new float4(1f, 0f, 0f, 1f);
    protected readonly float4 GREEN = new float4(0f, 1f, 0f, 1f);
    protected readonly float4 BLUE = new float4(0f, 0f, 1f, 1f);

    [NonSerialized] public float Width;
    [NonSerialized] public float Height;
    [NonSerialized] public float2 Size;

    [NonSerialized] public float MouseX;
    [NonSerialized] public float MouseY;
    [NonSerialized] public float2 MousePosition;
    [NonSerialized] public bool MouseHeld;
    
    public bool KeyPressed(Key key) => !_isSubFrame && Application.isFocused && Keyboard.current[key].wasPressedThisFrame;
    public bool KeyHeld(Key key) => !_isSubFrame && Application.isFocused && Keyboard.current[key].isPressed;
    public bool KeyReleased(Key key) => !_isSubFrame && Application.isFocused && Keyboard.current[key].wasReleasedThisFrame;

    private float _drawAlpha = 1f;
    public void Color(float brightness) => Draw.Color = new Color(brightness, brightness, brightness, _drawAlpha);
    public void Color(float3 rgb) => Draw.Color = new Color(rgb.x, rgb.y, rgb.z, _drawAlpha);
    public void Color(float4 rgba) => Draw.Color = new Color(rgba.x, rgba.y, rgba.z, rgba.w * _drawAlpha);

    public void AdditiveBlend() => Draw.BlendMode = ShapesBlendMode.Additive;
    public void TransparentBlend() => Draw.BlendMode = ShapesBlendMode.Transparent;

    public void StrokeWeight(float weight) => Draw.Thickness = weight;

    public void Fill() => Draw.Rectangle(new Rect(0, 0, Width, Height), 0f);
    public void Line(float x1, float y1, float x2, float y2) => Draw.Line(new Vector3(x1, y1), new Vector3(x2, y2));
    public void Line(float2 p1, float2 p2) => Draw.Line((Vector2)p1, (Vector2)p2);
    public void Circle(float x, float y, float radius) => Draw.Disc(new Vector2(x, y), radius);
    public void Circle(float2 position, float radius) => Draw.Disc((Vector2)position, radius);
    public void Ring(float2 position, float radius) => Draw.Ring((Vector2)position, radius);
    public void Square(float2 p, float size, float cornerRadius = 0f) => Draw.Rectangle(new Vector2(p.x, p.y), new Vector2(size, size), cornerRadius);
    public void Square(float2 p, float size, float angle, float cornerRadius = 0f)
    {
        Draw.PushMatrix();
        RotateAround(p, angle);
        Draw.Rectangle(new Vector2(p.x, p.y), new Vector2(size, size), cornerRadius);
    }

    public void Rectangle(float2 p, float2 size, float cornerRadius = 0f) => Draw.Rectangle(new Vector2(p.x, p.y), size, cornerRadius);
    public void Rectangle(float2 p, float2 size, float angle, float cornerRadius = 0f)
    {
        Draw.PushMatrix();
        RotateAround(p, angle);
        Draw.Rectangle(new Vector2(p.x, p.y), size, cornerRadius);
    }

    public void Square(float x, float y, float size, float cornerRadius = 0f) => Draw.Rectangle(new Vector2(x, y), new Vector2(size, size), cornerRadius);
    public void Square(float x, float y, float size, float angle, float cornerRadius = 0f)
    {
        Draw.PushMatrix();
        RotateAround(x, y, angle);
        Draw.Rectangle(new Vector2(x, y), new Vector2(size, size), cornerRadius);
        Draw.PopMatrix();
    }

    public void Rotate(float degrees)
    {
        RotateAround(Width / 2, Height / 2, degrees);
    }

    public void RotateAround(float2 pivot, float degrees)
    {
        var rotation = Quaternion.Euler(0, 0, degrees);
        Draw.Matrix *= Matrix4x4.Translate(new Vector3(pivot.x, pivot.y)) * Matrix4x4.Rotate(rotation) * Matrix4x4.Translate(new Vector3(-pivot.x, -pivot.y)); 
    }

    public void RotateAround(float pivotX, float pivotY, float degrees)
    {
        var rotation = Quaternion.Euler(0, 0, degrees);
        Draw.Matrix *= Matrix4x4.Translate(new Vector3(pivotX, pivotY)) * Matrix4x4.Rotate(rotation) * Matrix4x4.Translate(new Vector3(-pivotX, -pivotY)); 
    }


    [NonSerialized]
    public Unity.Mathematics.Random Random;
    public float2 RandomScreenPoint(float padding = 0f) => Random.NextFloat2(padding, Size - padding);
    public float4 RandomColor() => float4(Random.NextFloat4(0f, 1f));
    public float4 RandomColor(float alpha) => float4(Random.NextFloat3(0f, 1f), alpha);
    public float4 RandomColorHue(float saturation, float value, float alpha = 1f)
    {
        var color = UnityEngine.Color.HSVToRGB(Random.NextFloat(0f, 1f), saturation, value);
        return float4(color.r, color.g, color.b, alpha);
    }
    public float4 HSV(float hue, float saturation, float brightness, float alpha = 1f)
    {
        var color = UnityEngine.Color.HSVToRGB(frac(hue), saturation, brightness);
        return float4(color.r, color.g, color.b, alpha);
    }

    public float PingPong(float value, float length) => abs(fmod(value, length * 2f) - length);
    public float PingPong(float value, float start, float end) => start + PingPong(value - start, end - start);

    public float2 PointOnCircle(float radius, float angle)
    {
        var rad = radians(angle);
        return float2(cos(rad), sin(rad)) * radius;
    }
}