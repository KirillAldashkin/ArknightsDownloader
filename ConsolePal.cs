using System.Runtime.CompilerServices;

static class ConsolePal
{
    public static void WriteLine(ReadOnlySpan<char> text, ConsoleColor? fore = default, ConsoleColor? back = default)
    {
        var oldFore = Console.ForegroundColor;
        var oldBack = Console.BackgroundColor;
        Console.ForegroundColor = fore ?? oldFore;
        Console.BackgroundColor = back ?? oldBack;
        Console.Out.WriteLine(text);
        Console.BackgroundColor = oldBack;
        Console.ForegroundColor = oldFore;
    }

    public static void Write(ReadOnlySpan<char> text, ConsoleColor? fore = default, ConsoleColor? back = default)
    {
        var oldFore = Console.ForegroundColor;
        var oldBack = Console.BackgroundColor;
        Console.ForegroundColor = fore ?? oldFore;
        Console.BackgroundColor = back ?? oldBack;
        Console.Out.Write(text);
        Console.BackgroundColor = oldBack;
        Console.ForegroundColor = oldFore;
    }

    public static void WriteError(ReadOnlySpan<char> message, 
        [CallerFilePath] string file = null!, 
        [CallerLineNumber] int line = default)
    {
        Write("ERROR - ", ConsoleColor.DarkRed);
        Write($"{file}:{line}", ConsoleColor.Yellow);
        WriteLine(":");
        WriteLine(message);
    }

    // returns -1 on cancel, otherwise index into given list
    public static int SelectOne<T>(string header, IReadOnlyList<T> values, Func<T, string> format)
    {
        if (values.Count == 0) return -1;

        bool firstAttempt = true;
        var (left, top) = Console.GetCursorPosition();
        var oldInputLength = 0;
        while(true)
        {
            Console.CursorLeft = left;
            Console.CursorTop = top;
            Console.WriteLine(header);
            var length = 1 + (int)Math.Log10(values.Count);
            for (var i = 0; i < values.Count; ++i)
            {
                // print as 1-indexed
                Write($"{(i + 1).ToString().PadLeft(length)}) ", ConsoleColor.White);
                Console.WriteLine(format(values[i]));
            }

            if (!firstAttempt) Console.Write("Wrong index. ");
            firstAttempt = false;
            Console.Write("Write index (0 to cancel): ");

            // clear previous input
            var (pLeft, pTop) = Console.GetCursorPosition();
            for (var i = 0; i < oldInputLength; ++i) Console.Write(' ');
            Console.CursorLeft = pLeft;
            Console.CursorTop = pTop;

            var input = Console.ReadLine()!;
            oldInputLength = input.Length;
            if (!int.TryParse(input, out var index) || index < 0 || index > values.Count) continue;

            return index - 1; // was printed/read as 1-indexed, transform back to 0-indexed
        }
    }

    public readonly struct LineSpacing
    {
        private readonly List<int> _lines;
        private readonly int _cursorLeft;
        private readonly int _cursorTop;

        public LineSpacing()
        {
            _lines = [];
            (_cursorLeft, _cursorTop) = Console.GetCursorPosition();
        }

        public readonly Iterator Start()
        {
            Console.CursorLeft = _cursorLeft;
            Console.CursorTop = _cursorTop;
            return new(_lines);
        }

        public struct Iterator(List<int> lines)
        {
            private int _line = 0;

            public void NextLine()
            {
                if (lines.Count <= _line) lines.Add(0);
                var currentLeft = Console.CursorLeft;
                var fix = Math.Max(0, lines[_line] - currentLeft);
                for (var i = 0; i < fix; ++i) Console.Write(' ');
                Console.WriteLine();
                lines[_line++] = currentLeft;
            }

            public void Finish()
            {
                while (_line < lines.Count) NextLine();
            }
        }
    }
}