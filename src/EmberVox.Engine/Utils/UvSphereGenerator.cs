using System.Numerics;

namespace EmberVox.Engine.Utils;

public class UvSphereGenerator
{
    public int[] Indices { get; }
    public Vector3[] Vertices { get; }
    public Vector2[] UVs { get; }

    /// <summary>
    /// <see cref="UvSphereGenerator"/>'s constructor.
    /// </summary>
    /// <param name="radius">The radius of the generated UV sphere.</param>
    /// <param name="depth">
    /// Number of polar and azimuthal divisions. Must be >= 3.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="depth"/> is lower than 3.
    /// </exception>
    public UvSphereGenerator(float radius, ushort depth)
    {
        if (depth < 3)
            throw new ArgumentOutOfRangeException(
                nameof(depth),
                "UV sphere depth must be greater than or equal to 3."
            );

        Vertices = BuildVertices(radius, depth);
        UVs = BuildUVs(depth);
        Indices = BuildIndices(depth);
    }

    // ──────────────────────────────────────────────
    // Buffer layout (total = 2*D + (D-1)*(D+1)):
    //
    //   [0 .. D-1]                     → top pole fans     (D vertices)
    //   [D .. D + (D-1)*(D+1) - 1]     → body rings        ((D-1) rings × (D+1) verts)
    //   [D + (D-1)*(D+1) .. end]       → bottom pole fans  (D vertices)
    //
    // Each ring has (D+1) vertices so that the seam vertex at j=D
    // shares position with j=0 but carries u=1.0 instead of u=0.0.
    // ──────────────────────────────────────────────

    private static int TopPoleBase(int depth) => 0;

    private static int BodyBase(int depth) => depth;

    private static int BottomPoleBase(int depth) => depth + (depth - 1) * (depth + 1);

    private static int TotalVertexCount(int depth) => 2 * depth + (depth - 1) * (depth + 1);

    // Ring i (0-based, 0 = first ring below top pole), vertex column j (0..D inclusive).
    private static int RingVertex(int depth, int ring, int j) =>
        BodyBase(depth) + ring * (depth + 1) + j;

    // ── Vertices ──────────────────────────────────

    private static Vector3[] BuildVertices(float radius, ushort depth)
    {
        var verts = new Vector3[TotalVertexCount(depth)];
        var polarDelta = MathF.PI / depth;
        var azimuthDelta = 2f * MathF.PI / depth;

        // Top pole: D copies of (0, r, 0) — one per azimuthal sector.
        for (var j = 0; j < depth; j++)
            verts[TopPoleBase(depth) + j] = Vector3.UnitY * radius;

        // Body rings.
        for (var ring = 0; ring < depth - 1; ring++)
        {
            var polar = polarDelta * (ring + 1); // skips polar=0 (the pole itself)
            for (var j = 0; j <= depth; j++)
            {
                // j == depth wraps back to azimuth 0 but gets u = 1.0 in BuildUVs.
                var azimuth = azimuthDelta * (j % depth);
                verts[RingVertex(depth, ring, j)] = PolarToCartesian(polar, azimuth, radius);
            }
        }

        // Bottom pole: D copies of (0, -r, 0).
        for (var j = 0; j < depth; j++)
            verts[BottomPoleBase(depth) + j] = -Vector3.UnitY * radius;

        return verts;
    }

    // ── UVs ───────────────────────────────────────

    private static Vector2[] BuildUVs(ushort depth)
    {
        var uvs = new Vector2[TotalVertexCount(depth)];

        // Top pole: u centered in each sector, v = 0.
        for (var j = 0; j < depth; j++)
            uvs[TopPoleBase(depth) + j] = new Vector2((j + 0.5f) / depth, 1f); // era 0f

        // Body rings.
        for (var ring = 0; ring < depth - 1; ring++)
        {
            var v = 1f - (float)(ring + 1) / depth; // body rings
            for (var j = 0; j <= depth; j++)
                uvs[RingVertex(depth, ring, j)] = new Vector2((float)j / depth, v);
        }

        // Bottom pole: u centered in each sector, v = 1.
        for (var j = 0; j < depth; j++)
            uvs[BottomPoleBase(depth) + j] = new Vector2((j + 0.5f) / depth, 0f); // era 1f

        return uvs;
    }

    // ── Indices ───────────────────────────────────

    private static int[] BuildIndices(ushort depth)
    {
        // Top/bottom fans: D triangles each × 3 indices.
        // Body quads:      (D-2) rings × D quads × 2 triangles × 3 indices.
        var indexCount =
            depth * 3 // top fan
            + depth * 3 // bottom fan
            + (depth - 2) * depth * 6; // body quads
        var indices = new int[indexCount];
        var ix = 0;

        // Top fan: pole[j] → ring0[j] → ring0[j+1]  (CCW from outside)
        for (var j = 0; j < depth; j++)
        {
            indices[ix++] = TopPoleBase(depth) + j;
            indices[ix++] = RingVertex(depth, 0, j);
            indices[ix++] = RingVertex(depth, 0, j + 1);
        }

        // Body quads between ring[ring] and ring[ring+1].
        for (var ring = 0; ring < depth - 2; ring++)
        {
            for (var j = 0; j < depth; j++)
            {
                var a = RingVertex(depth, ring, j);
                var b = RingVertex(depth, ring, j + 1);
                var c = RingVertex(depth, ring + 1, j);
                var d = RingVertex(depth, ring + 1, j + 1);

                // Triangle 1
                indices[ix++] = a;
                indices[ix++] = c;
                indices[ix++] = b;

                // Triangle 2
                indices[ix++] = b;
                indices[ix++] = c;
                indices[ix++] = d;
            }
        }

        // Bottom fan: pole[j] → lastRing[j+1] → lastRing[j]  (winding flipped vs top)
        var lastRing = depth - 2;
        for (var j = 0; j < depth; j++)
        {
            indices[ix++] = BottomPoleBase(depth) + j;
            indices[ix++] = RingVertex(depth, lastRing, j + 1);
            indices[ix++] = RingVertex(depth, lastRing, j);
        }

        return indices;
    }

    // ── Helpers ───────────────────────────────────

    private static Vector3 PolarToCartesian(float polar, float azimuth, float radius)
    {
        var sinPolar = MathF.Sin(polar);
        return new Vector3(
            sinPolar * MathF.Cos(azimuth) * radius,
            MathF.Cos(polar) * radius,
            sinPolar * MathF.Sin(azimuth) * radius
        );
    }
}
