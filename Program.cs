using System.Text;
using System.Net.Sockets;
using System.Net;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
╔═════════════════════════════════════════════════════════════╗
║                                                             ║
║                       LOADTEST TOOL                         ║
║                                                             ║
╚═════════════════════════════════════════════════════════════╝");
        Console.ResetColor();

        var config = new TestConfig
        {
            Protocol = TestProtocol.Http,
            Port = 80,
            NumberOfRequests = 100,
            NumberOfConcurrentRequests = 10,
            TimeoutSeconds = 10,
            Url = string.Empty,
            Method = "GET",
            Headers = new Dictionary<string, string>(),
            Body = string.Empty,
            DelayBetweenRequestsMs = 0,
            VerboseOutput = false
        };

        try
        {
            if (args.Length < 1)
            {
                CommandLineHelper.ShowHelp();
                return;
            }

            CommandLineHelper.ProcessArguments(args, config);

            if (string.IsNullOrEmpty(config.Url))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("URL is required. Use -u or --url to specify the target URL.");
                Console.ResetColor();
                return;
            }

            // Validate protocol-specific requirements
            if (config.Protocol != TestProtocol.Http)
            {
                if (config.Port <= 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Port is required for {config.Protocol} protocol. Use -p or --port to specify the port.");
                    Console.ResetColor();
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Using {config.Protocol} protocol on port {config.Port}");
                Console.ResetColor();
            }

            await SelfUpdate.CheckForUpdates();

            // Create appropriate client based on protocol
            object client;
            switch (config.Protocol)
            {
                case TestProtocol.Http:
                    client = HttpClientFactory.CreateHttpClient(config);
                    break;
                case TestProtocol.Tcp:
                    client = new TcpClient();
                    break;
                case TestProtocol.Udp:
                    client = new UdpClient();
                    break;
                default:
                    throw new ArgumentException($"Unsupported protocol: {config.Protocol}");
            }

            ResultsReporter.PrintTestConfiguration(config);

            // Run load test with appropriate client
            if (client is HttpClient httpClient)
            {
                await LoadTestRunner.RunLoadTest(httpClient, config);
            }
            else if (client is TcpClient tcpClient)
            {
                await LoadTestRunner.RunLoadTest(tcpClient, config);
            }
            else if (client is UdpClient udpClient)
            {
                await LoadTestRunner.RunLoadTest(udpClient, config);
            }

            // Cleanup
            if (client is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }
}