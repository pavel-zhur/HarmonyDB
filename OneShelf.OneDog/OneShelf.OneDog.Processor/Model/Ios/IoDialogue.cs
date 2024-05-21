using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.OneDog.Processor.Model.IoMemories;
using Telegram.BotAPI.AvailableTypes;

namespace OneShelf.OneDog.Processor.Model.Ios;

public class IoDialogue : IoWithFinishBase
{
    private readonly IoMemory _memory;

    public IoDialogue(IoMemory ioMemory, IOptions<TelegramOptions> telegramOptions, ILogger<Io> logger)
        : base(ioMemory.UserId, ioMemory.Parameters, telegramOptions, logger)
    {
        _memory = ioMemory;
        _memory.Restart();
    }

    public IoDialogue(long userId, string alias, string? parameters, IOptions<TelegramOptions> telegramOptions, ILogger<Io> logger) 
        : base(userId, parameters, telegramOptions, logger)
    {
        _memory = new(userId, alias, parameters);
    }

    public override bool SupportsInput => true;

    public override bool SupportsFinishSwitch => true;

    public override bool SupportsMarkdownOutput => true;

    public override IoMemory GetMemory() => _memory;

    public override void Write(Markdown markdown)
    {
        lock (Lock)
        {
            base.Write(markdown);
            _memory.Event(new IoMemoryItemWrite(markdown.ToString()));
        }
    }

    public override string FreeChoice(string request, IReadOnlyCollection<string>? buttons = null)
    {
        lock (Lock)
        {
            WriteLine(request);
            Finish.ReplyMessageMarkup =
                buttons?.Any() == true
                    ? new ReplyKeyboardMarkup(
                        buttons.Select(x => new[] { new KeyboardButton(x) })
                            .Append(new[] { new KeyboardButton("/start: В начало") }), inputFieldPlaceholder: request)
                    {
                        ResizeKeyboard = true,
                    }
                    : new ForceReply
                    {
                        InputFieldPlaceholder = request,
                    };
            _memory.Event(new IoMemoryItemOptions(buttons));
            _memory.Event(new IoMemoryItemPlaceholder(request));
            var result = ReadLine();
            Finish.ReplyMessageMarkup = null;
            _memory.Event(new IoMemoryItemOptions(null));
            _memory.Event(new IoMemoryItemPlaceholder(null));

            return result;
        }
    }

    private string ReadLine()
    {
        lock (Lock)
        {
            if (IsMonologue) throw new InvalidOperationException("It is already switched to monologue.");
            var result = _memory.GetNextInput();
            Finish.ReplyMessageBody = Markdown.Empty;
            Finish.ReplyMessageMarkup = null;
            return result;
        }
    }
}