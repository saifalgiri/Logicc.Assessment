using System.ComponentModel.DataAnnotations;

namespace Logicc.VictoriaLogSync.Configuration;

/// <summary>
/// VictoriaLogs HTTP API settings, bound from the "VictoriaLogs" configuration section.
/// </summary>
public class VictoriaLogsOptions
{
    public const string SectionName = "VictoriaLogs";

    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; set; } = "http://localhost:9428";

    /// <summary>
    /// Relative path of the VictoriaLogs JSON line ingestion endpoint.
    /// </summary>
    public string IngestPath { get; set; } = "/insert/jsonline";
}
