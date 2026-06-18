using System;
using UnityEngine;
using UnityEngine.UIElements;
using YoonseulFishing.Data;

namespace YoonseulFishing.Rendering
{
    /// <summary>
    /// Procedural scene drawing with UI Toolkit <see cref="Painter2D"/>.
    ///
    /// COMPOSITION: a gentle over-the-shoulder view (~25° downward) — we sit behind
    /// the angler looking out over calm water to distant low-poly mountains under a
    /// soft sky. The boat + fisherman are seen from behind (hat, shoulders, back, an
    /// arm on the rod); fish glide as silhouettes under the surface; a grassy shore
    /// sits in a foreground corner; 윤슬 glints scatter on the water. Time of day and
    /// weather tint the sky / water / light.
    ///
    /// Painter2D fills are SOLID (no gradient brush / radial / additive), so gradients
    /// are band-approximated and soft light is layered translucent shapes. The
    /// DrawScene signature is stable, so the renderer/bootstrap that drive it are
    /// unchanged.
    /// </summary>
    public static class ScenePainter
    {
        public struct Palette
        {
            public Color SkyTop, SkyBottom;
            public Color Mtn1, Mtn2;
            public Color WaterFar, WaterNear;
            public Color ShoreSand, ShoreGrass, Reed, Lily;
            public Color LightTint; public float LightAlpha;
            public Color Sparkle, Fish;
        }

        public static Palette PaletteFor(TimeOfDay time)
        {
            switch (time)
            {
                case TimeOfDay.Sunset:
                    return new Palette
                    {
                        SkyTop = Rgb(0x514068), SkyBottom = Rgb(0xFCCC9B),
                        Mtn1 = Rgb(0x704A6B), Mtn2 = Rgb(0x94618E),
                        WaterFar = Rgb(0xD49A8C), WaterNear = Rgb(0x8A4E54),
                        ShoreSand = Rgb(0xB89663), ShoreGrass = Rgb(0x7A9A5C), Reed = Rgb(0x5A7A3C), Lily = Rgb(0x5A946A),
                        LightTint = Rgb(0xFF9E5A), LightAlpha = 0.22f, Sparkle = Rgb(0xFFD9B0), Fish = Rgb(0x3A2630),
                    };
                case TimeOfDay.Night:
                    return new Palette
                    {
                        SkyTop = Rgb(0x0D1224), SkyBottom = Rgb(0x1B2342),
                        Mtn1 = Rgb(0x19213D), Mtn2 = Rgb(0x222C52),
                        WaterFar = Rgb(0x223A56), WaterNear = Rgb(0x13192F),
                        ShoreSand = Rgb(0x595440), ShoreGrass = Rgb(0x3E5742), Reed = Rgb(0x36502E), Lily = Rgb(0x2F5A40),
                        LightTint = Rgb(0xAFC4E0), LightAlpha = 0.16f, Sparkle = Rgb(0xCFE2FA), Fish = Rgb(0x0C2436),
                    };
                default: // Day
                    return new Palette
                    {
                        SkyTop = Rgb(0xBFE3E8), SkyBottom = Rgb(0xE6F2F4),
                        Mtn1 = Rgb(0x85AFAF), Mtn2 = Rgb(0x9EBFBF),
                        WaterFar = Rgb(0x86C2CC), WaterNear = Rgb(0x49929E),
                        ShoreSand = Rgb(0xCBB180), ShoreGrass = Rgb(0x8FB76A), Reed = Rgb(0x5E8B3E), Lily = Rgb(0x57A368),
                        LightTint = Rgb(0xFFF1CE), LightAlpha = 0.16f, Sparkle = Rgb(0xFFFFFF), Fish = Rgb(0x1F4658),
                    };
            }
        }

