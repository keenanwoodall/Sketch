Create Processing-like sketches in Unity

### Examples

Hello Circle

![image](https://github.com/keenanwoodall/Sketch/assets/9631530/fc59a2d6-d6e7-4605-a496-4f159668b114)

```cs
public class SimpleExample : Sketch
{
    protected override void OnDraw()
    {
        Color(BLACK);
        Fill();
        Color(WHITE);
        Circle(Width / 2, Height / 2, 100f);
    }
}
```

Accumulation Motion Blur

![Unity_oc1xQHLEuU](https://github.com/keenanwoodall/Sketch/assets/9631530/3d6f9331-2ddc-4927-91eb-6b05c4367c3f)

```cs
public float speed = 1080;
public float speedMult = 1;
public int circleCount = 6;
float angle;
protected override void OnStart()
{
    MotionBlur(subFrames: 256, shutterProfile: SmoothShutter);   
}
protected override void OnDrawBackground()
{
    Color(BLACK);
    Fill();
}
protected override void OnDraw()
{
    angle -= speed * speedMult * DeltaTime * math.pow(MouseX / Width, 2f);
    angle %= 360f;
    AdditiveBlend();
    Rotate(angle);
    StrokeWeight(2);
    var center = Size / 2;
    for (int i = 0; i < circleCount; i++)
    {
        var offset = PointOnCircle(radius: 300, angle: i / (float)circleCount * 360f);
        var color = HSV(i / (float)circleCount, 1f, 1f);
        Color(color);
        Ring(position: center + offset, radius: 20);
        Line(center, center + offset);
    }
}
```

TODO:
- Get/Set pixels
- Integrated recorder
- Multiple canvases
- Audio
- 3D mode
- Custom rendering backend
- Hot reloading
- Runtime code editing
- GUI

## ⚠️ Requires Shapes ⚠️
For now, this package uses the [Shapes](https://www.acegikmo.com/shapes/) library for rendering. This is a paid asset.
