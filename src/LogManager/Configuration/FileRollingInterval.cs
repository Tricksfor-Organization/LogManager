namespace LogManager.Configuration;

/// <summary>
/// Specifies the frequency at which the log file should roll
/// </summary>
public enum FileRollingInterval
{
    /// <summary>
    /// The log file will never roll; no time period information will be appended to the log file name
    /// </summary>
    Infinite,

    /// <summary>
    /// Roll every year. Filenames will have a four-digit year appended in the pattern yyyy
    /// </summary>
    Year,

    /// <summary>
    /// Roll every calendar month. Filenames will have yyyyMM appended
    /// </summary>
    Month,

    /// <summary>
    /// Roll every day. Filenames will have yyyyMMdd appended
    /// </summary>
    Day,

    /// <summary>
    /// Roll every hour. Filenames will have yyyyMMddHH appended
    /// </summary>
    Hour,

    /// <summary>
    /// Roll every minute. Filenames will have yyyyMMddHHmm appended
    /// </summary>
    Minute
}
