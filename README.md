Create Processing-like sketches in Unity for URP.

Mostly made for myself and very incomplete!

## ⚠️ Requires Shapes ⚠️
For now, this package uses the [Shapes](https://www.acegikmo.com/shapes/) library for rendering. This is a paid asset.

## Cool Stuff
- Easily add high-quality accumulation motion blur by simply calling `MotionBlur(subFrames)` from `OnStart`
- Easily record your sketch by calling `StartRecording(mode, quality)` from `OnStart`
- Easily add screen shake by calling `ScreenShake(seed, time, amp, freq)` from `OnDraw`

## Instructions
- Install [Shapes](https://www.acegikmo.com/shapes/)
- Add Shapes' Immediate Mode render feature
- Add Sketch package via git url `https://github.com/keenanwoodall/Sketch.git`
- Create a new script derived from the `Sketch` base class.
- Override sketch functions like `OnStart` and `OnDraw`.
- Add script to game-object in a blank scene

I recommend poking around the `Sketch` base class to see the various utilities and functions it provides.
Its goal is to provide (almost) everything you need in a simple API.
For example, rather than using `UnityEngine.Time.time` you can simply use the `Time` variable.

`Sketch` components are (currently) not the best neighbor since sketches are meant to the the primary "application."
You can have other stuff happening outside of the sketch if you like, but you may run into issues.
Sketches high-jack the input system and update it manually to support their custom update loop.
They run at 60 fps by default, so there will be flickering if you have any cameras in your scene.


TODO 🤞
- Get/Set pixels
- Integrated recorder
- Multiple canvases
- Audio
- 3D mode
- Custom rendering backend
- Hot reloading
- Runtime code editing
- GUI
- Physics
- Masking
- Shaders

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
<details>
<summary>Shooter</summary>

![Unity_Qo4hvAeTxD](https://github.com/keenanwoodall/Sketch/assets/9631530/bf9646a8-8937-4e95-a6b5-174caae946a7)

```cs
struct Player { public float2 position, velocity, size; }
struct Projectile { public float2 position, velocity; public float size; }
struct Target { public float2 position, velocity; public float4 color; public float radius; }
struct Shake { public float startTime; }
Player player;
List<Projectile> projectiles;
List<Target> targets;
List<Shake> shakes;
float _lastShootTime;
protected override void OnStart()
{
    player = new Player 
    {
        position = new(Width / 2, 0),
        size = float2(50, 100)
    };
    projectiles = new();
    targets = new();
    shakes = new();
    for (int i = 0; i < 10; i++)
        targets.Add(new Target { position = RandomScreenPoint(100), radius = Random.NextFloat(20, 50), color = RED });
    _lastShootTime = float.NegativeInfinity;
    Bloom();
    MotionBlur(60, SmoothShutter);
}
protected override void OnDrawBackground()
{
    LinearGradient(BLACK, float4(0.05f, 0.02f, 0.1f, 1f));
    Fill();
}
protected override void OnDraw()
{
    AdditiveBlend();
    // Player Physics
    {
        var movementSpeed = 1_000_000f;
        var jumpSpeed = 3500f;
        // Move left/right
        if (KeyHeld(Key.A) || KeyHeld(Key.LeftArrow))
            player.velocity.x = lerp(player.velocity.x, -movementSpeed, 1f - exp(-DeltaTime));
        if (KeyHeld(Key.D) || KeyHeld(Key.RightArrow))
            player.velocity.x = lerp(player.velocity.x, movementSpeed, 1f - exp(-DeltaTime));
        else
            player.velocity.x = lerp(player.velocity.x, 0f, 1f - exp(-DeltaTime * 20f));
        // Jump
        if (KeyPressed(Key.Space) || KeyPressed(Key.W) || KeyPressed(Key.UpArrow))
            player.velocity.y = max(jumpSpeed, player.velocity.y);
        
        // Gravity
        var gravityForce = float2(0f, -20000f);
        player.velocity += gravityForce * DeltaTime;
        // Apply velocity
        player.position += player.velocity * DeltaTime;
        // Window edges
        var playerCenter = player.position + float2(0, player.size.y / 2f);
        HandleScreenBoundary(ref playerCenter, ref player.velocity, player.size, 0f);
        player.position = playerCenter - float2(0, player.size.y / 2f);
    }
    // Projectile physics
    {
        for (int i = 0; i < projectiles.Count; i++)
        {
            var projectile = projectiles[i];
            projectile.position += projectile.velocity * DeltaTime;
            projectiles[i] = projectile;
            if (CheckScreenBoundary(projectile.position, projectile.size))
            {
                projectiles.RemoveAt(i);
                i--;
            }
        }
    }
    // Target physics
    {
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            for (int j = 0; j < projectiles.Count; j++)
            {
                var projectile = projectiles[j];
                var offset = projectile.position - target.position;
                var distance = length(offset);
                // Projectile hit target!
                if (distance < target.radius + projectile.size)
                {
                    // Knockback force
                    target.velocity += projectile.velocity / (target.radius * target.radius * PI) * 500f;
                    // Flash white
                    target.color = WHITE * 2f;
                    target.color.w = 1f;
                    // Delete projectile
                    projectiles.RemoveAt(j);
                    j--;
                }
            }
            // Targets bounce of screen edges
            HandleScreenBoundary(ref target.position, ref target.velocity, float2(target.radius), bounciness: 1f);
            target.velocity = lerp(target.velocity, 0, 1f - exp(-DeltaTime * 5f));
            target.position += target.velocity * DeltaTime;
            targets[i] = target;
        }
    }
    // Camera shake
    {
        for (int i = 0; i < shakes.Count; i++)
        {
            var startTime = shakes[i].startTime;
            var elapsedTime = Time - startTime;
            // Amplitude dies out over 0.2 seconds
            var amplitude = smoothstep(0.2f, 0f, elapsedTime) * 2f;
            // Remove shake if amplitude is small enough
            if (amplitude <= 0.01f)
            {
                shakes.RemoveAt(i);
                i--;
                continue;
            }
            // Shake the canvas
            ScreenShake(seed: i * 10, time: elapsedTime, amplitude: amplitude, frequency: 5f);
        }
    }
    // Draw player
    Rectangle(player.position + float2(0, player.size.y / 2f), player.size);
    // Draw projectiles
    Color(float3(1f, 0.5f, 0.1f) * 10f); // Multiply color by 10 for it to glow
    for (int i = 0; i < projectiles.Count; i++)
        Circle(projectiles[i].position, projectiles[i].size);
    // Draw targets
    for (int i = 0; i < targets.Count; i++)
    {
        var target = targets[i];
        Color(target.color);
        Circle(target.position, target.radius);
        target.color = lerp(target.color, RED, DeltaTime * 5f);
        targets[i] = target;
    }
}
// Machine Gun
protected override void OnMouseHeld()
{
    if (!MouseButtonHeld(MouseButton.Left))
        return;
    var shootDelay = 0.1f;
    var shootKick = 100f;
    if (Time - _lastShootTime < shootDelay)
        return;
    _lastShootTime = Time;
    var bulletSpeed = 10000f;
    var bulletAngle = Random.NextFloat(-2f, 5f);
    Shoot(bulletSpeed, bulletAngle, Random.NextFloat(2, 4), out var _, out var direction);
    player.velocity -= normalize(direction) * shootKick;
}
// Shotgun
protected override void OnMousePress()
{
    if (!MouseButtonPressed(MouseButton.Right))
        return;
    int burstCount = 8;
    var shootKick = 100f;
    for (int i = 0; i < burstCount; i++)
    {
        var bulletAngle = Random.NextFloat(-10f, 10f);
        var bulletSpeed = Random.NextFloat(10_000f, 15_000);
        Shoot(bulletSpeed, bulletAngle, Random.NextFloat(3, 6), out var _, out var direction);
        player.velocity -= normalize(direction) * shootKick;
    }
}
private void Shoot(float speed, float angle, float size, out float2 position, out float2 direction)
{
    var playerCenter = player.position + float2(0f, player.size.y * 0.5f);
    var aimSign = sign(MouseX - playerCenter);
    var projectilePosition = playerCenter + aimSign * player.size.x * 0.5f;
    var projectileDirection = normalize(MousePosition - projectilePosition);
    // Rotate direction based on relative angle
    var angleRadians = radians(angle);
    var cAngle = cos(angleRadians);
    var sAngle = sin(angleRadians);
    projectileDirection = float2(projectileDirection.x * cAngle - projectileDirection.y * sAngle, projectileDirection.x * sAngle + pro
    var projectileVelocity = projectileDirection * speed;
    // Add new projectile
    projectiles.Add(new Projectile { position = projectilePosition, velocity = projectileVelocity, size = size });
    // Add new camera shale
    shakes.Add(new Shake { startTime = Time });
    position = projectilePosition;
    direction = projectileDirection;
}
private bool HandleScreenBoundary(ref float2 position, ref float2 velocity, float2 size, float bounciness)
{
    var hit = false;
    var halfSize = size / 2f;
    // Bottom
    if (position.y - halfSize.y < 0)
    {
        position.y = halfSize.y;
        velocity.y *= -bounciness;
        hit = true;
    }
    // Top
    if (position.y + halfSize.y > Height)
    {
        position.y = Height - halfSize.y;
        velocity.y *= -bounciness;
        hit = true;
    }
    // Left
    if (position.x - halfSize.x < 0f)
    {
        position.x = halfSize.x;
        velocity.x *= -bounciness;
        hit = true;
    }
    // Right
    if (position.x > Width - halfSize.x)
    {
        position.x = Width - halfSize.x;
        velocity.x *= -bounciness;
        hit = true;
    }
    return hit;
}
private bool CheckScreenBoundary(float2 position, float2 size)
{
    var halfSize = size / 2f;
    // Bottom
    if (position.y - halfSize.y < 0)
        return true;
    // Top
    if (position.y + halfSize.y > Height)
        return true;
    // Left
    if (position.x - halfSize.x < 0f)
        return true;
    // Right
    if (position.x > Width - halfSize.x)
        return true;
    return false;
}
```
</details>

