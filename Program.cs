using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;

using Core.Models;

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
                ShowHelp();
                return;
            }

            ProcessArguments(args, config);

            if (string.IsNullOrEmpty(config.Url))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("URL is required. Use -u or --url to specify the target URL.");
                Console.ResetColor();
                return;
            }

            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
            };

            client.DefaultRequestHeaders.Add("User-Agent", "PowerLoadTest/1.0");
            foreach (var header in config.Headers)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            PrintTestConfiguration(config);

            await RunLoadTest(client, config);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    static void ShowHelp()
    {
        Console.WriteLine("Usage: LoadTest [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -u, --url <url>           Target URL (required)");
        Console.WriteLine("  -n, --number <count>      Number of total requests (default: 100)");
        Console.WriteLine("  -c, --concurrency <count> Number of concurrent requests (default: 10)");
        Console.WriteLine("  -t, --timeout <seconds>   Timeout in seconds (default: 10)");
        Console.WriteLine("  -m, --method <method>     HTTP method (default: GET)");
        Console.WriteLine("  -H, --header <header>     Add header (format: 'key:value')");
        Console.WriteLine("  -d, --data <data>         Request body data");
        Console.WriteLine("  --delay <milliseconds>    Delay between requests in milliseconds (default: 0)");
        Console.WriteLine("  -v, --verbose             Verbose output");
        Console.WriteLine("  -h, --help                Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  LoadTest -u https://example.com -n 1000 -c 20");
        Console.WriteLine("  LoadTest -u 192.168.1.1:8080 -n 500 -c 50 -m POST -H \"Content-Type:application/json\" -d \"{\\\"key\\\":\\\"value\\\"}\"");
    }

    static void ProcessArguments(string[] args, TestConfig config)
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
                case "-c":
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

    static void PrintTestConfiguration(TestConfig config)
    {
        Console.WriteLine("╭" + new string('─', 60) + "╮");
        Console.WriteLine($"│ {"Test Configuration:",-58} │");
        Console.WriteLine("├" + new string('─', 60) + "┤");
        Console.WriteLine($"│ {"Target URL:",-15} {config.Url,-42} │");
        Console.WriteLine($"│ {"Method:",-15} {config.Method,-42} │");
        Console.WriteLine($"│ {"Total Requests:",-15} {config.NumberOfRequests,-42} │");
        Console.WriteLine($"│ {"Concurrency:",-15} {config.NumberOfConcurrentRequests,-42} │");
        Console.WriteLine($"│ {"Timeout:",-15} {config.TimeoutSeconds} sec{new string(' ', 36)} │");

        if (config.DelayBetweenRequestsMs > 0)
        {
            Console.WriteLine($"│ {"Delay:",-15} {config.DelayBetweenRequestsMs} ms{new string(' ', 37)} │");
        }

        Console.WriteLine("╰" + new string('─', 60) + "╯");
        Console.WriteLine();
    }

    static async Task RunLoadTest(HttpClient client, TestConfig config)
    {
        Console.WriteLine("⚡ Starting load test...");

        var results = new ConcurrentBag<RequestResult>();
        var stopwatch = new Stopwatch();
        int completedRequests = 0;
        int inProgressRequests = 0;
        var progressLock = new object();

        var progressTimer = new Timer(_ =>
        {
            lock (progressLock)
            {
                double percentComplete = (double)completedRequests / config.NumberOfRequests * 100;
                int progressBarLength = 30;
                int filledLength = (int)Math.Floor(progressBarLength * completedRequests / (double)config.NumberOfRequests);

                string progressBar = "[" + new string('█', filledLength) + new string('░', progressBarLength - filledLength) + "]";

                Console.Write($"\r{progressBar} {percentComplete:F1}% ({completedRequests}/{config.NumberOfRequests}) - In progress: {inProgressRequests}    ");
            }
        }, null, 0, 200);

        stopwatch.Start();

        using (var semaphore = new SemaphoreSlim(config.NumberOfConcurrentRequests))
        {
            var tasks = new List<Task>();

            for (int i = 0; i < config.NumberOfRequests; i++)
            {
                await semaphore.WaitAsync();

                var task = Task.Run(async () =>
                {
                    Interlocked.Increment(ref inProgressRequests);

                    try
                    {
                        var result = await SendRequest(client, config);
                        results.Add(result);
                    }
                    finally
                    {
                        semaphore.Release();
                        Interlocked.Decrement(ref inProgressRequests);
                        Interlocked.Increment(ref completedRequests);

                        if (config.DelayBetweenRequestsMs > 0)
                        {
                            await Task.Delay(config.DelayBetweenRequestsMs);
                        }
                    }
                });

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        stopwatch.Stop();
        progressTimer.Dispose();

        Console.WriteLine();
        Console.WriteLine();

        DisplayResults(results, stopwatch.ElapsedMilliseconds, config.NumberOfRequests);
    }

    static async Task<RequestResult> SendRequest(HttpClient client, TestConfig config)
    {
        var result = new RequestResult();
        var requestStopwatch = new Stopwatch();

        try
        {
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(config.Method), config.Url);

            if (!string.IsNullOrEmpty(config.Body) &&
                (config.Method == "POST" || config.Method == "PUT" || config.Method == "PATCH"))
            {
                request.Content = new StringContent(config.Body, Encoding.UTF8, "application/json");
            }

            requestStopwatch.Start();
            HttpResponseMessage response = await client.SendAsync(request);
            requestStopwatch.Stop();

            result.StatusCode = (int)response.StatusCode;
            result.IsSuccessful = response.IsSuccessStatusCode;
            result.ResponseTime = requestStopwatch.ElapsedMilliseconds;
            result.ContentLength = response.Content.Headers.ContentLength ?? 0;

            if (config.VerboseOutput)
            {
                Console.WriteLine($"Request completed: Status={result.StatusCode}, Time={result.ResponseTime}ms");
            }
        }
        catch (HttpRequestException ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = $"Request failed: {ex.Message}";
            requestStopwatch.Stop();
            result.ResponseTime = requestStopwatch.ElapsedMilliseconds;

            if (config.VerboseOutput)
            {
                Console.WriteLine($"Request failed: {ex.Message}");
            }
        }
        catch (TaskCanceledException)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = "Request timed out";
            requestStopwatch.Stop();
            result.ResponseTime = requestStopwatch.ElapsedMilliseconds;
            result.IsTimeout = true;

            if (config.VerboseOutput)
            {
                Console.WriteLine("Request timed out");
            }
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = $"Unexpected error: {ex.Message}";
            requestStopwatch.Stop();
            result.ResponseTime = requestStopwatch.ElapsedMilliseconds;

            if (config.VerboseOutput)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
        }

        return result;
    }

    static void DisplayResults(ConcurrentBag<RequestResult> results, long totalTimeMs, int totalRequests)
    {
        var resultsList = results.ToList();

        int successfulRequests = resultsList.Count(r => r.IsSuccessful);
        int failedRequests = resultsList.Count(r => !r.IsSuccessful);
        int timeoutRequests = resultsList.Count(r => r.IsTimeout);

        double successRate = (double)successfulRequests / totalRequests * 100;

        long minResponseTime = resultsList.Any() ? resultsList.Min(r => r.ResponseTime) : 0;
        long maxResponseTime = resultsList.Any() ? resultsList.Max(r => r.ResponseTime) : 0;
        double avgResponseTime = resultsList.Any() ? resultsList.Average(r => r.ResponseTime) : 0;

        var sortedResponseTimes = resultsList.Select(r => r.ResponseTime).OrderBy(t => t).ToList();
        long p50 = CalculatePercentile(sortedResponseTimes, 50);
        long p90 = CalculatePercentile(sortedResponseTimes, 90);
        long p95 = CalculatePercentile(sortedResponseTimes, 95);
        long p99 = CalculatePercentile(sortedResponseTimes, 99);

        double requestsPerSecond = totalRequests / (totalTimeMs / 1000.0);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╭" + new string('─', 60) + "╮");
        Console.WriteLine($"│ {"Test Results:",-58} │");
        Console.WriteLine("├" + new string('─', 60) + "┤");
        Console.ResetColor();

        Console.WriteLine($"│ {"Total time:",-25} {totalTimeMs / 1000.0:F2} seconds{new string(' ', 20)} │");
        Console.WriteLine($"│ {"Requests per second:",-25} {requestsPerSecond:F2} req/sec{new string(' ', 19)} │");
        Console.WriteLine($"│ {"Total requests:",-25} {totalRequests}{new string(' ', 30)} │");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"│ {"Successful requests:",-25} {successfulRequests} ({successRate:F1}%){new string(' ', 21)} │");
        Console.ResetColor();

        if (failedRequests > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"│ {"Failed requests:",-25} {failedRequests}{new string(' ', 33)} │");
            Console.ResetColor();
        }

        if (timeoutRequests > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"│ {"Timeout requests:",-25} {timeoutRequests}{new string(' ', 33)} │");
            Console.ResetColor();
        }

        Console.WriteLine("├" + new string('─', 60) + "┤");
        Console.WriteLine($"│ {"Response Time Statistics:",-58} │");
        Console.WriteLine("├" + new string('─', 60) + "┤");
        Console.WriteLine($"│ {"Min response time:",-25} {minResponseTime} ms{new string(' ', 26)} │");
        Console.WriteLine($"│ {"Max response time:",-25} {maxResponseTime} ms{new string(' ', 26)} │");
        Console.WriteLine($"│ {"Average response time:",-25} {avgResponseTime:F2} ms{new string(' ', 23)} │");
        Console.WriteLine($"│ {"50th percentile (p50):",-25} {p50} ms{new string(' ', 26)} │");
        Console.WriteLine($"│ {"90th percentile (p90):",-25} {p90} ms{new string(' ', 26)} │");
        Console.WriteLine($"│ {"95th percentile (p95):",-25} {p95} ms{new string(' ', 26)} │");
        Console.WriteLine($"│ {"99th percentile (p99):",-25} {p99} ms{new string(' ', 26)} │");

        var statusCodeGroups = resultsList
            .GroupBy(r => r.StatusCode)
            .OrderBy(g => g.Key)
            .ToList();

        if (statusCodeGroups.Any())
        {
            Console.WriteLine("├" + new string('─', 60) + "┤");
            Console.WriteLine($"│ {"Status Code Distribution:",-58} │");
            Console.WriteLine("├" + new string('─', 60) + "┤");

            foreach (var group in statusCodeGroups)
            {
                string statusCode = group.Key.ToString();
                int count = group.Count();
                double percentage = (double)count / totalRequests * 100;

                if (group.Key >= 200 && group.Key < 300)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                else if (group.Key >= 300 && group.Key < 400)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                }
                else if (group.Key >= 400 && group.Key < 500)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }
                else if (group.Key >= 500)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                }

                Console.WriteLine($"│ {"HTTP " + statusCode + ":",-25} {count} ({percentage:F1}%){new string(' ', 21)} │");
                Console.ResetColor();
            }
        }

        if (failedRequests > 0 && statusCodeGroups.Count(g => g.Key == 0) > 0)
        {
            var errorGroups = resultsList
                .Where(r => !r.IsSuccessful)
                .GroupBy(r => r.ErrorMessage)
                .OrderByDescending(g => g.Count())
                .Take(3);

            Console.WriteLine("├" + new string('─', 60) + "┤");
            Console.WriteLine($"│ {"Common Errors:",-58} │");
            Console.WriteLine("├" + new string('─', 60) + "┤");

            foreach (var group in errorGroups)
            {
                string message = group.Key;
                if (message.Length > 50)
                {
                    message = message.Substring(0, 47) + "...";
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"│ {message,-58} │");
                Console.ResetColor();
            }
        }

        Console.WriteLine("╰" + new string('─', 60) + "╯");
    }

    static long CalculatePercentile(List<long> sortedData, int percentile)
    {
        if (!sortedData.Any())
            return 0;

        if (sortedData.Count == 1)
            return sortedData[0];

        double index = (percentile / 100.0) * (sortedData.Count - 1);
        int lowerIndex = (int)Math.Floor(index);
        int upperIndex = (int)Math.Ceiling(index);

        if (lowerIndex == upperIndex)
            return sortedData[lowerIndex];

        double weight = index - lowerIndex;
        return (long)(sortedData[lowerIndex] * (1 - weight) + sortedData[upperIndex] * weight);
    }
}