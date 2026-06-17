using System;
using UnityEngine;
using UnityEngine.UIElements;
using YoonseulFishing.Data;

namespace YoonseulFishing.Rendering
{
    /// <summary>
    /// Procedural scene drawing with UI Toolkit <see cref="Painter2D"/> — the Unity
    /// port of the background half of <c>HealingFishingGame.kt</c>'s Compose Canvas
    /// (drawNightSky / drawPastelClouds / drawCelestialSource / drawMountainRange /
    /// drawWaterLowPoly + the 윤슬 sparkles).
    ///
    /// NOTE on fidelity: Painter2D fills are SOLID — it has no gradient brush,
    /// radial gradient, or additive blend mode (unlike Compose's DrawScope). So
    /// vertical gradients (sky, water tint) are approximated with interpolated
    /// horizontal bands, which actually suits the game's low-poly look. Celestial
    /// glow uses layered translucent circles (exactly as the original did). The
    /// additive atmosphere overlay (bokeh motes / bloom / vignette) is deferred —
    /// it needs vertex-coloured meshes to do faithfully.
    ///
    /// This is the background pass only. Foreground actors (boat, bobber, ripples,
    /// jumping fish, particles, rain/wind) come in the next slice.
    /// </summary>
    public static class ScenePainter
    {
        // ---- Per-time-of-day palette (0xRRGGBB, opaque), straight from the original ----
        public struct Palette
        {
            public Color SkyTop, SkyMid, SkyBottom;
            public Color WaterTop, WaterMid, WaterBottom;
            public Color MountainPrimary, MountainSecondary;
        }

        public static Palette PaletteFor(TimeOfDay time)
        {
            switch (time)
            {
                case TimeOfDay.Sunset:
                    return new Palette
                    {
                        SkyTop = Rgb(0x514068), SkyMid = Rgb(0xEB8A74), SkyBottom = Rgb(0xFCCC9B),
                        WaterTop = Rgb(0xD4746A), WaterMid = Rgb(0xBA5E5C), WaterBottom = Rgb(0x863B48),
                        MountainPrimary = Rgb(0x704A6B), MountainSecondary = Rgb(0x94618E),
                    };
                case TimeOfDay.Night:
                    return new Palette
                    {
                        SkyTop = Rgb(0x0D1224), SkyMid = Rgb(0x141A33), SkyBottom = Rgb(0x1B2342),
                        WaterTop = Rgb(0x13192F), WaterMid = Rgb(0x1E2849), WaterBottom = Rgb(0x26325C),
                        MountainPrimary = Rgb(0x19213D), MountainSecondary = Rgb(0x222C52),
                    };
                default: // Day
                    return new Palette
                    {
                        SkyTop = Rgb(0xBFE3E8), SkyMid = Rgb(0xD1E9EC), SkyBottom = Rgb(0xE3F0F4),
                        WaterTop = Rgb(0x90C2C8), WaterMid = Rgb(0x72A9B0), WaterBottom = Rgb(0x558E95),
                        MountainPrimary = Rgb(0x85AFAF), MountainSecondary = Rgb(0x9EBFBF),
                    };
            }
        }

        /// <summary>Lightweight value types for the procedurally generated decor.</summary>
        public struct Star { public float RelX, RelY; public float TwinkleSpeed; public bool Big; }
        public struct Sparkle { public float RelX, RelY; public float ScaleFactor; public float Phase; }

        /// <summary>
        /// Draws the full background scene into <paramref name="p"/> over
        /// <paramref name="rect"/> (the element's content rect, origin top-left).
        /// <paramref name="ticks"/> is elapsed milliseconds (drives animation).
        /// </summary>
        public static void DrawScene(Painter2D p, Rect rect, long ticks, TimeOfDay time, Weather weather,
            FishingState fishingState, float bobberX, float bobberY, Star[] stars, Sparkle[] sparkles)
        {
            float w = rect.width;
            float h = rect.height;
            if (w <= 1f || h <= 1f) return;

            Palette pal = PaletteFor(time);

            // 1. Sky backdrop — vertical 3-stop gradient (band-approximated).
            FillVerticalGradient(p, new Rect(0, 0, w, h), pal.SkyTop, pal.SkyMid, pal.SkyBottom, 40);

            // Night stars / daytime clouds.
            if (time == TimeOfDay.Night) DrawNightSky(p, stars, ticks, w, h, 1f);
            else DrawPastelClouds(p, ticks, w, h, 1f);

            // 2. Sun / moon.
            DrawCelestialSource(p, time, h, w, pal.SkyTop);

            // 3. Low-poly mountains.
            DrawMountainRange(p, pal.MountainPrimary, pal.MountainSecondary, w, h);

            // 4. Water + 윤슬 sparkles.
            DrawWaterLowPoly(p, pal, sparkles, ticks, time, weather, w, h);

            // 5. Bobber + ripples (only while actively fishing).
            DrawBobberAndRipples(p, fishingState, bobberX, bobberY, ticks, weather, w, h);

            // 6. Rolling lake fog during mist weather.
            if (weather == Weather.Mist) DrawMist(p, ticks, w, h);

            // 7. Wooden boat + fisherman (+ cast line once fishing).
            DrawBoatAndFisherman(p, ticks, fishingState, bobberX, bobberY, w, h);
        }

