#if UNITY_EDITOR
#define RECORDING_SUPPORTED
#endif

using System;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using Shapes;

using static Unity.Mathematics.math;
using UnityEngine.Rendering;

#if RECORDING_SUPPORTED
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.Recorder.Encoder;
#endif

public abstract class Sketch : MonoBehaviour
{
    private Camera _targetCamera;

    private double _startTime;
    private double _lastDrawTime;
    private int _subFrames;
    private bool _isSubFrame;
    private float _drawAlpha = 1f;
#if RECORDING_SUPPORTED
    private bool _recording;
    private string _lastRecordingPath;
    private static readonly string RecordingTakeKey = "SKETCH_TAKE";
    private RecorderController _recorderController;
    private RecorderControllerSettings _recorderControllerSettings;
    private MovieRecorderSettings _movieRecorderSettings;
    private ImageRecorderSettings _imageRecorderSettings;
#endif
    private ShutterProfile _shutterProfile;

    private Volume _volume;
    private VolumeProfile _volumeProfile;
    private Bloom _bloom;
    private Vignette _vignette;

    protected void OnEnable() => StartSketch();
    protected void OnDisable() => StopSketch();

    protected void Update()
    {
        if (!_targetCamera) 
            return;
        
        var currentTime = UnityEngine.Time.timeAsDouble - _startTime;
        var deltaTime   = currentTime - _lastDrawTime;

        using (Draw.Command(_targetCamera))
        {
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

            PrepareDrawing();
            _drawAlpha = 1f;
            OnDrawForeground();

            _lastDrawTime = UnityEngine.Time.timeAsDouble - _startTime;

            LastMousePosition = MousePosition;
        }
    }

    public void StartSketch()
    {
        _targetCamera               = gameObject.AddComponent<Camera>();
        _targetCamera.cullingMask   = 0;
        _targetCamera.orthographic  = true;
        _targetCamera.clearFlags    = CameraClearFlags.Nothing;
        _targetCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        _volume                     = gameObject.AddComponent<Volume>();
        _volumeProfile              = ScriptableObject.CreateInstance<VolumeProfile>();
        _volume.profile             = _volumeProfile;
        _bloom                      = _volumeProfile.Add<Bloom>(overrides: false);
        _vignette                   = _volumeProfile.Add<Vignette>(overrides: false);

        var seed = (uint)UnityEngine.Random.Range(0, 4294967295);
        Random = new Unity.Mathematics.Random(seed);

#if RECORDING_SUPPORTED
        _recording = false;
        _recorderControllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        _movieRecorderSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        _imageRecorderSettings = ScriptableObject.CreateInstance<ImageRecorderSettings>();
        _recorderController = new RecorderController(_recorderControllerSettings);
#endif

        InitializeInput();
        InitializeTime();
        FrameRate(60);
        MotionBlur(0, UniformShutter);
        Bloom(intensity: 0f);
        PrepareDrawing();
        UpdateInput();
        OnStart();  
    }

