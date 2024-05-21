namespace OneShelf.Telegram.Runner.Polling.Model;

public class PollerOptions
{
    public required string IncomingUpdateUrl { get; set; }
    public required string Token { get; set; }
    public required string WebHooksSecretToken { get; set; }
    public required string WebHooksSecretHeader { get; set; }
}