using System.Collections.Concurrent;

static class ResultsReporter
{
    public static void PrintTestConfiguration(TestConfig config)
    {
        Console.WriteLine("╭" + new string('─', 60) + "╮");
        Console.WriteLine($"│ {"Test Configuration:",-58} │");
        Console.WriteLine("├" + new string('─', 60) + "┤");
        Console.WriteLine($"│ {"Target URL:",-15} {config.Url,-42} │");
        Console.WriteLine($"│ {"Protocol:",-15} {config.Protocol,-42} │");
        Console.WriteLine($"│ {"Method:",-15} {config.Method,-42} │");
        Console.WriteLine($"│ {"Total Requests:",-15} {config.NumberOfRequests,-42} │");
        Console.WriteLine($"│ {"Concurrency:",-15} {config.NumberOfConcurrentRequests,-42} │");
        Console.WriteLine($"│ {"Timeout:",-15} {config.TimeoutSeconds} sec{new string(' ', 36)} │");

        if (config.Port > 0)
        {
            Console.WriteLine($"│ {"Port:",-15} {config.Port}{new string(' ', 40)} │");
        }

        if (config.DelayBetweenRequestsMs > 0)
        {
            Console.WriteLine($"│ {"Delay:",-15} {config.DelayBetweenRequestsMs} ms{new string(' ', 37)} │");
        }

        Console.WriteLine("╰" + new string('─', 60) + "╯");
        Console.WriteLine();
    }

    public static void DisplayResults(ConcurrentBag<RequestResult> results, long totalTimeMs, int totalRequests)
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

                Console.WriteLine($"│ {"HTTP " + statusCode + ":",-25} {count} ({percentage:F1}%){new string(' ', 23)} │");
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