using UnityEngine;

namespace TenCandles
{
    // A sprite laid on the map at a pixel position (sprite pivot = centre).
    [System.Serializable]
    public struct MapProp
    {
        public Sprite sprite;
        public Vector2 pixel;
        public int order;
        public bool flipX;
    }

    // The road pieces of one art theme. Turn names say which two tile edges the road touches.
    [System.Serializable]
    public class RoadTiles
    {
        public Sprite horizontal;      // 256x171
        public Sprite vertical;        // 171x256
        public Sprite bottomRight;     // road_1
        public Sprite leftBottom;      // road_2
        public Sprite topRight;        // road_3
        public Sprite topLeft;         // road_4
        [Tooltip("Optional vertical river strip (142x256) crossed by a bridge.")]
        public Sprite riverVertical;
        public Sprite bridge;
        [Tooltip("Scattered around the road. Pieces bigger than 280 px only go near the screen edges.")]
        public Sprite[] decor;
    }

    // One playable map: a painted background plus the road and pads that match it,
    // or (generated = true) a theme that MapGenerator turns into a fresh layout every game.
    // Positions are pixels on the 1920x1080 background (top-left origin), so they can be read straight off the image.
    [CreateAssetMenu(menuName = "Ten Candles/Map", fileName = "Map")]
    public class MapData : ScriptableObject
    {
        public const float ImageWidth = 1920f;
        public const float ImageHeight = 1080f;
        // Matches the camera: orthographic size 6.5 shows 13 world units vertically.
        public const float WorldHeight = 13f;
        public static float PixelsPerUnit => ImageHeight / WorldHeight;

        public string displayName = "Map";
        public Sprite background;
        [Tooltip("Pad art drawn under the extra spots.")]
        public Sprite padSprite;
        [Tooltip("Tileable ground shown beyond the image on screens wider than 16:9.")]
        public Sprite filler;
        public Color cameraColor = new Color(0.07f, 0.05f, 0.1f);

        [Tooltip("Road waypoints in order. The last one is where the cake stands.")]
        public Vector2[] route;
        [Tooltip("Optional second road from another entrance. It joins the main road and ends at the same cake.")]
        public Vector2[] secondRoute;
        [Tooltip("From this year on, part of every wave takes the second road.")]
        public int secondRouteFromYear = 4;
        [Tooltip("Tower pads already painted into the background.")]
        public Vector2[] paintedSpots;
        [Tooltip("Extra tower pads; padSprite is drawn under them.")]
        public Vector2[] extraSpots;
        public MapProp[] props;

        [Header("Generated layout")]
        public bool generated;
        public RoadTiles tiles;

        public static Vector3 ToWorld(Vector2 pixel) =>
            new Vector3((pixel.x - ImageWidth * 0.5f) / PixelsPerUnit, (ImageHeight * 0.5f - pixel.y) / PixelsPerUnit, 0f);
    }
}