        public struct Star { public float RelX, RelY; public float TwinkleSpeed; public bool Big; }
        public struct Sparkle { public float RelX, RelY; public float ScaleFactor; public float Phase; }
        public struct WindStroke { public float X, Y, Length, Width, Speed, Opacity; }
        public struct RainStroke { public float X, Y, Length, Speed; }
        public struct AtmMote { public float RelX, BaseY, Radius, Depth, Phase, DriftSpeed, SwayAmp; }
        public struct SplashParticle { public float X, Y, Vx, Vy, BaseSize, Alpha, Rotation, RotationSpeed; public int ShapeType; public Color Color; }

        private const float HorizonRel = 0.34f; // horizon sits in the upper third (gentle downward gaze)

        public static void DrawScene(Painter2D p, Rect rect, long ticks, TimeOfDay time, Weather weather,
            FishingState fishingState, float bobberX, float bobberY, FishSpecies splashFish, float splashProgress,
            Star[] stars, Sparkle[] sparkles, WindStroke[] windStrokes, RainStroke[] rainStrokes, AtmMote[] motes,
            SplashParticle[] particles, float rhythmScale, bool rhythmActive)
        {
            float w = rect.width, h = rect.height;
            if (w <= 1f || h <= 1f) return;
            Palette pal = PaletteFor(time);
            float hy = h * HorizonRel;

            // 1. Sky band + soft sun/moon glow.
            FillVBands(p, new Rect(0, 0, w, hy), pal.SkyTop, pal.SkyBottom, 22);
            FillCircle(p, new Vector2(w * 0.78f, hy * 0.42f), 110f, WithAlpha(pal.LightTint, pal.LightAlpha * 0.7f));
            FillCircle(p, new Vector2(w * 0.78f, hy * 0.42f), 58f, WithAlpha(pal.LightTint, pal.LightAlpha * 1.7f));

            // 2. Distant low-poly mountains along the horizon.
            DrawMountains(p, pal.Mtn1, pal.Mtn2, w, hy);

            // 3. Water plane (lighter far, darker near) + a horizon light streak.
            FillVBands(p, new Rect(0, hy, w, h - hy), pal.WaterFar, pal.WaterNear, 26);
            FillEllipse(p, new Vector2(w * 0.5f, hy + 6f), w * 0.30f, 9f, WithAlpha(pal.LightTint, pal.LightAlpha * 0.9f), 24);

            // 4. Surface wind ripples.
            DrawWindRipples(p, windStrokes, w, h, hy);

            // 5. Ambient fish gliding under the surface.
            DrawAmbientFish(p, ticks, w, h, hy, pal.Fish);

            // 6. 윤슬 glints (water only).
            DrawSparkles(p, sparkles, ticks, time, weather, pal.Sparkle, w, h, hy);

            // 7. Grassy shore corner (foreground).
            DrawShore(p, w, h, pal);

            // 8. Boat + fisherman from behind + rod, line, bobber.
            DrawBoatBehind(p, ticks, fishingState, bobberX, bobberY, w, h, pal, weather);

            // 9. Leaping fish during SPLASHING.
            if (fishingState == FishingState.Splashing && splashFish != null)
            {
                float fishX = bobberX * w + (splashProgress - 0.5f) * 200f;
                float arcH = 220f * Mathf.Sin(splashProgress * Mathf.PI);
                float fishY = bobberY * h - arcH;
                float rot = -50f + splashProgress * 100f + Mathf.Sin(ticks * 0.04f) * 15f;
                DrawJumpingFish(p, splashFish, ticks, fishX, fishY, rot, splashProgress > 0.5f);
            }

            // 10. Splash particles.
            DrawSplashParticles(p, particles);

            // 11–12. Weather.
            if (weather == Weather.Mist) DrawMistPatches(p, ticks, w, h, hy);
            if (weather == Weather.Rain) DrawRainStrokes(p, rainStrokes, w, h);

            // 13. Floating light motes.
            DrawMotes(p, motes, ticks, time, weather, w, h);

            // 14. Reeling rhythm ring.
            DrawRhythmRing(p, fishingState, rhythmScale, rhythmActive, bobberX, bobberY, w, h);
        }

