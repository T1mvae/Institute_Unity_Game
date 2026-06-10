using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// Pure geometry helpers for pointy-top hexes in world space (XY plane, z = 0).
    /// Corner positions are y-symmetric, so they tile correctly with HexCoord.ToWorld,
    /// which flips Y so increasing r runs "south".
    /// </summary>
    public static class HexMetrics
    {
        /// <summary>World position of corner i (0..5) of a hex centered at <paramref name="center"/>.</summary>
        public static Vector3 Corner(Vector3 center, float size, int i)
        {
            float angle = Mathf.Deg2Rad * (60f * i + 30f);
            return new Vector3(center.x + size * Mathf.Cos(angle), center.y + size * Mathf.Sin(angle), center.z);
        }

        public static void FillCorners(Vector3 center, float size, Vector3[] buffer)
        {
            for (int i = 0; i < 6; i++)
                buffer[i] = Corner(center, size, i);
        }

        /// <summary>
        /// Given a hex center and the two corners of one edge, returns the world center of the
        /// neighbor hex on the far side of that edge (reflection of the center across the edge).
        /// </summary>
        public static Vector3 NeighborCenterAcrossEdge(Vector3 center, Vector3 cornerA, Vector3 cornerB)
        {
            Vector3 edgeMid = (cornerA + cornerB) * 0.5f;
            return 2f * edgeMid - center;
        }
    }
}
