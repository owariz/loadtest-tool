using System.Collections.Concurrent;

static class ResultsReporter
{
    private static class Border
    {
        public const string TopLeft = "┌";
        public const string TopRight = "┐";
        public const string BottomLeft = "└";
        public const string BottomRight = "┘";
        public const string Horizontal = "─";
        public const string Vertical = "│";
        public const string LeftT = "├";
        public const string RightT = "┤";
        public const string TopT = "┬";
        public const string BottomT = "┴";
        public const string Cross = "┼";
    }

    private static string CreateLine(int width) => Border.Horizontal.PadRight(width, '─');
    private static string CreateHeader(string text, int width) =>
        $"{Border.Vertical} {text.PadRight(width - 4)} {Border.Vertical}";

    public static void PrintTestConfiguration(TestConfig config)
    {
        const int width = 80;
        Console.WriteLine();
        Console.WriteLine($"{Border.TopLeft}{CreateLine(width - 2)}{Border.TopRight}");
        Console.WriteLine(CreateHeader("", width));
        Console.WriteLine(CreateHeader("  🚀 Load Test Configuration", width));
        Console.WriteLine(CreateHeader("", width));
        Console.WriteLine($"{Border.LeftT}{CreateLine(width - 2)}{Border.RightT}");
        
        Console.WriteLine(CreateHeader($"  {"Target URL:",-20} {config.Url}", width));
        Console.WriteLine(CreateHeader($"  {"Protocol:",-20} {config.Protocol}", width));
        Console.WriteLine(CreateHeader($"  {"Method:",-20} {config.Method}", width));
        Console.WriteLine(CreateHeader($"  {"Total Requests:",-20} {config.NumberOfRequests}", width));
        Console.WriteLine(CreateHeader($"  {"Concurrency:",-20} {config.NumberOfConcurrentRequests}", width));
        Console.WriteLine(CreateHeader($"  {"Timeout:",-20} {config.TimeoutSeconds} seconds", width));

        if (config.Port > 0)
        {
            Console.WriteLine(CreateHeader($"  {"Port:",-20} {config.Port}", width));
        }

        if (config.DelayBetweenRequestsMs > 0)
        {
            Console.WriteLine(CreateHeader($"  {"Delay:",-20} {config.DelayBetweenRequestsMs} ms", width));
        }

        Console.WriteLine(CreateHeader("", width));
        Console.WriteLine($"{Border.BottomLeft}{CreateLine(width - 2)}{Border.BottomRight}");
        Console.WriteLine();
    }

    public static void DisplayResults(ConcurrentBag<RequestResult> results, long totalTimeMs, int totalRequests)
    {
        const int width = 80;
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

        Console.WriteLine();
        Console.WriteLine($"{Border.TopLeft}{CreateLine(width - 2)}{Border.TopRight}");
        Console.WriteLine(CreateHeader("", width));
        Console.WriteLine(CreateHeader("  📊 Test Results Summary", width));
        Console.WriteLine(CreateHeader("", width));
        Console.WriteLine($"{Border.LeftT}{CreateLine(width - 2)}{Border.RightT}");

        Console.WriteLine(CreateHeader($"  {"Total Duration:",-25} {totalTimeMs / 1000.0:F2} seconds", width));
        Console.WriteLine(CreateHeader($"  {"Throughput:",-25} {requestsPerSecond:F2} req/sec", width));
        Console.WriteLine(CreateHeader($"  {"Total Requests:",-25} {totalRequests:N0}", width));

        Console.WriteLine($"{Border.LeftT}{CreateLine(width - 2)}{Border.RightT}");
        Console.WriteLine(CreateHeader("", width));
        Console.WriteLine(CreateHeader("  📈 Response Statistics", width));
        Console.WriteLine(CreateHeader("", width));
        Console.WriteLine($"{Border.LeftT}{CreateLine(width - 2)}{Border.RightT}");

        Console.WriteLine(CreateHeader($"  {"Min Response Time:",-25} {minResponseTime:N0} ms", width));
        Console.WriteLine(CreateHeader($"  {"Max Response Time:",-25} {maxResponseTime:N0} ms", width));
        Console.WriteLine(CreateHeader($"  {"Average Response Time:",-25} {avgResponseTime:N2} ms", width));
        Console.WriteLine(CreateHeader($"  {"50th Percentile (p50):",-25} {p50:N0} ms", width));
        Console.WriteLine(CreateHeader($"  {"90th Percentile (p90):",-25} {p90:N0} ms", width));
        Console.WriteLine(CreateHeader($"  {"95th Percentile (p95):",-25} {p95:N0} ms", width));
        Console.WriteLine(CreateHeader($"  {"99th Percentile (p99):",-25} {p99:N0} ms", width));

        Console.WriteLine($"{Border.LeftT}{CreateLine(width - 2)}{Border.RightT}");
        Console.WriteLine(CreateHeader("", width));
        Console.WriteLine(CreateHeader("  🎯 Request Status", width));
        Console.WriteLine(CreateHeader("", width));
        Console.WriteLine($"{Border.LeftT}{CreateLine(width - 2)}{Border.RightT}");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(CreateHeader($"  {"Successful Requests:",-25} {successfulRequests:N0} ({successRate:F1}%)", width));
        Console.ResetColor();

        if (failedRequests > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(CreateHeader($"  {"Failed Requests:",-25} {failedRequests:N0}", width));
            Console.ResetColor();
        }

        if (timeoutRequests > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(CreateHeader($"  {"Timeout Requests:",-25} {timeoutRequests:N0}", width));
            Console.ResetColor();
        }

        var statusCodeGroups = resultsList
            .GroupBy(r => r.StatusCode)
            .OrderBy(g => g.Key)
            .ToList();

        if (statusCodeGroups.Any())
        {
            Console.WriteLine($"{Border.LeftT}{CreateLine(width - 2)}{Border.RightT}");
            Console.WriteLine(CreateHeader("", width));
            Console.WriteLine(CreateHeader("  📊 Status Code Distribution", width));
            Console.WriteLine(CreateHeader("", width));
            Console.WriteLine($"{Border.LeftT}{CreateLine(width - 2)}{Border.RightT}");

            foreach (var group in statusCodeGroups)
            {
                string statusCode = group.Key.ToString();
                int count = group.Count();
                double percentage = (double)count / totalRequests * 100;

                ConsoleColor color = group.Key switch
                {
                    >= 200 and < 300 => ConsoleColor.Green,
                    >= 300 and < 400 => ConsoleColor.Cyan,
                    >= 400 and < 500 => ConsoleColor.Yellow,
                    >= 500 => ConsoleColor.Red,
                    _ => ConsoleColor.Gray
                };

                Console.ForegroundColor = color;
                Console.WriteLine(CreateHeader($"  {"HTTP " + statusCode + ":",-25} {count:N0} ({percentage:F1}%)", width));
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

            Console.WriteLine($"{Border.LeftT}{CreateLine(width - 2)}{Border.RightT}");
            Console.WriteLine(CreateHeader("", width));
            Console.WriteLine(CreateHeader("  ⚠️ Common Errors", width));
            Console.WriteLine(CreateHeader("", width));
            Console.WriteLine($"{Border.LeftT}{CreateLine(width - 2)}{Border.RightT}");

            foreach (var group in errorGroups)
            {
                string message = group.Key;
                if (message.Length > width - 20)
                {
                    message = message.Substring(0, width - 23) + "...";
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(CreateHeader($"  {message}", width));
                Console.ResetColor();
            }
        }

        Console.WriteLine($"{Border.BottomLeft}{CreateLine(width - 2)}{Border.BottomRight}");
        Console.WriteLine();
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