        // ------------------------------------------------------------------
        //  Distance: sky + mountains
        // ------------------------------------------------------------------

        private static void DrawMountains(Painter2D p, Color m1, Color m2, float w, float hy)
        {
            FillPoly(p, WithAlpha(m1, 0.85f),
                new Vector2(0, hy), new Vector2(w * 0.20f, hy * 0.42f), new Vector2(w * 0.40f, hy * 0.80f),
                new Vector2(w * 0.62f, hy * 0.34f), new Vector2(w * 0.82f, hy * 0.74f), new Vector2(w, hy * 0.48f),
                new Vector2(w, hy), new Vector2(0, hy));
            FillPoly(p, m2,
                new Vector2(0, hy), new Vector2(w * 0.30f, hy * 0.64f), new Vector2(w * 0.54f, hy * 0.92f),
                new Vector2(w * 0.78f, hy * 0.56f), new Vector2(w, hy * 0.82f), new Vector2(w, hy));
        }

        // ------------------------------------------------------------------
        //  Water life
        // ------------------------------------------------------------------

        private static void DrawAmbientFish(Painter2D p, long ticks, float w, float h, float hy, Color fishCol)
        {
            float t = ticks * 0.001f;
            for (int i = 0; i < 4; i++)
            {
                float baseX = (0.18f + 0.20f * i) * w;
                float baseY = hy + (0.10f + 0.18f * i) * (h - hy);
                float dx = Mathf.Sin(t * (0.30f + 0.07f * i) + i) * (w * 0.12f);
                float dy = Mathf.Cos(t * (0.22f + 0.05f * i) + i * 1.7f) * (h * 0.04f);
                DrawFishSilhouette(p, baseX + dx, baseY + dy, 0.7f, dx < 0f, WithAlpha(fishCol, 0.30f));
            }
        }

        private static void DrawFishSilhouette(Painter2D p, float cx, float cy, float scale, bool faceLeft, Color col)
        {
            FillEllipse(p, new Vector2(cx, cy), 18f * scale, 7f * scale, col);
            float tx = faceLeft ? cx + 15f * scale : cx - 15f * scale;
            float tip = faceLeft ? cx + 29f * scale : cx - 29f * scale;
            FillPoly(p, col, new Vector2(tx, cy), new Vector2(tip, cy - 8f * scale), new Vector2(tip, cy + 8f * scale));
        }

        private static void DrawSparkles(Painter2D p, Sparkle[] sparkles, long ticks, TimeOfDay time, Weather weather, Color baseCol, float w, float h, float hy)
        {
            if (sparkles == null) return;
            float mul = weather == Weather.Rain ? RainSparkle(time) : weather == Weather.Mist ? MistSparkle(time) : ClearSparkle(time);
            for (int i = 0; i < sparkles.Length; i++)
            {
                Sparkle s = sparkles[i];
                float spY = s.RelY * h;
                if (spY < hy + 6f) continue; // water only
                float spX = s.RelX * w;
                float brightness = 0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(s.Phase + ticks * 0.0016f));
                float len = 12f * s.ScaleFactor * brightness;
                FillPoly(p, WithAlpha(baseCol, mul * brightness),
                    new Vector2(spX - len, spY), new Vector2(spX, spY - 3f), new Vector2(spX + len, spY), new Vector2(spX, spY + 3f));
            }
        }

        private static float ClearSparkle(TimeOfDay t) => t == TimeOfDay.Day ? 0.7f : t == TimeOfDay.Sunset ? 0.75f : 0.8f;
        private static float MistSparkle(TimeOfDay t) => t == TimeOfDay.Day ? 0.45f : t == TimeOfDay.Sunset ? 0.5f : 0.55f;
        private static float RainSparkle(TimeOfDay t) => t == TimeOfDay.Day ? 0.3f : 0.38f;

