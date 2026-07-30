using Godot;

public partial class Background : Node2D
{
    [Export] public Vector2 AreaMin = Vector2.Zero;
    [Export] public Vector2 AreaMax = new Vector2(1000, 500);
    [Export] public int LightRayCount = 14;
    // Fraction of area width where the sun sits above the surface (0 = left edge, 1 = right edge)
    [Export] public float SunXFraction = 0.62f;
    [Export] public int AmbientBubbleCount = 18;

    private float _time = 0f;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    private struct LightRay
    {
        public float X;
        public float TopWidth;
        public float Phase;
        public float Speed;
        public float Length;
    }

    private struct AmbientBubble
    {
        public float X;
        public float Y;
        public float Speed;
        public float Radius;
        public float Phase;
    }

    private LightRay[] _rays;
    private AmbientBubble[] _bubbles;

    public override void _Ready()
    {
        ZIndex = -10;
        _rng.Randomize();
        InitRays();
        InitBubbles();
    }

    private void InitRays()
    {
        _rays = new LightRay[LightRayCount];
        float span = AreaMax.X - AreaMin.X;
        for (int i = 0; i < LightRayCount; i++)
        {
            _rays[i] = new LightRay
            {
                X = AreaMin.X + span * ((i + 0.5f) / LightRayCount) + _rng.RandfRange(-span * 0.03f, span * 0.03f),
                TopWidth = _rng.RandfRange(2f, 8f),
                Phase = _rng.RandfRange(0f, Mathf.Tau),
                Speed = _rng.RandfRange(0.10f, 0.28f),
                Length = _rng.RandfRange(0.45f, 0.92f),
            };
        }
    }

