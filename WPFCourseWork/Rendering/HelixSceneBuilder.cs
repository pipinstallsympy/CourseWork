using System.Windows.Media;
using System.Windows.Media.Media3D;
using CourseWorkZherbin;
using HelixToolkit.Wpf.SharpDX;
using SharpDX;
using WpfColor = System.Windows.Media.Color;
using Color4 = SharpDX.Color4;
using PerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;

namespace WPFCourseWork.Rendering;

public static class HelixSceneBuilder
{
    private const double UnitAxisLength = 1.0;
    private const double DefaultGridSpacing = 1.0;

    private static readonly Dictionary<WpfColor, PhongMaterial> MaterialCache = new();

    /// <summary>
    /// Coordinate grid in the Z–X plane (normal along Y), visible from application start.
    /// </summary>
    public static AxisPlaneGridModel3D CreateZxPlaneGrid()
    {
        return new AxisPlaneGridModel3D
        {
            UpAxis = Axis.Y,
            Offset = 0,
            AutoSpacing = false,
            GridSpacing = DefaultGridSpacing,
            GridThickness = 0.015,
            GridColor = WpfColor.FromRgb(0x70, 0x70, 0x70),
            PlaneColor = WpfColor.FromArgb(0, 0xFF, 0xFF, 0xFF),
            FadingFactor = 0.85,
            IsHitTestVisible = false
        };
    }

    /// <summary>
    /// Unit X/Y/Z direction arrows at world origin (0, 0, 0).
    /// </summary>
    public static IReadOnlyList<MeshGeometryModel3D> CreateOriginUnitAxes()
    {
        const float len = (float)UnitAxisLength;
        const float shaftDiameter = 0.030f;
        const float headLength = 0.20f;
        const float headBaseDiameter = 0.11f;
        return
        [
            CreateAxisArrow(WpfColor.FromRgb(0xE0, 0x30, 0x30), new Vector3(len, 0, 0), shaftDiameter, headLength, headBaseDiameter),
            CreateAxisArrow(WpfColor.FromRgb(0x30, 0xC0, 0x30), new Vector3(0, len, 0), shaftDiameter, headLength, headBaseDiameter),
            CreateAxisArrow(WpfColor.FromRgb(0x30, 0x70, 0xE0), new Vector3(0, 0, len), shaftDiameter, headLength, headBaseDiameter)
        ];
    }

    private static MeshGeometryModel3D CreateAxisArrow(
        WpfColor color,
        Vector3 tip,
        float shaftDiameter,
        float headLength,
        float headBaseDiameter)
    {
        var direction = tip;
        direction.Normalize();
        var shaftEnd = tip - direction * headLength;
        var shaftRadius = shaftDiameter * 0.5f;
        var headBaseRadius = headBaseDiameter * 0.5f;

        var builder = new MeshBuilder();
        builder.AddCylinder(Vector3.Zero, shaftEnd, shaftRadius, thetaDiv: 18, cap1: true, cap2: true);
        builder.AddCone(shaftEnd, direction, headBaseRadius, topRadius: 0f, headLength, baseCap: true, topCap: false, thetaDiv: 20);
        var diffuse = new Color4(color.R / 255f, color.G / 255f, color.B / 255f, 1f);
        return new MeshGeometryModel3D
        {
            Geometry = builder.ToMesh(),
            Material = new PhongMaterial
            {
                DiffuseColor = diffuse,
                AmbientColor = diffuse,
                EmissiveColor = ScaleColor4(diffuse, 0.2f)
            },
            IsHitTestVisible = false
        };
    }

    public static IReadOnlyList<Element3D> CreateDefaultSceneLights()
    {
        WpfColor fill = WpfColor.FromRgb(160, 160, 160);
        return
        [
            CreateDirectionalLight(new Vector3D(-1, -1, -1), fill),
            CreateDirectionalLight(new Vector3D(-1, 1, -1), fill),
            CreateDirectionalLight(new Vector3D(1, -1, -1), fill),
            CreateDirectionalLight(new Vector3D(0, 0, 1), WpfColor.FromRgb(120, 120, 130)),
            new AmbientLight3D { Color = WpfColor.FromRgb(0x30, 0x30, 0x30) }
        ];
    }

    public static IReadOnlyList<Element3D> BuildBatchedMaterialMeshes(
        CubeLine line,
        bool useComponentColors,
        Dictionary<Cube, WpfColor>? colorMap)
    {
        var builders = new Dictionary<WpfColor, MeshBuilder>();

        for (int idx = 0; idx < line.Count(); idx++)
        {
            var cube = line[idx];
            if (cube.IsEmpty) continue;

            WpfColor color = WpfColor.FromRgb(0xCC, 0x22, 0x22);
            if (useComponentColors && colorMap != null && colorMap.TryGetValue(cube, out var mapped))
            {
                color = mapped;
            }

            if (!builders.TryGetValue(color, out var builder))
            {
                builder = new MeshBuilder();
                builders[color] = builder;
            }

            AddCubeToBuilder(builder, cube);
        }

        var result = new List<Element3D>(builders.Count);
        foreach (var (color, builder) in builders)
        {
            var mesh = builder.ToMesh();
            if (mesh == null || mesh.Positions == null || mesh.Positions.Count == 0) continue;

            result.Add(new MeshGeometryModel3D
            {
                Geometry = mesh,
                Material = GetOrCreateMaterial(color, 1.0),
                IsHitTestVisible = false
            });
        }

        return result;
    }

