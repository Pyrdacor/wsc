using WSC.Compression;
using System.Diagnostics;

namespace WSC;

public static class Program
{
    public static void Main(string[] args)
    {
        // TODO: usage, options, etc
        var input = args[0];
        var output = args[1];

#if true
        var data = File.ReadAllBytes(input);

        var timer = Stopwatch.StartNew();
        var compressedData = Compressor.Compress(data.Length % 2 == 0 ? data : [ ..data, 0 ]);
        timer.Stop();
        Console.WriteLine($"Compression time: {timer.Elapsed}, ratio: {100.0 - compressedData.Length * 100.0 / data.Length:0.00}%");

        if (compressedData.Length < data.Length)
        {
            Console.WriteLine($"Compressed from {data.Length} to {compressedData.Length}");

            File.WriteAllBytes(output, compressedData);
        }
#else
        var data = File.ReadAllBytes(input);

        var uncompressed = Decompressor.Decompress(data);

        File.WriteAllBytes(output, uncompressed);
#endif
    }
}