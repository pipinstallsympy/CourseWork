using System.Text;
using CourseWorkZherbin;

namespace TestCourseWork;

public class UnitTestObjExporter
{
    private static (int vertices, int faces, string content) ExportMaterialToString(CubeLine line)
    {
        using var writer = new StringWriter();
        ObjExporter.ExportMaterial(line, writer);
        string content = writer.ToString();
        return CountObjContent(content);
    }

    private static (int vertices, int faces, string content) ExportPoresToString(CubeLine line)
    {
        using var writer = new StringWriter();
        ObjExporter.ExportPores(line, writer);
        string content = writer.ToString();
        return CountObjContent(content);
    }

    private static (int vertices, int faces, string content) CountObjContent(string content)
    {
        int vertices = content.Split('\n').Count(line => line.StartsWith("v ", StringComparison.Ordinal));
        int faces = content.Split('\n').Count(line => line.StartsWith("f ", StringComparison.Ordinal));
        return (vertices, faces, content);
    }

    [Fact]
    public void ExportMaterial_2x2x2WithoutPores_Produces48Triangles()
    {
        using var grid = new CubeGrid(2);
        var line = grid.GenerateLineFromGrid()!;

        var (vertices, faces, _) = ExportMaterialToString(line);

        Assert.Equal(48, faces);
        Assert.InRange(vertices, 8, 26);
    }

    [Fact]
    public void ExportMaterial_SparseMaterial_ProducesFewerFacesThanSolid()
    {
        using var solidGrid = new CubeGrid(3);
        var solid = ExportMaterialToString(solidGrid.GenerateLineFromGrid()!);

        using var grid = new CubeGrid(3);
        var line = grid.GenerateLineFromGrid()!;
        for (int i = 0; i < line.Count(); i++)
        {
            line[i].IsEmpty = true;
        }

        line[line.Count() / 2].IsEmpty = false;
        var sparse = ExportMaterialToString(line);

        Assert.True(sparse.faces < solid.faces);
        Assert.Equal(12, sparse.faces);
    }

    [Fact]
    public void ExportMaterial_AllPores_WritesNoFaces()
    {
        using var grid = new CubeGrid(2, isEmpty: true);
        var line = grid.GenerateLineFromGrid()!;

        var (_, faces, content) = ExportMaterialToString(line);

        Assert.Equal(0, faces);
        Assert.StartsWith("# CourseWorkZherbin material export", content);
    }

    [Fact]
    public void ExportMaterial_NullLine_ThrowsArgumentNullException()
    {
        using var writer = new StringWriter();
        Assert.Throws<ArgumentNullException>(() => ObjExporter.ExportMaterial(null!, writer));
        Assert.Throws<ArgumentNullException>(() => ObjExporter.ExportMaterial(null!, "test.obj"));
    }

    [Fact]
    public void ExportMaterial_NonCubicLine_ThrowsArgumentException()
    {
        var line = new CubeLine();
        line.Line.Add(new Cube(new Point(), 1));
        line.Line.Add(new Cube(new Point(1, 0, 0), 1));

        using var writer = new StringWriter();
        Assert.Throws<ArgumentException>(() => ObjExporter.ExportMaterial(line, writer));
    }

    [Fact]
    public void ExportPores_2x2x2AllPores_Produces48Triangles()
    {
        using var grid = new CubeGrid(2, isEmpty: true);
        var line = grid.GenerateLineFromGrid()!;

        var (vertices, faces, content) = ExportPoresToString(line);

        Assert.Equal(48, faces);
        Assert.InRange(vertices, 8, 26);
        Assert.StartsWith("# CourseWorkZherbin pores export", content);
    }

    [Fact]
    public void ExportPores_AllMaterial_WritesNoFaces()
    {
        using var grid = new CubeGrid(2);
        var line = grid.GenerateLineFromGrid()!;

        var (_, faces, content) = ExportPoresToString(line);

        Assert.Equal(0, faces);
        Assert.StartsWith("# CourseWorkZherbin pores export", content);
    }

