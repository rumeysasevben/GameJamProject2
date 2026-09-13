using System.Collections.Generic;
using UnityEngine;

namespace TenCandles
{
    // Builds a random map from a theme's tile pieces: a winding road, an optional river with a bridge,
    // tower pads beside the road and decoration around it. Works in background pixels (y down).
    //
    // Road tiles are 256 px long and 171 px wide. A turn tile is a quarter ring (inner radius 85, outer 256),
    // so every turn shifts the road sideways by 85 px; the table in Place() handles that.
    public static class MapGenerator
    {
        public const string InstanceSuffix = " (generated)";

        const float RoadWidth = 171f;
        const float Half = RoadWidth * 0.5f;
        const float TileLength = 256f;
        const float Inner = 85f;
        const float ArcRadius = Inner + Half;

        // Visible playfield between the HUD panels.
        static readonly Rect Playfield = Rect.MinMaxRect(270f, 180f, 1650f, 1060f);
        // Where road tiles may go at all (the entrance may start under the left panel).
        static readonly Rect RoadArea = Rect.MinMaxRect(-300f, 175f, 1660f, 1075f);

        const float MinLength = 2300f;
        const float MaxLength = 3100f;
        const int MaxPads = 9;

        enum Dir { E, S, W, N }

        struct Piece
        {
            public Sprite sprite;
            public Rect rect;
            public Dir dirAfter;
            public float bandAfter, lineAfter;
            public List<Vector2> path;
            public float length;
            public bool straight;
        }

        public static MapData Generate(MapData theme, int seed)
        {
            var rng = new System.Random(seed);
            RoadTiles t = theme.tiles;

            List<Piece> road = null;
            for (int attempt = 0; attempt < 40 && road == null; attempt++) road = BuildRoad(t, rng);
            if (road == null) road = FallbackRoad(t);

            var map = ScriptableObject.CreateInstance<MapData>();
            map.name = theme.name + InstanceSuffix;
            map.displayName = theme.displayName;
            map.background = theme.background;
            map.padSprite = theme.padSprite;
            map.filler = theme.filler;
            map.cameraColor = theme.cameraColor;
            map.paintedSpots = new Vector2[0];

            var route = new List<Vector2>();
            var props = new List<MapProp>();
            foreach (var p in road)
            {
                foreach (var point in p.path)
                    if (route.Count == 0 || (route[route.Count - 1] - point).sqrMagnitude > 1f) route.Add(point);
                props.Add(new MapProp { sprite = p.sprite, pixel = p.rect.center, order = -90 });
            }
            map.route = route.ToArray();

            // The road carries on past the cake to the edge of the screen, so the cake sits on it
            // instead of at a cut-off end. Enemies stop at the cake; the rest is scenery.
            var fullRoad = new List<Vector2>(route);
            foreach (var p in ExtendToEdge(t, road))
            {
                props.Add(new MapProp { sprite = p.sprite, pixel = p.rect.center, order = -90 });
                fullRoad.Add(p.path[p.path.Count - 1]);
                road.Add(p);
            }
            Vector2[] clearance = fullRoad.ToArray();

            var obstacles = new List<Rect>();
            AddRiver(t, road, props, obstacles, rng);
            map.extraSpots = PlacePads(map.route, clearance, obstacles, rng).ToArray();
            AddDecor(t, clearance, map.extraSpots, obstacles, props, rng);
            map.props = props.ToArray();
            return map;
        }

        // ---------------------------------------------------------------- road

        static List<Piece> BuildRoad(RoadTiles t, System.Random rng)
        {
            var pieces = new List<Piece>();
            // Enter from the left, under the build panel.
            float band = 260f + (float)rng.NextDouble() * 520f;
            int budget = 4000;
            return Extend(t, pieces, Dir.E, band, -TileLength, 0f, 0, 0, rng, ref budget) ? pieces : null;
        }

        const int MaxStraightRun = 3;
        const int MinTurns = 4;

        static bool Extend(RoadTiles t, List<Piece> pieces, Dir dir, float band, float line, float length, int straightRun, int turns,
            System.Random rng, ref int budget)
        {
            if (--budget <= 0) return false;

            if (length >= MinLength && turns >= MinTurns && EndsInside(dir, band, line)) return true;
            if (length > MaxLength) return false;

            // First two tiles always go straight so the entrance reads clearly; after that no long boring runs.
            int[] options = pieces.Count < 2 ? new[] { 0 } : Shuffle(rng);
            foreach (int turn in options)
            {
                if (turn == 0 && pieces.Count >= 2 && straightRun >= MaxStraightRun) continue;
                Piece p = Place(t, dir, band, line, turn);
                if (!Fits(p, pieces)) continue;
                pieces.Add(p);
                if (Extend(t, pieces, p.dirAfter, p.bandAfter, p.lineAfter, length + p.length,
                        turn == 0 ? straightRun + 1 : 0, turn == 0 ? turns : turns + 1, rng, ref budget)) return true;
                pieces.RemoveAt(pieces.Count - 1);
                if (budget <= 0) return false;
            }
            return false;
        }