        // ------------------------------------------------------------------
        //  Shore (grassy bank, foreground corner)
        // ------------------------------------------------------------------

        private static void DrawShore(Painter2D p, float w, float h, Palette pal)
        {
            p.fillColor = pal.ShoreSand;
            p.BeginPath();
            p.MoveTo(new Vector2(0, h));
            p.LineTo(new Vector2(0, h * 0.74f));
            p.QuadraticCurveTo(new Vector2(w * 0.13f, h * 0.78f), new Vector2(w * 0.20f, h * 0.90f));
            p.QuadraticCurveTo(new Vector2(w * 0.23f, h * 0.97f), new Vector2(w * 0.14f, h));
            p.ClosePath();
            p.Fill();

            p.fillColor = pal.ShoreGrass;
            p.BeginPath();
            p.MoveTo(new Vector2(0, h));
            p.LineTo(new Vector2(0, h * 0.76f));
            p.QuadraticCurveTo(new Vector2(w * 0.10f, h * 0.80f), new Vector2(w * 0.15f, h * 0.91f));
            p.QuadraticCurveTo(new Vector2(w * 0.17f, h * 0.97f), new Vector2(w * 0.10f, h));
            p.ClosePath();
            p.Fill();

            StrokeLine(p, new Vector2(w * 0.075f, h * 0.84f), new Vector2(w * 0.066f, h * 0.74f), pal.Reed, 3f);
            StrokeLine(p, new Vector2(w * 0.105f, h * 0.86f), new Vector2(w * 0.116f, h * 0.75f), pal.Reed, 3f);
            StrokeLine(p, new Vector2(w * 0.135f, h * 0.90f), new Vector2(w * 0.128f, h * 0.79f), pal.Reed, 3f);

            DrawLilyPad(p, new Vector2(w * 0.26f, h * 0.92f), 20f, pal);
            DrawLilyPad(p, new Vector2(w * 0.17f, h * 0.99f), 15f, pal);
        }

        private static void DrawLilyPad(Painter2D p, Vector2 c, float r, Palette pal)
        {
            FillEllipse(p, c, r, r * 0.6f, pal.Lily);
            FillPoly(p, pal.WaterNear, c, new Vector2(c.x + r, c.y - r * 0.4f), new Vector2(c.x + r, c.y + r * 0.4f));
        }

        // ------------------------------------------------------------------
        //  Boat + fisherman seen from behind
        // ------------------------------------------------------------------