    public void StopSketch(bool immediate = false)
    {
        try { OnStop(); }
        finally
        {
            if (_targetCamera && _targetCamera.gameObject)
            {
                if (immediate)
                {
                    DestroyImmediate(_targetCamera.GetUniversalAdditionalCameraData());
                    DestroyImmediate(_targetCamera);
                    DestroyImmediate(_volume);
                    DestroyImmediate(_volumeProfile);
#if RECORDING_SUPPORTED
                    DestroyImmediate(_recorderControllerSettings);
                    DestroyImmediate(_movieRecorderSettings);
                    DestroyImmediate(_imageRecorderSettings);
#endif
                }
                else
                {
                    Destroy(_targetCamera.GetUniversalAdditionalCameraData());
                    Destroy(_targetCamera);
                    Destroy(_volume);
                    Destroy(_volumeProfile);
#if RECORDING_SUPPORTED
                    Destroy(_recorderControllerSettings);
                    Destroy(_movieRecorderSettings);
                    Destroy(_imageRecorderSettings);
#endif
                }

                _targetCamera   = null;
                _volume         = null;
                _volumeProfile  = null;

                if (_recording)
                    StopRecording();
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
        Width   = _targetCamera.pixelWidth;
        Height  = _targetCamera.pixelHeight;
        Size    = float2(Width, Height);

        Color(WHITE);
        StrokeWeight(1);
        TransparentBlend();
        NoGradient();

        Draw.LineEndCaps    = LineEndCap.None;
        Draw.ThicknessSpace = ThicknessSpace.Meters;
        Draw.Matrix         = SketchUtils.GetScreenToWorldMatrix(_targetCamera);
    }

    private void InitializeInput()
    {
        var focused = Application.isFocused;
        if (focused)
        {
            var mousePosition           = Mouse.current?.position.value ?? Vector2.zero;
            var viewportMousePosition   = _targetCamera.ScreenToViewportPoint(mousePosition);
            var pixelMousePosition      = Vector2.Scale(viewportMousePosition, new Vector2(_targetCamera.pixelWidth, _targetCamera.pixelHeight));

            MousePosition               = pixelMousePosition;
            LastMousePosition           = MousePosition;
            MouseDelta                  = 0;
            MouseX                      = pixelMousePosition.x;
            MouseY                      = pixelMousePosition.y;
        }
    }

    private void UpdateInput()
    {
        var focused = Application.isFocused;
        if (focused)
        {
            var mousePosition           = Mouse.current?.position.value ?? Vector2.zero;
            var viewportMousePosition   = _targetCamera.ScreenToViewportPoint(mousePosition);
            var pixelMousePosition      = Vector2.Scale(viewportMousePosition, new Vector2(_targetCamera.pixelWidth, _targetCamera.pixelHeight));

            MousePosition               = pixelMousePosition;
            MouseDelta                  = MousePosition - LastMousePosition;
            MouseX                      = pixelMousePosition.x;
            MouseY                      = pixelMousePosition.y;
        }

        if (focused && Keyboard.current.anyKey.wasPressedThisFrame)
            OnKeyPress();
        if (focused && Keyboard.current.anyKey.isPressed)
            OnKeyHeld();
        if (focused && Keyboard.current.anyKey.wasReleasedThisFrame)
            OnKeyRelease();

        if (focused && Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame || Mouse.current.middleButton.wasPressedThisFrame)
            OnMousePress();
        if (focused && Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed || Mouse.current.middleButton.isPressed)
            OnMouseHeld();
        if (focused && Mouse.current.leftButton.wasReleasedThisFrame || Mouse.current.rightButton.wasReleasedThisFrame || Mouse.current.middleButton.wasReleasedThisFrame)
            OnMouseRelease();
    }

    protected virtual void OnStart(){}
    protected virtual void OnStop(){}
    protected virtual void OnDrawBackground(){}
    protected virtual void OnDraw(){}
    protected virtual void OnDrawForeground(){}


    [NonSerialized] public float Time;
    [NonSerialized] public double PreciseTime;
    [NonSerialized] public float DeltaTime;
    [NonSerialized] public double PreciseDeltaTime;
    [NonSerialized] public int FrameCount;
    public int FrameRate() => Application.targetFrameRate;
    public void FrameRate(int newFrameRate) => Application.targetFrameRate = newFrameRate;

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

    public void Bloom(float threshold = 1f, float intensity = 1f, float scatter = 0.7f)
    {
        _bloom.threshold.overrideState  = true;
        _bloom.intensity.overrideState  = true;
        _bloom.scatter.overrideState    = true;

        _bloom.threshold.value          = threshold;
        _bloom.intensity.value          = intensity;
        _bloom.scatter.value            = scatter;
    }

    public void Vignette(float4 color, float intensity = 1f, float smoothness = 0.2f)
    {
        _vignette.color.overrideState       = true;
        _vignette.intensity.overrideState   = true;
        _vignette.smoothness.overrideState  = true;

        _vignette.color.value               = new Color(color.x, color.y, color.z, color.w);
        _vignette.intensity.value           = intensity;
        _vignette.smoothness.value          = smoothness;
    }

    public void Vignette(float intensity = 1f, float smoothness = 0.2f)
    {
        _vignette.color.overrideState       = true;
        _vignette.intensity.overrideState   = true;
        _vignette.smoothness.overrideState  = true;

        _vignette.color.value               = new Color(0f, 0f, 0f, 1f);
        _vignette.intensity.value           = intensity;
        _vignette.smoothness.value          = smoothness;
    }

    public void ScreenShake(int seed, float time, float amplitude = 1, float frequency = 12f)
    {
        noise.snoise(seed + float3(time, time, time) * frequency, out var offset);
        offset *= amplitude;

        Translate(offset.xy);
    }

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
    [NonSerialized] public float2 LastMousePosition;
    [NonSerialized] public float2 MouseDelta;

    public enum MouseButton { Left, Middle, Right }
    public bool MouseButtonPressed(MouseButton mouseButton)
    {
        if (_isSubFrame || !Application.isFocused)
            return false;
        return mouseButton switch
        {
            MouseButton.Left => Mouse.current.leftButton.wasPressedThisFrame,
            MouseButton.Middle => Mouse.current.middleButton.wasPressedThisFrame,
            MouseButton.Right => Mouse.current.rightButton.wasPressedThisFrame,
            _ => false
        };
    }
    public bool MouseButtonHeld(MouseButton mouseButton)
    {
        if (_isSubFrame || !Application.isFocused)
            return false;
        return mouseButton switch
        {
            MouseButton.Left => Mouse.current.leftButton.isPressed,
            MouseButton.Middle => Mouse.current.middleButton.isPressed,
            MouseButton.Right => Mouse.current.rightButton.isPressed,
            _ => false
        };
    }
    public bool MouseButtonReleased(MouseButton mouseButton)
    {
        if (_isSubFrame || !Application.isFocused)
            return false;
        return mouseButton switch
        {
            MouseButton.Left => Mouse.current.leftButton.wasReleasedThisFrame,
            MouseButton.Middle => Mouse.current.middleButton.wasReleasedThisFrame,
            MouseButton.Right => Mouse.current.rightButton.wasReleasedThisFrame,
            _ => false
        };
    }
    
    public bool KeyPressed(Key key) => !_isSubFrame && Application.isFocused && Keyboard.current[key].wasPressedThisFrame;
    public bool KeyHeld(Key key) => !_isSubFrame && Application.isFocused && Keyboard.current[key].isPressed;
    public bool KeyReleased(Key key) => !_isSubFrame && Application.isFocused && Keyboard.current[key].wasReleasedThisFrame;

    public void Color(float brightness) => Draw.Color = new Color(brightness, brightness, brightness, _drawAlpha);
    public void Color(float3 rgb) => Draw.Color = new Color(rgb.x, rgb.y, rgb.z, _drawAlpha);
    public void Color(float4 rgba) => Draw.Color = new Color(rgba.x, rgba.y, rgba.z, rgba.w * _drawAlpha);

    public void AdditiveBlend() => Draw.BlendMode = ShapesBlendMode.Additive;
    public void TransparentBlend() => Draw.BlendMode = ShapesBlendMode.Transparent;

    public void StrokeWeight(float weight) => Draw.Thickness = weight;

    public void NoGradient()
    {
        Draw.GradientFill = GradientFill.defaultFill;
        Draw.UseGradientFill = false;
    }

    public void LinearGradient(float4 startColor, float4 endColor, float2 startPosition, float2 endPosition)
    {
        Draw.UseGradientFill            = true;
        Draw.GradientFillColorStart     = new Color(startColor.x, startColor.y, startColor.z, startColor.w);
        Draw.GradientFillColorEnd       = new Color(endColor.x, endColor.y, endColor.z, endColor.w);
        Draw.GradientFillType           = FillType.LinearGradient;
        Draw.GradientFillSpace          = FillSpace.Local;
        Draw.GradientFillLinearStart    = (Vector2)startPosition;
        Draw.GradientFillLinearEnd      = (Vector2)endPosition;
    }
    public void LinearGradient(float3 startColor, float3 endColor, float2 startPosition, float2 endPosition) => LinearGradient(float4(startColor, 1f), float4(endColor, 1f), startPosition, endPosition);
    public void LinearGradient(float4 startColor, float4 endColor) => LinearGradient(startColor, endColor, float2(Width / 2, 0f), float2(Width / 2, Height));
    public void LinearGradient(float3 startColor, float3 endColor) => LinearGradient(float4(startColor, 1f), float4(endColor, 1f), float2(Width / 2, 0f), float2(Width / 2, Height));

    public void RadialGradient(float4 startColor, float4 endColor, float2 origin, float radius)
    {
        Draw.UseGradientFill            = true;
        Draw.GradientFillColorStart     = new Color(startColor.x, startColor.y, startColor.z, startColor.w);
        Draw.GradientFillColorEnd       = new Color(endColor.x, endColor.y, endColor.z, endColor.w);
        Draw.GradientFillType           = FillType.RadialGradient;
        Draw.GradientFillSpace          = FillSpace.Local;
        Draw.GradientFillRadialOrigin   = (Vector2)origin;
        Draw.GradientFillRadialRadius   = radius;
    }
    public void RadialGradient(float3 innerColor, float3 outerColor, float2 origin, float radius) => RadialGradient(float4(innerColor, 1f), float4(outerColor, 1f), origin, radius);

    public void RadialGradient(float4 innerColor, float4 outerColor) => RadialGradient(innerColor, outerColor, Size / 2f, max(Width, Height));
    public void RadialGradient(float3 innerColor, float3 outerColor) => RadialGradient(float4(innerColor, 1f), float4(outerColor, 1f), Size / 2f, max(Width, Height));


    public void Fill() => Draw.Rectangle(new Rect(0, 0, Width, Height), cornerRadius: 0f);
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

    public void ResetMatrix() => Draw.Matrix = SketchUtils.GetScreenToWorldMatrix(_targetCamera);

    public void Translate(float2 offset) => Draw.Matrix *= Matrix4x4.Translate((Vector2)offset);
    public void Translate(float offsetX, float offsetY) => Draw.Matrix *= Matrix4x4.Translate(new Vector3(offsetX, offsetY));

    public void Rotate(float degrees) => RotateAround(Width / 2, Height / 2, degrees);
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

#if RECORDING_SUPPORTED 
    public enum RecordingMode { ImageSequence, Movie }
    public enum RecordingQuality { Low, High }
    public void StartRecording(RecordingMode mode, RecordingQuality quality)
    {
        _recording = true;

        var take = UnityEditor.EditorPrefs.GetInt(RecordingTakeKey, 0);        
        var outputPath = Application.dataPath + "/../Recordings/" + gameObject.name;

        RecorderSettings recorderSettings = null;
        switch (mode)
        {
            case RecordingMode.ImageSequence:
            {
                _imageRecorderSettings.Enabled = true;
                _movieRecorderSettings.Enabled = false;

                _lastRecordingPath = outputPath + $"/Take_{take}/";
                outputPath = outputPath + $"/Take_{take}/{gameObject.name}_Take_{take}_{DefaultWildcard.Frame}";

                _imageRecorderSettings.OutputFormat = quality switch 
                { 
                    RecordingQuality.Low => ImageRecorderSettings.ImageRecorderOutputFormat.JPEG,
                    RecordingQuality.High => ImageRecorderSettings.ImageRecorderOutputFormat.PNG,
                    _ => ImageRecorderSettings.ImageRecorderOutputFormat.PNG
                };

                _imageRecorderSettings.CaptureAlpha = false;
                _imageRecorderSettings.OutputFile = outputPath;
                _imageRecorderSettings.FrameRatePlayback = FrameRatePlayback.Constant;
                _imageRecorderSettings.FrameRate = FrameRate();
                _imageRecorderSettings.CapFrameRate = true;
                _imageRecorderSettings.imageInputSettings = new GameViewInputSettings();

                recorderSettings = _imageRecorderSettings;
                break;
            }
            case RecordingMode.Movie:
            {
                _movieRecorderSettings.Enabled = true;
                _imageRecorderSettings.Enabled = false;

                _lastRecordingPath = outputPath;
                outputPath = outputPath + $"/{gameObject.name}_Take_{take}";

                _movieRecorderSettings.OutputFile = outputPath;
                _movieRecorderSettings.CaptureAudio = false;
                _movieRecorderSettings.CaptureAlpha = false;
                _movieRecorderSettings.FrameRatePlayback = FrameRatePlayback.Constant;
                _movieRecorderSettings.FrameRate = FrameRate();
                _movieRecorderSettings.CapFrameRate = true;
                _movieRecorderSettings.EncoderSettings = new CoreEncoderSettings
                {
                    Codec = CoreEncoderSettings.OutputCodec.WEBM,
                    EncodingQuality = quality switch 
                    {
                        RecordingQuality.Low => CoreEncoderSettings.VideoEncodingQuality.Low,
                        RecordingQuality.High => CoreEncoderSettings.VideoEncodingQuality.High,
                        _ => CoreEncoderSettings.VideoEncodingQuality.Low
                    }
                };

                recorderSettings = _movieRecorderSettings;
                break;
            }
        };
        

        if (recorderSettings == null)
        {
            Debug.LogError($"Failed to start recording. Couldn't find recorder settings for {mode}");
            return;
        }

        _recorderControllerSettings.AddRecorderSettings(recorderSettings);
        _recorderControllerSettings.SetRecordModeToManual();

        _recorderController.PrepareRecording();
        _recorderController.StartRecording();
    }

    public void StopRecording(bool revealInFolder = false)
    {
        if (!_recording)
            return;
        _recording = false;
        var take = UnityEditor.EditorPrefs.GetInt(RecordingTakeKey, 0);
        UnityEditor.EditorPrefs.SetInt(RecordingTakeKey, take + 1);
        _recorderController.StopRecording();

        if (revealInFolder)
            Application.OpenURL($"file:///{_lastRecordingPath}");
    }
    public void ResetRecordingTakeCount() => UnityEditor.EditorPrefs.SetInt(RecordingTakeKey, 0);
    public string GetLastRecordingPath() => _lastRecordingPath;
#endif
}