    private void InitBubbles()
    {
        _bubbles = new AmbientBubble[AmbientBubbleCount];
        float width = AreaMax.X - AreaMin.X;
        float height = AreaMax.Y - AreaMin.Y;
        for (int i = 0; i < AmbientBubbleCount; i++)
        {
            _bubbles[i] = new AmbientBubble
            {
                X = AreaMin.X + _rng.RandfRange(0, width),
                Y = AreaMin.Y + _rng.RandfRange(0, height),
                Speed = _rng.RandfRange(12f, 40f),
                Radius = _rng.RandfRange(2f, 7f),
                Phase = _rng.RandfRange(0f, Mathf.Tau),
            };
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;

        float height = AreaMax.Y - AreaMin.Y;
        float width = AreaMax.X - AreaMin.X;
        for (int i = 0; i < _bubbles.Length; i++)
        {
            _bubbles[i].Y -= _bubbles[i].Speed * dt;
            if (_bubbles[i].Y < AreaMin.Y - 12f)
            {
                _bubbles[i].Y = AreaMax.Y + _rng.RandfRange(0, height * 0.3f);
                _bubbles[i].X = AreaMin.X + _rng.RandfRange(0, width);
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var topLeft = AreaMin;
        var size = AreaMax - AreaMin;
        if (size == Vector2.Zero) return;

        // ── Water gradient ─────────────────────────────────────────────────────
        // Top (near surface): bright cyan-turquoise → bottom: deep teal-blue
        const int strips = 60;
        float stripH = size.Y / strips;
        for (int i = 0; i < strips; i++)
        {
            float t = (float)i / strips;
            var color = new Color(
                Mathf.Lerp(0.08f, 0.02f, t),   // R – stays low, slight warmth near top
                Mathf.Lerp(0.55f, 0.22f, t),   // G – bright teal fading to muted
                Mathf.Lerp(0.75f, 0.44f, t),   // B – vivid azure → deep blue
                1.0f
            );
            DrawRect(new Rect2(topLeft.X, topLeft.Y + i * stripH, size.X, stripH + 1f), color);
        }

        // ── Light rays ─────────────────────────────────────────────────────────
        // All rays diverge from a single sun point above the surface
        float sunX = AreaMin.X + size.X * SunXFraction;
        var rayTint = new Color(0.90f, 1.0f, 0.85f, 1f);
        foreach (var ray in _rays)
        {
            // Subtle sway — slow and gentle, not dramatic
            float sway = Mathf.Sin(_time * ray.Speed + ray.Phase) * 6f;
            float topX = ray.X + sway;

            // Bottom tip leans away from the sun, simulating diverging god-rays
            float lean = (topX - sunX) / size.X * size.Y * 0.22f;
            float rayLen = size.Y * ray.Length;
            float botX = topX + lean;

            float halfTop = ray.TopWidth * 0.5f;
            float halfBot = ray.TopWidth * 0.7f;   // barely spreads — stays thin

            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * ray.Speed * 0.55f + ray.Phase);
            float peakAlpha = Mathf.Lerp(0.12f, 0.26f, pulse);

            // Soft outer glow — wider trapezoid, very transparent, fades to nothing
            DrawPolygon(
                new Vector2[]
                {
                    new Vector2(topX - halfTop * 5f, topLeft.Y),
                    new Vector2(topX + halfTop * 5f, topLeft.Y),
                    new Vector2(botX + halfBot * 6f,  topLeft.Y + rayLen),
                    new Vector2(botX - halfBot * 6f,  topLeft.Y + rayLen),
                },
                new Color[]
                {
                    new Color(rayTint.R, rayTint.G, rayTint.B, peakAlpha * 0.10f),
                    new Color(rayTint.R, rayTint.G, rayTint.B, peakAlpha * 0.10f),
                    new Color(rayTint.R, rayTint.G, rayTint.B, 0f),
                    new Color(rayTint.R, rayTint.G, rayTint.B, 0f),
                }
            );

            // Bright core — thin, gradient from peakAlpha at top to fully transparent
            DrawPolygon(
                new Vector2[]
                {
                    new Vector2(topX - halfTop, topLeft.Y),
                    new Vector2(topX + halfTop, topLeft.Y),
                    new Vector2(botX + halfBot,  topLeft.Y + rayLen),
                    new Vector2(botX - halfBot,  topLeft.Y + rayLen),
                },
                new Color[]
                {
                    new Color(rayTint.R, rayTint.G, rayTint.B, peakAlpha),
                    new Color(rayTint.R, rayTint.G, rayTint.B, peakAlpha),
                    new Color(rayTint.R, rayTint.G, rayTint.B, 0f),
                    new Color(rayTint.R, rayTint.G, rayTint.B, 0f),
                }
            );
        }

        // ── Surface shimmer ────────────────────────────────────────────────────
        // Height oscillates slowly downward: base 7px, grows up to ~18px
        float surfaceHeightAnim = 7f
            + (Mathf.Sin(_time * 0.70f) + 1f) * 0.5f * 7f
            + (Mathf.Sin(_time * 0.42f + 1.3f) + 1f) * 0.5f * 4f;
        float shimmer = (Mathf.Sin(_time * 1.8f) + 1f) * 0.5f;
        var surfaceGlow = new Color(0.65f, 0.94f, 1.0f, 0.28f + shimmer * 0.14f);
        DrawRect(new Rect2(topLeft.X, topLeft.Y, size.X, surfaceHeightAnim), surfaceGlow);
        DrawRect(new Rect2(topLeft.X, topLeft.Y, size.X, 2f), new Color(1f, 1f, 1f, 0.18f));

        // Small undulating highlight bands
        for (int b = 0; b < 3; b++)
        {
            float bandY = topLeft.Y + surfaceHeightAnim + 4f + b * 6f;
            float bandAlpha = 0.06f * (Mathf.Sin(_time * 1.2f + b * 1.8f) + 1f) * 0.5f;
            DrawRect(new Rect2(topLeft.X, bandY, size.X, 2f), new Color(1f, 1f, 1f, bandAlpha));
        }

        // ── Ambient background bubbles ─────────────────────────────────────────
        foreach (var bubble in _bubbles)
        {
            float sway = Mathf.Sin(_time * 0.7f + bubble.Phase) * 5f;
            float alpha = 0.10f + 0.05f * Mathf.Sin(_time * 1.1f + bubble.Phase);
            var bubblePos = new Vector2(bubble.X + sway, bubble.Y);

            // Body
            DrawCircle(bubblePos, bubble.Radius, new Color(0.75f, 0.94f, 1.0f, alpha));
            // Specular highlight
            var highlightPos = bubblePos + new Vector2(-bubble.Radius * 0.28f, -bubble.Radius * 0.28f);
            DrawCircle(highlightPos, bubble.Radius * 0.32f, new Color(1f, 1f, 1f, alpha * 0.9f));
        }
    }
}
