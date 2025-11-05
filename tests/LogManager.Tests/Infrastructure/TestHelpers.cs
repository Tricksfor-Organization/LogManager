namespace LogManager.Tests.Infrastructure;

/// <summary>
/// Helper utilities for integration tests
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Create a temporary test directory that will be cleaned up
    /// </summary>
    public static string CreateTempDirectory(string prefix = "logmanager_test")
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);
        return tempPath;
    }

    /// <summary>
    /// Clean up a test directory and all its contents
    /// </summary>
    public static void CleanupDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    /// <summary>
    /// Wait for a condition with timeout
    /// </summary>
    public static async Task<bool> WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        TimeSpan? pollInterval = null)
    {
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(interval);
        }

        return condition();
    }

    /// <summary>
    /// Read all text from a file with retry logic (handles file locks)
    /// </summary>
    public static async Task<string> ReadFileWithRetryAsync(string filePath, int maxRetries = 5)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await File.ReadAllTextAsync(filePath);
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                await Task.Delay(100);
            }
        }

        return await File.ReadAllTextAsync(filePath);
    }
}
