using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Vykhod
{
    /// <summary>Минимальный JSON: Dictionary&lt;string,object&gt;, List&lt;object&gt;, string, double, bool, null.</summary>
    public static class Json
    {
        public static object Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var p = new Parser(s);
            p.SkipWs();
            return p.Value();
        }

        public static string Serialize(object o, bool pretty = false)
        {
            var sb = new StringBuilder();
            Write(o, sb, pretty, 0);
            return sb.ToString();
        }

        class Parser
        {
            readonly string s; int i;
            public Parser(string src) { s = src; i = 0; }
            public void SkipWs() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
            char Peek => i < s.Length ? s[i] : '\0';

            public object Value()
            {
                SkipWs();
                char c = Peek;
                if (c == '{') return Obj();
                if (c == '[') return Arr();
                if (c == '"') return Str();
                if (c == 't') { i += 4; return true; }
                if (c == 'f') { i += 5; return false; }
                if (c == 'n') { i += 4; return null; }
                return Num();
            }

            Dictionary<string, object> Obj()
            {
                var d = new Dictionary<string, object>();
                i++; // {
                SkipWs();
                if (Peek == '}') { i++; return d; }
                while (i < s.Length)
                {
                    SkipWs();
                    string k = Str();
                    SkipWs();
                    if (Peek == ':') i++;
                    var v = Value();
                    d[k] = v;
                    SkipWs();
                    if (Peek == ',') { i++; continue; }
                    if (Peek == '}') { i++; break; }
                    break;
                }
                return d;
            }

            List<object> Arr()
            {
                var l = new List<object>();
                i++; // [
                SkipWs();
                if (Peek == ']') { i++; return l; }
                while (i < s.Length)
                {
                    l.Add(Value());
                    SkipWs();
                    if (Peek == ',') { i++; continue; }
                    if (Peek == ']') { i++; break; }
                    break;
                }
                return l;
            }

            string Str()
            {
                var sb = new StringBuilder();
                if (Peek != '"') return "";
                i++;
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') break;
                    if (c == '\\' && i < s.Length)
                    {
                        char e = s[i++];
                        switch (e)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 't': sb.Append('\t'); break;
                            case 'r': sb.Append('\r'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'u':
                                if (i + 4 <= s.Length)
                                {
                                    sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                                    i += 4;
                                }
                                break;
                            default: sb.Append(e); break;
                        }
                    }
                    else sb.Append(c);
                }
                return sb.ToString();
            }

            object Num()
            {
                int start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
                string t = s.Substring(start, i - start);
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
                if (i == start) i++; // защита от зацикливания на мусоре
                return 0.0;
            }
        }

        static void Indent(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty) return;
            sb.Append('\n');
            for (int k = 0; k < depth; k++) sb.Append(' ');
        }

        static void Write(object o, StringBuilder sb, bool pretty, int depth)
        {
            switch (o)
            {
                case null: sb.Append("null"); break;
                case string str: WriteStr(str, sb); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case long n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case IDictionary<string, object> dict:
                    {
                        sb.Append('{');
                        bool first = true;
                        foreach (var kv in dict)
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            Indent(sb, pretty, depth + 1);
                            WriteStr(kv.Key, sb);
                            sb.Append(':');
                            Write(kv.Value, sb, pretty, depth + 1);
                        }
                        Indent(sb, pretty, depth);
                        sb.Append('}');
                        break;
                    }
                case IDictionary<string, float> fd:
                    {
                        sb.Append('{'); bool first = true;
                        foreach (var kv in fd) { if (!first) sb.Append(','); first = false; WriteStr(kv.Key, sb); sb.Append(':'); sb.Append(kv.Value.ToString("R", CultureInfo.InvariantCulture)); }
                        sb.Append('}'); break;
                    }
                case IDictionary<string, int> id:
                    {
                        sb.Append('{'); bool first = true;
                        foreach (var kv in id) { if (!first) sb.Append(','); first = false; WriteStr(kv.Key, sb); sb.Append(':'); sb.Append(kv.Value.ToString(CultureInfo.InvariantCulture)); }
                        sb.Append('}'); break;
                    }
                case IDictionary<string, string> sd:
                    {
                        sb.Append('{'); bool first = true;
                        foreach (var kv in sd) { if (!first) sb.Append(','); first = false; WriteStr(kv.Key, sb); sb.Append(':'); WriteStr(kv.Value, sb); }
                        sb.Append('}'); break;
                    }
                case System.Collections.IEnumerable list:
                    {
                        sb.Append('[');
                        bool first = true;
                        foreach (var v in list)
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            Write(v, sb, pretty, depth + 1);
                        }
                        sb.Append(']');
                        break;
                    }
                default: WriteStr(o.ToString(), sb); break;
            }
        }

        static void WriteStr(string s, StringBuilder sb)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    /// <summary>Хелперы доступа к распарсенному JSON.</summary>
    public static class J
    {
        public static Dictionary<string, object> D(object o) => o as Dictionary<string, object>;
        public static List<object> L(object o) => o as List<object>;

        public static Dictionary<string, object> D(Dictionary<string, object> d, string k)
            => d != null && d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;

        public static List<object> L(Dictionary<string, object> d, string k)
            => d != null && d.TryGetValue(k, out var v) ? v as List<object> : null;

        public static string S(Dictionary<string, object> d, string k, string def = "")
        {
            if (d == null || !d.TryGetValue(k, out var v) || v == null) return def;
            if (v is string s) return s;
            if (v is double x) return x.ToString(CultureInfo.InvariantCulture);
            return v.ToString();
        }

        public static float F(Dictionary<string, object> d, string k, float def = 0)
        {
            if (d == null || !d.TryGetValue(k, out var v) || v == null) return def;
            if (v is double x) return (float)x;
            if (v is bool b) return b ? 1 : 0;
            if (v is string s && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return f;
            return def;
        }

        public static float F(object v, float def = 0)
        {
            if (v is double x) return (float)x;
            if (v is bool b) return b ? 1 : 0;
            if (v is string s && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return f;
            return def;
        }

        public static int I(Dictionary<string, object> d, string k, int def = 0) => (int)Math.Round(F(d, k, def));

        public static bool B(Dictionary<string, object> d, string k, bool def = false)
        {
            if (d == null || !d.TryGetValue(k, out var v) || v == null) return def;
            if (v is bool b) return b;
            return F(v) != 0;
        }

        public static List<string> SL(Dictionary<string, object> d, string k)
        {
            var r = new List<string>();
            var l = L(d, k);
            if (l != null) foreach (var o in l) if (o != null) r.Add(o.ToString());
            return r;
        }

        public static List<string> SL(object o)
        {
            var r = new List<string>();
            if (o is List<object> l) foreach (var x in l) if (x != null) r.Add(x.ToString());
            return r;
        }

        public static Dictionary<string, float> FD(Dictionary<string, object> d, string k)
        {
            var r = new Dictionary<string, float>();
            var sub = D(d, k);
            if (sub != null) foreach (var kv in sub) r[kv.Key] = F(kv.Value);
            return r;
        }

        public static Dictionary<string, int> ID(Dictionary<string, object> d, string k)
        {
            var r = new Dictionary<string, int>();
            var sub = D(d, k);
            if (sub != null) foreach (var kv in sub) r[kv.Key] = (int)Math.Round(F(kv.Value));
            return r;
        }

        public static Dictionary<string, string> SD(Dictionary<string, object> d, string k)
        {
            var r = new Dictionary<string, string>();
            var sub = D(d, k);
            if (sub != null) foreach (var kv in sub) r[kv.Key] = kv.Value?.ToString() ?? "";
            return r;
        }

        public static List<Dictionary<string, object>> DL(Dictionary<string, object> d, string k)
        {
            var r = new List<Dictionary<string, object>>();
            var l = L(d, k);
            if (l != null) foreach (var o in l) if (o is Dictionary<string, object> x) r.Add(x);
            return r;
        }

        public static List<Dictionary<string, object>> DL(object o)
        {
            var r = new List<Dictionary<string, object>>();
            if (o is List<object> l) foreach (var x in l) if (x is Dictionary<string, object> dd) r.Add(dd);
            return r;
        }
    }
}
