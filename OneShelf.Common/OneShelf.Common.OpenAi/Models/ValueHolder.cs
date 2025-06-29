namespace OneShelf.Common.OpenAi.Models;

public class ValueHolder<T>(T value)
    where T : struct
{
    public T Value { get; set; } = value;
}