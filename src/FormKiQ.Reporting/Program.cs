using System.Globalization;
using CsvHelper;

namespace FormKiQ.Reporting;

static class Program
{
    static void Main()
    {
        var d = Environment.GetEnvironmentVariable("DocumentScanDirectory");

        if (string.IsNullOrWhiteSpace(d))
        {
            Console.Error.WriteLine("Directory environment variable is not set");
            return;
        }

        var directoryInfo = new DirectoryInfo(d);

        if (!directoryInfo.Exists)
        {
            Console.Error.WriteLine($"Directory '{d}' does not exist");
            return;
        }

        var scans = new List<ScanDirectory>();

        foreach (var directory in directoryInfo.GetDirectories())
        {
            var files = directory.GetFiles("*.png");

            scans.Add(new(directory.Name, files.Length));
        }

        using var writer = new StreamWriter("report.csv");
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(scans);
    }
}

record ScanDirectory(string Date, int Count)
{
    public DateOnly DateOnly => DateOnly.FromDateTime(DateTime.ParseExact(Date, "yyyyMMdd", CultureInfo.InvariantCulture));
}
