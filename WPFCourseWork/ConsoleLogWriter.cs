using System.IO;
using System.Text;

namespace WPFCourseWork;

public sealed class ConsoleLogWriter : TextWriter
{
    private readonly Action<string> _write;

    public ConsoleLogWriter(Action<string> write)
    {
        _write = write;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            _write(value);
    }

    public override void WriteLine(string? value)
    {
        Write(value);
        Write(Environment.NewLine);
    }
}