        private static void DrawBoatBehind(Painter2D p, long ticks, FishingState state, float bobberX, float bobberY,
            float w, float h, Palette pal, Weather weather)
        {
            float bx = w * 0.50f;
            float by = h * 0.70f + Mathf.Sin(ticks * 0.0018f) * 4f;

            // Shadow.
            FillEllipse(p, new Vector2(bx, by + 42f), 94f, 20f, new Color(0f, 0f, 0f, 0.13f));

            // Canoe hull (vertical: pointed bow away/up, wider stern near) — bezier outline.
            p.fillColor = Rgb(0xA9743C);
            p.strokeColor = Rgb(0xD8B07E);
            p.lineWidth = 3.5f;
            p.BeginPath();
            p.MoveTo(new Vector2(bx, by + 46f));
            p.BezierCurveTo(new Vector2(bx - 38f, by + 44f), new Vector2(bx - 60f, by + 14f), new Vector2(bx - 54f, by - 8f));
            p.BezierCurveTo(new Vector2(bx - 48f, by - 34f), new Vector2(bx - 18f, by - 52f), new Vector2(bx, by - 54f));
            p.BezierCurveTo(new Vector2(bx + 18f, by - 52f), new Vector2(bx + 48f, by - 34f), new Vector2(bx + 54f, by - 8f));
            p.BezierCurveTo(new Vector2(bx + 60f, by + 14f), new Vector2(bx + 38f, by + 44f), new Vector2(bx, by + 46f));
            p.ClosePath();
            p.Fill();
            p.Stroke();

            // Inner cavity (we look into the boat).
            p.fillColor = Rgb(0x5E3F23);
            p.BeginPath();
            p.MoveTo(new Vector2(bx, by + 34f));
            p.BezierCurveTo(new Vector2(bx - 27f, by + 32f), new Vector2(bx - 43f, by + 10f), new Vector2(bx - 39f, by - 7f));
            p.BezierCurveTo(new Vector2(bx - 34f, by - 28f), new Vector2(bx - 13f, by - 41f), new Vector2(bx, by - 43f));
            p.BezierCurveTo(new Vector2(bx + 13f, by - 41f), new Vector2(bx + 34f, by - 28f), new Vector2(bx + 39f, by - 7f));
            p.BezierCurveTo(new Vector2(bx + 43f, by + 10f), new Vector2(bx + 27f, by + 32f), new Vector2(bx, by + 34f));
            p.ClosePath();
            p.Fill();

            // Seat plank.
            FillPoly(p, Rgb(0xC9985A), new Vector2(bx - 36f, by - 6f), new Vector2(bx + 36f, by - 6f), new Vector2(bx + 34f, by + 4f), new Vector2(bx - 34f, by + 4f));

            // Fisherman from behind: shoulder-tapered torso → head → straw hat, arm on the rod.
            p.fillColor = Rgb(0xF2F2EF);
            p.BeginPath();
            p.MoveTo(new Vector2(bx - 23f, by + 2f));
            p.LineTo(new Vector2(bx - 29f, by - 34f));
            p.QuadraticCurveTo(new Vector2(bx, by - 56f), new Vector2(bx + 29f, by - 34f));
            p.LineTo(new Vector2(bx + 23f, by + 2f));
            p.ClosePath();
            p.Fill();
            FillEllipse(p, new Vector2(bx, by - 48f), 15f, 16f, Rgb(0xEBC19F));   // head
            FillEllipse(p, new Vector2(bx, by - 52f), 34f, 15f, Rgb(0xE3C788));   // hat brim
            FillEllipse(p, new Vector2(bx, by - 62f), 19f, 13f, Rgb(0xD4B26E));   // hat crown
            FillPoly(p, Rgb(0x2A2A2A), new Vector2(bx - 18f, by - 56f), new Vector2(bx + 18f, by - 56f), new Vector2(bx + 17f, by - 60f), new Vector2(bx - 17f, by - 60f)); // band

            // Right arm to the rod.
            Vector2 shoulder = new Vector2(bx + 27f, by - 30f);
            Vector2 elbow = new Vector2(bx + 50f, by - 12f);
            Vector2 hand = new Vector2(bx + 64f, by - 26f);
            StrokeLine(p, shoulder, elbow, Rgb(0xF2F2EF), 9f);
            StrokeLine(p, elbow, hand, Rgb(0xEBC19F), 7f);

            // Rod aimed at the bobber.
            float bobx = bobberX * w, boby = bobberY * h;
            float dirx = bobx - hand.x, diry = boby - hand.y;
            float dlen = Mathf.Sqrt(dirx * dirx + diry * diry);
            if (dlen < 0.001f) { dirx = 0.4f; diry = -0.9f; dlen = 1f; }
            dirx /= dlen; diry /= dlen;
            Vector2 rodTip = new Vector2(hand.x + dirx * 64f, hand.y + diry * 64f);
            StrokeLine(p, hand, rodTip, Rgb(0xC9B074), 3.5f);

            // Line + bobber + ripples once fishing.
            if (state != FishingState.Idle && state != FishingState.Casting)
            {
                double bob = ticks * 0.0035;
                float dip = state == FishingState.Waiting ? Mathf.Sin((float)bob) * 4f
                          : state == FishingState.Nibble ? Mathf.Sin((float)(bob * 3.0)) * 9f
                          : state == FishingState.Bite ? 14f : 0f;

                p.strokeColor = new Color(1f, 1f, 1f, 0.6f);
                p.lineWidth = 1.2f;
                p.BeginPath();
                p.MoveTo(rodTip);
                p.QuadraticCurveTo(new Vector2((rodTip.x + bobx) * 0.5f, (rodTip.y + boby) * 0.5f - 18f), new Vector2(bobx, boby + dip));
                p.Stroke();

                float rp = (ticks * (weather == Weather.Rain ? 0.0022f : 0.0015f)) % 1f;
                StrokeRipple(p, new Vector2(bobx, boby + dip), 14f + rp * (weather == Weather.Rain ? 70f : 56f), White(0.55f * (1f - rp)), 2f, 10);
                if (state == FishingState.Nibble || state == FishingState.Bite)
                {
                    float mr = (ticks * 0.0035f) % 1f;
                    StrokeRipple(p, new Vector2(bobx, boby + dip), 8f + mr * 26f, White(0.8f * (1f - mr)), 3f, 8);
                }
                if (state != FishingState.Bite)
                {
                    FillEllipse(p, new Vector2(bobx, boby + dip), 6f, 5f, Rgb(0xFF3C3C));
                    FillEllipse(p, new Vector2(bobx, boby + dip + 1f), 4f, 3f, Rgb(0xFFFFFF));
                }
            }
        }