        // Straight is tried first most of the time, which keeps long readable stretches.
        static int[] Shuffle(System.Random rng)
        {
            double r = rng.NextDouble();
            if (r < 0.4) return rng.NextDouble() < 0.5 ? new[] { 0, 1, -1 } : new[] { 0, -1, 1 };
            if (r < 0.7) return rng.NextDouble() < 0.5 ? new[] { 1, 0, -1 } : new[] { 1, -1, 0 };
            return rng.NextDouble() < 0.5 ? new[] { -1, 0, 1 } : new[] { -1, 1, 0 };
        }

        // The road runs out beside the right-hand panel or along the bottom edge, so the cake guards a corner
        // of the field instead of sitting in the middle of it.
        static bool EndsInside(Dir dir, float band, float line)
        {
            Vector2 end = dir == Dir.E || dir == Dir.W ? new Vector2(line, band + Half) : new Vector2(band + Half, line);
            if (!Playfield.Contains(end)) return false;
            // Far enough right to guard the edge, but the whole cake stays clear of the side panel.
            if (dir == Dir.E) return end.x >= Playfield.xMax - 270f && end.x <= Playfield.xMax - 70f;
            if (dir == Dir.S) return end.y >= Playfield.yMax - 130f && end.x >= 1050f;
            if (dir == Dir.N) return end.y <= Playfield.yMin + 150f && end.x >= 1050f;
            return false;
        }

        // turn: 0 straight, 1 clockwise (right), -1 counter-clockwise (left). y grows downward.
        static Piece Place(RoadTiles t, Dir dir, float b, float l, int turn)
        {
            var p = new Piece { straight = turn == 0 };
            switch (dir)
            {
                case Dir.E:
                    if (turn == 0) Straight(ref p, t.horizontal, new Rect(l, b, TileLength, RoadWidth), Dir.E, b, l + TileLength, new Vector2(l, b + Half), new Vector2(l + TileLength, b + Half));
                    else if (turn == 1) Turn(ref p, t.leftBottom, new Vector2(l, b), Dir.S, l + Inner, b + TileLength, new Vector2(l, b + TileLength), -90f, 0f);
                    else Turn(ref p, t.topLeft, new Vector2(l, b - Inner), Dir.N, l + Inner, b - Inner, new Vector2(l, b - Inner), 90f, 0f);
                    break;
                case Dir.W:
                    if (turn == 0) Straight(ref p, t.horizontal, new Rect(l - TileLength, b, TileLength, RoadWidth), Dir.W, b, l - TileLength, new Vector2(l, b + Half), new Vector2(l - TileLength, b + Half));
                    else if (turn == -1) Turn(ref p, t.bottomRight, new Vector2(l - TileLength, b), Dir.S, l - TileLength, b + TileLength, new Vector2(l, b + TileLength), -90f, -180f);
                    else Turn(ref p, t.topRight, new Vector2(l - TileLength, b - Inner), Dir.N, l - TileLength, b - Inner, new Vector2(l, b - Inner), 90f, 180f);
                    break;
                case Dir.S:
                    if (turn == 0) Straight(ref p, t.vertical, new Rect(b, l, RoadWidth, TileLength), Dir.S, b, l + TileLength, new Vector2(b + Half, l), new Vector2(b + Half, l + TileLength));
                    else if (turn == -1) Turn(ref p, t.topRight, new Vector2(b, l), Dir.E, l + Inner, b + TileLength, new Vector2(b + TileLength, l), 180f, 90f);
                    else Turn(ref p, t.topLeft, new Vector2(b - Inner, l), Dir.W, l + Inner, b - Inner, new Vector2(b - Inner, l), 0f, 90f);
                    break;
                default: // N
                    if (turn == 0) Straight(ref p, t.vertical, new Rect(b, l - TileLength, RoadWidth, TileLength), Dir.N, b, l - TileLength, new Vector2(b + Half, l), new Vector2(b + Half, l - TileLength));
                    else if (turn == 1) Turn(ref p, t.bottomRight, new Vector2(b, l - TileLength), Dir.E, l - TileLength, b + TileLength, new Vector2(b + TileLength, l), 180f, 270f);
                    else Turn(ref p, t.leftBottom, new Vector2(b - Inner, l - TileLength), Dir.W, l - TileLength, b - Inner, new Vector2(b - Inner, l), 0f, -90f);
                    break;
            }
            return p;
        }

