Create Processing-like sketches in Unity.

Mostly made for fun and very incomplete!

## ⚠️ Requires Shapes ⚠️
For now, this package uses the [Shapes](https://www.acegikmo.com/shapes/) library for rendering. This is a paid asset.

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

### Examples

<details>
<summary></summart>Hello Circle</summary>

![image](https://github.com/keenanwoodall/Sketch/assets/9631530/1f62a32c-391d-4c65-ae55-26e2c90711bd)

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
</details>
<details>
<summary>Accumulation Motion Blur</summary>

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
</details>
<details>
<summary>Bouncing Balls</summary>

![Unity_qWt2rhO9GQ](https://github.com/keenanwoodall/Sketch/assets/9631530/dc14cbc6-f35f-4b9b-ae0e-84397d2c5cd5)


```cs
public class Ball
{
    public float Radius;
    public float2 Position;
    public float2 Velocity;
    public float4 Color;
}
public float gravity = -1f;
public int ballCount = 1;
public float minRadius = 10;
public float maxRadius = 50;
public float minInitialVelocity = 1000;
public float maxInitialVelocity = 5000;
List<Ball> balls;
protected override void OnStart()
{
    FrameRate(60);
    MotionBlur(30, UniformShutter);
    balls = new();
    for (int i = 0; i < ballCount; i++)
    {
        var radius = Random.NextFloat(minRadius, maxRadius);
        var newBall = new Ball
        {
            Radius = radius,
            Position = RandomScreenPoint(padding: radius),
            Velocity = Random.NextFloat2Direction() * Random.NextFloat(minInitialVelocity, maxInitialVelocity),
            Color = RandomColorHue(saturation: 0.8f, value: 1f)
        };
        balls.Add(newBall);
    }
}
protected override void OnDrawBackground()
{
    Color(BLACK);
    Fill();
}
protected override void OnDraw()
{
    AdditiveBlend();
    Color(WHITE);
    foreach (var ball in balls)
    {
        ball.Velocity += float2(0, gravity * DeltaTime);
        ball.Position += ball.Velocity * DeltaTime;
        EdgeBounce(ball);
    }
    
    foreach (var ball in balls)
    {
        Color(ball.Color);
        Circle(ball.Position, ball.Radius);
    }
}
protected override void OnMouseHeld()
{
    var radius = Random.NextFloat(minRadius, maxRadius);
    var newBall = new Ball
    {
        Radius = radius,
        Position = MousePosition,
        Velocity = Random.NextFloat2Direction() * Random.NextFloat(minInitialVelocity, maxInitialVelocity),
        Color = RandomColorHue(saturation: 0.8f, value: 1f)
    };
    balls.Add(newBall);
}
void EdgeBounce(Ball ball)
{
    if (ball.Position.x < ball.Radius)
    {
        ball.Position.x = ball.Radius;
        ball.Velocity.x *= -1;
    }
    if (ball.Position.x > Width - ball.Radius)
    {
        ball.Position.x = Width - ball.Radius;
        ball.Velocity.x *= -1;
    }
    if (ball.Position.y < ball.Radius)
    {
        ball.Position.y = ball.Radius;
        ball.Velocity.y *= -1;
    }
    if (ball.Position.y > Height - ball.Radius)
    {
        ball.Position.y = Height - ball.Radius;
        ball.Velocity.y *= -1;
    }
}
```
</details>
<details>
<summary>Smooth Snake</summary>

![Unity_FXp8344Bb3](https://github.com/keenanwoodall/Sketch/assets/9631530/76c25209-8c71-4b2a-b36b-c64fc9d52df0)

```cs
public float radius = 25f;
public float followSpeed = 30f;
public float followPadding = 5f;
float2[] positions;
protected override void OnStart()
{
    positions = new float2[8];
    for (int i = 0; i < positions.Length; i++)
        positions[i] = (Size / 2f) + left().xy * i * (radius + followPadding);
    FrameRate(60);
    MotionBlur(256, SmoothShutter);
}
protected override void OnDrawBackground()
{
    Color(BLACK);
    Fill();
}
protected override void OnDraw()
{
    positions[0] = lerp(positions[0], MousePosition, 1f - exp(-followSpeed * DeltaTime));
    AdditiveBlend();
    Color(WHITE);
    Circle(positions[0], radius);
    for (int i = 1; i < positions.Length; i++)
    {
        var currentPosition = positions[i];
        var targetPosition  = positions[i - 1];
        var direction       = normalize(targetPosition - currentPosition);
        var newPosition     = lerp(currentPosition, targetPosition - direction * (radius * 2f + followPadding), 1f - exp(-followSpeed * DeltaTime));
        
        Circle(newPosition, radius);
        positions[i] = newPosition;
    }
}
```
</details>
