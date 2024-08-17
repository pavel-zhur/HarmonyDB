using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Telegram.Helpers;

namespace OneShelf.Telegram.Model.Ios;

public abstract class IoWithFinishBase : Io, IDisposable
{
    private IoFinish? _finish = new();
    private bool _isMonologue;

    protected object Lock { get; } = new();

    protected bool IsMonologue => _isMonologue;

    protected IoWithFinishBase(long userId, string? parameters, IOptions<TelegramOptions> telegramOptions, ILogger<Io> logger)
        : base(userId, parameters, telegramOptions, logger)
    {
    }

    protected IoFinish Finish => _finish ?? throw new("Already finished");

    public override bool SupportsFinish => true;

    public override bool SupportsOutput => true;

    public override bool SupportsAdditionalOutput => true;

    public override IoFinish FinishAndGetFinish()
    {
        var result = Finish;
        _finish = null;
        return result;
    }

    public override IoFinish GetFinishAndSwitchToMonologue()
    {
        if (!SupportsFinishSwitch) throw new InvalidOperationException("The finish switch is not supported.");

        lock (Lock)
        {
            if (_isMonologue) throw new InvalidOperationException("It is already switched to monologue.");
            _isMonologue = true;
            var result = _finish ?? throw new("Already finished.");
            _finish = new();
            return result;
        }
    }

    public void Dispose()
    {
        lock (Lock)
        {
            if (_finish != null) Logger.LogError("Finish not used.");
        }
    }

    public override void AdditionalOutput(Markdown markdown)
    {
        lock (Lock)
        {
            CheckAnyOk();

            Finish.AdditionalOutputs.Add((markdown, null));
        }
    }

    protected override void CheckAnyOk()
    {
        lock (Lock)
        {
            if (_finish == null) throw new("Already finished");
        }
    }

    public override void Write(string line) => Write(line.ToMarkdown());

    public override void Write(Markdown markdown)
    {
        lock (Lock)
        {
            CheckAnyOk();

            Finish.ReplyMessageBody += markdown;
        }
    }
}