using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SolarMajesty
{
    /// <summary>
    /// Tiny JSON reader/writer for the local AI links (Laya decisions, hero narration).
    /// Objects → Dictionary&lt;string, object&gt;, arrays → List&lt;object&gt;, numbers → double.
    /// JsonUtility cannot read dictionary-shaped replies, hence this.
    /// </summary>
    public static class LocalJson
    {
        /// <summary>Parses one JSON value. Throws <see cref="System.FormatException"/> on bad input.</summary>
        public static object Parse(string json) => new MiniJson(json).ParseValue();

        /// <summary>Appends <paramref name="s"/> as a quoted, escaped JSON string.</summary>
        public static void AppendString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char ch in s ?? "")
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            sb.Append('"');
        }

        sealed class MiniJson
        {
            readonly string _s;
            int _i;

            public MiniJson(string s) { _s = s; }

            public object ParseValue()
            {
                SkipWs();
                if (_i >= _s.Length) throw new System.FormatException("eof");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ParseNumber();
                }
            }

            Dictionary<string, object> ParseObject()
            {
                var d = new Dictionary<string, object>();
                _i++;
                SkipWs();
                if (Peek() == '}') { _i++; return d; }
                while (true)
                {
                    SkipWs();
                    string key = ParseString();
                    SkipWs();
                    if (Next() != ':') throw new System.FormatException("colon");
                    d[key] = ParseValue();
                    SkipWs();
                    char n = Next();
                    if (n == '}') return d;
                    if (n != ',') throw new System.FormatException("object");
                }
            }

            List<object> ParseArray()
            {
                var l = new List<object>();
                _i++;
                SkipWs();
                if (Peek() == ']') { _i++; return l; }
                while (true)
                {
                    l.Add(ParseValue());
                    SkipWs();
                    char n = Next();
                    if (n == ']') return l;
                    if (n != ',') throw new System.FormatException("array");
                }
            }

            string ParseString()
            {
                if (Next() != '"') throw new System.FormatException("string");
                var sb = new StringBuilder();
                while (true)
                {
                    char c = Next();
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    char e = Next();
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw new System.FormatException("escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber));
                            _i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
            }

            double ParseNumber()
            {
                int start = _i;
                while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0) _i++;
                if (start == _i ||
                    !double.TryParse(_s.Substring(start, _i - start), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double v))
                    throw new System.FormatException("number");
                return v;
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0)
                    throw new System.FormatException(word);
                _i += word.Length;
            }

            char Peek() => _i < _s.Length ? _s[_i] : '\0';

            char Next()
            {
                if (_i >= _s.Length) throw new System.FormatException("eof");
                return _s[_i++];
            }

            void SkipWs()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }
        }
    }
}
