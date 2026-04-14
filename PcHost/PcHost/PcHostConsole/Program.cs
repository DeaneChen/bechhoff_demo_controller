using System;
using System.Globalization;
using System.IO;
using System.Threading;
using PcHost.Core;

namespace PcHostConsole
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                PrintHelp();
                return 2;
            }

            string amsNetId = null;
            int port = PlcSymbols.DefaultPlcPort;

            int index = 0;
            while (index < args.Length && args[index].StartsWith("--", StringComparison.Ordinal))
            {
                string key = args[index];
                if (string.Equals(key, "--ams", StringComparison.OrdinalIgnoreCase))
                {
                    amsNetId = GetRequiredValue(args, ref index, "--ams");
                }
                else if (string.Equals(key, "--port", StringComparison.OrdinalIgnoreCase))
                {
                    string v = GetRequiredValue(args, ref index, "--port");
                    port = int.Parse(v, CultureInfo.InvariantCulture);
                }
                else if (string.Equals(key, "--help", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(key, "-h", StringComparison.OrdinalIgnoreCase))
                {
                    PrintHelp();
                    return 0;
                }
                else
                {
                    Console.Error.WriteLine("Unknown option: " + key);
                    return 2;
                }

                index++;
            }

            if (index >= args.Length)
            {
                PrintHelp();
                return 2;
            }

            string command = args[index];
            if (string.IsNullOrWhiteSpace(amsNetId))
            {
                Console.Error.WriteLine("Missing required option: --ams <AmsNetId> (e.g. 169.254.231.128.1.1)");
                return 2;
            }

            var settings = new PlcConnectionSettings(amsNetId, port);

            try
            {
                if (string.Equals(command, "connect", StringComparison.OrdinalIgnoreCase))
                {
                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        Console.WriteLine("Connected: " + plc.IsConnected);
                    }

                    return 0;
                }

                if (string.Equals(command, "read-u32", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: read-u32 <SYMBOL>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        uint value = plc.ReadSymbol<uint>(symbol);
                        Console.WriteLine(value.ToString(CultureInfo.InvariantCulture));
                    }

                    return 0;
                }

                if (string.Equals(command, "read-i16", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: read-i16 <SYMBOL>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        short value = plc.ReadSymbol<short>(symbol);
                        Console.WriteLine(value.ToString(CultureInfo.InvariantCulture));
                    }

                    return 0;
                }

                if (string.Equals(command, "write-u32", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 2 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: write-u32 <SYMBOL> <VALUE>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    uint value = uint.Parse(args[index + 2], CultureInfo.InvariantCulture);

                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        plc.WriteSymbol(symbol, value);
                    }

                    return 0;
                }

                if (string.Equals(command, "watch-i16", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: watch-i16 <SYMBOL> [--ms 10] [--out file.csv]");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    int periodMs = 10;
                    string outCsv = null;

                    int i = index + 2;
                    while (i < args.Length)
                    {
                        string k = args[i];
                        if (string.Equals(k, "--ms", StringComparison.OrdinalIgnoreCase))
                        {
                            periodMs = int.Parse(RequireArg(args, ref i, "--ms"), CultureInfo.InvariantCulture);
                        }
                        else if (string.Equals(k, "--out", StringComparison.OrdinalIgnoreCase))
                        {
                            outCsv = RequireArg(args, ref i, "--out");
                        }
                        else
                        {
                            Console.Error.WriteLine("Unknown watch-i16 option: " + k);
                            return 2;
                        }

                        i++;
                    }

                    Console.WriteLine("Connecting to " + amsNetId + ":" + port);
                    Console.WriteLine("Watching: " + symbol);
                    Console.WriteLine("Period (ms): " + periodMs.ToString(CultureInfo.InvariantCulture));
                    if (!string.IsNullOrWhiteSpace(outCsv))
                    {
                        Console.WriteLine("CSV: " + Path.GetFullPath(outCsv));
                    }
                    Console.WriteLine("Press Ctrl+C to stop.");

                    var cts = new CancellationTokenSource();
                    Console.CancelKeyPress += (s, e) =>
                    {
                        e.Cancel = true;
                        cts.Cancel();
                    };

                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);

                        StreamWriter writer = null;
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(outCsv))
                            {
                                writer = new StreamWriter(new FileStream(outCsv, FileMode.Append, FileAccess.Write, FileShare.Read));
                                writer.WriteLine("pc_utc_iso,value");
                                writer.Flush();
                            }

                            while (!cts.IsCancellationRequested)
                            {
                                short value = plc.ReadSymbol<short>(symbol);
                                string ts = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

                                if (writer != null)
                                {
                                    writer.WriteLine(ts + "," + value.ToString(CultureInfo.InvariantCulture));
                                    writer.Flush();
                                }
                                else
                                {
                                    Console.WriteLine(ts + " " + value.ToString(CultureInfo.InvariantCulture));
                                }

                                Thread.Sleep(periodMs);
                            }
                        }
                        finally
                        {
                            if (writer != null)
                            {
                                writer.Dispose();
                            }
                        }
                    }

                    return 0;
                }

                if (string.Equals(command, "ring-dump", StringComparison.OrdinalIgnoreCase))
                {
                    string headSymbol = null;
                    string bufferSymbol = null;
                    int bufferSize = 0;
                    string outPath = null;
                    int pollMs = 10;

                    int i = index + 1;
                    while (i < args.Length)
                    {
                        string k = args[i];
                        if (string.Equals(k, "--head", StringComparison.OrdinalIgnoreCase))
                        {
                            headSymbol = RequireArg(args, ref i, "--head");
                        }
                        else if (string.Equals(k, "--buffer", StringComparison.OrdinalIgnoreCase))
                        {
                            bufferSymbol = RequireArg(args, ref i, "--buffer");
                        }
                        else if (string.Equals(k, "--size", StringComparison.OrdinalIgnoreCase))
                        {
                            bufferSize = int.Parse(RequireArg(args, ref i, "--size"), CultureInfo.InvariantCulture);
                        }
                        else if (string.Equals(k, "--out", StringComparison.OrdinalIgnoreCase))
                        {
                            outPath = RequireArg(args, ref i, "--out");
                        }
                        else if (string.Equals(k, "--poll-ms", StringComparison.OrdinalIgnoreCase))
                        {
                            pollMs = int.Parse(RequireArg(args, ref i, "--poll-ms"), CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            Console.Error.WriteLine("Unknown ring-dump option: " + k);
                            return 2;
                        }

                        i++;
                    }

                    if (string.IsNullOrWhiteSpace(headSymbol) ||
                        string.IsNullOrWhiteSpace(bufferSymbol) ||
                        bufferSize <= 0 ||
                        string.IsNullOrWhiteSpace(outPath))
                    {
                        Console.Error.WriteLine("Usage: ring-dump --head <SYMBOL> --buffer <SYMBOL> --size <BYTES> --out <FILE> [--poll-ms 10]");
                        return 2;
                    }

                    Console.WriteLine("Connecting to " + amsNetId + ":" + port);
                    Console.WriteLine("Dumping ring buffer to: " + Path.GetFullPath(outPath));
                    Console.WriteLine("Press Ctrl+C to stop.");

                    var cts = new CancellationTokenSource();
                    Console.CancelKeyPress += (s, e) =>
                    {
                        e.Cancel = true;
                        cts.Cancel();
                    };

                    using (var plc = new AdsPlcClient())
                    using (var fs = new FileStream(outPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    {
                        plc.Connect(settings);
                        var cursor = new RingBufferCursor(0);

                        while (!cts.IsCancellationRequested)
                        {
                            byte[] data = RingBufferReader.ReadNewBytes(plc, headSymbol, bufferSymbol, bufferSize, cursor);
                            if (data.Length > 0)
                            {
                                fs.Write(data, 0, data.Length);
                                fs.Flush();
                            }

                            Thread.Sleep(pollMs);
                        }
                    }

                    return 0;
                }

                Console.Error.WriteLine("Unknown command: " + command);
                PrintHelp();
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static string GetRequiredValue(string[] args, ref int index, string key)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException("Missing value for " + key);
            }

            index++;
            return args[index];
        }

        private static string RequireArg(string[] args, ref int index, string key)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException("Missing value for " + key);
            }

            index++;
            return args[index];
        }

        private static void PrintHelp()
        {
            Console.WriteLine("PcHostConsole (ADS client)");
            Console.WriteLine();
            Console.WriteLine("Common options:");
            Console.WriteLine("  --ams <AmsNetId>        e.g. 169.254.231.128.1.1");
            Console.WriteLine("  --port <AdsPort>        default 851 (PLC1)");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  connect");
            Console.WriteLine("  read-u32 <SYMBOL>");
            Console.WriteLine("  read-i16 <SYMBOL>");
            Console.WriteLine("  write-u32 <SYMBOL> <VALUE>");
            Console.WriteLine("  watch-i16 <SYMBOL> [--ms 10] [--out file.csv]");
            Console.WriteLine("  ring-dump --head <SYMBOL> --buffer <SYMBOL> --size <BYTES> --out <FILE> [--poll-ms 10]");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  For 500~2000Hz logging, prefer PLC ring-buffer + PC batch/poll read (ring-dump) over per-sample reads.");
        }
    }
}