    public static LineGeometryModel3D? BuildMaterialWireframe(
        IEnumerable<(int x1, int y1, int z1, int x2, int y2, int z2)> edges,
        Cube referenceCube)
    {
        var edgeList = edges as IList<(int, int, int, int, int, int)> ?? edges.ToList();
        if (edgeList.Count == 0) return null;

        double half = referenceCube.SideLength * 0.5;
        double step = referenceCube.SideLength;
        double baseX = referenceCube.CentralPoint.X - half;
        double baseY = referenceCube.CentralPoint.Y - half;
        double baseZ = referenceCube.CentralPoint.Z - half;

        var positions = new Vector3Collection(edgeList.Count * 2);
        foreach (var (x1, y1, z1, x2, y2, z2) in edgeList)
        {
            positions.Add(new Vector3(
                (float)(baseX + x1 * step),
                (float)(baseY + y1 * step),
                (float)(baseZ + z1 * step)));
            positions.Add(new Vector3(
                (float)(baseX + x2 * step),
                (float)(baseY + y2 * step),
                (float)(baseZ + z2 * step)));
        }

        return new LineGeometryModel3D
        {
            Geometry = CreateLineGeometry(positions),
            Color = WpfColor.FromRgb(0x69, 0x69, 0x69),
            Thickness = 1.0,
            IsHitTestVisible = false
        };
    }

    public static LineGeometryModel3D? BuildSampleBoundaryWireframe(CubeLine line, double thickness = 1.0)
    {
        return BuildSampleBoundaryWireframe(ComputeSceneBounds(line), thickness);
    }

    public static LineGeometryModel3D? BuildSampleBoundaryWireframe(
        (double minX, double minY, double minZ, double maxX, double maxY, double maxZ) bounds,
        double thickness = 3.0)
    {
        var (minX, minY, minZ, maxX, maxY, maxZ) = bounds;
        var positions = new Vector3Collection(24);

        void AddEdge(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            positions.Add(new Vector3((float)x1, (float)y1, (float)z1));
            positions.Add(new Vector3((float)x2, (float)y2, (float)z2));
        }

        // Bottom face (Y = minY)
        AddEdge(minX, minY, minZ, maxX, minY, minZ);
        AddEdge(maxX, minY, minZ, maxX, minY, maxZ);
        AddEdge(maxX, minY, maxZ, minX, minY, maxZ);
        AddEdge(minX, minY, maxZ, minX, minY, minZ);
        // Top face (Y = maxY)
        AddEdge(minX, maxY, minZ, maxX, maxY, minZ);
        AddEdge(maxX, maxY, minZ, maxX, maxY, maxZ);
        AddEdge(maxX, maxY, maxZ, minX, maxY, maxZ);
        AddEdge(minX, maxY, maxZ, minX, maxY, minZ);
        // Vertical edges
        AddEdge(minX, minY, minZ, minX, maxY, minZ);
        AddEdge(maxX, minY, minZ, maxX, maxY, minZ);
        AddEdge(maxX, minY, maxZ, maxX, maxY, maxZ);
        AddEdge(minX, minY, maxZ, minX, maxY, maxZ);

        return new LineGeometryModel3D
        {
            Geometry = CreateLineGeometry(positions),
            Color = WpfColor.FromRgb(0x69, 0x69, 0x69),
            Thickness = thickness,
            IsHitTestVisible = false
        };
    }

    public static MeshGeometryModel3D? BuildBatchedPoreMesh(
        IEnumerable<Cube> cubes,
        WpfColor color,
        double opacity)
    {
        var builder = new MeshBuilder();
        foreach (var cube in cubes)
            AddCubeToBuilder(builder, cube);

        var geometry = builder.ToMesh();
        if (geometry == null || geometry.Positions == null || geometry.Positions.Count == 0)
            return null;

        bool transparent = opacity < 1.0;
        return new MeshGeometryModel3D
        {
            Geometry = geometry,
            Material = GetOrCreateMaterial(color, opacity),
            IsTransparent = transparent,
            IsHitTestVisible = false
        };
    }

    public static MeshGeometryModel3D BuildPoreMesh(Cube cube, WpfColor color, double opacity)
    {
        var builder = new MeshBuilder();
        AddCubeToBuilder(builder, cube);
        var geometry = builder.ToMesh();

        bool transparent = opacity < 1.0;
        return new MeshGeometryModel3D
        {
            Geometry = geometry,
            Material = GetOrCreateMaterial(color, opacity),
            Tag = cube,
            IsTransparent = transparent,
            IsHitTestVisible = true
        };
    }

