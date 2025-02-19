using System.ComponentModel.DataAnnotations;

namespace CommunityToolkit.Aspire.Hosting.MailPit.SendMailApi;

public class MailData
{
    [Required]
    public required string From { get; set; }
    [Required]
    public required string To { get; set; }
    [Required]
    public required string Body { get; set; }
    [Required]
    public required string Subject { get; set; }
}