    [Fact]
    public void ExportPores_SparsePore_ProducesFewerFacesThanSolid()
    {
        using var solidGrid = new CubeGrid(3, isEmpty: true);
        var solid = ExportPoresToString(solidGrid.GenerateLineFromGrid()!);

        using var grid = new CubeGrid(3);
        var line = grid.GenerateLineFromGrid()!;
        line[line.Count() / 2].IsEmpty = true;
        var sparse = ExportPoresToString(line);

        Assert.True(sparse.faces < solid.faces);
        Assert.Equal(12, sparse.faces);
    }

    [Fact]
    public void ExportPores_NullLine_ThrowsArgumentNullException()
    {
        using var writer = new StringWriter();
        Assert.Throws<ArgumentNullException>(() => ObjExporter.ExportPores(null!, writer));
        Assert.Throws<ArgumentNullException>(() => ObjExporter.ExportPores(null!, "test.obj"));
    }

    [Fact]
    public void ExportPores_NonCubicLine_ThrowsArgumentException()
    {
        var line = new CubeLine();
        line.Line.Add(new Cube(new Point(), 1));
        line.Line.Add(new Cube(new Point(1, 0, 0), 1));

        using var writer = new StringWriter();
        Assert.Throws<ArgumentException>(() => ObjExporter.ExportPores(line, writer));
    }

    [Fact]
    public void ExportCombined_HasBothSections()
    {
        using var grid = new CubeGrid(3);
        var line = grid.GenerateLineFromGrid()!;
        line[0].IsEmpty = true;
        line[1].IsEmpty = true;

        string objPath = Path.Combine(Path.GetTempPath(), $"combined_{Guid.NewGuid():N}.obj");
        try
        {
            ObjExporter.ExportCombined(line, objPath);
            string content = File.ReadAllText(objPath);

            Assert.Contains("usemtl solid_CC2222", content);
            Assert.Contains("usemtl pore_8FBC8F", content);
            Assert.Contains("mtllib", content);
            Assert.True(content.Split('\n').Count(lineText => lineText.StartsWith("f ", StringComparison.Ordinal)) > 0);
        }
        finally
        {
            File.Delete(objPath);
            string mtlPath = Path.ChangeExtension(objPath, ".mtl");
            if (File.Exists(mtlPath))
            {
                File.Delete(mtlPath);
            }
        }
    }

    [Fact]
    public void ExportCombined_WritesMtlFile()
    {
        using var grid = new CubeGrid(2);
        var line = grid.GenerateLineFromGrid()!;
        line[0].IsEmpty = true;

        string objPath = Path.Combine(Path.GetTempPath(), $"combined_{Guid.NewGuid():N}.obj");
        string mtlPath = Path.ChangeExtension(objPath, ".mtl");
        try
        {
            ObjExporter.ExportCombined(line, objPath);

            Assert.True(File.Exists(mtlPath));
            string mtlContent = File.ReadAllText(mtlPath);
            Assert.Contains("newmtl solid_CC2222", mtlContent);
            Assert.Contains("newmtl pore_8FBC8F", mtlContent);
            Assert.Contains("Kd 0.8 0.133 0.133", mtlContent);
            Assert.Contains("Kd 0.561 0.737 0.561", mtlContent);
            Assert.Contains("d 1.0", mtlContent);
            Assert.Contains("illum 2", mtlContent);
        }
        finally
        {
            if (File.Exists(objPath))
            {
                File.Delete(objPath);
            }

            if (File.Exists(mtlPath))
            {
                File.Delete(mtlPath);
            }
        }
    }