    public static void ApplyPoreAppearance(MeshGeometryModel3D mesh, WpfColor color, double opacity)
    {
        mesh.Material = GetOrCreateMaterial(color, opacity);
        mesh.IsTransparent = opacity < 1.0;
    }

    public static (double minX, double minY, double minZ, double maxX, double maxY, double maxZ) ComputeSceneBounds(CubeLine line)
    {
        return ComputeBounds(line);
    }

    public static PerspectiveCamera CreateIsometricCamera(CubeLine line, double fieldOfView = 50)
    {
        return CreateIsometricCameraForBounds(ComputeBounds(line), fieldOfView);
    }

    public static PerspectiveCamera CreateIsometricCameraForBounds(
        (double minX, double minY, double minZ, double maxX, double maxY, double maxZ) bounds,
        double fieldOfView = 50)
    {
        var (minX, minY, minZ, maxX, maxY, maxZ) = bounds;
        double cx = (minX + maxX) * 0.5;
        double cy = (minY + maxY) * 0.5;
        double cz = (minZ + maxZ) * 0.5;
        var center = new Point3D(cx, cy, cz);

        double dx = maxX - minX;
        double dy = maxY - minY;
        double dz = maxZ - minZ;
        double radius = 0.5 * Math.Sqrt(dx * dx + dy * dy + dz * dz);

        var direction = new Vector3D(1, 0.75, 1);
        direction.Normalize();

        double halfFovRad = fieldOfView * 0.5 * Math.PI / 180.0;
        double distance = Math.Max(radius / Math.Sin(halfFovRad) * 1.3, 2.0);

        var position = center + direction * distance;
        var lookDirection = center - position;

        return new PerspectiveCamera
        {
            Position = position,
            LookDirection = lookDirection,
            UpDirection = new Vector3D(0, 1, 0),
            FieldOfView = fieldOfView
        };
    }

    private static (double minX, double minY, double minZ, double maxX, double maxY, double maxZ) ComputeBounds(CubeLine line)
    {
        var first = line[0];
        double half = first.SideLength * 0.5;
        double minX = first.CentralPoint.X - half;
        double minY = first.CentralPoint.Y - half;
        double minZ = first.CentralPoint.Z - half;
        double maxX = first.CentralPoint.X + half;
        double maxY = first.CentralPoint.Y + half;
        double maxZ = first.CentralPoint.Z + half;

        for (int idx = 1; idx < line.Count(); idx++)
        {
            var cube = line[idx];
            half = cube.SideLength * 0.5;
            minX = Math.Min(minX, cube.CentralPoint.X - half);
            minY = Math.Min(minY, cube.CentralPoint.Y - half);
            minZ = Math.Min(minZ, cube.CentralPoint.Z - half);
            maxX = Math.Max(maxX, cube.CentralPoint.X + half);
            maxY = Math.Max(maxY, cube.CentralPoint.Y + half);
            maxZ = Math.Max(maxZ, cube.CentralPoint.Z + half);
        }

        return (minX, minY, minZ, maxX, maxY, maxZ);
    }

    private static DirectionalLight3D CreateDirectionalLight(Vector3D direction, WpfColor color)
    {
        return new DirectionalLight3D
        {
            Direction = direction,
            Color = color
        };
    }

    private static LineGeometry3D CreateLineGeometry(Vector3Collection positions)
    {
        var indices = new IntCollection(positions.Count);
        for (int i = 0; i < positions.Count; i++)
            indices.Add(i);

        return new LineGeometry3D
        {
            Positions = positions,
            Indices = indices
        };
    }

    private static void AddCubeToBuilder(MeshBuilder builder, Cube cube)
    {
        double side = cube.SideLength;
        builder.AddBox(
            new Vector3(
                (float)cube.CentralPoint.X,
                (float)cube.CentralPoint.Y,
                (float)cube.CentralPoint.Z),
            side, side, side);
    }

    private static PhongMaterial GetOrCreateMaterial(WpfColor color, double opacity)
    {
        byte a = opacity < 1.0
            ? (byte)Math.Round(opacity * 255)
            : color.A;

        var key = WpfColor.FromArgb(a, color.R, color.G, color.B);
        if (MaterialCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var diffuse = new Color4(color.R / 255f, color.G / 255f, color.B / 255f, a / 255f);
        var material = new PhongMaterial
        {
            DiffuseColor = diffuse,
            AmbientColor = ScaleColor4(diffuse, 0.35f),
            SpecularColor = new Color4(0.15f, 0.15f, 0.15f, a / 255f),
            SpecularShininess = 24f
        };
        MaterialCache[key] = material;
        return material;
    }

    private static Color4 ScaleColor4(Color4 color, float factor)
    {
        return new Color4(
            color.Red * factor,
            color.Green * factor,
            color.Blue * factor,
            color.Alpha);
    }
}