        static void Straight(ref Piece p, Sprite sprite, Rect rect, Dir after, float band, float line, Vector2 from, Vector2 to)
        {
            p.sprite = sprite;
            p.rect = rect;
            p.dirAfter = after;
            p.bandAfter = band;
            p.lineAfter = line;
            p.path = new List<Vector2> { from, to };
            p.length = TileLength;
        }

        // Road end state: heading E/W -> band = y of the road's top edge, line = x of the cut end.
        //                  heading N/S -> band = x of the road's left edge, line = y of the cut end.
        static void Turn(ref Piece p, Sprite sprite, Vector2 tileTopLeft, Dir after, float bandAfter, float lineAfter, Vector2 centre, float fromDeg, float toDeg)
        {
            p.sprite = sprite;
            p.rect = new Rect(tileTopLeft.x, tileTopLeft.y, TileLength, TileLength);
            p.dirAfter = after;
            p.bandAfter = bandAfter;
            p.lineAfter = lineAfter;
            p.path = new List<Vector2>();
            for (int i = 0; i <= 6; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, i / 6f) * Mathf.Deg2Rad;
                p.path.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ArcRadius);
            }
            p.length = ArcRadius * Mathf.PI * 0.5f;
        }

        static bool Fits(Piece p, List<Piece> pieces)
        {
            Rect r = p.rect;
            if (r.xMin < RoadArea.xMin || r.xMax > RoadArea.xMax || r.yMin < RoadArea.yMin || r.yMax > RoadArea.yMax) return false;
            for (int i = 0; i < pieces.Count; i++)
            {
                int age = pieces.Count - i;
                if (age == 1) continue;
                // Older road keeps a wide gap so a tower pad fits between two stretches.
                float gap = age == 2 ? -8f : 150f;
                if (Inflate(pieces[i].rect, gap).Overlaps(r)) return false;
            }
            return true;
        }

        static List<Piece> ExtendToEdge(RoadTiles t, List<Piece> road)
        {
            var extra = new List<Piece>();
            Piece last = road[road.Count - 1];
            Dir dir = last.dirAfter;
            float band = last.bandAfter, line = last.lineAfter;
            for (int i = 0; i < 6; i++)
            {
                bool offScreen = dir == Dir.E ? line >= MapData.ImageWidth
                    : dir == Dir.W ? line <= 0f
                    : dir == Dir.S ? line >= MapData.ImageHeight
                    : line <= 0f;
                if (offScreen) break;
                Piece p = Place(t, dir, band, line, 0);
                extra.Add(p);
                line = p.lineAfter;
            }
            return extra;
        }

        static List<Piece> FallbackRoad(RoadTiles t)
        {
            var pieces = new List<Piece>();
            float line = -TileLength;
            for (int i = 0; i < 9; i++)
            {
                Piece p = Place(t, Dir.E, 450f, line, 0);
                pieces.Add(p);
                line = p.lineAfter;
            }
            return pieces;
        }

        // ---------------------------------------------------------------- river

        static void AddRiver(RoadTiles t, List<Piece> road, List<MapProp> props, List<Rect> obstacles, System.Random rng)
        {
            if (t.riverVertical == null || t.bridge == null) return;

            var candidates = new List<int>();
            for (int i = 2; i < road.Count; i++)
                if (road[i].straight && road[i].rect.width > road[i].rect.height && road[i].rect.center.x > 520f && road[i].rect.center.x < 1450f)
                    candidates.Add(i);

            while (candidates.Count > 0)
            {
                int pick = candidates[rng.Next(candidates.Count)];
                candidates.Remove(pick);
                Piece crossing = road[pick];
                float width = t.riverVertical.rect.width;
                var strip = new Rect(crossing.rect.center.x - width * 0.5f, 0f, width, MapData.ImageHeight);

                bool clear = true;
                for (int i = 0; i < road.Count && clear; i++)
                    if (i != pick && Inflate(road[i].rect, 20f).Overlaps(strip)) clear = false;
                if (!clear) continue;

                float tileHeight = t.riverVertical.rect.height;
                for (float y = tileHeight * 0.5f; y - tileHeight * 0.5f < MapData.ImageHeight; y += tileHeight)
                    props.Add(new MapProp { sprite = t.riverVertical, pixel = new Vector2(strip.center.x, y), order = -95 });
                props.Add(new MapProp { sprite = t.bridge, pixel = crossing.rect.center, order = -88 });
                obstacles.Add(Inflate(strip, 10f));
                return;
            }
        }

        // ---------------------------------------------------------------- pads

        // route: where enemies walk (tower coverage). clearance: all road on screen, pads keep off it.
        static List<Vector2> PlacePads(Vector2[] route, Vector2[] clearance, List<Rect> obstacles, System.Random rng)
        {
            var samples = Sample(route, 40f);
            var candidates = new List<(Vector2 pos, float score)>();
            Vector2 goal = route[route.Length - 1];

            for (float y = Playfield.yMin + 50f; y <= Playfield.yMax - 50f; y += 30f)
            for (float x = Playfield.xMin + 100f; x <= Playfield.xMax - 100f; x += 30f)
            {
                var c = new Vector2(x, y);
                float d = DistanceToRoute(route, c);
                if (d > 270f || DistanceToRoute(clearance, c) < 150f) continue;
                if (!EllipseClearOfRoad(clearance, c)) continue;
                if ((c - goal).magnitude < 150f) continue;
                if (HitsAny(obstacles, new Rect(c.x - 100f, c.y - 50f, 200f, 100f))) continue;

                int covered = 0;
                foreach (var s in samples) if ((s - c).sqrMagnitude < 260f * 260f) covered++;
                candidates.Add((c, covered + (float)rng.NextDouble() * 3f));
            }

            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            var pads = new List<Vector2>();
            foreach (var cand in candidates)
            {
                bool free = true;
                foreach (var p in pads)
                    if (Mathf.Abs(p.x - cand.pos.x) < 215f && Mathf.Abs(p.y - cand.pos.y) < 130f) { free = false; break; }
                if (!free) continue;
                pads.Add(cand.pos);
                if (pads.Count >= MaxPads) break;
            }
            foreach (var p in pads) obstacles.Add(new Rect(p.x - 110f, p.y - 150f, 220f, 210f));
            return pads;
        }

        static bool EllipseClearOfRoad(Vector2[] route, Vector2 c)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                var edge = c + new Vector2(Mathf.Cos(a) * 100f, Mathf.Sin(a) * 50f);
                if (DistanceToRoute(route, edge) < Half + 12f) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- decor

        static void AddDecor(RoadTiles t, Vector2[] route, Vector2[] pads, List<Rect> obstacles, List<MapProp> props, System.Random rng)
        {
            if (t.decor == null || t.decor.Length == 0) return;
            var placed = new List<Rect>();
            var screen = new Rect(0f, 0f, MapData.ImageWidth, MapData.ImageHeight);

            for (int attempt = 0; attempt < 500 && placed.Count < 34; attempt++)
            {
                Sprite s = t.decor[rng.Next(t.decor.Length)];
                if (s == null) continue;
                float w = s.rect.width, h = s.rect.height;
                bool large = Mathf.Max(w, h) > 280f;
                var c = new Vector2((float)rng.NextDouble() * MapData.ImageWidth, (float)rng.NextDouble() * MapData.ImageHeight);
                var rect = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);

                // Big pieces frame the edges; they never block the middle of the field.
                if (large && Playfield.Contains(c) && Inflate(Playfield, -220f).Contains(c)) continue;
                if (!screen.Overlaps(rect)) continue;
                if (HitsAny(obstacles, rect) || HitsAny(placed, Inflate(rect, -Mathf.Min(w, h) * 0.15f))) continue;
                if (!RectClearOfRoad(route, rect)) continue;

                placed.Add(rect);
                // Lower on screen = drawn later, so overlapping pieces stack correctly.
                props.Add(new MapProp { sprite = s, pixel = c, order = -75 + Mathf.RoundToInt(c.y / MapData.ImageHeight * 20f), flipX = rng.NextDouble() < 0.5 });
            }
        }

        static bool RectClearOfRoad(Vector2[] route, Rect r)
        {
            for (int iy = 0; iy <= 4; iy++)
            for (int ix = 0; ix <= 4; ix++)
            {
                var p = new Vector2(Mathf.Lerp(r.xMin, r.xMax, ix / 4f), Mathf.Lerp(r.yMin, r.yMax, iy / 4f));
                if (DistanceToRoute(route, p) < Half + 20f) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- geometry

        static List<Vector2> Sample(Vector2[] route, float step)
        {
            var list = new List<Vector2>();
            for (int i = 1; i < route.Length; i++)
            {
                float len = Vector2.Distance(route[i - 1], route[i]);
                for (float d = 0f; d < len; d += step) list.Add(Vector2.Lerp(route[i - 1], route[i], d / len));
            }
            return list;
        }

        static float DistanceToRoute(Vector2[] route, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 1; i < route.Length; i++)
            {
                Vector2 a = route[i - 1], ab = route[i] - a;
                float tt = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * tt));
            }
            return best;
        }

        static bool HitsAny(List<Rect> rects, Rect r)
        {
            foreach (var o in rects) if (o.Overlaps(r)) return true;
            return false;
        }

        static Rect Inflate(Rect r, float by) => Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);
    }
}
