using System.Text;

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

            await SelfUpdate.CheckForUpdates();

            using var client = HttpClientFactory.CreateHttpClient(config);

            ResultsReporter.PrintTestConfiguration(config);

            await LoadTestRunner.RunLoadTest(client, config);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }
}