        // ------------------------------------------------------------------
        //  Leaping fish + particles
        // ------------------------------------------------------------------

        private static void DrawJumpingFish(Painter2D p, FishSpecies fish, long ticks,
            float worldX, float worldY, float rotDeg, bool mirror)
        {
            float rad = rotDeg * Mathf.Deg2Rad;
            float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            Vector2 Tf(float lx, float ly)
            {
                if (mirror) ly = -ly;
                float rx = (lx * cs - ly * sn) * 1.2f;
                float ry = (lx * sn + ly * cs) * 1.2f;
                return new Vector2(worldX + rx, worldY + ry);
            }
            float tail = Mathf.Sin(ticks * 0.012f) * 16f;
            Color body = fish.Color;
            Color belly = new Color(body.r * 0.82f, body.g * 0.82f, body.b * 0.82f, 1f);
            FillPoly(p, WithAlpha(body, 0.95f), Tf(-45, 0), Tf(-10, -18), Tf(30, -5), Tf(45, -2), Tf(0, 2));
            FillPoly(p, belly, Tf(-45, 0), Tf(-12, 16), Tf(28, 6), Tf(45, -2), Tf(0, 2));
            FillPoly(p, WithAlpha(body, 0.75f), Tf(45, -2), Tf(62, -16 + tail), Tf(54, -2 + tail * 0.4f), Tf(62, 12 + tail));
            FillCircle(p, Tf(-30, -3), 3.5f * 1.2f, White(1f));
            FillCircle(p, Tf(-31, -3), 1.8f * 1.2f, new Color(0f, 0f, 0f, 1f));
            FillPoly(p, White(0.6f), Tf(-16, 3), Tf(-6, 12 + tail * 0.3f), Tf(-4, 5));
        }

        private static void DrawSplashParticles(Painter2D p, SplashParticle[] parts)
        {
            if (parts == null) return;
            for (int i = 0; i < parts.Length; i++)
            {
                SplashParticle q = parts[i];
                if (q.Alpha <= 0f) continue;
                Color col = WithAlpha(q.Color, q.Alpha);
                float r = q.BaseSize;
                double a = q.Rotation * Mathf.Deg2Rad;
                if (q.ShapeType == 0)
                    FillPoly(p, col, Pt(q.X, q.Y, r, a), Pt(q.X, q.Y, r, a + 2.0943951), Pt(q.X, q.Y, r, a + 4.1887902));
                else if (q.ShapeType == 1)
                    FillPoly(p, col, Pt(q.X, q.Y, r * 1.3f, a), Pt(q.X, q.Y, r * 0.7f, a + 1.5707963),
                        Pt(q.X, q.Y, r * 1.3f, a + 3.1415927), Pt(q.X, q.Y, r * 0.7f, a + 4.712389));
                else
                {
                    p.fillColor = col;
                    p.BeginPath();
                    for (int k = 0; k < 6; k++)
                    {
                        Vector2 pt = Pt(q.X, q.Y, r, a + k * 1.0471976);
                        if (k == 0) p.MoveTo(pt); else p.LineTo(pt);
                    }
                    p.ClosePath();
                    p.Fill();
                }
            }
        }

