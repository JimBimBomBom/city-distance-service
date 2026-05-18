using System;
using System.Threading.Tasks;

public static class RetryHelper
{
    /// <summary>
    /// Retry an async operation with fixed delay between attempts.
    /// </summary>
    public static async Task RetryOnExceptionAsync(
        int maxRetries,
        TimeSpan delay,
        Func<Task> operation,
        string operationName = "Operation")
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"Attempting {operationName} (attempt {attempt}/{maxRetries})...");
                await operation();
                Console.WriteLine($"{operationName} succeeded on attempt {attempt}.");
                return; // Success - exit the retry loop
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    Console.WriteLine($"{operationName} failed after {maxRetries} attempts. Last error: {ex.Message}");
                    throw; // Re-throw on final attempt
                }

                Console.WriteLine($"Attempt {attempt} failed: {ex.Message}");
                Console.WriteLine($"Retrying in {delay.TotalSeconds} seconds...");
                await Task.Delay(delay);
            }
        }
    }
}