using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Surab.Analysis;

internal class Peeker<T>(IEnumerator<T> source) : IEnumerable<T>, IEnumerator<T>
	where T : notnull
{
	private readonly List<T?> _peeks = [];
	private T? _current = default;

	private IEnumerator<T>? _enumerator = source;

	public Peeker(IEnumerable<T> source) : this(source.GetEnumerator())
	{
	}

	public T Current => _current!;

	object IEnumerator.Current => Current!;

	public IEnumerator<T> GetEnumerator() => this;

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public bool MoveNext()
	{
		ObjectDisposedException.ThrowIf(_enumerator == null, this);

		if (_peeks.Count != 0)
		{
			// Shift
			_current = _peeks[0];
			_peeks.RemoveAt(0);
			return true;
		}

		var advanced = _enumerator.MoveNext();
		if (!advanced) return false;

		_current = _enumerator.Current;
		return true;
	}

	public void Reset()
	{
		ObjectDisposedException.ThrowIf(_enumerator == null, this);

		_peeks.Clear();
		_enumerator.Reset();
	}

	public void Dispose()
	{
		if (_enumerator != null)
		{
			_enumerator.Dispose();
			_enumerator = null;
		}
	}

	/// <summary>
	/// Peeks into the source's nth element. (n = 0 is next element)
	/// </summary>
	[DebuggerStepThrough]
	public bool TryPeek(int n, [NotNullWhen(true)] out T? result)
	{
		ObjectDisposedException.ThrowIf(_enumerator == null, this);

		result = default;

		if (_peeks.Count < n + 1)
		{
			for (var i = _peeks.Count; i <= n; i++)
			{
				var advanced = _enumerator.MoveNext();
				if (!advanced)
				{
					return false;
				}
				_peeks.Add(_enumerator.Current);
			}
		}

		result = _peeks[n]!;
		return true;
	}

	public bool TryMoveNext(out T? result)
	{
		result = default;

		if (!MoveNext())
		{
			return false;
		}

		result = Current;
		return true;
	}
}
