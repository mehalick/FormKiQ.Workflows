namespace FormKiQ.Uploader;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        var f = new FileInfo(@"D:\Scans\20251116094546-0001A.tif");

        var c = f.CreationTimeUtc;

        Console.WriteLine(c.ToString("O"));
    }
}
