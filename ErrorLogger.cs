using System;
using System.IO;

public class ErrorLogger
{
    private readonly string _logFilePath;

    public ErrorLogger(string logDirectory)
    {
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
        _logFilePath = Path.Combine(logDirectory, $"ErrorLog_{DateTime.Now:yyyyMMdd}.txt");
    }

    public void LogError(Exception ex, string additionalInfo = "")
    {
        var errorMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Error: {ex.Message}\n" +
                           $"Additional Info: {additionalInfo}\n" +
                           $"Stack Trace: {ex.StackTrace}\n\n";

        File.AppendAllText(_logFilePath, errorMessage);
    }
}
