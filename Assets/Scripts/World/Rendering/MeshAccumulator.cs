using System.Collections.Generic;
using UnityEngine;

namespace Institute.World
{
    /// <summary>Builds a vertex-colored mesh from quads and hex fans. Used for borders/highlights.</summary>
    public class MeshAccumulator
    {
        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<int> _tris = new List<int>();
        readonly List<Color> _colors = new List<Color>();

        public int VertexCount => _verts.Count;

        public void Clear()
        {
            _verts.Clear();
            _tris.Clear();
            _colors.Clear();
        }

        /// <summary>Adds a thick line segment (a quad of the given width) on the XY plane.</summary>
        public void AddLine(Vector3 a, Vector3 b, float width, Color color, float z)
        {
            Vector3 dir = (b - a);
            float len = dir.magnitude;
            if (len < 1e-5f) return;
            dir /= len;
            Vector3 normal = new Vector3(-dir.y, dir.x, 0f) * (width * 0.5f);

            a.z = z; b.z = z;
            int baseIndex = _verts.Count;
            _verts.Add(a + normal);
            _verts.Add(b + normal);
            _verts.Add(b - normal);
            _verts.Add(a - normal);
            for (int i = 0; i < 4; i++) _colors.Add(color);

            _tris.Add(baseIndex);
            _tris.Add(baseIndex + 1);
            _tris.Add(baseIndex + 2);
            _tris.Add(baseIndex);
            _tris.Add(baseIndex + 2);
            _tris.Add(baseIndex + 3);
        }

        /// <summary>Adds a filled hexagon (triangle fan) at the given center.</summary>
        public void AddHexFill(Vector3 center, float size, Color color, float z)
        {
            center.z = z;
            int baseIndex = _verts.Count;
            _verts.Add(center);
            _colors.Add(color);
            for (int i = 0; i < 6; i++)
            {
                Vector3 corner = HexMetrics.Corner(center, size, i);
                corner.z = z;
                _verts.Add(corner);
                _colors.Add(color);
            }
            for (int i = 0; i < 6; i++)
            {
                _tris.Add(baseIndex);
                _tris.Add(baseIndex + 1 + i);
                _tris.Add(baseIndex + 1 + (i + 1) % 6);
            }
        }

        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_verts);
            mesh.SetTriangles(_tris, 0);
            mesh.SetColors(_colors);
            mesh.RecalculateBounds();
        }
    }
}
