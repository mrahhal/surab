using System.Collections;

namespace Surab.Analyzer.Infrastructure;

internal class Peeker<T> : IEnumerable<T>, IEnumerator<T>
    where T : notnull
{
    private readonly List<T?> _peeks = new();
    private T? _current = default;

    private IEnumerator<T>? _enumerator;

    public Peeker(IEnumerable<T> source) : this(source.GetEnumerator())
    {
    }

    public Peeker(IEnumerator<T> source)
    {
        _enumerator = source;
    }

    public T Current => _current!;

    object IEnumerator.Current => Current!;

    public IEnumerator<T> GetEnumerator() => this;

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool MoveNext()
    {
        if (_enumerator == null) throw new ObjectDisposedException(GetType().Name);

        if (_peeks.Any())
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
        if (_enumerator == null) throw new ObjectDisposedException(GetType().Name);

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
    /// Peeks into the source's nth element. (n=0 is next element)
    /// </summary>
    public bool TryPeek(int n, out T? result)
    {
        if (_enumerator == null) throw new ObjectDisposedException(GetType().Name);

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

        result = _peeks[n];
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