        // ------------------------------------------------------------------
        //  Layers
        // ------------------------------------------------------------------

        private static void DrawNightSky(Painter2D p, Star[] stars, long ticks, float w, float h, float alphaMul)
        {
            if (stars == null) return;
            for (int i = 0; i < stars.Length; i++)
            {
                Star s = stars[i];
                float phase = ticks * s.TwinkleSpeed;
                float a = (0.35f + 0.6f * (0.5f + 0.5f * Mathf.Sin(phase))) * alphaMul;
                FillCircle(p, new Vector2(s.RelX * w, s.RelY * h), s.Big ? 2.5f : 1.5f, White(a));
            }
        }

        private static void DrawPastelClouds(Painter2D p, long ticks, float w, float h, float alphaMul)
        {
            float cloudSpeedX = (ticks * 0.015f) % w;
            Vector2[] positions =
            {
                new Vector2(200f, 150f),
                new Vector2(w - 300f, 220f),
                new Vector2(w / 2f, 100f),
            };
            Color c = White(0.25f * alphaMul);
            foreach (var pos in positions)
            {
                float x = (pos.x + cloudSpeedX) % w;
                FillCircle(p, new Vector2(x, pos.y), 35f, c);
                FillCircle(p, new Vector2(x + 30f, pos.y + 10f), 50f, c);
                FillCircle(p, new Vector2(x + 60f, pos.y), 35f, c);
            }
        }

        private static void DrawCelestialSource(Painter2D p, TimeOfDay time, float h, float w, Color skyTop)
        {
            if (time == TimeOfDay.Day)
            {
                var c = new Vector2(w * 0.8f, h * 0.18f);
                FillCircle(p, c, 120f, Rgb(0xFFF7DB, 0.20f)); // outer glow
                FillCircle(p, c, 65f, Rgb(0xFFF7DB, 0.9f));   // sun
            }
            else if (time == TimeOfDay.Sunset)
            {
                var c = new Vector2(w * 0.75f, h * 0.44f);
                FillCircle(p, c, 150f, Rgb(0xFF6B6B, 0.25f)); // outer glow
                FillCircle(p, c, 70f, Rgb(0xFF9B6B, 0.95f));  // sunset sun
            }
            else // Night — crescent moon (foreground disc minus an offset sky-coloured disc)
            {
                var c = new Vector2(w * 0.8f, h * 0.18f);
                FillCircle(p, c, 80f, Rgb(0xFFF6B8, 0.1f));               // halo
                FillCircle(p, c, 40f, Rgb(0xFFF6B8, 0.9f));              // moon disc
                FillCircle(p, new Vector2(c.x - 14f, c.y - 4f), 40f, skyTop); // carve crescent
            }
        }

        private static void DrawMountainRange(Painter2D p, Color primary, Color secondary, float w, float h)
        {
            // Far range
            FillPoly(p, WithAlpha(primary, 0.6f),
                new Vector2(0, h * 0.55f), new Vector2(w * 0.25f, h * 0.35f), new Vector2(w * 0.45f, h * 0.55f),
                new Vector2(w * 0.72f, h * 0.28f), new Vector2(w, h * 0.55f), new Vector2(w, h), new Vector2(0, h));
            // Far facet
            FillPoly(p, WithAlpha(primary, 0.8f),
                new Vector2(w * 0.25f, h * 0.35f), new Vector2(w * 0.45f, h * 0.55f), new Vector2(w * 0.25f, h * 0.55f));
            // Near range (open path closed implicitly by Fill)
            FillPoly(p, secondary,
                new Vector2(0, h * 0.55f), new Vector2(w * 0.48f, h * 0.39f), new Vector2(w * 0.88f, h * 0.55f), new Vector2(w, h * 0.55f));
            // Near facet
            FillPoly(p, WithAlpha(secondary, 0.88f),
                new Vector2(w * 0.48f, h * 0.39f), new Vector2(w * 0.88f, h * 0.55f), new Vector2(w * 0.48f, h * 0.55f));
        }

