static class CommandLineHelper
{
    public static void ShowHelp()
    {
        Console.WriteLine("Usage: LoadTest [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -u, --url <url>           Target URL (required)");
        Console.WriteLine("  -n, --number <count>      Number of total requests (default: 100)");
        Console.WriteLine("  -conc, --concurrency <count> Number of concurrent requests (default: 10)");
        Console.WriteLine("  -t, --timeout <seconds>   Timeout in seconds (default: 10)");
        Console.WriteLine("  -m, --method <method>     HTTP method (default: GET)");
        Console.WriteLine("  -H, --header <header>     Add header (format: 'key:value')");
        Console.WriteLine("  -d, --data <data>         Request body data");
        Console.WriteLine("  --delay <milliseconds>    Delay between requests in milliseconds (default: 0)");
        Console.WriteLine("  -pt, --port <port>        Port number (required for TCP/UDP)");
        Console.WriteLine("  --protocol <protocol>     Protocol to use (http/tcp/udp, default: http)");
        Console.WriteLine("  -v, --verbose             Verbose output");
        Console.WriteLine("  -h, --help                Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  LoadTest -u https://example.com -n 1000 -conc 20");
        Console.WriteLine("  LoadTest -u 192.168.1.1 -pt 8080 --protocol tcp -n 500 -conc 50");
        Console.WriteLine("  LoadTest -u example.com -pt 8080 --protocol udp -n 1000");
        Console.WriteLine("  LoadTest -u example.com -n 500 -c 50 -m POST -H \"Content-Type:application/json\" -d \"{\\\"key\\\":\\\"value\\\"}\"");
    }

    public static void ProcessArguments(string[] args, TestConfig config)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-u":
                case "--url":
                    if (i + 1 < args.Length)
                    {
                        config.Url = FormatUrl(args[++i]);
                    }
                    break;
                case "-n":
                case "--number":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int num))
                    {
                        config.NumberOfRequests = num;
                    }
                    break;
                case "-conc":
                case "--concurrency":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int conc))
                    {
                        config.NumberOfConcurrentRequests = conc;
                    }
                    break;
                case "-t":
                case "--timeout":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int timeout))
                    {
                        config.TimeoutSeconds = timeout;
                    }
                    break;
                case "-m":
                case "--method":
                    if (i + 1 < args.Length)
                    {
                        config.Method = args[++i].ToUpper();
                    }
                    break;
                case "-h":
                case "--header":
                    if (i + 1 < args.Length)
                    {
                        string header = args[++i];
                        int separatorIndex = header.IndexOf(':');
                        if (separatorIndex > 0)
                        {
                            string key = header.Substring(0, separatorIndex).Trim();
                            string value = header.Substring(separatorIndex + 1).Trim();
                            config.Headers[key] = value;
                        }
                    }
                    break;
                case "-d":
                case "--data":
                    if (i + 1 < args.Length)
                    {
                        config.Body = args[++i];
                    }
                    break;
                case "--delay":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int delay))
                    {
                        config.DelayBetweenRequestsMs = delay;
                    }
                    break;
                case "-pt":
                case "--port":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int port))
                    {
                        config.Port = port;
                    }
                    break;
                case "--protocol":
                    if (i + 1 < args.Length)
                    {
                        config.Protocol = ParseProtocol(args[++i]);
                    }
                    break;
                case "-v":
                case "--verbose":
                    config.VerboseOutput = true;
                    break;
                case "--help":
                    ShowHelp();
                    Environment.Exit(0);
                    break;
            }
        }
    }

    static string FormatUrl(string input)
    {
        if (input.StartsWith("http://") || input.StartsWith("https://"))
        {
            return input;
        }

        return "http://" + input;
    }

    static TestProtocol ParseProtocol(string protocol)
    {
        return protocol.ToLower() switch
        {
            "http" => TestProtocol.Http,
            "tcp" => TestProtocol.Tcp,
            "udp" => TestProtocol.Udp,
            _ => throw new ArgumentException($"Unsupported protocol: {protocol}. Supported protocols are: http, tcp, udp")
        };
    }
}