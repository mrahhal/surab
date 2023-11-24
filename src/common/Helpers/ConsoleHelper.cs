namespace Surab;

public static class ConsoleHelper
{
    private static readonly object _lock = new();

    public static void Write(string value, ConsoleColor? color = null, ConsoleColor? backgroundColor = null)
    {
        new ConsoleTextBuilder().Append(value, color, backgroundColor).Write();
    }

    public static void Write(ConsoleText text)
    {
        lock (_lock)
        {
            foreach (var message in text.Messages)
            {
                var value = message.Value;
                var color = message.Color;
                var backgroundColor = message.BackgroundColor;

                var newLines = 0;
                while (value.EndsWith(Environment.NewLine))
                {
                    newLines++;
                    value = value[..^Environment.NewLine.Length];
                }

                var prevColor = Console.ForegroundColor;
                var prevBackgroundColor = Console.BackgroundColor;

                try
                {
                    Console.ForegroundColor = color ?? prevColor;
                    Console.BackgroundColor = backgroundColor ?? prevBackgroundColor;

                    Console.Write(value);
                }
                finally
                {
                    Console.ForegroundColor = prevColor;
                    Console.BackgroundColor = prevBackgroundColor;
                }

                for (var i = 0; i < newLines; i++)
                {
                    Console.WriteLine();
                }
            }
        }
    }

    public static ConsoleTextBuilder StartBuilding()
    {
        return new ConsoleTextBuilder();
    }

    public static void WriteInfo(string value) => Write(value, ConsoleColor.Blue);

    public static void WriteSuccess(string value) => Write(value, ConsoleColor.Green);

    public static void WriteWarning(string value) => Write(value, ConsoleColor.Yellow);

    public static void WriteError(string value) => Write(value, ConsoleColor.Red);
}

public class ConsoleProgressShower
{
    private string? _previous;
    private bool _finished;
    private readonly int _total;
    private int _current;
    private readonly Timer _timer;

    public ConsoleProgressShower(int total)
    {
        _total = total;
        _timer = new Timer(_ => HandleTimer(), null, TimeSpan.FromSeconds(.1), TimeSpan.FromSeconds(.2));
    }

    public void WriteProgress(int current)
    {
        lock (this)
        {
            _current = current;
        }
    }

    private void ReturnToInitial()
    {
        if (_previous != null)
        {
            var left = Console.CursorLeft - _previous.Length;
            if (left < 0 || left >= Console.BufferWidth) left = 0;

            Console.SetCursorPosition(left, Console.CursorTop);
        }
    }

    public void Finish()
    {
        if (_previous == null) return;

        lock (this)
        {
            ReturnToInitial();

            var text = "".PadRight(_previous.Length);
            Console.Write(text);

            ReturnToInitial();

            _finished = true;

            _timer.Dispose();
        }
    }

    private void HandleTimer()
    {
        lock (this)
        {
            if (_finished) return;

            var total = _total;
            var current = _current;

            ReturnToInitial();

            var text = $"{current}/{total} ({Math.Round((double)current / total * 100, 2)}%)";
            if (_previous != null && text.Length < _previous.Length)
            {
                text = text.PadRight(_previous.Length);
            }

            _previous = text;
            Console.Write(text);
        }
    }
}

public class ConsoleTextBuilder
{
    private readonly ConsoleText _text = new();

    public ConsoleTextBuilder Append(string value, ConsoleColor? color = null, ConsoleColor? backgroundColor = null)
    {
        _text.Append(new ConsoleTextMessage(value, color, backgroundColor));
        return this;
    }

    public ConsoleTextBuilder AppendNewLine()
    {
        _text.Append(new ConsoleTextMessage(string.Empty.NL(), null, null));
        return this;
    }

    public ConsoleText Build() => _text;

    public void Write()
    {
        ConsoleHelper.Write(_text);
    }
}

public class ConsoleTextMessage(string value, ConsoleColor? color, ConsoleColor? backgroundColor)
{
    public string Value { get; set; } = value;
    public ConsoleColor? Color { get; set; } = color;
    public ConsoleColor? BackgroundColor { get; set; } = backgroundColor;
}

public class ConsoleText
{
    public List<ConsoleTextMessage> Messages { get; set; } = [];

    public ConsoleText Append(ConsoleTextMessage text)
    {
        Messages.Add(text);
        return this;
    }
}