        private static void DrawWaterLowPoly(Painter2D p, Palette pal, Sparkle[] sparkles, long ticks,
            TimeOfDay time, Weather weather, float w, float h)
        {
            float waterHorizon = h * 0.52f;
            float waterHeight = h - waterHorizon;

            // Base fill.
            FillRect(p, new Rect(0, waterHorizon, w, waterHeight), pal.WaterTop);

            // 5 jagged low-poly wave strips, tinted top→bottom.
            const int rows = 5;
            const int segments = 8;
            for (int row = 1; row <= rows; row++)
            {
                float rowY = waterHorizon + (waterHeight / rows) * row;
                float prevRowY = waterHorizon + (waterHeight / rows) * (row - 1);

                float mix = row / (float)rows;
                Color strip = LerpColor(pal.WaterTop, pal.WaterMid, mix);

                p.fillColor = strip;
                p.BeginPath();
                p.MoveTo(new Vector2(0, prevRowY));
                float widthStep = w / segments;
                for (int seg = 0; seg <= segments; seg++)
                {
                    float currX = seg * widthStep;
                    float waveAmp = 12f * (row * 0.5f);
                    float waveProgress = (ticks * 0.0019f) + (seg * 0.5f);
                    float currY = prevRowY + Mathf.Sin(waveProgress) * waveAmp;
                    p.LineTo(new Vector2(currX, currY));
                }
                p.LineTo(new Vector2(w, rowY));
                p.LineTo(new Vector2(0, rowY));
                p.ClosePath();
                p.Fill();
            }

            // 윤슬 — shimmering diamonds along the surface.
            if (sparkles != null)
            {
                float weatherMul = weather == Weather.Rain ? RainSparkle(time)
                                 : weather == Weather.Mist ? MistSparkle(time)
                                 : ClearSparkle(time);
                Color baseCol = time == TimeOfDay.Day ? Rgb(0xFFF4D4)
                              : time == TimeOfDay.Sunset ? Rgb(0xFFCCAA)
                              : Rgb(0xD7EAFE);

                for (int i = 0; i < sparkles.Length; i++)
                {
                    Sparkle s = sparkles[i];
                    float spX = s.RelX * w;
                    float spY = s.RelY * h;
                    float brightness = 0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(s.Phase + ticks * 0.0016f));
                    float baseLen = 14f * s.ScaleFactor * brightness;
                    Color col = WithAlpha(baseCol, weatherMul * brightness);
                    FillPoly(p, col,
                        new Vector2(spX - baseLen, spY), new Vector2(spX, spY - 3f),
                        new Vector2(spX + baseLen, spY), new Vector2(spX, spY + 3f));
                }
            }
        }

        // Sparkle alpha multipliers (clear/mist/rain) per time of day, from the original.
        private static float ClearSparkle(TimeOfDay t) => t == TimeOfDay.Day ? 0.75f : t == TimeOfDay.Sunset ? 0.8f : 0.85f;
        private static float MistSparkle(TimeOfDay t) => t == TimeOfDay.Day ? 0.5f : t == TimeOfDay.Sunset ? 0.55f : 0.6f;
        private static float RainSparkle(TimeOfDay t) => t == TimeOfDay.Day ? 0.35f : t == TimeOfDay.Sunset ? 0.4f : 0.4f;

        // ------------------------------------------------------------------
        //  Foreground: bobber + ripples + mist (slice 2)
        // ------------------------------------------------------------------

        private static void DrawBobberAndRipples(Painter2D p, FishingState state, float bobberX, float bobberY,
            long ticks, Weather weather, float w, float h)
        {
            if (state == FishingState.Idle || state == FishingState.Casting) return;

            float bx = bobberX * w;
            float by = bobberY * h;

            double bobValue = ticks * 0.0035;
            float bobOffset =
                state == FishingState.Waiting ? Mathf.Sin((float)bobValue) * 5f :
                state == FishingState.Nibble ? Mathf.Sin((float)(bobValue * 3.0)) * 10f :
                state == FishingState.Bite ? 16f : 0f;

            // Expanding concentric ripple — wider/faster under rain.
            float ripplePhase = (ticks * (weather == Weather.Rain ? 0.0022f : 0.0015f)) % 1.0f;
            StrokeRipple(p, new Vector2(bx, by),
                16f + ripplePhase * (weather == Weather.Rain ? 75f : 60f),
                White(0.6f * (1f - ripplePhase)), 2.5f, 8);

            if (state == FishingState.Nibble || state == FishingState.Bite)
            {
                float micro = (ticks * 0.0035f) % 1.0f;
                StrokeRipple(p, new Vector2(bx, by), 8f + micro * 30f, White(0.8f * (1f - micro)), 3.5f, 6);
            }

            // Physical bobber — hidden while fully plunged under on BITE.
            if (state != FishingState.Bite)
            {
                var topR = new Vector2(bx, by + bobOffset - 15f);
                var botR = new Vector2(bx, by + bobOffset);
                FillCircle(p, topR, 5.5f, Rgb(0xFF3C3C));                                         // red tip
                StrokeLine(p, topR, botR, White(1f), 3f);                                         // white stem
                FillEllipse(p, new Vector2(bx, by + bobOffset - 3f), 6f, 5f, White(1f));          // white body
                FillEllipse(p, new Vector2(bx, by + bobOffset - 5.5f), 6f, 2.5f, Rgb(0xFF3C3C));  // red band on top half
            }
        }

