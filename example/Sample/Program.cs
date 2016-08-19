using System;
using System.IO;
using Serilog;

namespace Sample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("log.txt")
                .CreateLogger();

            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (var i = 0; i < 1000000; ++i)
            {
                Log.Information("Hello, file logger!");
            }

            Log.CloseAndFlush();

            sw.Stop();

            Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds} ms");

            File.Delete("log.txt");
        }
    }
}
