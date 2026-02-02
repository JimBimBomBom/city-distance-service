using System;
using System.Threading.Tasks;

public static class RetryHelper
{
    /// <summary>
    /// Retry a synchronous operation with exponential backoff
    /// </summary>
    public static void RetryOnException(int maxRetries, TimeSpan delay, Action operation, string operationName = "Operation")
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"Attempting {operationName} (attempt {attempt}/{maxRetries})...");
                operation();
                Console.WriteLine($"{operationName} succeeded on attempt {attempt}.");
                return; // Success - exit the retry loop
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    Console.WriteLine($"{operationName} failed after {maxRetries} attempts.");
                    throw; // Re-throw on final attempt
                }

                Console.WriteLine($"Attempt {attempt} failed: {ex.Message}. Retrying in {delay.TotalSeconds} seconds...");
                Thread.Sleep(delay);
            }
        }
    }

    /// <summary>
    /// Retry an async operation with exponential backoff
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

    /// <summary>
    /// Retry an async operation with a custom retry condition
    /// </summary>
    public static async Task<T> RetryOnExceptionAsync<T>(
        int maxRetries,
        TimeSpan delay,
        Func<Task<T>> operation,
        Func<Exception, bool> shouldRetry,
        string operationName = "Operation")
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"Attempting {operationName} (attempt {attempt}/{maxRetries})...");
                var result = await operation();
                Console.WriteLine($"{operationName} succeeded on attempt {attempt}.");
                return result;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries || !shouldRetry(ex))
                {
                    Console.WriteLine($"{operationName} failed: {ex.Message}");
                    throw;
                }

                Console.WriteLine($"Attempt {attempt} failed: {ex.Message}");
                Console.WriteLine($"Retrying in {delay.TotalSeconds} seconds...");
                await Task.Delay(delay);
            }
        }

        throw new InvalidOperationException($"{operationName} failed after {maxRetries} attempts");
    }

    /// <summary>
    /// Retry with exponential backoff
    /// </summary>
    public static async Task RetryWithExponentialBackoffAsync(
        int maxRetries,
        TimeSpan initialDelay,
        Func<Task> operation,
        string operationName = "Operation",
        double backoffMultiplier = 2.0)
    {
        var currentDelay = initialDelay;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"Attempting {operationName} (attempt {attempt}/{maxRetries})...");
                await operation();
                Console.WriteLine($"{operationName} succeeded on attempt {attempt}.");
                return;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    Console.WriteLine($"{operationName} failed after {maxRetries} attempts.");
                    throw;
                }

                Console.WriteLine($"Attempt {attempt} failed: {ex.Message}");
                Console.WriteLine($"Retrying in {currentDelay.TotalSeconds} seconds...");
                await Task.Delay(currentDelay);

                // Exponential backoff
                currentDelay = TimeSpan.FromMilliseconds(currentDelay.TotalMilliseconds * backoffMultiplier);
            }
        }
    }

    /// <summary>
    /// Wait until a condition is met or timeout
    /// </summary>
    public static async Task<bool> WaitUntilAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan checkInterval,
        string operationName = "Condition check")
    {
        var startTime = DateTime.UtcNow;
        var attempts = 0;

        while (DateTime.UtcNow - startTime < timeout)
        {
            attempts++;
            try
            {
                Console.WriteLine($"Checking {operationName} (attempt {attempts})...");
                if (await condition())
                {
                    Console.WriteLine($"{operationName} succeeded after {attempts} attempts.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Check failed: {ex.Message}");
            }

            await Task.Delay(checkInterval);
        }

        Console.WriteLine($"{operationName} timed out after {timeout.TotalSeconds} seconds and {attempts} attempts.");
        return false;
    }
}