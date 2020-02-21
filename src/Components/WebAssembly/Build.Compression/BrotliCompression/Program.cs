using System;
using System.IO;
using System.IO.Compression;

namespace BrotliCompression
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Error: Wrong number of arguments.");
                Console.WriteLine("brotlicompression.exe <<source>> <<target>>");
            }

            var source = args[0];
            var target = args[1];

            using var sourceStream = File.OpenRead(source);
            using var fileStream = new FileStream(target, FileMode.Create);
            using var stream = new BrotliStream(fileStream, CompressionLevel.Optimal);

            sourceStream.CopyTo(stream);

            return 0;
        }
    }
}
