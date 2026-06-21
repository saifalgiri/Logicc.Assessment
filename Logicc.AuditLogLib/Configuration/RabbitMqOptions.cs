using System.ComponentModel.DataAnnotations;

namespace Logicc.AuditLogLib.Configuration;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required(AllowEmptyStrings = false)]
    public string Host { get; set; } = "localhost";

    public string VirtualHost { get; set; } = "/";

    [Required(AllowEmptyStrings = false)]
    public string Username { get; set; } = "guest";

    [Required(AllowEmptyStrings = false)]
    public string Password { get; set; } = "guest";
}