        private static void DrawMist(Painter2D p, long ticks, float w, float h)
        {
            float mistY = h * 0.52f;
            for (int layer = 0; layer < 3; layer++)
            {
                float layerY = mistY + layer * 16f;
                p.fillColor = White(0.08f - layer * 0.02f);
                p.BeginPath();
                p.MoveTo(new Vector2(0, layerY - 15f));
                const int steps = 6;
                float stepW = w / steps;
                for (int step = 0; step <= steps; step++)
                {
                    float px = step * stepW;
                    float py = layerY + Mathf.Sin((ticks * 0.0006f) + step * 1.5f + layer * 2f) * 12f;
                    p.LineTo(new Vector2(px, py));
                }
                p.LineTo(new Vector2(w, layerY + 50f));
                p.LineTo(new Vector2(0, layerY + 50f));
                p.ClosePath();
                p.Fill();
            }
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
                float y = center.y + (radius * 0.4f) * (float)Math.Sin(ang); // vertical flatten for water perspective
                if (i == 0) p.MoveTo(new Vector2(x, y)); else p.LineTo(new Vector2(x, y));
            }
            p.ClosePath();
            p.Stroke();
        }

        // Compose drew the boat in a translated+rotated local space (withTransform);
        // Painter2D has no transform stack, so we map each local point to world via Tf().
        private static void DrawBoatAndFisherman(Painter2D p, long ticks, FishingState state,
            float bobberX, float bobberY, float w, float h)
        {
            float baseX = w * 0.36f;
            float baseY = h * 0.61f;
            float bobTime = ticks * 0.0018f;
            float swayY = Mathf.Sin(bobTime) * 4.5f;
            float rollDeg = Mathf.Cos(bobTime) * 1.5f; // gentle roll
            float rad = rollDeg * Mathf.Deg2Rad;
            float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);

            Vector2 Tf(float lx, float ly)
            {
                float rx = lx * cs - ly * sn;
                float ry = lx * sn + ly * cs;
                return new Vector2(baseX + rx, baseY + swayY + ry);
            }

            p.lineCap = LineCap.Round;

            // Shadow under the boat.
            FillEllipse(p, Tf(0f, 26f), 80f, 11f, new Color(0f, 0f, 0f, 0.2f));

            // Hollow canoe in low-poly facets.
            FillPoly(p, Rgb(0x382312), Tf(-70, -2), Tf(-18, 11), Tf(60, -2), Tf(0, -5));   // inside cavity
            FillPoly(p, Rgb(0xBE783A), Tf(-75, 0), Tf(-20, 15), Tf(65, 0), Tf(0, -6));     // hull
            FillPoly(p, Rgb(0x8C5325), Tf(-75, 0), Tf(-20, 15), Tf(0, 4));                 // hull shadow
            FillPoly(p, Rgb(0xE5B083), Tf(-75, 0), Tf(65, 0), Tf(55, -3), Tf(-65, -3));    // rim

            // Fisherman.
            FillPoly(p, Rgb(0x2E5B88), Tf(-16, 0), Tf(6, 0), Tf(8, -11), Tf(-18, -11));    // shorts
            FillPoly(p, Rgb(0xFBFBFB), Tf(-18, -11), Tf(8, -11), Tf(2, -36), Tf(-12, -36)); // white shirt
            FillCircle(p, Tf(-5, -36), 8.5f, Rgb(0x282828)); // hair
            FillCircle(p, Tf(-5, -33), 6.5f, Rgb(0xEBC19F)); // neck/skin
            StrokeLine(p, Tf(-11, -25), Tf(-1, -20), Rgb(0xFBFBFB), 6.5f); // sleeve
            StrokeLine(p, Tf(-1, -20), Tf(4, -24), Rgb(0xEBC19F), 5.2f);   // forearm

            // Straw hat (head centred at -5,-40).
            FillEllipse(p, Tf(-5f, -34.5f), 22f, 4.5f, Rgb(0xE9CB99)); // brim
            FillEllipse(p, Tf(-5f, -35f), 18f, 3.5f, Rgb(0xD6B57E));   // brim inner
            FillPoly(p, Rgb(0x1C1C1C), Tf(-16, -46), Tf(6, -46), Tf(6, -39), Tf(-16, -39)); // black band
            FillPoly(p, Rgb(0xE9CB99), Tf(-16, -57), Tf(6, -57), Tf(6, -46), Tf(-16, -46)); // crown
            FillPoly(p, Rgb(0xD6B57E), Tf(-5, -57), Tf(6, -57), Tf(6, -46), Tf(-5, -46));   // crown shade

            // Bamboo rod (rest pose; rod-bend animation wired in Phase 6).
            var rodRoot = new Vector2(4f, -24f);
            var rodTip = new Vector2(45f, -55f);
            StrokeLine(p, Tf(rodRoot.x, rodRoot.y), Tf(rodTip.x, rodTip.y), Rgb(0xBBB189), 3.5f);
            for (int step = 1; step <= 4; step++)
            {
                float fr = step / 4f;
                float jx = rodRoot.x * (1f - fr) + rodTip.x * fr;
                float jy = rodRoot.y * (1f - fr) + rodTip.y * fr;
                FillCircle(p, Tf(jx, jy), 2.5f, Rgb(0x867E52)); // bamboo joints
            }

            // Fishing line — quadratic Bézier that sags down to the bobber.
            if (state != FishingState.Idle)
            {
                float localBobberX = bobberX * w - baseX;
                float localBobberY = bobberY * h - (baseY + swayY);
                float ctrlX = (rodTip.x + localBobberX) * 0.5f;
                float ctrlY = (rodTip.y + localBobberY) * 0.5f + 50f;
                p.strokeColor = new Color(1f, 1f, 1f, 0.55f);
                p.lineWidth = 1.2f;
                p.BeginPath();
                p.MoveTo(Tf(rodTip.x, rodTip.y));
                p.QuadraticCurveTo(Tf(ctrlX, ctrlY), Tf(localBobberX, localBobberY));
                p.Stroke();
            }

            p.lineCap = LineCap.Butt;
        }

        // ------------------------------------------------------------------
        //  Painter2D helpers
        // ------------------------------------------------------------------

        private static void FillCircle(Painter2D p, Vector2 center, float radius, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.Arc(center, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            p.Fill();
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

        private static void FillRect(Painter2D p, Rect r, Color color)
        {
            FillPoly(p, color,
                new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
                new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax));
        }

        private static void StrokeLine(Painter2D p, Vector2 a, Vector2 b, Color color, float width)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        private static void FillEllipse(Painter2D p, Vector2 c, float rx, float ry, Color color, int segments = 16)
        {
            p.fillColor = color;
            p.BeginPath();
            for (int i = 0; i < segments; i++)
            {
                double a = i * 2.0 * Math.PI / segments;
                var pt = new Vector2(c.x + rx * (float)Math.Cos(a), c.y + ry * (float)Math.Sin(a));
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.ClosePath();
            p.Fill();
        }

        /// <summary>Approximates a vertical 3-stop gradient with <paramref name="bands"/> solid strips.</summary>
        private static void FillVerticalGradient(Painter2D p, Rect area, Color top, Color mid, Color bottom, int bands)
        {
            for (int i = 0; i < bands; i++)
            {
                float f = (i + 0.5f) / bands;
                Color c = f < 0.5f ? LerpColor(top, mid, f / 0.5f) : LerpColor(mid, bottom, (f - 0.5f) / 0.5f);
                float y0 = area.yMin + area.height * (i / (float)bands);
                float y1 = area.yMin + area.height * ((i + 1) / (float)bands);
                FillRect(p, Rect.MinMaxRect(area.xMin, y0, area.xMax, y1), c);
            }
        }

        private static Color LerpColor(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));
        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
        private static Color White(float a) => new Color(1f, 1f, 1f, a);

        private static Color Rgb(uint rgb, float alpha = 1f)
        {
            byte r = (byte)((rgb >> 16) & 0xFF);
            byte g = (byte)((rgb >> 8) & 0xFF);
            byte b = (byte)(rgb & 0xFF);
            return new Color(r / 255f, g / 255f, b / 255f, alpha);
        }
    }
}
