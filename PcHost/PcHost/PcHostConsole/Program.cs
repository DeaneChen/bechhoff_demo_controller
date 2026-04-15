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

                if (string.Equals(command, "read-i32", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: read-i32 <SYMBOL>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        int value = plc.ReadSymbol<int>(symbol);
                        Console.WriteLine(value.ToString(CultureInfo.InvariantCulture));
                    }

                    return 0;
                }

                if (string.Equals(command, "read-u8", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: read-u8 <SYMBOL>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        byte value = plc.ReadSymbol<byte>(symbol);
                        Console.WriteLine(value.ToString(CultureInfo.InvariantCulture));
                    }

                    return 0;
                }

                if (string.Equals(command, "read-bool", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: read-bool <SYMBOL>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        bool value = plc.ReadSymbol<bool>(symbol);
                        Console.WriteLine(value ? "true" : "false");
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

                if (string.Equals(command, "read-u16", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: read-u16 <SYMBOL>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        ushort value = plc.ReadSymbol<ushort>(symbol);
                        Console.WriteLine(value.ToString(CultureInfo.InvariantCulture));
                    }

                    return 0;
                }

                if (string.Equals(command, "read-bytes", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 2 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: read-bytes <SYMBOL> <LEN>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    int len = int.Parse(args[index + 2], CultureInfo.InvariantCulture);
                    if (len < 0) len = 0;
                    if (len > 4096) len = 4096;

                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        byte[] data = plc.ReadBytes(symbol, len);
                        Console.WriteLine(ToHex(data, data.Length));
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

                if (string.Equals(command, "write-i32", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 2 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: write-i32 <SYMBOL> <VALUE>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    int value = int.Parse(args[index + 2], CultureInfo.InvariantCulture);

                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        plc.WriteSymbol(symbol, value);
                    }

                    return 0;
                }

                if (string.Equals(command, "write-bool", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 2 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: write-bool <SYMBOL> <true|false|1|0>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    string raw = args[index + 2];
                    bool value;
                    if (string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase))
                    {
                        value = true;
                    }
                    else if (string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase))
                    {
                        value = false;
                    }
                    else
                    {
                        value = bool.Parse(raw);
                    }

                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        plc.WriteSymbol(symbol, value);
                    }

                    return 0;
                }

                if (string.Equals(command, "write-u8", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 2 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: write-u8 <SYMBOL> <VALUE>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    byte value = byte.Parse(args[index + 2], CultureInfo.InvariantCulture);

                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        plc.WriteSymbol(symbol, value);
                    }

                    return 0;
                }

                if (string.Equals(command, "write-i8", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 2 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: write-i8 <SYMBOL> <VALUE>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    sbyte value = sbyte.Parse(args[index + 2], CultureInfo.InvariantCulture);

                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        plc.WriteSymbol(symbol, value);
                    }

                    return 0;
                }

                if (string.Equals(command, "write-u16", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 2 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: write-u16 <SYMBOL> <VALUE>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    ushort value = ushort.Parse(args[index + 2], CultureInfo.InvariantCulture);

                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        plc.WriteSymbol(symbol, value);
                    }

                    return 0;
                }

                if (string.Equals(command, "write-i16", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 2 >= args.Length)
                    {
                        Console.Error.WriteLine("Usage: write-i16 <SYMBOL> <VALUE>");
                        return 2;
                    }

                    string symbol = args[index + 1];
                    short value = short.Parse(args[index + 2], CultureInfo.InvariantCulture);

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

                if (string.Equals(command, "el6022-loopback", StringComparison.OrdinalIgnoreCase))
                {
                    int txCh = 1;
                    int timeoutMs = 2000;
                    string hex = null;
                    int len = -1;

                    int i = index + 1;
                    while (i < args.Length)
                    {
                        string k = args[i];
                        if (string.Equals(k, "--tx-ch", StringComparison.OrdinalIgnoreCase))
                        {
                            txCh = int.Parse(RequireArg(args, ref i, "--tx-ch"), CultureInfo.InvariantCulture);
                        }
                        else if (string.Equals(k, "--hex", StringComparison.OrdinalIgnoreCase))
                        {
                            hex = RequireArg(args, ref i, "--hex");
                        }
                        else if (string.Equals(k, "--len", StringComparison.OrdinalIgnoreCase))
                        {
                            len = int.Parse(RequireArg(args, ref i, "--len"), CultureInfo.InvariantCulture);
                        }
                        else if (string.Equals(k, "--timeout-ms", StringComparison.OrdinalIgnoreCase))
                        {
                            timeoutMs = int.Parse(RequireArg(args, ref i, "--timeout-ms"), CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            Console.Error.WriteLine("Unknown el6022-loopback option: " + k);
                            return 2;
                        }

                        i++;
                    }

                    if (txCh != 1 && txCh != 2)
                    {
                        Console.Error.WriteLine("--tx-ch must be 1 or 2");
                        return 2;
                    }

                    if (string.IsNullOrWhiteSpace(hex))
                    {
                        Console.Error.WriteLine("Usage: el6022-loopback --tx-ch 1|2 --hex \"01 02 03\" [--len N] [--timeout-ms 2000]");
                        return 2;
                    }

                    byte[] payload = ParseHexBytes(hex);
                    if (payload.Length > 22)
                    {
                        Console.Error.WriteLine("Payload too long: " + payload.Length + " (max 22 bytes)");
                        return 2;
                    }

                    int txLen = len >= 0 ? len : payload.Length;
                    if (txLen < 0 || txLen > 22)
                    {
                        Console.Error.WriteLine("--len must be 0..22");
                        return 2;
                    }

                    var txData = new byte[22];
                    Buffer.BlockCopy(payload, 0, txData, 0, payload.Length);

                    Console.WriteLine("Connecting to " + amsNetId + ":" + port);
                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);
                        plc.WriteSymbol("GVL_Rs485Demo.Enable", true);

                        // Prepare TX
                        plc.WriteSymbol("GVL_Rs485Demo.TxUseCh2", txCh == 2);
                        plc.WriteSymbol("GVL_Rs485Demo.TxLen", (byte)txLen);
                        plc.WriteBytes("GVL_Rs485Demo.TxData", txData);

                        uint prevRxSeq = plc.ReadSymbol<uint>("GVL_Rs485Demo.RxSeq");
                        uint txSeq = plc.ReadSymbol<uint>("GVL_Rs485Demo.TxSeq") + 1;
                        plc.WriteSymbol("GVL_Rs485Demo.TxSeq", txSeq);

                        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

                        // Wait for TX done
                        while (DateTime.UtcNow < deadline)
                        {
                            uint done = plc.ReadSymbol<uint>("GVL_Rs485Demo.TxDoneSeq");
                            if (done == txSeq)
                            {
                                break;
                            }

                            Thread.Sleep(5);
                        }

                        uint doneSeq = plc.ReadSymbol<uint>("GVL_Rs485Demo.TxDoneSeq");
                        if (doneSeq != txSeq)
                        {
                            Console.Error.WriteLine("TX timeout. TxDoneSeq=" + doneSeq.ToString(CultureInfo.InvariantCulture));
                            return 1;
                        }

                        // Wait for RX seq change
                        while (DateTime.UtcNow < deadline)
                        {
                            uint rxSeq = plc.ReadSymbol<uint>("GVL_Rs485Demo.RxSeq");
                            if (rxSeq != prevRxSeq)
                            {
                                bool fromCh2 = plc.ReadSymbol<bool>("GVL_Rs485Demo.RxFromCh2");
                                ushort status = plc.ReadSymbol<ushort>("GVL_Rs485Demo.RxStatus");
                                byte rxLen = plc.ReadSymbol<byte>("GVL_Rs485Demo.RxLen");
                                byte[] rxData = plc.ReadBytes("GVL_Rs485Demo.RxData", 22);

                                Console.WriteLine("RX from: " + (fromCh2 ? "Ch2" : "Ch1"));
                                Console.WriteLine("RX status: 0x" + status.ToString("X4", CultureInfo.InvariantCulture));
                                Console.WriteLine("RX len: " + rxLen.ToString(CultureInfo.InvariantCulture));
                                Console.WriteLine("RX data: " + ToHex(rxData, rxLen));
                                return 0;
                            }

                            Thread.Sleep(5);
                        }

                        Console.Error.WriteLine("RX timeout. No new RxSeq.");
                        return 1;
                    }
                }

                if (string.Equals(command, "el6022-dump", StringComparison.OrdinalIgnoreCase))
                {
                    using (var plc = new AdsPlcClient())
                    {
                        plc.Connect(settings);

                        ushort ch1Status = plc.ReadSymbol<ushort>("GVL_Rs485Demo.Debug_Ch1_Status");
                        ushort ch2Status = plc.ReadSymbol<ushort>("GVL_Rs485Demo.Debug_Ch2_Status");
                        ushort ch1Ctrl = plc.ReadSymbol<ushort>("GVL_Rs485Demo.Debug_Ch1_Ctrl");
                        ushort ch2Ctrl = plc.ReadSymbol<ushort>("GVL_Rs485Demo.Debug_Ch2_Ctrl");

                        uint rxSeq = plc.ReadSymbol<uint>("GVL_Rs485Demo.RxSeq");
                        bool rxFromCh2 = plc.ReadSymbol<bool>("GVL_Rs485Demo.RxFromCh2");
                        byte rxLen = plc.ReadSymbol<byte>("GVL_Rs485Demo.RxLen");
                        ushort rxStatus = plc.ReadSymbol<ushort>("GVL_Rs485Demo.RxStatus");
                        byte[] rxData = plc.ReadBytes("GVL_Rs485Demo.RxData", 22);

                        Console.WriteLine("Ch1 Status: 0x" + ch1Status.ToString("X4", CultureInfo.InvariantCulture));
                        Console.WriteLine("Ch1 Ctrl:   0x" + ch1Ctrl.ToString("X4", CultureInfo.InvariantCulture));
                        Console.WriteLine("Ch2 Status: 0x" + ch2Status.ToString("X4", CultureInfo.InvariantCulture));
                        Console.WriteLine("Ch2 Ctrl:   0x" + ch2Ctrl.ToString("X4", CultureInfo.InvariantCulture));
                        Console.WriteLine("RxSeq:      " + rxSeq.ToString(CultureInfo.InvariantCulture));
                        Console.WriteLine("RxFrom:     " + (rxFromCh2 ? "Ch2" : "Ch1"));
                        Console.WriteLine("RxStatus:   0x" + rxStatus.ToString("X4", CultureInfo.InvariantCulture));
                        Console.WriteLine("RxLen:      " + rxLen.ToString(CultureInfo.InvariantCulture));
                        Console.WriteLine("RxData:     " + ToHex(rxData, rxLen));

                        return 0;
                    }
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
            Console.WriteLine("  read-i32 <SYMBOL>");
            Console.WriteLine("  read-u8 <SYMBOL>");
            Console.WriteLine("  read-bool <SYMBOL>");
            Console.WriteLine("  read-i16 <SYMBOL>");
            Console.WriteLine("  read-u16 <SYMBOL>");
            Console.WriteLine("  read-bytes <SYMBOL> <LEN>");
            Console.WriteLine("  write-u32 <SYMBOL> <VALUE>");
            Console.WriteLine("  write-i32 <SYMBOL> <VALUE>");
            Console.WriteLine("  write-bool <SYMBOL> <true|false|1|0>");
            Console.WriteLine("  write-u8 <SYMBOL> <VALUE>");
            Console.WriteLine("  write-i8 <SYMBOL> <VALUE>");
            Console.WriteLine("  write-u16 <SYMBOL> <VALUE>");
            Console.WriteLine("  write-i16 <SYMBOL> <VALUE>");
            Console.WriteLine("  watch-i16 <SYMBOL> [--ms 10] [--out file.csv]");
            Console.WriteLine("  ring-dump --head <SYMBOL> --buffer <SYMBOL> --size <BYTES> --out <FILE> [--poll-ms 10]");
            Console.WriteLine("  el6022-loopback --tx-ch 1|2 --hex \"01 02 03\" [--len N] [--timeout-ms 2000]");
            Console.WriteLine("  el6022-dump");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  For 500~2000Hz logging, prefer PLC ring-buffer + PC batch/poll read (ring-dump) over per-sample reads.");
        }

        private static byte[] ParseHexBytes(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            string cleaned = text.Replace(",", " ").Trim();
            cleaned = cleaned.Replace("0x", "").Replace("0X", "");

            var parts = cleaned.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1 && parts[0].Length > 2)
            {
                // Allow "010203" style
                string s = parts[0];
                if (s.Length % 2 != 0) throw new FormatException("Hex string length must be even.");
                var bytes = new byte[s.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = byte.Parse(s.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                }

                return bytes;
            }

            var output = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                output[i] = byte.Parse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return output;
        }

        private static string ToHex(byte[] data, int len)
        {
            if (data == null) return string.Empty;
            if (len < 0) len = 0;
            if (len > data.Length) len = data.Length;

            var sb = new System.Text.StringBuilder(len * 3);
            for (int i = 0; i < len; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }
    }
}
