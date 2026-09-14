using System.Globalization;
using System.Text;

namespace OscTasks.Core;

public enum EventKind { Text, Title, Prompt, Input, Execute, Finish, Progress, Unknown, Invalid }

public sealed record StreamEvent(EventKind Kind, string Detail, int? Value = null, int? Percent = null);

/// <summary>Streaming UTF-8 OSC decoder. Not a terminal emulator; never executes output.</summary>
public sealed class OscParser(Action<StreamEvent> emit)
{
    public const int MaxSequenceLength = 4096;
    private enum Mode { Text, Escape, Osc, OscEscape, Discard, DiscardEscape }
    private readonly Decoder _decoder = CreateDecoder(emit);
    private readonly StringBuilder _sequence = new();
    private readonly StringBuilder _text = new();
    private Mode _mode;
    private bool _completed;

    public void Feed(ReadOnlySpan<byte> bytes)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        Span<char> chars = stackalloc char[1024];
        while (!bytes.IsEmpty)
        {
            _decoder.Convert(bytes, chars, false, out int used, out int written, out _);
            foreach (char ch in chars[..written]) Accept(ch);
            bytes = bytes[used..];
        }
        FlushText();
    }

    public void Complete()
    {
        if (_completed) return;
        _completed = true;
        Span<char> tail = stackalloc char[4];
        int count = _decoder.GetChars([], tail, true);
        foreach (char ch in tail[..count]) Accept(ch);
        FlushText();
        if (_mode != Mode.Text)
            emit(new(EventKind.Invalid, "EOF: truncated escape/OSC sequence discarded"));
        _sequence.Clear();
        _mode = Mode.Text;
    }

    private void Accept(char ch)
    {
        switch (_mode)
        {
            case Mode.Text:
                if (ch == '\x1b') { FlushText(); _mode = Mode.Escape; }
                else if (!char.IsControl(ch) || ch is '\n' or '\r' or '\t') _text.Append(ch);
                if (_text.Length >= 1024) FlushText();
                break;
            case Mode.Escape:
                if (ch == ']') { _sequence.Clear(); _mode = Mode.Osc; }
                else
                {
                    emit(new(EventKind.Invalid, "Non-OSC escape ignored (not a terminal emulator)"));
                    _mode = ch == '\x1b' ? Mode.Escape : Mode.Text;
                }
                break;
            case Mode.Osc:
                if (ch == '\a') FinishOsc();
                else if (ch == '\x1b') _mode = Mode.OscEscape;
                else Append(ch);
                break;
            case Mode.OscEscape:
                if (ch == '\\') FinishOsc();
                else
                {
                    emit(new(EventKind.Invalid, "Malformed OSC terminator; discarded until BEL/ST"));
                    _sequence.Clear();
                    _mode = ch == '\a' ? Mode.Text : ch == '\x1b' ? Mode.DiscardEscape : Mode.Discard;
                }
                break;
            case Mode.Discard:
                if (ch == '\a') _mode = Mode.Text;
                else if (ch == '\x1b') _mode = Mode.DiscardEscape;
                break;
            case Mode.DiscardEscape:
                _mode = ch is '\\' or '\a' ? Mode.Text : ch == '\x1b' ? Mode.DiscardEscape : Mode.Discard;
                break;
        }
    }

    private void Append(char ch)
    {
        if (_sequence.Length < MaxSequenceLength) { _sequence.Append(ch); return; }
        _sequence.Clear();
        _mode = Mode.Discard;
        emit(new(EventKind.Invalid, $"OSC exceeds {MaxSequenceLength} characters; discarded until BEL/ST"));
    }

    private void FinishOsc()
    {
        string raw = _sequence.ToString();
        _sequence.Clear();
        _mode = Mode.Text;
        string[] fields = raw.Split(';');
        if (fields[0] is "0" or "2" && fields.Length >= 2)
        {
            emit(new(EventKind.Title, SafeText(raw[(raw.IndexOf(';') + 1)..], 120)));
            return;
        }
        if (fields[0] == "133")
        {
            if (fields.Length == 2 && fields[1] is "A" or "B" or "C")
            {
                var kind = fields[1] switch { "A" => EventKind.Prompt, "B" => EventKind.Input, _ => EventKind.Execute };
                emit(new(kind, $"OSC {raw}"));
                return;
            }
            if (fields.Length is 2 or 3 && fields[1] == "D")
            {
                if (fields.Length == 2) emit(new(EventKind.Finish, "OSC 133;D: outcome unknown"));
                else if (int.TryParse(fields[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int exit))
                    emit(new(EventKind.Finish, $"OSC 133;D;{exit}", exit));
                else emit(new(EventKind.Invalid, "OSC 133;D: invalid exit code"));
                return;
            }
            emit(new(EventKind.Invalid, $"Malformed lifecycle: {SafeText(raw, 160)}"));
            return;
        }
        if (fields.Length >= 2 && fields[0] == "9" && fields[1] == "4")
        {
            if (fields.Length is 3 or 4 &&
                int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int state) && state is >= 0 and <= 4)
            {
                int? percent = null;
                if (fields.Length == 4)
                {
                    if (!int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out int p) || p is < 0 or > 100)
                    { emit(new(EventKind.Invalid, "OSC 9;4: invalid percentage")); return; }
                    percent = p;
                }
                if (state is 1 or 2 or 4 && percent is null)
                { emit(new(EventKind.Invalid, "OSC 9;4: numeric progress requires percentage")); return; }
                emit(new(EventKind.Progress, $"OSC {raw}", state, percent));
            }
            else emit(new(EventKind.Invalid, "OSC 9;4: invalid progress state/field count"));
            return;
        }
        emit(new(EventKind.Unknown, $"Ignored OSC: {SafeText(raw, 160)}"));
    }

    private void FlushText()
    {
        if (_text.Length == 0) return;
        emit(new(EventKind.Text, _text.ToString()));
        _text.Clear();
    }

    public static string SafeText(string text, int limit) =>
        new(text.Where(ch => !char.IsControl(ch) && CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.Format)
            .Take(limit).ToArray());

    private static Decoder CreateDecoder(Action<StreamEvent> emit)
    {
        var decoder = Encoding.UTF8.GetDecoder();
        decoder.Fallback = new ReportingFallback(() =>
            emit(new(EventKind.Invalid, "Invalid or incomplete UTF-8 replaced with U+FFFD")));
        return decoder;
    }

    private sealed class ReportingFallback(Action report) : DecoderFallback
    {
        public override int MaxCharCount => 1;
        public override DecoderFallbackBuffer CreateFallbackBuffer() => new ReportingBuffer(report);
    }

    private sealed class ReportingBuffer(Action report) : DecoderFallbackBuffer
    {
        private readonly DecoderFallbackBuffer _inner = new DecoderReplacementFallback("\ufffd").CreateFallbackBuffer();
        public override bool Fallback(byte[] bytesUnknown, int index)
        {
            report();
            return _inner.Fallback(bytesUnknown, index);
        }
        public override char GetNextChar() => _inner.GetNextChar();
        public override bool MovePrevious() => _inner.MovePrevious();
        public override int Remaining => _inner.Remaining;
        public override void Reset() => _inner.Reset();
    }
}
