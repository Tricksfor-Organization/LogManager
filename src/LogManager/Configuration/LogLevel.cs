namespace LogManager.Configuration;

/// <summary>
/// Serilog log levels
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Verbose is the noisiest level, rarely (if ever) enabled for a production app
    /// </summary>
    Verbose,

    /// <summary>
    /// Debug is used for internal system events that are not necessarily observable from the outside
    /// </summary>
    Debug,

    /// <summary>
    /// Information events describe things happening in the system that correspond to its responsibilities and functions
    /// </summary>
    Information,

    /// <summary>
    /// Warning events occur when service is degraded, endangered, or may be behaving outside of its expected parameters
    /// </summary>
    Warning,

    /// <summary>
    /// Error events indicate a failure within the application or connected system
    /// </summary>
    Error,

    /// <summary>
    /// Fatal events demand immediate attention
    /// </summary>
    Fatal
}
