using System.Globalization;
using System.Text;

namespace CourseWorkZherbin;

public enum ObjExportMode
{
    MaterialOnly,
    PoresOnly,
    Combined
}

public static class ObjExporter
{
    private const long QuantizeScale = 1_000_000;
    private static readonly UTF8Encoding ObjFileEncoding = new(encoderShouldEmitUTF8Identifier: false);

    private static StreamWriter CreateObjFileWriter(string filePath) =>
        new(filePath, append: false, ObjFileEncoding) { NewLine = "\n" };

    private static string GetSolidMaterialName(ObjColor color) =>
        $"solid_{color.R:X2}{color.G:X2}{color.B:X2}";

    private static string GetPoreMaterialName(ObjColor color) =>
        $"pore_{color.R:X2}{color.G:X2}{color.B:X2}";

    public static void Export(CubeLine line, string filePath, ObjExportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(settings);

        switch (settings.Mode)
        {
            case ObjExportMode.MaterialOnly:
                ExportMaterial(
                    line,
                    filePath,
                    settings.IncludeMaterialColor ? settings.MaterialColor : null);
                break;
            case ObjExportMode.PoresOnly:
                ExportPores(
                    line,
                    filePath,
                    settings.IncludePoreColor ? settings.PoreColor : null);
                break;
            case ObjExportMode.Combined:
                ExportCombined(line, filePath, settings.MaterialColor, settings.PoreColor);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(settings), settings.Mode, null);
        }
    }

    public static void Export(CubeLine line, string filePath, ObjExportMode mode)
    {
        Export(line, filePath, new ObjExportSettings(
            mode,
            ObjColor.DefaultMaterial,
            ObjColor.DefaultPore,
            IncludeMaterialColor: false,
            IncludePoreColor: false));
    }

    public static void ExportMaterial(CubeLine line, string filePath, ObjColor? color = null)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(filePath);

        if (color.HasValue)
        {
            string mtlFileName = Path.GetFileName(Path.ChangeExtension(filePath, ".mtl"));
            string mtlFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? ".", mtlFileName);

            using var writer = CreateObjFileWriter(filePath);
            ExportMaterial(line, writer, mtlFileName, color.Value);
            WriteSingleMtlFile(mtlFilePath, GetSolidMaterialName(color.Value), color.Value);
            return;
        }

        using var plainWriter = CreateObjFileWriter(filePath);
        ExportMaterial(line, plainWriter, null, null);
    }

    public static void ExportMaterial(CubeLine line, TextWriter writer)
    {
        ExportMaterial(line, writer, null, null);
    }

    private static void ExportMaterial(
        CubeLine line,
        TextWriter writer,
        string? mtlFileName,
        ObjColor? color)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("# CourseWorkZherbin material export");
        if (!string.IsNullOrEmpty(mtlFileName))
        {
            writer.WriteLine($"mtllib {mtlFileName}");
        }

        int n = ValidateAndGetGridSize(line);
        if (line.GetMaterial().Count == 0)
        {
            return;
        }

        using var grid = line.GenerateGridFromLine();
        var mesh = BuildShellMesh(grid, n, cube => !cube.IsEmpty, ShouldExportMaterialFace);
        WriteMesh(writer, mesh, color.HasValue ? GetSolidMaterialName(color.Value) : null);
    }

    public static void ExportPores(CubeLine line, string filePath, ObjColor? color = null)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(filePath);

        if (color.HasValue)
        {
            string mtlFileName = Path.GetFileName(Path.ChangeExtension(filePath, ".mtl"));
            string mtlFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? ".", mtlFileName);

            using var writer = CreateObjFileWriter(filePath);
            ExportPores(line, writer, mtlFileName, color.Value);
            WriteSingleMtlFile(mtlFilePath, GetPoreMaterialName(color.Value), color.Value);
            return;
        }

        using var plainWriter = CreateObjFileWriter(filePath);
        ExportPores(line, plainWriter, null, null);
    }

    public static void ExportPores(CubeLine line, TextWriter writer)
    {
        ExportPores(line, writer, null, null);
    }

    private static void ExportPores(
        CubeLine line,
        TextWriter writer,
        string? mtlFileName,
        ObjColor? color)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("# CourseWorkZherbin pores export");
        if (!string.IsNullOrEmpty(mtlFileName))
        {
            writer.WriteLine($"mtllib {mtlFileName}");
        }

        int n = ValidateAndGetGridSize(line);
        if (line.PoreAmount() == 0)
        {
            return;
        }

        using var grid = line.GenerateGridFromLine();
        var mesh = BuildPoreMesh(grid, n, skipMaterialBoundary: false);
        WriteMesh(writer, mesh, color.HasValue ? GetPoreMaterialName(color.Value) : null);
    }

    public static void ExportCombined(
        CubeLine line,
        string filePath,
        ObjColor materialColor,
        ObjColor poreColor)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(filePath);

        int n = ValidateAndGetGridSize(line);
        if (line.GetMaterial().Count == 0 && line.PoreAmount() == 0)
        {
            return;
        }

        string mtlFileName = Path.GetFileName(Path.ChangeExtension(filePath, ".mtl"));
        string mtlFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? ".", mtlFileName);

        string solidMaterialName = GetSolidMaterialName(materialColor);
        string poreMaterialName = GetPoreMaterialName(poreColor);

        using var grid = line.GenerateGridFromLine();
        var materialMesh = BuildShellMesh(grid, n, cube => !cube.IsEmpty, ShouldExportMaterialFace);
        var poreMesh = BuildPoreMesh(grid, n, skipMaterialBoundary: false);

        using (var writer = CreateObjFileWriter(filePath))
        {
            writer.WriteLine("# CourseWorkZherbin combined export");
            writer.WriteLine($"mtllib {mtlFileName}");

            var combinedVertices = new List<(long x, long y, long z)>();
            var combinedVertexIndex = new Dictionary<(long x, long y, long z), int>();
            var materialFaces = RemapFaces(materialMesh, combinedVertices, combinedVertexIndex);
            var poreFaces = RemapFaces(poreMesh, combinedVertices, combinedVertexIndex);

            WriteVertices(writer, combinedVertices);

            var culture = CultureInfo.InvariantCulture;
            if (materialFaces.Count > 0)
            {
                writer.WriteLine($"usemtl {solidMaterialName}");
                WriteFaces(writer, materialFaces, culture);
            }

            if (poreFaces.Count > 0)
            {
                writer.WriteLine($"usemtl {poreMaterialName}");
                WriteFaces(writer, poreFaces, culture);
            }
        }

        WriteCombinedMtlFile(mtlFilePath, solidMaterialName, materialColor, poreMaterialName, poreColor);
    }

    public static void ExportCombined(CubeLine line, string filePath)
    {
        ExportCombined(line, filePath, ObjColor.DefaultMaterial, ObjColor.DefaultPore);
    }

    private static int ValidateAndGetGridSize(CubeLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        int cellCount = line.Count();
        int n = (int)Math.Round(Math.Cbrt(cellCount));
        if (n * n * n != cellCount)
        {
            throw new ArgumentException("CubeLine must represent an N×N×N grid.", nameof(line));
        }

        return n;
    }

    private static ShellMesh BuildPoreMesh(CubeGrid grid, int n, bool skipMaterialBoundary) =>
        BuildShellMesh(
            grid,
            n,
            cube => cube.IsEmpty,
            (g, size, i, j, k, ni, nj, nk) =>
                ShouldExportPoreFace(g, size, i, j, k, ni, nj, nk, skipMaterialBoundary),
            ShouldReversePoreFaceAtMaterialBoundary);

    private static ShellMesh BuildShellMesh(
        CubeGrid grid,
        int n,
        Func<Cube, bool> includeCell,
        Func<CubeGrid, int, int, int, int, int, int, int, bool> shouldExportFace,
        Func<CubeGrid, int, int, int, int, int, int, int, bool>? shouldReverseWinding = null)
    {
        var vertexKeys = new List<(long x, long y, long z)>();
        var vertexIndex = new Dictionary<(long x, long y, long z), int>();
        var faces = new List<(int a, int b, int c)>();

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                for (int k = 0; k < n; k++)
                {
                    var cube = grid.Grid[i][j][k];
                    if (!includeCell(cube))
                    {
                        continue;
                    }

                    double cx = cube.CentralPoint.X;
                    double cy = cube.CentralPoint.Y;
                    double cz = cube.CentralPoint.Z;
                    double h = cube.SideLength * 0.5;

                    TryAddFace(grid, n, i, j, k, i - 1, j, k, shouldExportFace, shouldReverseWinding, vertexKeys, vertexIndex, faces,
                        cx - h, cy - h, cz - h,
                        cx - h, cy + h, cz - h,
                        cx - h, cy + h, cz + h,
                        cx - h, cy - h, cz + h);

                    TryAddFace(grid, n, i, j, k, i + 1, j, k, shouldExportFace, shouldReverseWinding, vertexKeys, vertexIndex, faces,
                        cx + h, cy - h, cz - h,
                        cx + h, cy + h, cz - h,
                        cx + h, cy + h, cz + h,
                        cx + h, cy - h, cz + h);

                    TryAddFace(grid, n, i, j, k, i, j - 1, k, shouldExportFace, shouldReverseWinding, vertexKeys, vertexIndex, faces,
                        cx - h, cy - h, cz - h,
                        cx + h, cy - h, cz - h,
                        cx + h, cy - h, cz + h,
                        cx - h, cy - h, cz + h);

                    TryAddFace(grid, n, i, j, k, i, j + 1, k, shouldExportFace, shouldReverseWinding, vertexKeys, vertexIndex, faces,
                        cx - h, cy + h, cz - h,
                        cx + h, cy + h, cz - h,
                        cx + h, cy + h, cz + h,
                        cx - h, cy + h, cz + h);

                    TryAddFace(grid, n, i, j, k, i, j, k - 1, shouldExportFace, shouldReverseWinding, vertexKeys, vertexIndex, faces,
                        cx - h, cy - h, cz - h,
                        cx + h, cy - h, cz - h,
                        cx + h, cy + h, cz - h,
                        cx - h, cy + h, cz - h);

                    TryAddFace(grid, n, i, j, k, i, j, k + 1, shouldExportFace, shouldReverseWinding, vertexKeys, vertexIndex, faces,
                        cx - h, cy - h, cz + h,
                        cx + h, cy - h, cz + h,
                        cx + h, cy + h, cz + h,
                        cx - h, cy + h, cz + h);
                }
            }
        }

        return new ShellMesh(vertexKeys, faces);
    }

    private static List<(int a, int b, int c)> RemapFaces(
        ShellMesh mesh,
        List<(long x, long y, long z)> targetVertices,
        Dictionary<(long x, long y, long z), int> targetVertexIndex)
    {
        var remappedFaces = new List<(int a, int b, int c)>(mesh.Faces.Count);
        foreach (var (a, b, c) in mesh.Faces)
        {
            remappedFaces.Add((
                RemapVertexIndex(mesh.Vertices[a - 1], targetVertices, targetVertexIndex),
                RemapVertexIndex(mesh.Vertices[b - 1], targetVertices, targetVertexIndex),
                RemapVertexIndex(mesh.Vertices[c - 1], targetVertices, targetVertexIndex)));
        }

        return remappedFaces;
    }

    private static int RemapVertexIndex(
        (long x, long y, long z) key,
        List<(long x, long y, long z)> targetVertices,
        Dictionary<(long x, long y, long z), int> targetVertexIndex)
    {
        if (targetVertexIndex.TryGetValue(key, out int existing))
        {
            return existing;
        }

        int index = targetVertices.Count + 1;
        targetVertices.Add(key);
        targetVertexIndex[key] = index;
        return index;
    }

    private static void WriteMesh(TextWriter writer, ShellMesh mesh, string? materialName)
    {
        WriteVertices(writer, mesh.Vertices);

        var culture = CultureInfo.InvariantCulture;
        if (!string.IsNullOrEmpty(materialName))
        {
            writer.WriteLine($"usemtl {materialName}");
        }

        WriteFaces(writer, mesh.Faces, culture);
    }

    private static void WriteVertices(TextWriter writer, List<(long x, long y, long z)> vertices)
    {
        var culture = CultureInfo.InvariantCulture;
        foreach (var (x, y, z) in vertices)
        {
            writer.WriteLine(
                string.Format(
                    culture,
                    "v {0} {1} {2}",
                    x / (double)QuantizeScale,
                    y / (double)QuantizeScale,
                    z / (double)QuantizeScale));
        }
    }

    private static void WriteFaces(
        TextWriter writer,
        List<(int a, int b, int c)> faces,
        CultureInfo culture)
    {
        foreach (var (a, b, c) in faces)
        {
            writer.WriteLine(string.Format(culture, "f {0} {1} {2}", a, b, c));
        }
    }

    private static void WriteSingleMtlFile(string mtlFilePath, string materialName, ObjColor color)
    {
        using var writer = CreateObjFileWriter(mtlFilePath);
        writer.WriteLine($"newmtl {materialName}");
        WriteMtlColorProperties(writer, color);
    }

    private static void WriteCombinedMtlFile(
        string mtlFilePath,
        string solidMaterialName,
        ObjColor materialColor,
        string poreMaterialName,
        ObjColor poreColor)
    {
        using var writer = CreateObjFileWriter(mtlFilePath);
        writer.WriteLine($"newmtl {solidMaterialName}");
        WriteMtlColorProperties(writer, materialColor);
        writer.WriteLine();
        writer.WriteLine($"newmtl {poreMaterialName}");
        WriteMtlColorProperties(writer, poreColor);
    }

    private static void WriteMtlColorProperties(TextWriter writer, ObjColor color)
    {
        writer.WriteLine(color.ToMtlKa());
        writer.WriteLine(color.ToMtlKd());
        writer.WriteLine("Ks 0 0 0");
        writer.WriteLine("Ns 0");
        writer.WriteLine("d 1.0");
        writer.WriteLine("illum 2");
    }

    private static bool ShouldExportMaterialFace(
        CubeGrid grid,
        int n,
        int i,
        int j,
        int k,
        int ni,
        int nj,
        int nk)
    {
        _ = (i, j, k);

        if (ni < 0 || ni >= n || nj < 0 || nj >= n || nk < 0 || nk >= n)
        {
            return true;
        }

        return grid.Grid[ni][nj][nk].IsEmpty;
    }

    private static bool ShouldExportPoreFace(
        CubeGrid grid,
        int n,
        int i,
        int j,
        int k,
        int ni,
        int nj,
        int nk,
        bool skipMaterialBoundary)
    {
        if (ni < 0 || ni >= n || nj < 0 || nj >= n || nk < 0 || nk >= n)
        {
            return true;
        }

        if (!grid.Grid[ni][nj][nk].IsEmpty)
        {
            return !skipMaterialBoundary;
        }

        return CompareCell(i, j, k, ni, nj, nk) < 0;
    }

    private static bool ShouldReversePoreFaceAtMaterialBoundary(
        CubeGrid grid,
        int n,
        int i,
        int j,
        int k,
        int ni,
        int nj,
        int nk)
    {
        _ = (i, j, k);

        if (ni < 0 || ni >= n || nj < 0 || nj >= n || nk < 0 || nk >= n)
        {
            return false;
        }

        return !grid.Grid[ni][nj][nk].IsEmpty;
    }

    private static int CompareCell(int i, int j, int k, int ni, int nj, int nk)
    {
        if (i != ni)
        {
            return i.CompareTo(ni);
        }

        if (j != nj)
        {
            return j.CompareTo(nj);
        }

        return k.CompareTo(nk);
    }

    private static void TryAddFace(
        CubeGrid grid,
        int n,
        int i,
        int j,
        int k,
        int ni,
        int nj,
        int nk,
        Func<CubeGrid, int, int, int, int, int, int, int, bool> shouldExportFace,
        Func<CubeGrid, int, int, int, int, int, int, int, bool>? shouldReverseWinding,
        List<(long x, long y, long z)> vertexKeys,
        Dictionary<(long x, long y, long z), int> vertexIndex,
        List<(int a, int b, int c)> faces,
        double x1, double y1, double z1,
        double x2, double y2, double z2,
        double x3, double y3, double z3,
        double x4, double y4, double z4)
    {
        if (!shouldExportFace(grid, n, i, j, k, ni, nj, nk))
        {
            return;
        }

        bool reverseWinding = shouldReverseWinding?.Invoke(grid, n, i, j, k, ni, nj, nk) ?? false;
        AddFace(vertexKeys, vertexIndex, faces, reverseWinding, x1, y1, z1, x2, y2, z2, x3, y3, z3, x4, y4, z4);
    }

    private static void AddFace(
        List<(long x, long y, long z)> vertexKeys,
        Dictionary<(long x, long y, long z), int> vertexIndex,
        List<(int a, int b, int c)> faces,
        bool reverseWinding,
        double x1, double y1, double z1,
        double x2, double y2, double z2,
        double x3, double y3, double z3,
        double x4, double y4, double z4)
    {
        int i1 = GetOrAddVertex(vertexKeys, vertexIndex, x1, y1, z1);
        int i2 = GetOrAddVertex(vertexKeys, vertexIndex, x2, y2, z2);
        int i3 = GetOrAddVertex(vertexKeys, vertexIndex, x3, y3, z3);
        int i4 = GetOrAddVertex(vertexKeys, vertexIndex, x4, y4, z4);

        if (reverseWinding)
        {
            faces.Add((i1, i3, i2));
            faces.Add((i1, i4, i3));
        }
        else
        {
            faces.Add((i1, i2, i3));
            faces.Add((i1, i3, i4));
        }
    }

    private static int GetOrAddVertex(
        List<(long x, long y, long z)> vertexKeys,
        Dictionary<(long x, long y, long z), int> vertexIndex,
        double x,
        double y,
        double z)
    {
        var key = (
            (long)Math.Round(x * QuantizeScale),
            (long)Math.Round(y * QuantizeScale),
            (long)Math.Round(z * QuantizeScale));

        if (vertexIndex.TryGetValue(key, out int existing))
        {
            return existing;
        }

        int index = vertexKeys.Count + 1;
        vertexKeys.Add(key);
        vertexIndex[key] = index;
        return index;
    }

    private sealed record ShellMesh(
        List<(long x, long y, long z)> Vertices,
        List<(int a, int b, int c)> Faces);
}