    [Fact]
    public void ExportCombined_NullLine_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ObjExporter.ExportCombined(null!, "test.obj"));
    }

    [Fact]
    public void ExportCombined_NonCubicLine_ThrowsArgumentException()
    {
        var line = new CubeLine();
        line.Line.Add(new Cube(new Point(), 1));
        line.Line.Add(new Cube(new Point(1, 0, 0), 1));

        Assert.Throws<ArgumentException>(() => ObjExporter.ExportCombined(line, "test.obj"));
    }

    [Theory]
    [InlineData("#CC2222", 0xCC, 0x22, 0x22)]
    [InlineData("8FBC8F", 0x8F, 0xBC, 0x8F)]
    public void ObjColor_TryParseHex_ParsesValidValues(string hex, byte r, byte g, byte b)
    {
        Assert.True(ObjColor.TryParseHex(hex, out ObjColor color));
        Assert.Equal(new ObjColor(r, g, b), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#GGGGGG")]
    [InlineData("12345")]
    public void ObjColor_TryParseHex_RejectsInvalidValues(string hex)
    {
        Assert.False(ObjColor.TryParseHex(hex, out _));
    }

    [Fact]
    public void ObjColor_ToMtlKd_WritesNormalizedComponents()
    {
        Assert.Equal("Kd 0.8 0.133 0.133", ObjColor.DefaultMaterial.ToMtlKd());
        Assert.Equal("Ka 0.8 0.133 0.133", ObjColor.DefaultMaterial.ToMtlKa());
    }

    [Fact]
    public void ExportMaterial_WithColor_WritesMtl()
    {
        using var grid = new CubeGrid(2);
        var line = grid.GenerateLineFromGrid()!;

        string objPath = Path.Combine(Path.GetTempPath(), $"material_{Guid.NewGuid():N}.obj");
        string mtlPath = Path.ChangeExtension(objPath, ".mtl");
        var customColor = new ObjColor(0x11, 0x22, 0x33);

        try
        {
            ObjExporter.ExportMaterial(line, objPath, customColor);

            string objContent = File.ReadAllText(objPath);
            Assert.Contains("mtllib", objContent);
            Assert.Contains("usemtl solid_112233", objContent);
            Assert.Contains("illum 2", File.ReadAllText(mtlPath));
            Assert.True(File.Exists(mtlPath));
            Assert.Contains(customColor.ToMtlKd(), File.ReadAllText(mtlPath));
        }
        finally
        {
            if (File.Exists(objPath)) File.Delete(objPath);
            if (File.Exists(mtlPath)) File.Delete(mtlPath);
        }
    }

    [Fact]
    public void ExportMaterial_WithoutColor_NoMtl()
    {
        using var grid = new CubeGrid(2);
        var line = grid.GenerateLineFromGrid()!;

        string objPath = Path.Combine(Path.GetTempPath(), $"material_{Guid.NewGuid():N}.obj");
        string mtlPath = Path.ChangeExtension(objPath, ".mtl");

        try
        {
            ObjExporter.ExportMaterial(line, objPath);

            Assert.DoesNotContain("mtllib", File.ReadAllText(objPath));
            Assert.False(File.Exists(mtlPath));
        }
        finally
        {
            if (File.Exists(objPath)) File.Delete(objPath);
            if (File.Exists(mtlPath)) File.Delete(mtlPath);
        }
    }

    [Fact]
    public void ExportPores_WithColor_WritesMtl()
    {
        using var grid = new CubeGrid(2, isEmpty: true);
        var line = grid.GenerateLineFromGrid()!;

        string objPath = Path.Combine(Path.GetTempPath(), $"pores_{Guid.NewGuid():N}.obj");
        string mtlPath = Path.ChangeExtension(objPath, ".mtl");
        var customColor = new ObjColor(0x44, 0x55, 0x66);

        try
        {
            ObjExporter.ExportPores(line, objPath, customColor);

            string objContent = File.ReadAllText(objPath);
            Assert.Contains("mtllib", objContent);
            Assert.Contains("usemtl pore_445566", objContent);
            Assert.Contains("illum 2", File.ReadAllText(mtlPath));
            Assert.True(File.Exists(mtlPath));
            Assert.Contains(customColor.ToMtlKd(), File.ReadAllText(mtlPath));
        }
        finally
        {
            if (File.Exists(objPath)) File.Delete(objPath);
            if (File.Exists(mtlPath)) File.Delete(mtlPath);
        }
    }

    [Fact]
    public void ExportCombined_Ff0000And8FBC8F_WritesExactKd()
    {
        using var grid = new CubeGrid(2);
        var line = grid.GenerateLineFromGrid()!;
        line[0].IsEmpty = true;

        Assert.True(ObjColor.TryParseHex("#FF0000", out ObjColor materialColor));
        Assert.True(ObjColor.TryParseHex("#8FBC8F", out ObjColor poreColor));

        string objPath = Path.Combine(Path.GetTempPath(), $"combined_{Guid.NewGuid():N}.obj");
        string mtlPath = Path.ChangeExtension(objPath, ".mtl");
        try
        {
            ObjExporter.ExportCombined(line, objPath, materialColor, poreColor);
            string mtlContent = File.ReadAllText(mtlPath);

            Assert.Contains("newmtl solid_FF0000", mtlContent);
            Assert.Contains("newmtl pore_8FBC8F", mtlContent);
            Assert.Contains("Kd 1 0 0", mtlContent);
            Assert.Contains("Kd 0.561 0.737 0.561", mtlContent);
        }
        finally
        {
            if (File.Exists(objPath)) File.Delete(objPath);
            if (File.Exists(mtlPath)) File.Delete(mtlPath);
        }
    }

    [Fact]
    public void ExportCombined_CustomColors_WritesExpectedKd()
    {
        using var grid = new CubeGrid(2);
        var line = grid.GenerateLineFromGrid()!;
        line[0].IsEmpty = true;

        string objPath = Path.Combine(Path.GetTempPath(), $"combined_{Guid.NewGuid():N}.obj");
        string mtlPath = Path.ChangeExtension(objPath, ".mtl");
        var materialColor = new ObjColor(0x10, 0x20, 0x30);
        var poreColor = new ObjColor(0x40, 0x50, 0x60);

        try
        {
            ObjExporter.ExportCombined(line, objPath, materialColor, poreColor);
            string mtlContent = File.ReadAllText(mtlPath);

            Assert.Contains(materialColor.ToMtlKd(), mtlContent);
            Assert.Contains(poreColor.ToMtlKd(), mtlContent);
        }
        finally
        {
            if (File.Exists(objPath)) File.Delete(objPath);
            if (File.Exists(mtlPath)) File.Delete(mtlPath);
        }
    }

    [Fact]
    public void ExportCombined_WritesBlenderCompatibleFormat_NoBomLfOnly()
    {
        using var grid = new CubeGrid(2);
        var line = grid.GenerateLineFromGrid()!;
        line[0].IsEmpty = true;

        string objPath = Path.Combine(Path.GetTempPath(), $"combined_{Guid.NewGuid():N}.obj");
        string mtlPath = Path.ChangeExtension(objPath, ".mtl");
        try
        {
            ObjExporter.ExportCombined(line, objPath, new ObjColor(0xFF, 0, 0), new ObjColor(0x8F, 0xBC, 0x8F));

            byte[] objBytes = File.ReadAllBytes(objPath);
            byte[] mtlBytes = File.ReadAllBytes(mtlPath);

            Assert.False(objBytes.Length >= 3 && objBytes[0] == 0xEF && objBytes[1] == 0xBB && objBytes[2] == 0xBF);
            Assert.False(mtlBytes.Length >= 3 && mtlBytes[0] == 0xEF && mtlBytes[1] == 0xBB && mtlBytes[2] == 0xBF);
            Assert.DoesNotContain((byte)'\r', objBytes);
            Assert.DoesNotContain((byte)'\r', mtlBytes);
            Assert.Equal("newmtl solid_FF0000\n"u8.ToArray(), mtlBytes.Take(20).ToArray());
        }
        finally
        {
            if (File.Exists(objPath)) File.Delete(objPath);
            if (File.Exists(mtlPath)) File.Delete(mtlPath);
        }
    }

    [Fact]
    public void Export_WithSettings_UsesOptionalColorFlags()
    {
        using var grid = new CubeGrid(2);
        var line = grid.GenerateLineFromGrid()!;

        string objPath = Path.Combine(Path.GetTempPath(), $"material_{Guid.NewGuid():N}.obj");
        string mtlPath = Path.ChangeExtension(objPath, ".mtl");

        try
        {
            ObjExporter.Export(line, objPath, new ObjExportSettings(
                ObjExportMode.MaterialOnly,
                ObjColor.DefaultMaterial,
                ObjColor.DefaultPore,
                IncludeMaterialColor: false,
                IncludePoreColor: false));

            Assert.DoesNotContain("mtllib", File.ReadAllText(objPath));
            Assert.False(File.Exists(mtlPath));
        }
        finally
        {
            if (File.Exists(objPath)) File.Delete(objPath);
            if (File.Exists(mtlPath)) File.Delete(mtlPath);
        }
    }
}
