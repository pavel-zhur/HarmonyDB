namespace OneShelf.Telegram.Processor.Tests;

public class EventsTests
{
    [Fact]
    public async Task Test1()
    {
        await new WithEvent().OnEvent1();
    }

    [Fact]
    public async Task Test2()
    {
        var withEvent = new WithEvent();

        var counter = 0;

        withEvent.Event1 += async () =>
        {
            await Task.Delay(1000);
            counter++;
            return 0;
        };

        await withEvent.OnEvent1();

        Assert.Equal(1, counter);
    }

    [Fact]
    public async Task Test3()
    {
        var withEvent = new WithEvent();

        var counter = 0;

        withEvent.Event1 += async () =>
        {
            await Task.Delay(1000);
            counter++;
            return 5;
        };

        withEvent.Event1 += async () =>
        {
            await Task.Delay(100);
            counter++;
            return 6;
        };

        var result = await withEvent.OnEvent1();

        Assert.Equal(1, counter);

        Assert.Equal(6, result);

        await Task.Delay(1000);

        Assert.Equal(2, counter);
    }

    [Fact]
    public async Task Test4()
    {
        var withEvent = new WithEvent();

        var counter = 0;

        withEvent.Event1 += async () =>
        {
            await Task.Delay(100);
            counter++;
            return 5;
        };

        withEvent.Event1 += async () =>
        {
            await Task.Delay(1000);
            counter++;
            return 6;
        };

        var result = await withEvent.OnEvent1();

        Assert.Equal(2, counter);
        Assert.Equal(6, result);
    }

    [Fact]
    public async Task Test5()
    {
        var withEvent = new WithEvent2();

        var counter = 0;

        withEvent.Event1 += async () =>
        {
            await Task.Delay(100);
            counter++;
        };

        withEvent.Event1 += async () =>
        {
            await Task.Delay(1000);
            counter++;
        };

        await withEvent.OnEvent1();

        Assert.Equal(2, counter);
    }

    [Fact]
    public async Task Test6()
    {
        var withEvent = new WithEvent2();

        var counter = 0;

        withEvent.Event1 += async () =>
        {
            await Task.Delay(1000);
            counter++;
        };

        withEvent.Event1 += async () =>
        {
            await Task.Delay(100);
            counter++;
        };

        await withEvent.OnEvent1();

        Assert.Equal(2, counter);
    }

    public class WithEvent
    {
        public event Func<Task<int>>? Event1;

        public async Task<int> OnEvent1()
        {
            var event1 = Event1;
            if (event1 == null) return 3;
            return await event1.Invoke();
        }
    }

    public class WithEvent2
    {
        public event Func<Task>? Event1;

        public async Task OnEvent1()
        {
            await Task.WhenAll(Event1?.GetInvocationList().Cast<Func<Task>>().Select(x => x()) ?? Enumerable.Empty<Task>());
        }
    }
}