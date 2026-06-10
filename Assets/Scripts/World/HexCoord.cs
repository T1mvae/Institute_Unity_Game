using System;
using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// Axial hex coordinate (q, r) for a pointy-top hex grid.
    /// The implicit cube coordinate is s = -q - r.
    /// Neighbor order matches <see cref="Directions"/>.
    /// This is a small struct: it holds coordinate logic only, never gameplay state.
    /// </summary>
    [Serializable]
    public struct HexCoord : IEquatable<HexCoord>
    {
        public int q;
        public int r;

        /// <summary>Cube z axis, derived. (x = q, z = r, y = -q-r in cube space.)</summary>
        public int S => -q - r;

        public HexCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        /// <summary>The six axial neighbor offsets in clockwise order starting East.</summary>
        public static readonly HexCoord[] Directions =
        {
            new HexCoord(1, 0),
            new HexCoord(1, -1),
            new HexCoord(0, -1),
            new HexCoord(-1, 0),
            new HexCoord(-1, 1),
            new HexCoord(0, 1),
        };

        public HexCoord Neighbor(int direction)
        {
            HexCoord d = Directions[((direction % 6) + 6) % 6];
            return new HexCoord(q + d.q, r + d.r);
        }

        public HexCoord[] AllNeighbors()
        {
            var result = new HexCoord[6];
            for (int i = 0; i < 6; i++)
                result[i] = new HexCoord(q + Directions[i].q, r + Directions[i].r);
            return result;
        }

        public static int Distance(HexCoord a, HexCoord b)
        {
            return (Mathf.Abs(a.q - b.q) + Mathf.Abs(a.q + a.r - b.q - b.r) + Mathf.Abs(a.r - b.r)) / 2;
        }

        /// <summary>
        /// Converts a fractional cube coordinate to the nearest valid hex (cube rounding).
        /// Used by <see cref="FromWorld"/> for click/hover pick.
        /// </summary>
        public static HexCoord Round(float fq, float fr)
        {
            float fs = -fq - fr;
            int rq = Mathf.RoundToInt(fq);
            int rr = Mathf.RoundToInt(fr);
            int rs = Mathf.RoundToInt(fs);

            float dq = Mathf.Abs(rq - fq);
            float dr = Mathf.Abs(rr - fr);
            float ds = Mathf.Abs(rs - fs);

            if (dq > dr && dq > ds)
                rq = -rr - rs;
            else if (dr > ds)
                rr = -rq - rs;

            return new HexCoord(rq, rr);
        }

        /// <summary>
        /// World-space center of this hex on the XY plane (z = 0), pointy-top layout.
        /// </summary>
        public Vector3 ToWorld(float hexSize)
        {
            float x = hexSize * Mathf.Sqrt(3f) * (q + r * 0.5f);
            float y = hexSize * 1.5f * r;
            // Flip Y so increasing r goes "down" the screen, like a typical strategy map.
            return new Vector3(x, -y, 0f);
        }

        /// <summary>
        /// Inverse of <see cref="ToWorld"/>: pixel/world position to the hex it lands in.
        /// </summary>
        public static HexCoord FromWorld(Vector3 world, float hexSize)
        {
            float px = world.x;
            float py = -world.y; // undo the Y flip in ToWorld
            float fq = (Mathf.Sqrt(3f) / 3f * px - 1f / 3f * py) / hexSize;
            float fr = (2f / 3f * py) / hexSize;
            return Round(fq, fr);
        }

        public bool Equals(HexCoord other) => q == other.q && r == other.r;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => unchecked((q * 73856093) ^ (r * 19349663));
        public override string ToString() => $"({q}, {r})";

        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);
    }
}