        // ------------------------------------------------------------------
        //  Weather + atmosphere + rhythm
        // ------------------------------------------------------------------

        private static void DrawWindRipples(Painter2D p, WindStroke[] strokes, float w, float h, float hy)
        {
            if (strokes == null) return;
            for (int i = 0; i < strokes.Length; i++)
            {
                WindStroke ws = strokes[i];
                float sy = ws.Y * h;
                if (sy < hy + 8f) continue;
                float sx = ws.X * w;
                StrokeLine(p, new Vector2(sx, sy), new Vector2(sx + ws.Length, sy + ws.Length * 0.03f), White(ws.Opacity * 0.6f), ws.Width);
            }
        }

        private static void DrawMistPatches(Painter2D p, long ticks, float w, float h, float hy)
        {
            float t = ticks * 0.0004f;
            for (int i = 0; i < 4; i++)
            {
                float x = ((0.2f + 0.25f * i + t) % 1.2f - 0.1f) * w;
                float y = hy + (0.08f + 0.16f * i) * (h - hy);
                FillEllipse(p, new Vector2(x, y), 150f, 40f, new Color(1f, 1f, 1f, 0.07f), 24);
            }
        }

        private static void DrawRainStrokes(Painter2D p, RainStroke[] strokes, float w, float h)
        {
            if (strokes == null) return;
            for (int i = 0; i < strokes.Length; i++)
            {
                RainStroke rs = strokes[i];
                if (rs.Y < 0f || rs.Y > 1.1f) continue;
                float sx = rs.X * w, sy = rs.Y * h;
                StrokeLine(p, new Vector2(sx, sy), new Vector2(sx - 4f, sy + rs.Length), White(0.22f), 1.5f);
            }
        }

        private static void DrawMotes(Painter2D p, AtmMote[] motes, long ticks, TimeOfDay time, Weather weather, float w, float h)
        {
            if (motes == null) return;
            float t = ticks * 0.001f;
            Color mc = time == TimeOfDay.Day ? Rgb(0xFFF6DF) : time == TimeOfDay.Sunset ? Rgb(0xFFE1B0) : Rgb(0xCBDDFF);
            float damp = weather == Weather.Rain ? 0.6f : 1f;
            for (int i = 0; i < motes.Length; i++)
            {
                AtmMote m = motes[i];
                float y = (((m.BaseY - t * m.DriftSpeed) % 1f) + 1f) % 1f;
                float sway = Mathf.Sin(t * (0.4f + m.Depth) + m.Phase) * m.SwayAmp;
                float cx = ((((m.RelX + sway) % 1f) + 1f) % 1f) * w;
                float twinkle = 0.55f + 0.45f * Mathf.Sin(t * 1.2f + m.Phase * 1.7f);
                float a = (0.05f + 0.09f * m.Depth) * twinkle * damp;
                FillCircle(p, new Vector2(cx, y * h), m.Radius * (0.7f + 0.6f * m.Depth), WithAlpha(mc, a));
            }
        }

