using OneShelf.Common;

namespace OneShelf.Frontend.Web.Services;

public class LogoImageSequencer : IDisposable
{
	private readonly List<(int x, int y)> _sequence;
	private int _currentIndex;
	private readonly Timer _timer;

	public LogoImageSequencer()
	{
		var oneSequence = new[]
		{
			1, 2, 3, 6, 5, 4, 7, 8, 9,
		};

		_sequence = new (int x, bool reverse)[]
		{
			(1, true),
			(2, false),
			(3, true),
			(4, false),
			(5, true),
		}.SelectMany(x => oneSequence.SelectSingle(s => x.reverse ? s.Reverse() : s).Select(y => (x.x, y))).ToList();

		_currentIndex = Random.Shared.Next(0, _sequence.Count);

		var period = TimeSpan.FromMinutes(0.25);
		_timer = new(TimerUpdate, null, period, period);

		Current = GetClass(_sequence[_currentIndex]);
	}

	public string Current { get; private set; }

	public event Action? Change;

	private void TimerUpdate(object? state)
	{
		_currentIndex = (_currentIndex + 1) % _sequence.Count;

		Current = GetClass(_sequence[_currentIndex]);

		OnChange();
	}

	private string GetClass((int x, int y) item)
	{
		var (x, y) = item;
		return $"l{x} l{x}-{y}";
	}

	protected virtual void OnChange()
	{
		Change?.Invoke();
	}

	public void Dispose()
	{
		_timer.Dispose();
	}
}