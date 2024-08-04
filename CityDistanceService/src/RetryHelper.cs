public static class RetryHelper
{
    public static void RetryOnException(int times, TimeSpan delay, Action operation)
    {
        for (int i = 1; i <= times; ++i)
        {
            try
            {
                operation();
                break; // Success! Break out of the loop.
            }
            catch (Exception ex)
            {
                if (i == times)
                {
                    throw; // Final attempt failed, rethrow exception.
                }

                Console.WriteLine($"Attempt {i} failed: {ex.Message}. Retrying in {delay.TotalSeconds} seconds...");
                Thread.Sleep(delay);
            }
        }
    }
}