        private static void DrawRhythmRing(Painter2D p, FishingState state, float scale, bool active,
            float bobberX, float bobberY, float w, float h)
        {
            if (state != FishingState.Reeling || !active) return;
            float cx = bobberX * w, cy = bobberY * h - 40f;
            const float maxR = 56f;
            StrokeCircleFull(p, cx, cy, maxR * 0.23f, new Color(1f, 1f, 1f, 0.35f), 2f);
            StrokeCircleFull(p, cx, cy, maxR * 0.43f, new Color(1f, 1f, 1f, 0.35f), 2f);
            bool inSweet = scale >= 0.23f && scale <= 0.43f;
            Color col = inSweet ? new Color(0.55f, 1f, 0.7f, 0.95f) : new Color(1f, 1f, 1f, 0.85f);
            StrokeCircleFull(p, cx, cy, maxR * Mathf.Clamp01(scale), col, 3.5f);
        }

        // ------------------------------------------------------------------
        //  Painter2D primitives
        // ------------------------------------------------------------------

        private static void FillVBands(Painter2D p, Rect area, Color top, Color bottom, int bands)
        {
            for (int i = 0; i < bands; i++)
            {
                float f = (i + 0.5f) / bands;
                Color c = Color.Lerp(top, bottom, f);
                float y0 = area.yMin + area.height * (i / (float)bands);
                float y1 = area.yMin + area.height * ((i + 1) / (float)bands);
                FillPoly(p, c, new Vector2(area.xMin, y0), new Vector2(area.xMax, y0), new Vector2(area.xMax, y1), new Vector2(area.xMin, y1));
            }
        }

        private static void FillCircle(Painter2D p, Vector2 c, float r, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.Arc(c, r, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            p.Fill();
        }

        private static void FillEllipse(Painter2D p, Vector2 c, float rx, float ry, Color color, int segments = 16)
        {
            p.fillColor = color;
            p.BeginPath();
            for (int i = 0; i < segments; i++)
            {
                double a = i * 2.0 * Math.PI / segments;
                Vector2 pt = new Vector2(c.x + rx * (float)Math.Cos(a), c.y + ry * (float)Math.Sin(a));
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.ClosePath();
            p.Fill();
        }

        private static void StrokeCircleEllipse(Painter2D p, float cx, float cy, float rx, float ry, Color color, float width, int segments = 18)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.BeginPath();
            for (int i = 0; i < segments; i++)
            {
                double a = i * 2.0 * Math.PI / segments;
                Vector2 pt = new Vector2(cx + rx * (float)Math.Cos(a), cy + ry * (float)Math.Sin(a));
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.ClosePath();
            p.Stroke();
        }

        private static void FillPoly(Painter2D p, Color color, params Vector2[] pts)
        {
            if (pts == null || pts.Length < 2) return;
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath();
            p.Fill();
        }

        private static void StrokeLine(Painter2D p, Vector2 a, Vector2 b, Color color, float width)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        private static void StrokeRipple(Painter2D p, Vector2 center, float radius, Color color, float strokeWidth, int segments)
        {
            p.strokeColor = color;
            p.lineWidth = strokeWidth;
            p.BeginPath();
            for (int i = 0; i < segments; i++)
            {
                double ang = i * 2.0 * Math.PI / segments;
                float x = center.x + radius * (float)Math.Cos(ang);
                float y = center.y + (radius * 0.5f) * (float)Math.Sin(ang);
                if (i == 0) p.MoveTo(new Vector2(x, y)); else p.LineTo(new Vector2(x, y));
            }
            p.ClosePath();
            p.Stroke();
        }

        private static void StrokeCircleFull(Painter2D p, float cx, float cy, float r, Color color, float width)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), r, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            p.Stroke();
        }

        private static Vector2 Pt(float cx, float cy, float r, double ang)
            => new Vector2(cx + r * (float)Math.Cos(ang), cy + r * (float)Math.Sin(ang));

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
        private static Color White(float a) => new Color(1f, 1f, 1f, a);

        private static Color Rgb(uint rgb)
        {
            byte r = (byte)((rgb >> 16) & 0xFF);
            byte g = (byte)((rgb >> 8) & 0xFF);
            byte b = (byte)(rgb & 0xFF);
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
