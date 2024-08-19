using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Telegram.Model.IoMemories;
using OneShelf.Telegram.Options;

namespace OneShelf.Telegram.Model.Ios;

public abstract class Io
{
    protected Io(long userId, string? parameters, IOptions<TelegramOptions> telegramOptions, ILogger<Io> logger)
    {
        UserId = userId;
        IsAdmin = telegramOptions.Value.IsAdmin(userId);
        Parameters = parameters;
        Logger = logger;
    }

    public long UserId { get; }

    public bool IsAdmin { get; }

    public string? Parameters { get; }


    protected ILogger<Io> Logger { get; }


    public virtual bool SupportsFinish => false;

    public virtual bool SupportsFinishSwitch => false;

    public virtual bool SupportsOutput => false;

    public virtual bool SupportsMarkdownOutput => false;

    public virtual bool SupportsInput => false;

    public virtual bool SupportsAdditionalOutput => false;


    public virtual IoFinish FinishAndGetFinish() => throw new InvalidOperationException();
    public virtual IoFinish GetFinishAndSwitchToMonologue() => throw new InvalidOperationException();
    public virtual IoMemory GetMemory() => throw new InvalidOperationException();


    protected virtual void CheckAnyOk() => throw new InvalidOperationException("Not supported.");

    public virtual string FreeChoice(string request, IReadOnlyCollection<string>? buttons = null) => throw new InvalidOperationException("Not supported.");

    public virtual void Write(string line) => throw new InvalidOperationException("Not supported.");

    public virtual void Write(Markdown markdown) => throw new InvalidOperationException("Not supported.");

    public virtual void AdditionalOutput(Markdown markdown) => throw new InvalidOperationException("Not supported.");


    public int FreeChoiceInt(string request) => StrictChoice(request, int.Parse, error: $"Пожалуйста, просто число. {request}");

    public void WriteLine(string? line = null) => Write($"{line}{Environment.NewLine}");

    public void WriteLine(Markdown markdown)
    {
        Write(markdown);
        WriteLine();
    }

    public T StrictChoice<T>(string request, string? error = null, Func<T, bool>? isActionAvailable = null)
        where T : struct, Enum
    {
        var map = typeof(T)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(x => (name: x.Name, caption: x.GetCustomAttribute<DisplayAttribute>()?.Name ?? x.Name, value: Enum.Parse<T>(x.Name)))
            .Where(x => isActionAvailable?.Invoke(x.value) ?? true)
            .ToList();

        if (!map.Any()) throw new("At least one action should be available.");

        return StrictChoice(
            request, 
            x => Enum.Parse<T>(map.Single(p => p.caption == x).name)
                .SelectSingle(x => (isActionAvailable?.Invoke(x) ?? true) ? x : throw new("The action is unavailable.")), 
            map.Select(x => x.caption).ToList(),
            error: error ?? "Выберите один из предложенных вариантов, пожалуйста.");
    }

    public T? StrictChoiceNullable<T>(string request, string? error = null)
        where T : struct, Enum 
        => StrictChoice(request, x => x == "<null>" ? (T?)null : Enum.Parse<T>(x), Enum.GetNames<T>().Append("<null>").ToList(), error ?? "Выберите один из предложенных вариантов, пожалуйста.");

    public (string? strict, string? free) SemiStrictChoice(string request, IReadOnlyCollection<string> buttons)
        => FreeChoice(request, buttons)
            .SelectSingle(x => buttons.Contains(x) ? ((string?)x, (string?)null) : (null, x));

    public T StrictChoice<T>(string request, Func<string, T> selector, IReadOnlyCollection<string>? buttons = null, string? error = null)
    {
        var currentRequest = request;
        while (true)
        {
            try
            {
                return selector(FreeChoice(currentRequest, buttons).Trim());
            }
            catch (NeedDialogResponseException)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch
            {
                currentRequest = error ?? $"Пожалуйста, выберите один из предложенных вариантов. {request}";
            }
        }
    }
}