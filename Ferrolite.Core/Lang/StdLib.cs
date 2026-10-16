using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Ferrolite.Core.Lang;






public interface IScriptGuiBridge
{
    int CreateOverlay(string title, int width, int height);
    int AddLabel(int overlayId, string text, int x, int y);
    int AddButton(int overlayId, string text, int x, int y);
    int AddToggle(int overlayId, string text, int x, int y, bool @default);
    void BeginSession(int sessionId);
    void CloseSession(int sessionId);
    void SetText(int overlayId, int elementId, string text);
    void SetElementPosition(int overlayId, int elementId, int x, int y);
    bool ButtonClicked(int overlayId, int elementId);
    bool GetToggle(int overlayId, int elementId);
    void RemoveElement(int overlayId, int elementId);
    void SetAlwaysOnTop(int overlayId, bool enabled);
    void SetPosition(int overlayId, int x, int y);
    void SetVisible(int overlayId, bool visible);
    void CloseOverlay(int overlayId);
}

public static class StdLib
{
    
    private delegate void RegFunc(string name, Func<object?[], object?> fn, int? arity = null);

    public static void BuildGlobals(Environment env, Interpreter interp)
    {
        
        void Reg(string name, Func<object?[], object?> fn, int? arity = null)
        {
            env.Define(name, new NativeFunction(name, fn, arity));
        }

        
        Reg("print", args => { interp.Log(string.Join(" ", args.Select(ValueHelpers.VsRepr))); return null; });
        Reg("log", args => { interp.Log(string.Join(" ", args.Select(ValueHelpers.VsRepr))); return null; });
        Reg("debug", args => { interp.Log("[DEBUG] " + string.Join(" ", args.Select(ValueHelpers.VsRepr))); return null; });

        
        Reg("typeof", args => ValueHelpers.VsTypeName(args[0]));
        Reg("str", args => ValueHelpers.VsRepr(args[0]));
        Reg("num", args => {
            var v = args[0];
            if (v is string s) { s = s.Trim(); return s.Contains('.') || s.Contains('e', StringComparison.OrdinalIgnoreCase) ? double.Parse(s, CultureInfo.InvariantCulture) : (double)int.Parse(s); }
            return v;
        });
        Reg("bool", args => ValueHelpers.IsTruthy(args[0]));
        Reg("len", args => args[0] switch {
            List<object?> l => (double)l.Count, string s => (double)s.Length,
            Dictionary<object?, object?> d => (double)d.Count,
            _ => throw new VSRuntimeError($"len() not supported for {ValueHelpers.VsTypeName(args[0])}"),
        });
        Reg("assert", args => { if (!ValueHelpers.IsTruthy(args[0])) throw new VSRuntimeError(args.Length > 1 ? ValueHelpers.VsRepr(args[1])! : "Assertion failed"); return true; });
        Reg("range", args => {
            if (args.Length == 1) return (object?)Enumerable.Range(0, (int)Convert.ToDouble(args[0])).Select(x => (object?)((double)x)).ToList();
            if (args.Length == 2) { int start = (int)Convert.ToDouble(args[0]), end = (int)Convert.ToDouble(args[1]); return (object?)Enumerable.Range(start, end - start).Select(x => (object?)((double)x)).ToList(); }
            if (args.Length == 3) { int start = (int)Convert.ToDouble(args[0]), end = (int)Convert.ToDouble(args[1]), step = (int)Convert.ToDouble(args[2]); return (object?)Enumerable.Range(0, (end - start) / step).Select(x => (object?)((double)(start + x * step))).ToList(); }
            throw new VSRuntimeError("range() takes 1-3 arguments");
        });

        
        Reg("wait", args => { interp.SleepScaled(Convert.ToDouble(args[0])); return null; });
        Reg("sleep", args => { interp.SleepScaled(Convert.ToDouble(args[0])); return null; });
        Reg("delay", args => { interp.SleepScaled(Convert.ToDouble(args[0])); return null; });
        Reg("now", _ => (object?)((double)DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        Reg("now_ms", _ => (object?)((double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        Reg("current_hour", _ => (object?)((double)DateTime.Now.Hour));
        Reg("current_minute", _ => (object?)((double)DateTime.Now.Minute));
        Reg("current_time_string", _ => (object?)DateTime.Now.ToString("hh:mm tt"));
        Reg("wait_until", args => {
            int hour = Convert.ToInt32(args[0]); int minute = args.Length > 1 ? Convert.ToInt32(args[1]) : 0; int second = args.Length > 2 ? Convert.ToInt32(args[2]) : 0;
            if (args.Length > 3 && args[3] != null) { string m = args[3]!.ToString()!.ToLower(); if (m != "am" && m != "pm") throw new VSRuntimeError("wait_until(): meridiem must be 'AM' or 'PM'"); if (hour < 1 || hour > 12) throw new VSRuntimeError("wait_until(): hour must be 1-12 when AM/PM is given"); hour %= 12; if (m == "pm") hour += 12; }
            var target = DateTime.Today.AddHours(hour).AddMinutes(minute).AddSeconds(second); if (target <= DateTime.Now) target = target.AddDays(1);
            interp.SleepScaled((target - DateTime.Now).TotalSeconds); return null;
        });
        Reg("debug_break", _ => { interp.Log("[DEBUG] Breakpoint reached"); return null; });
        Reg("debug_locals", _ => { interp.Log("[DEBUG] Local variables: (requires environment access)"); return null; });
        Reg("profile_start", _ => { interp.Stats["__profile_start"] = DateTime.Now; interp.Stats["__profile_stmts"] = (int)interp.Stats["statements_executed"]; return null; });
        Reg("profile_end", _ => {
            if (!interp.Stats.ContainsKey("__profile_start")) return (object?)new Dictionary<object?, object?> { ["error"] = "Profiling not started" };
            var elapsed = (DateTime.Now - (DateTime)interp.Stats["__profile_start"]!).TotalSeconds; int stmts = (int)interp.Stats["statements_executed"] - (int)interp.Stats["__profile_stmts"];
            double rate = elapsed > 0 ? stmts / elapsed : 0; interp.Log($"[PROFILE] Time: {elapsed:F3}s, Statements: {stmts}, Rate: {rate:F0}/s");
            return (object?)new Dictionary<object?, object?> { ["time_ms"] = elapsed * 1000, ["statements"] = (double)stmts, ["statements_per_second"] = rate };
        });

        
        Reg("abs", args => (object?)Math.Abs(Convert.ToDouble(args[0])));
        Reg("floor", args => (object?)Math.Floor(Convert.ToDouble(args[0])));
        Reg("ceil", args => (object?)Math.Ceiling(Convert.ToDouble(args[0])));
        Reg("round", args => args.Length > 1 ? (object?)Math.Round(Convert.ToDouble(args[0]), Convert.ToInt32(args[1])) : (object?)Math.Round(Convert.ToDouble(args[0])));
        Reg("sqrt", args => (object?)Math.Sqrt(Convert.ToDouble(args[0])));
        Reg("pow", args => (object?)Math.Pow(Convert.ToDouble(args[0]), Convert.ToDouble(args[1])));
        Reg("min", args => { if (args.Length == 1 && args[0] is List<object?> lst) return lst.Min(x => Convert.ToDouble(x)); return args.Min(x => Convert.ToDouble(x)); });
        Reg("max", args => { if (args.Length == 1 && args[0] is List<object?> lst) return lst.Max(x => Convert.ToDouble(x)); return args.Max(x => Convert.ToDouble(x)); });
        Reg("sum", args => { var lst = (List<object?>)args[0]!; return (object?)lst.Sum(x => Convert.ToDouble(x)); });
        Reg("avg", args => { var lst = (List<object?>)args[0]!; return (object?)lst.Average(x => Convert.ToDouble(x)); });
        Reg("sin", args => (object?)Math.Sin(Convert.ToDouble(args[0])));
        Reg("cos", args => (object?)Math.Cos(Convert.ToDouble(args[0])));
        Reg("tan", args => (object?)Math.Tan(Convert.ToDouble(args[0])));
        Reg("log", args => args.Length > 1 ? (object?)Math.Log(Convert.ToDouble(args[0]), Convert.ToDouble(args[1])) : (object?)Math.Log(Convert.ToDouble(args[0])));
        Reg("pi", _ => (object?)Math.PI);
        Reg("clamp", args => (object?)Math.Max(Convert.ToDouble(args[1]), Math.Min(Convert.ToDouble(args[2]), Convert.ToDouble(args[0]))));
        Reg("random", _ => (object?)Random.Shared.NextDouble());
        Reg("random_int", args => (object?)((double)Random.Shared.Next(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]) + 1)));
        Reg("random_choice", args => args[0] is List<object?> lst ? lst[Random.Shared.Next(lst.Count)] : throw new VSRuntimeError("random_choice() expects a list"));
        Reg("random_float", args => { double val = Random.Shared.NextDouble() * (Convert.ToDouble(args[1]) - Convert.ToDouble(args[0])) + Convert.ToDouble(args[0]); return val; });

        
        Reg("acos", args => (object?)Math.Acos(Convert.ToDouble(args[0])));
        Reg("asin", args => (object?)Math.Asin(Convert.ToDouble(args[0])));
        Reg("atan", args => (object?)Math.Atan(Convert.ToDouble(args[0])));
        Reg("atan2", args => (object?)Math.Atan2(Convert.ToDouble(args[0]), Convert.ToDouble(args[1])));
        Reg("sinh", args => (object?)Math.Sinh(Convert.ToDouble(args[0])));
        Reg("cosh", args => (object?)Math.Cosh(Convert.ToDouble(args[0])));
        Reg("tanh", args => (object?)Math.Tanh(Convert.ToDouble(args[0])));
        Reg("gcd", args => { long a2 = (long)Math.Abs(Convert.ToDouble(args[0])); long b2 = (long)Math.Abs(Convert.ToDouble(args[1])); while (b2 != 0) { long t = b2; b2 = a2 % b2; a2 = t; } return (object?)((double)a2); });
        Reg("lcm", args => { long a3 = (long)Math.Abs(Convert.ToDouble(args[0])); long b3 = (long)Math.Abs(Convert.ToDouble(args[1])); return (object?)((double)(a3 / GCD(a3, b3) * b3)); });
        Reg("factorial", args => { int n = Convert.ToInt32(args[0]); double r = 1; for (int i = 2; i <= n; i++) r *= i; return (object?)r; });
        Reg("is_prime", args => { int n = Convert.ToInt32(args[0]); if (n < 2) return false; if (n == 2) return true; if (n % 2 == 0) return false; for (int i = 3; i * i <= n; i += 2) if (n % i == 0) return false; return true; });
        Reg("lerp", args => (object?)(Convert.ToDouble(args[0]) + (Convert.ToDouble(args[1]) - Convert.ToDouble(args[0])) * Convert.ToDouble(args[2])));
        Reg("map_range", args => (object?)((Convert.ToDouble(args[0]) - Convert.ToDouble(args[1])) / (Convert.ToDouble(args[2]) - Convert.ToDouble(args[1])) * (Convert.ToDouble(args[4]) - Convert.ToDouble(args[3])) + Convert.ToDouble(args[3])));
        Reg("median", args => { var sorted = ((List<object?>)args[0]!).Select(x => Convert.ToDouble(x)).OrderBy(x => x).ToList(); int mid = sorted.Count / 2; return (object?)(sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid]); });
        Reg("stdev", args => { var lst = ((List<object?>)args[0]!).Select(x => Convert.ToDouble(x)).ToList(); double avg = lst.Average(); double variance = lst.Sum(x => (x - avg) * (x - avg)) / lst.Count; return (object?)Math.Sqrt(variance); });

        
        Reg("reverse_str", args => (object?)new string(args[0]!.ToString()!.Reverse().ToArray()));
        Reg("count_str", args => (object?)((double)args[0]!.ToString()!.Split(args[1]!.ToString()!).Length - 1));
        Reg("title_case", args => (object?)System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(args[0]!.ToString()!.ToLower()));
        Reg("camel_case", args => { var parts = args[0]!.ToString()!.Split('_', '-', ' '); return (object?)(parts[0].ToLower() + string.Concat(parts.Skip(1).Select(p => char.ToUpper(p[0]) + p[1..]))); });
        Reg("snake_case", args => { var s = args[0]!.ToString()!; var result = new System.Text.StringBuilder(); for (int i = 0; i < s.Length; i++) { if (char.IsUpper(s[i]) && i > 0) result.Append('_'); result.Append(char.ToLower(s[i])); } return (object?)result.ToString(); });
        Reg("is_number", args => (object?)double.TryParse(args[0]!.ToString(), out _));
        Reg("is_alpha", args => (object?)args[0]!.ToString()!.All(char.IsLetter));
        Reg("is_digit", args => (object?)args[0]!.ToString()!.All(char.IsDigit));
        Reg("char_code", args => (object?)((double)args[0]!.ToString()![0]));
        Reg("char_from", args => ((char)Convert.ToInt32(args[0])).ToString());
        Reg("format", args => { string tpl = args[0]!.ToString()!; for (int i = 1; i < args.Length; i++) tpl = tpl.Replace($"{{{i - 1}}}", ValueHelpers.VsRepr(args[i])); return (object?)tpl; });
        Reg("escape_html", args => (object?)System.Net.WebUtility.HtmlEncode(args[0]!.ToString()));
        Reg("unescape_html", args => (object?)System.Net.WebUtility.HtmlDecode(args[0]!.ToString()));
        Reg("mask", args => { string s = args[0]!.ToString()!; int vis = args.Length > 1 ? Convert.ToInt32(args[1]) : 4; return (object?)(new string('*', Math.Max(0, s.Length - vis)) + s[^Math.Min(vis, s.Length)..]); });

        
        Reg("find", args => { var lst = (List<object?>)args[0]!; var fn = args[1]; foreach (var x in lst) { if (ValueHelpers.IsTruthy(CallUserFn(interp, fn, new[] { x }))) return x; } return null; });
        Reg("find_index", args => { var lst = (List<object?>)args[0]!; var fn = args[1]; for (int i = 0; i < lst.Count; i++) { if (ValueHelpers.IsTruthy(CallUserFn(interp, fn, new[] { lst[i] }))) return (object?)((double)i); } return (object?)((double)(-1)); });
        Reg("group_by", args => { var lst = (List<object?>)args[0]!; var fn = args[1]; var groups = new Dictionary<object?, object?>(); foreach (var x in lst) { var key = CallUserFn(interp, fn, new[] { x }); string k = ValueHelpers.VsRepr(key)!; if (!groups.ContainsKey(k)) groups[k] = new List<object?> { x }; else ((List<object?>)groups[k]!).Add(x); } return (object?)groups; });
        Reg("chunk", args => { var lst = (List<object?>)args[0]!; int size = Convert.ToInt32(args[1]); var result = new List<object?>(); for (int i = 0; i < lst.Count; i += size) result.Add(lst.Skip(i).Take(size).ToList()); return (object?)result; });
        Reg("flatten_deep", args => { var lst = (List<object?>)args[0]!; var result = new List<object?>(); foreach (var x in lst) { if (x is List<object?> l) result.AddRange(l); else result.Add(x); } return (object?)result; });
        Reg("take", args => (object?)((List<object?>)args[0]!).Take(Convert.ToInt32(args[1])).ToList());
        Reg("drop", args => (object?)((List<object?>)args[0]!).Skip(Convert.ToInt32(args[1])).ToList());
        Reg("rotate", args => { var lst = (List<object?>)args[0]!; int n = Convert.ToInt32(args[1]); int c = lst.Count; if (c == 0) return (object?)new List<object?>(); int s = n % c; if (s < 0) s += c; return (object?)lst.Skip(s).Concat(lst.Take(s)).ToList(); });
        Reg("shuffle", args => { var lst = ((List<object?>)args[0]!).ToList(); for (int i = lst.Count - 1; i > 0; i--) { int j = Random.Shared.Next(i + 1); (lst[i], lst[j]) = (lst[j], lst[i]); } return (object?)lst; });
        Reg("sample", args => { var lst = (List<object?>)args[0]!; int n = Convert.ToInt32(args[1]); var shuffled = lst.OrderBy(x => Random.Shared.Next()).ToList(); return (object?)shuffled.Take(n).ToList(); });

        
        Reg("map_map", args => { var m = (Dictionary<object?, object?>)args[0]!; var fn = args[1]; return (object?)m.ToDictionary(kv => kv.Key, kv => CallUserFn(interp, fn, new[] { kv.Value })); });
        Reg("map_filter", args => { var m = (Dictionary<object?, object?>)args[0]!; var fn = args[1]; return (object?)m.Where(kv => ValueHelpers.IsTruthy(CallUserFn(interp, fn, new[] { kv.Value }))).ToDictionary(kv => kv.Key, kv => kv.Value); });
        Reg("invert", args => (object?)((Dictionary<object?, object?>)args[0]!).ToDictionary(kv => kv.Value, kv => kv.Key));
        Reg("pick", args => { var m = (Dictionary<object?, object?>)args[0]!; var keys = ((List<object?>)args[1]!).Select(k => k!.ToString()!); return (object?)m.Where(kv => keys.Contains(kv.Key!.ToString()!)).ToDictionary(kv => kv.Key, kv => kv.Value); });
        Reg("to_pairs", args => (object?)((Dictionary<object?, object?>)args[0]!).Select(kv => (object?)new List<object?> { kv.Key, kv.Value }).ToList());

        
        Reg("format_date", args => (object?)DateTime.Now.ToString(args[0]!.ToString()));
        Reg("today", _ => (object?)DateTime.Now.ToString("yyyy-MM-dd"));
        Reg("timestamp", _ => (object?)((double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        Reg("date_diff", args => (object?)((Convert.ToDouble(args[0]) - Convert.ToDouble(args[1]))));
        Reg("timer", args => { if (args.Length == 0 || !ValueHelpers.IsTruthy(args[0])) { interp.Stats["__timer"] = Stopwatch.GetTimestamp(); return null; } return (object?)((Stopwatch.GetTimestamp() - (long)interp.Stats["__timer"]!) * 1000.0 / Stopwatch.Frequency); });

        
        Reg("base64_encode", args => (object?)Convert.ToBase64String(Encoding.UTF8.GetBytes(args[0]!.ToString()!)));
        Reg("base64_decode", args => (object?)Encoding.UTF8.GetString(Convert.FromBase64String(args[0]!.ToString()!)));
        Reg("hex_encode", args => (object?)BitConverter.ToString(Encoding.UTF8.GetBytes(args[0]!.ToString()!)).Replace("-", "").ToLower());
        Reg("hex_decode", args => { string hex = args[0]!.ToString()!; byte[] bytes = new byte[hex.Length / 2]; for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16); return (object?)Encoding.UTF8.GetString(bytes); });
        Reg("md5", args => { using var md5 = System.Security.Cryptography.MD5.Create(); return (object?)BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(args[0]!.ToString()!))).Replace("-", "").ToLower(); });
        Reg("sha256", args => { using var sha = System.Security.Cryptography.SHA256.Create(); return (object?)BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(args[0]!.ToString()!))).Replace("-", "").ToLower(); });
        Reg("uuid", _ => (object?)Guid.NewGuid().ToString());
        Reg("encode_url", args => (object?)Uri.EscapeDataString(args[0]!.ToString()!));
        Reg("decode_url", args => (object?)Uri.UnescapeDataString(args[0]!.ToString()!));

        
        Reg("open_url", args => { Process.Start(new ProcessStartInfo(args[0]!.ToString()!) { UseShellExecute = true }); return null; });
        Reg("open_file", args => { Process.Start(new ProcessStartInfo(args[0]!.ToString()!) { UseShellExecute = true }); return null; });
        Reg("desktop_path", _ => (object?)System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop));
        Reg("documents_path", _ => (object?)System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments));
        Reg("downloads_path", _ => (object?)Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads"));
        Reg("temp_path", _ => (object?)Path.GetTempPath());
        Reg("cpu_usage", _ => { using var pc = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total"); pc.NextValue(); Thread.Sleep(500); return (object?)pc.NextValue(); });
        Reg("memory_info", _ => { var gc = GC.GetTotalMemory(false); return (object?)new Dictionary<object?, object?> { ["used_bytes"] = (double)gc, ["used_mb"] = gc / 1048576.0 }; });

        
        var httpClient2 = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        Reg("http_put", args => { try { var req = new HttpRequestMessage(HttpMethod.Put, args[0]!.ToString()!); req.Content = new StringContent(args.Length > 1 ? args[1]!.ToString()! : "", Encoding.UTF8, "application/json"); var resp = httpClient2.SendAsync(req).GetAwaiter().GetResult(); return (object?)new Dictionary<object?, object?> { ["status"] = (double)(int)resp.StatusCode, ["body"] = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() }; } catch (Exception e) { throw new VSRuntimeError($"http_put error: {e.Message}"); } });
        Reg("http_delete", args => { try { var req = new HttpRequestMessage(HttpMethod.Delete, args[0]!.ToString()!); var resp = httpClient2.SendAsync(req).GetAwaiter().GetResult(); return (object?)new Dictionary<object?, object?> { ["status"] = (double)(int)resp.StatusCode, ["body"] = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() }; } catch (Exception e) { throw new VSRuntimeError($"http_delete error: {e.Message}"); } });
        Reg("http_download", args => { try { using var resp = httpClient2.GetAsync(args[0]!.ToString()!).GetAwaiter().GetResult(); resp.EnsureSuccessStatusCode(); File.WriteAllBytes(args[1]!.ToString()!, resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()); return (object?)true; } catch (Exception e) { throw new VSRuntimeError($"http_download error: {e.Message}"); } });

        
        Reg("db_open", args => { try { var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={args[0]!.ToString()}"); conn.Open(); return (object?)new Dictionary<object?, object?> { ["_conn"] = conn }; } catch (Exception e) { throw new VSRuntimeError($"db_open error: {e.Message}"); } });
        Reg("db_query", args => { try { var conn = ((Dictionary<object?, object?>)args[0]!)["_conn"] as Microsoft.Data.Sqlite.SqliteConnection ?? throw new VSRuntimeError("Invalid db handle"); var cmd = conn.CreateCommand(); cmd.CommandText = args[1]!.ToString()!; var reader = cmd.ExecuteReader(); var rows = new List<object?>(); while (reader.Read()) { var row = new Dictionary<object?, object?>(); for (int i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i); rows.Add(row); } return (object?)rows; } catch (Exception e) { throw new VSRuntimeError($"db_query error: {e.Message}"); } });
        Reg("db_execute", args => { try { var conn = ((Dictionary<object?, object?>)args[0]!)["_conn"] as Microsoft.Data.Sqlite.SqliteConnection ?? throw new VSRuntimeError("Invalid db handle"); var cmd = conn.CreateCommand(); cmd.CommandText = args[1]!.ToString()!; return (object?)((double)cmd.ExecuteNonQuery()); } catch (Exception e) { throw new VSRuntimeError($"db_execute error: {e.Message}"); } });
        Reg("db_close", args => { try { var conn = ((Dictionary<object?, object?>)args[0]!)["_conn"] as Microsoft.Data.Sqlite.SqliteConnection; conn?.Close(); conn?.Dispose(); return null; } catch (Exception e) { throw new VSRuntimeError($"db_close error: {e.Message}"); } });

        
        Dictionary<object?, object?> PxToColor(List<object> c) => new() { ["r"] = Convert.ToDouble(c[0]), ["g"] = Convert.ToDouble(c[1]), ["b"] = Convert.ToDouble(c[2]), ["hex"] = $"#{Convert.ToInt32(c[0]):X2}{Convert.ToInt32(c[1]):X2}{Convert.ToInt32(c[2]):X2}".ToLower() };
        Reg("get_pixel", args => (object?)PxToColor(interp.ActionBackend!.GetPixelColor(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]))));
        Reg("get_pixel_r", args => (object?)Convert.ToDouble(interp.ActionBackend!.GetPixelColor(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]))[0]));
        Reg("get_pixel_g", args => (object?)Convert.ToDouble(interp.ActionBackend!.GetPixelColor(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]))[1]));
        Reg("get_pixel_b", args => (object?)Convert.ToDouble(interp.ActionBackend!.GetPixelColor(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]))[2]));
        Reg("get_pixel_block", args => {
            int sx = Convert.ToInt32(args[0]), sy = Convert.ToInt32(args[1]), w = Convert.ToInt32(args[2]), h = Convert.ToInt32(args[3]);
            var result = new List<object?>();
            for (int y = sy; y < sy + h; y++) {
                var row = new List<object?>();
                for (int x = sx; x < sx + w; x++)
                    row.Add(PxToColor(interp.ActionBackend!.GetPixelColor(x, y)));
                result.Add(row);
            }
            return (object?)result;
        });
        Reg("color_distance", args => {
            var c1 = (Dictionary<object?, object?>)args[0]!; var c2 = (Dictionary<object?, object?>)args[1]!;
            double dr = Convert.ToDouble(c1["r"]) - Convert.ToDouble(c2["r"]), dg = Convert.ToDouble(c1["g"]) - Convert.ToDouble(c2["g"]), db = Convert.ToDouble(c1["b"]) - Convert.ToDouble(c2["b"]);
            return (object?)Math.Sqrt(dr * dr + dg * dg + db * db);
        });
        Reg("wait_for_color", args => {
            int x = Convert.ToInt32(args[0]), y = Convert.ToInt32(args[1]), r = Convert.ToInt32(args[2]), g = Convert.ToInt32(args[3]), b = Convert.ToInt32(args[4]);
            double tol = args.Length > 5 ? Convert.ToDouble(args[5]) : 10.0, timeout = args.Length > 6 ? Convert.ToDouble(args[6]) : 10.0;
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < timeout) {
                var px = interp.ActionBackend!.GetPixelColor(x, y);
                if (Math.Abs(Convert.ToDouble(px[0]) - r) <= tol && Math.Abs(Convert.ToDouble(px[1]) - g) <= tol && Math.Abs(Convert.ToDouble(px[2]) - b) <= tol) return (object?)true;
                interp.SleepScaled(0.05);
            }
            return (object?)false;
        });
        Reg("wait_for_color_gone", args => {
            int x = Convert.ToInt32(args[0]), y = Convert.ToInt32(args[1]), r = Convert.ToInt32(args[2]), g = Convert.ToInt32(args[3]), b = Convert.ToInt32(args[4]);
            double tol = args.Length > 5 ? Convert.ToDouble(args[5]) : 10.0, timeout = args.Length > 6 ? Convert.ToDouble(args[6]) : 10.0;
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < timeout) {
                var px = interp.ActionBackend!.GetPixelColor(x, y);
                if (!(Math.Abs(Convert.ToDouble(px[0]) - r) <= tol && Math.Abs(Convert.ToDouble(px[1]) - g) <= tol && Math.Abs(Convert.ToDouble(px[2]) - b) <= tol)) return (object?)true;
                interp.SleepScaled(0.05);
            }
            return (object?)false;
        });
        Reg("color_in_region", args => {
            int sx = Convert.ToInt32(args[0]), sy = Convert.ToInt32(args[1]), w = Convert.ToInt32(args[2]), h = Convert.ToInt32(args[3]);
            int r = Convert.ToInt32(args[4]), g = Convert.ToInt32(args[5]), b = Convert.ToInt32(args[6]);
            double tol = args.Length > 7 ? Convert.ToDouble(args[7]) : 10.0;
            int matchThreshold = args.Length > 8 ? Convert.ToInt32(args[8]) : 50;
            int total = 0, matches = 0;
            for (int y = sy; y < sy + h; y += 3) { for (int x = sx; x < sx + w; x += 3) {
                var px = interp.ActionBackend!.GetPixelColor(x, y);
                if (Math.Abs(Convert.ToDouble(px[0]) - r) <= tol && Math.Abs(Convert.ToDouble(px[1]) - g) <= tol && Math.Abs(Convert.ToDouble(px[2]) - b) <= tol) matches++;
                total++;
            }}
            return (object?)(total > 0 ? (matches * 100.0 / total >= matchThreshold) : false);
        });
        Reg("find_color", args => {
            int r = Convert.ToInt32(args[0]), g = Convert.ToInt32(args[1]), b = Convert.ToInt32(args[2]);
            double tol = args.Length > 3 ? Convert.ToDouble(args[3]) : 10.0;
            var (sw, sh) = interp.ActionBackend!.ScreenSize();
            int step = args.Length > 4 ? Math.Max(1, Convert.ToInt32(args[4])) : 4;
            for (int y = 0; y < sh; y += step) { for (int x = 0; x < sw; x += step) {
                var px = interp.ActionBackend!.GetPixelColor(x, y);
                if (Math.Abs(Convert.ToDouble(px[0]) - r) <= tol && Math.Abs(Convert.ToDouble(px[1]) - g) <= tol && Math.Abs(Convert.ToDouble(px[2]) - b) <= tol)
                    return (object?)new Dictionary<object?, object?> { ["x"] = (double)x, ["y"] = (double)y };
            }}
            return null;
        });
        Reg("find_color_region", args => {
            int sx = Convert.ToInt32(args[0]), sy = Convert.ToInt32(args[1]), w = Convert.ToInt32(args[2]), h = Convert.ToInt32(args[3]);
            int r = Convert.ToInt32(args[4]), g = Convert.ToInt32(args[5]), b = Convert.ToInt32(args[6]);
            double tol = args.Length > 7 ? Convert.ToDouble(args[7]) : 10.0;
            int step = args.Length > 8 ? Math.Max(1, Convert.ToInt32(args[8])) : 2;
            for (int y = sy; y < sy + h; y += step) { for (int x = sx; x < sx + w; x += step) {
                var px = interp.ActionBackend!.GetPixelColor(x, y);
                if (Math.Abs(Convert.ToDouble(px[0]) - r) <= tol && Math.Abs(Convert.ToDouble(px[1]) - g) <= tol && Math.Abs(Convert.ToDouble(px[2]) - b) <= tol)
                    return (object?)new Dictionary<object?, object?> { ["x"] = (double)x, ["y"] = (double)y };
            }}
            return null;
        });
        Reg("save_screenshot", args => { interp.ActionBackend!.Screenshot(args[0]!.ToString()!); return (object?)args[0]!.ToString()!; });
        Reg("compare_images", args => {
            string p1 = args[0]!.ToString()!, p2 = args[1]!.ToString()!;
            double tol = args.Length > 2 ? Convert.ToDouble(args[2]) : 30.0;
            using var img1 = System.Drawing.Image.FromFile(p1) as System.Drawing.Bitmap;
            using var img2 = System.Drawing.Image.FromFile(p2) as System.Drawing.Bitmap;
            if (img1 == null || img2 == null) throw new VSRuntimeError("compare_images: cannot load images");
            int w = Math.Min(img1.Width, img2.Width), h = Math.Min(img1.Height, img2.Height);
            int total = 0, match = 0;
            for (int y = 0; y < h; y += 2) { for (int x = 0; x < w; x += 2) {
                var c1 = img1.GetPixel(x, y); var c2 = img2.GetPixel(x, y);
                double dist = Math.Sqrt(Math.Pow(c1.R - c2.R, 2) + Math.Pow(c1.G - c2.G, 2) + Math.Pow(c1.B - c2.B, 2));
                if (dist <= tol) match++;
                total++;
            }}
            return (object?)(total > 0 ? (match * 100.0 / total) : 0.0);
        });

        
        Reg("upper", args => (object?)args[0]!.ToString()!.ToUpper());
        Reg("lower", args => (object?)args[0]!.ToString()!.ToLower());
        Reg("trim", args => (object?)args[0]!.ToString()!.Trim());
        Reg("ltrim", args => (object?)args[0]!.ToString()!.TrimStart());
        Reg("rtrim", args => (object?)args[0]!.ToString()!.TrimEnd());
        Reg("split", args => (object?)args[0]!.ToString()!.Split(args.Length > 1 ? args[1]!.ToString()! : " ").Select(x => (object?)x).ToList());
        Reg("join", args => { string sep = args.Length > 1 ? args[1]!.ToString()! : ""; return (object?)string.Join(sep, ((List<object?>)args[0]!).Select(ValueHelpers.VsRepr)); });
        Reg("replace", args => (object?)args[0]!.ToString()!.Replace(args[1]!.ToString()!, args[2]!.ToString()!));
        Reg("contains_str", args => (object?)args[0]!.ToString()!.Contains(args[1]!.ToString()!));
        Reg("starts_with", args => (object?)args[0]!.ToString()!.StartsWith(args[1]!.ToString()!));
        Reg("ends_with", args => (object?)args[0]!.ToString()!.EndsWith(args[1]!.ToString()!));
        Reg("substring", args => args.Length > 2 && args[2] != null ? (object?)args[0]!.ToString()!.Substring(Convert.ToInt32(args[1]), Convert.ToInt32(args[2]) - Convert.ToInt32(args[1])) : (object?)args[0]!.ToString()!.Substring(Convert.ToInt32(args[1])));
        Reg("char_at", args => (object?)args[0]!.ToString()![Convert.ToInt32(args[1])].ToString());
        Reg("index_of", args => (object?)((double)args[0]!.ToString()!.IndexOf(args[1]!.ToString()!)));
        Reg("repeat", args => (object?)string.Concat(Enumerable.Repeat(args[0]!.ToString()!, Convert.ToInt32(args[1]))));
        Reg("pad_left", args => (object?)args[0]!.ToString()!.PadLeft(Convert.ToInt32(args[1]), args.Length > 2 ? args[2]!.ToString()![0] : ' '));
        Reg("pad_right", args => (object?)args[0]!.ToString()!.PadRight(Convert.ToInt32(args[1]), args.Length > 2 ? args[2]!.ToString()![0] : ' '));
        Reg("format_number", args => (object?)Convert.ToDouble(args[0]).ToString($"F{Convert.ToInt32(args.Length > 1 ? args[1] : 2)}"));
        Reg("to_string", args => ValueHelpers.VsRepr(args[0]));
        Reg("split_lines", args => (object?)args[0]!.ToString()!.Split('\n').Select(x => (object?)x).ToList());
        Reg("trim_start", args => (object?)args[0]!.ToString()!.TrimStart());
        Reg("trim_end", args => (object?)args[0]!.ToString()!.TrimEnd());
        Reg("to_lower", args => (object?)args[0]!.ToString()!.ToLower());
        Reg("to_upper", args => (object?)args[0]!.ToString()!.ToUpper());
        Reg("is_empty", args => (object?)(args[0]!.ToString()!.Length == 0));

        
        Reg("regex_match", args => { var m = Regex.Match(args[1]!.ToString()!, args[0]!.ToString()!); return m.Success ? (object?)m.Value : null; });
        Reg("regex_match_all", args => (object?)Regex.Matches(args[1]!.ToString()!, args[0]!.ToString()!).Select(m => (object?)m.Value).ToList());
        Reg("regex_replace", args => (object?)Regex.Replace(args[1]!.ToString()!, args[0]!.ToString()!, args[2]!.ToString()!));
        Reg("regex_test", args => (object?)Regex.IsMatch(args[1]!.ToString()!, args[0]!.ToString()!));

        
        Reg("push", args => { ((List<object?>)args[0]!).Add(args[1]); return args[0]; });
        Reg("append", args => { ((List<object?>)args[0]!).Add(args[1]); return args[0]; });
        Reg("pop", args => { var lst = (List<object?>)args[0]!; if (args.Length > 1 && args[1] != null) { int idx = Convert.ToInt32(args[1]); object? val = lst[idx]; lst.RemoveAt(idx); return val; } object? last = lst[^1]; lst.RemoveAt(lst.Count - 1); return last; });
        Reg("insert", args => { ((List<object?>)args[0]!).Insert(Convert.ToInt32(args[1]), args[2]); return args[0]; });
        Reg("remove", args => { ((List<object?>)args[0]!).Remove(args[1]); return args[0]; });
        Reg("remove_at", args => { ((List<object?>)args[0]!).RemoveAt(Convert.ToInt32(args[1])); return args[0]; });
        Reg("sort", args => (object?)((List<object?>)args[0]!).OrderBy(x => x).ToList());
        Reg("reverse", args => (object?)((List<object?>)args[0]!).AsEnumerable().Reverse().ToList());
        Reg("slice", args => args.Length > 2 && args[2] != null ? (object?)((List<object?>)args[0]!).GetRange(Convert.ToInt32(args[1]), Convert.ToInt32(args[2]) - Convert.ToInt32(args[1])) : (object?)((List<object?>)args[0]!).Skip(Convert.ToInt32(args[1])).ToList());
        Reg("contains", args => (object?)((List<object?>)args[0]!).Contains(args[1]));
        Reg("unique", args => (object?)((List<object?>)args[0]!).Distinct().ToList());
        Reg("flatten", args => (object?)((List<object?>)args[0]!).SelectMany<object?, object?>(x => x is List<object?> l ? l : new object?[] { x }).ToList());
        Reg("zip_lists", args => (object?)((List<object?>)args[0]!).Zip((List<object?>)args[1]!, (a, b) => (object?)new List<object?> { a, b }).ToList());

        
        Reg("map", args => { var lst = (List<object?>)args[0]!; var fn = args[1]; return (object?)lst.Select(x => CallUserFn(interp, fn, new[] { x })).ToList(); });
        Reg("filter", args => { var lst = (List<object?>)args[0]!; var fn = args[1]; return (object?)lst.Where(x => ValueHelpers.IsTruthy(CallUserFn(interp, fn, new[] { x }))).ToList(); });
        Reg("reduce", args => { var lst = (List<object?>)args[0]!; var fn = args[1]; object? acc = args.Length > 2 ? args[2] : null; var items = lst; if (acc == null && items.Count > 0) { acc = items[0]; items = items.Skip(1).ToList(); } foreach (var x in items) acc = CallUserFn(interp, fn, new[] { acc, x }); return acc; });
        Reg("foreach", args => { var lst = (List<object?>)args[0]!; var fn = args[1]; foreach (var x in lst) CallUserFn(interp, fn, new[] { x }); return null; });

        
        Reg("keys", args => (object?)((Dictionary<object?, object?>)args[0]!).Keys.ToList());
        Reg("values", args => (object?)((Dictionary<object?, object?>)args[0]!).Values.ToList());
        Reg("has_key", args => (object?)((Dictionary<object?, object?>)args[0]!).ContainsKey(args[1]!));
        Reg("delete_key", args => { ((Dictionary<object?, object?>)args[0]!).Remove(args[1]!); return null; });
        Reg("merge", args => { var a = (Dictionary<object?, object?>)args[0]!; var b = (Dictionary<object?, object?>)args[1]!; return (object?)a.Concat(b).ToDictionary(x => x.Key, x => x.Value); });

        
        Reg("set", args => (object?)((List<object?>)args[0]!).Distinct().ToList());
        Reg("set_union", args => (object?)((List<object?>)args[0]!).Concat((List<object?>)args[1]!).Distinct().ToList());
        Reg("set_intersection", args => (object?)((List<object?>)args[0]!).Intersect((List<object?>)args[1]!).ToList());
        Reg("set_difference", args => (object?)((List<object?>)args[0]!).Except((List<object?>)args[1]!).ToList());

        
        Reg("json_parse", args => { try { var doc = JsonDocument.Parse(args[0]!.ToString()!); return JsonElementToObject(doc.RootElement); } catch (Exception e) { throw new VSRuntimeError($"json_parse error: {e.Message}"); } });
        Reg("json_stringify", args => { bool pretty = args.Length > 1 && ValueHelpers.IsTruthy(args[1]); return JsonSerializer.Serialize(args[0], new JsonSerializerOptions { WriteIndented = pretty }); });

        
        Reg("move", args => { interp.ActionBackend?.MouseMove(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), args.Length > 2 ? Convert.ToDouble(args[2]) : 0.0); return null; });
        Reg("mouse_move", args => { interp.ActionBackend?.MouseMove(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), args.Length > 2 ? Convert.ToDouble(args[2]) : 0.0); return null; });
        Reg("move_relative", args => { interp.ActionBackend?.MouseMoveRelative(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]), args.Length > 2 && args[2] != null ? Convert.ToDouble(args[2]) : 0.0, args.Length > 3 && args[3] != null ? Convert.ToInt32(args[3]) : null); return null; });
        Reg("click", args => { interp.ActionBackend?.MouseClick(args.Length > 0 && args[0] != null ? Convert.ToInt32(args[0]) : null, args.Length > 1 && args[1] != null ? Convert.ToInt32(args[1]) : null, args.Length > 2 ? args[2]!.ToString()! : "left"); return null; });
        Reg("double_click", args => { interp.ActionBackend?.MouseDoubleClick(args.Length > 0 && args[0] != null ? Convert.ToInt32(args[0]) : null, args.Length > 1 && args[1] != null ? Convert.ToInt32(args[1]) : null, args.Length > 2 ? args[2]!.ToString()! : "left"); return null; });
        Reg("right_click", args => { interp.ActionBackend?.MouseClick(args.Length > 0 && args[0] != null ? Convert.ToInt32(args[0]) : null, args.Length > 1 && args[1] != null ? Convert.ToInt32(args[1]) : null, "right"); return null; });
        Reg("mouse_down", args => { interp.ActionBackend?.MouseDown(args.Length > 0 ? args[0]!.ToString()! : "left"); return null; });
        Reg("mouse_up", args => { interp.ActionBackend?.MouseUp(args.Length > 0 ? args[0]!.ToString()! : "left"); return null; });
        Reg("drag", args => { interp.ActionBackend?.MouseDrag(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), Convert.ToInt32(args[2]), Convert.ToInt32(args[3]), args.Length > 4 ? Convert.ToDouble(args[4]) : 0.3, args.Length > 5 ? args[5]!.ToString()! : "left"); return null; });
        Reg("scroll", args => { interp.ActionBackend?.MouseScroll(Convert.ToInt32(args[0]), args.Length > 1 && args[1] != null ? Convert.ToInt32(args[1]) : null, args.Length > 2 && args[2] != null ? Convert.ToInt32(args[2]) : null); return null; });
        Reg("get_mouse_pos", _ => (object?)new Dictionary<object?, object?> { ["x"] = (double)interp.ActionBackend!.MousePosition().X, ["y"] = (double)interp.ActionBackend!.MousePosition().Y });
        Reg("mouse_position", _ => (object?)new Dictionary<object?, object?> { ["x"] = (double)interp.ActionBackend!.MousePosition().X, ["y"] = (double)interp.ActionBackend!.MousePosition().Y });
        Reg("is_key_down", args => (object?)interp.ActionBackend!.IsKeyDown(args[0]!.ToString()!));
        Reg("is_key_toggled", args => (object?)interp.ActionBackend!.IsKeyToggled(args[0]!.ToString()!));
        Reg("is_mouse_down", args => (object?)interp.ActionBackend!.IsMouseDown(args.Length > 0 ? args[0]!.ToString()! : "left"));
        Reg("key_press", args => { interp.ActionBackend?.KeyPress(args[0]!.ToString()!); return null; });
        Reg("press_key", args => { interp.ActionBackend?.KeyPress(args[0]!.ToString()!); return null; });
        Reg("key_down", args => { interp.ActionBackend?.KeyDown(args[0]!.ToString()!); return null; });
        Reg("key_up", args => { interp.ActionBackend?.KeyUp(args[0]!.ToString()!); return null; });
        Reg("type_text", args => { interp.ActionBackend?.TypeText(args[0]!.ToString()!, args.Length > 1 ? Convert.ToDouble(args[1]) : 0.0); return null; });
        Reg("type", args => { interp.ActionBackend?.TypeText(args[0]!.ToString()!, args.Length > 1 ? Convert.ToDouble(args[1]) : 0.0); return null; });
        Reg("hotkey", args => { interp.ActionBackend?.Hotkey(args.Select(a => a!.ToString()!).ToList()!); return null; });
        Reg("key_combo", args => { interp.ActionBackend?.Hotkey(args.Select(a => a!.ToString()!).ToList()!); return null; });
        Reg("key_hold", args => { string key = args[0]!.ToString()!; double ms = Convert.ToDouble(args[1]); interp.ActionBackend?.KeyDown(key); interp.SleepScaled(ms / 1000.0); interp.ActionBackend?.KeyUp(key); return null; });
        Reg("hold_start", args => { interp.ActionBackend?.KeyDown(args[0]!.ToString()!); return null; });
        Reg("hold_end", args => { interp.ActionBackend?.KeyUp(args[0]!.ToString()!); return null; });
        Reg("mouse_hold", args => { if (args[0] != null && args[1] != null) interp.ActionBackend?.MouseMove(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), 0.0); string btn = args[2]!.ToString()!; interp.ActionBackend?.MouseDown(btn); interp.SleepScaled(Convert.ToDouble(args[3]) / 1000.0); interp.ActionBackend?.MouseUp(btn); return null; });
        Reg("click_start", args => { if (args.Length > 0 && args[0] != null && args.Length > 1 && args[1] != null) interp.ActionBackend?.MouseMove(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), 0.0); interp.ActionBackend?.MouseDown(args.Length > 2 ? args[2]!.ToString()! : "left"); return null; });
        Reg("click_end", args => { interp.ActionBackend?.MouseUp(args.Length > 0 ? args[0]!.ToString()! : "left"); return null; });
        Reg("focus_window", args => (object?)interp.ActionBackend!.FocusWindow(args[0]!.ToString()!));
        Reg("find_window", args => interp.ActionBackend!.FindWindow(args[0]!.ToString()!));
        Reg("list_windows", _ => interp.ActionBackend!.ListWindows());
        Reg("window_exists", args => (object?)interp.ActionBackend!.FindWindow(args[0]!.ToString()!) != null);
        Reg("minimize_window", args => { interp.ActionBackend?.MinimizeWindow(args[0]!.ToString()!); return null; });
        Reg("maximize_window", args => { interp.ActionBackend?.MaximizeWindow(args[0]!.ToString()!); return null; });
        Reg("close_window", args => { interp.ActionBackend?.CloseWindow(args[0]!.ToString()!); return null; });
        Reg("get_active_window", _ => interp.ActionBackend!.GetActiveWindow());
        Reg("move_window", args => { interp.ActionBackend?.MoveWindow(args[0]!.ToString()!, Convert.ToInt32(args[1]), Convert.ToInt32(args[2]), args.Length > 3 && args[3] != null ? Convert.ToInt32(args[3]) : null, args.Length > 4 && args[4] != null ? Convert.ToInt32(args[4]) : null); return null; });
        Reg("get_pixel_color", args => interp.ActionBackend!.GetPixelColor(Convert.ToInt32(args[0]), Convert.ToInt32(args[1])));
        Reg("screen_size", _ => { var (w, h) = interp.ActionBackend!.ScreenSize(); return (object?)new Dictionary<object?, object?> { ["width"] = (double)w, ["height"] = (double)h }; });
        Reg("screenshot", args => interp.ActionBackend!.Screenshot(args.Length > 0 ? args[0]!.ToString() : null));
        Reg("find_image_on_screen", args => interp.ActionBackend!.FindImageOnScreen(args[0]!.ToString()!, args.Length > 1 ? Convert.ToDouble(args[1]) : 0.9));
        Reg("wait_for_pixel_color", args => (object?)interp.ActionBackend!.WaitForPixelColor(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), (List<object>)args[2]!, args.Length > 3 ? Convert.ToDouble(args[3]) : 10.0));
        Reg("clipboard_get", _ => interp.ActionBackend!.ClipboardGet());
        Reg("clipboard_set", args => { interp.ActionBackend?.ClipboardSet(args[0]!.ToString()!); return null; });

        
        Reg("play_recording", args => {
            string filename = args[0]!.ToString()!; double speed = args.Length > 1 ? Convert.ToDouble(args[1]) : 1.0;
            bool loop = args.Length > 2 && ValueHelpers.IsTruthy(args[2]); int? loopCount = args.Length > 3 && args[3] != null ? Convert.ToInt32(args[3]) : null;
            string path = Path.GetFullPath(filename); if (!File.Exists(path) && !path.EndsWith(".frlrec")) { string candidate = path + ".frlrec"; if (File.Exists(candidate)) path = candidate; }
            if (!File.Exists(path) && !Path.IsPathRooted(filename) && !string.IsNullOrEmpty(RuntimePaths.RecordingsDir)) { string candidate = Path.Combine(RuntimePaths.RecordingsDir, filename); if (File.Exists(candidate)) path = candidate; }
            string json = File.ReadAllText(path); using var doc = JsonDocument.Parse(json); var events = doc.RootElement.GetProperty("events").EnumerateArray().ToList();
            int reps = loopCount ?? (loop ? -1 : 1); if (reps == 0) return null;
            void PlayOnce() { double playbackStart = Stopwatch.GetTimestamp(); foreach (var ev in events) { string type = ev.GetProperty("type").GetString()!; double ts = ev.TryGetProperty("timestamp", out var tsProp) ? tsProp.GetDouble() : 0; double target = playbackStart + ts / speed / interp.SpeedMultiplier; double remaining = (target - Stopwatch.GetTimestamp()) / Stopwatch.Frequency; if (remaining > 0) interp.SleepScaled(remaining); int? x = ev.TryGetProperty("x", out var xp) && xp.ValueKind != JsonValueKind.Null ? xp.GetInt32() : null; int? y = ev.TryGetProperty("y", out var yp) && yp.ValueKind != JsonValueKind.Null ? yp.GetInt32() : null; double? dx = ev.TryGetProperty("dx", out var dxp) && dxp.ValueKind != JsonValueKind.Null ? dxp.GetDouble() : null; double? dy = ev.TryGetProperty("dy", out var dyp) && dyp.ValueKind != JsonValueKind.Null ? dyp.GetDouble() : null; string? button = ev.TryGetProperty("button", out var bp) && bp.ValueKind != JsonValueKind.Null ? bp.GetString() : "left"; string? key = ev.TryGetProperty("key", out var kp) && kp.ValueKind != JsonValueKind.Null ? kp.GetString() : null; int scrollDy = ev.TryGetProperty("scroll_dy", out var sp) ? sp.GetInt32() : 0; switch (type) { case "mouse_move": interp.ActionBackend?.MouseMove(x ?? 0, y ?? 0, 0.0); break; case "mouse_move_relative": interp.ActionBackend?.MouseMoveRelative(dx ?? 0.0, dy ?? 0.0, 0.0, null); break; case "mouse_click": interp.ActionBackend?.MouseClick(x, y, button!); break; case "mouse_down": if (x != null && y != null) interp.ActionBackend?.MouseMove(x.Value, y.Value, 0.0); interp.ActionBackend?.MouseDown(button!); break; case "mouse_up": interp.ActionBackend?.MouseUp(button!); break; case "mouse_scroll": interp.ActionBackend?.MouseScroll(scrollDy, x, y); break;                case "key_down": interp.ActionBackend?.KeyDown(key!); break; case "key_up": interp.ActionBackend?.KeyUp(key!); break; case "controller_button_press": if (interp.ActionBackend is IControllerBackend cbp) cbp.GamepadPress(ev.TryGetProperty("controller_id", out var cip) && cip.ValueKind != JsonValueKind.Null ? cip.GetInt32() : 0, button!); break; case "controller_button_release": if (interp.ActionBackend is IControllerBackend cbr) cbr.GamepadRelease(ev.TryGetProperty("controller_id", out var cir) && cir.ValueKind != JsonValueKind.Null ? cir.GetInt32() : 0, button!); break; case "controller_stick_move": if (interp.ActionBackend is IControllerBackend cbs) cbs.GamepadStick(ev.TryGetProperty("controller_id", out var cis) && cis.ValueKind != JsonValueKind.Null ? cis.GetInt32() : 0, ev.TryGetProperty("button", out var bst) && bst.ValueKind != JsonValueKind.Null ? bst.GetString()! : "left_stick", dx ?? 0.0, dy ?? 0.0); break; case "controller_trigger_move": if (interp.ActionBackend is IControllerBackend cbt) cbt.GamepadTrigger(ev.TryGetProperty("controller_id", out var cit) && cit.ValueKind != JsonValueKind.Null ? cit.GetInt32() : 0, ev.TryGetProperty("button", out var btr) && btr.ValueKind != JsonValueKind.Null ? btr.GetString()! : "left_trigger", ev.TryGetProperty("trigger_value", out var tv) && tv.ValueKind != JsonValueKind.Null ? tv.GetDouble() : 0.0); break; case "controller_dpad_move": if (interp.ActionBackend is IControllerBackend cbd) cbd.GamepadDPad(ev.TryGetProperty("controller_id", out var cid) && cid.ValueKind != JsonValueKind.Null ? cid.GetInt32() : 0, ev.TryGetProperty("dpad_direction", out var dd) && dd.ValueKind != JsonValueKind.Null ? dd.GetString()! : "up"); break; } } }
            if (reps == -1) { while (true) { interp.CheckControl(); PlayOnce(); } } else { for (int i = 0; i < reps; i++) { interp.CheckControl(); PlayOnce(); } }
            return null;
        });

        
        Reg("run_process", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); return b.RunProcess(args[0]!.ToString()!, args.Length > 1 && args[1] != null ? ((List<object?>)args[1]!).Select(a => a!.ToString()!).ToList()! : new List<string>(), args.Length > 2 && ValueHelpers.IsTruthy(args[2])); });
        Reg("launch", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); return b.RunProcess(args[0]!.ToString()!, args.Length > 1 && args[1] != null ? ((List<object?>)args[1]!).Select(a => a!.ToString()!).ToList()! : new List<string>(), args.Length > 2 && ValueHelpers.IsTruthy(args[2])); });
        Reg("kill_process", args => (object?)interp.ActionBackend?.KillProcess(args[0]!.ToString()!));
        Reg("process_exists", args => (object?)interp.ActionBackend!.ProcessExists(args[0]!.ToString()!));
        Reg("list_processes", _ => interp.ActionBackend!.ListProcesses());
        Reg("notify", args => { interp.ActionBackend?.Notify(args[0]!.ToString()!, args[1]!.ToString()!); return null; });
        Reg("show_notification", args => { interp.ActionBackend?.Notify(args[0]!.ToString()!, args[1]!.ToString()!); return null; });
        Reg("beep", args => { interp.ActionBackend?.Beep(args.Length > 0 ? Convert.ToInt32(args[0]) : 800, args.Length > 1 ? Convert.ToInt32(args[1]) : 200); return null; });
        Reg("env_get", args => (object?)System.Environment.GetEnvironmentVariable(args[0]!.ToString()!, System.EnvironmentVariableTarget.Process));

        
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        string SafePath(string p) { if (!Path.IsPathRooted(p)) { string sp = Path.Combine("scripts", p); if (File.Exists(sp) || !File.Exists(p)) return Path.GetFullPath(sp); } return Path.GetFullPath(p); }
        Reg("file_read", args => { try { return File.ReadAllText(SafePath(args[0]!.ToString()!)); } catch (Exception e) { throw new VSRuntimeError($"file_read error: {e.Message}"); } });
        Reg("read_file", args => { try { return File.ReadAllText(SafePath(args[0]!.ToString()!)); } catch (Exception e) { throw new VSRuntimeError($"file_read error: {e.Message}"); } });
        Reg("file_write", args => { try { File.WriteAllText(SafePath(args[0]!.ToString()!), args[1]!.ToString()!); return null; } catch (Exception e) { throw new VSRuntimeError($"file_write error: {e.Message}"); } });
        Reg("write_file", args => { try { File.WriteAllText(SafePath(args[0]!.ToString()!), args[1]!.ToString()!); return null; } catch (Exception e) { throw new VSRuntimeError($"file_write error: {e.Message}"); } });
        Reg("file_append", args => { try { File.AppendAllText(SafePath(args[0]!.ToString()!), args[1]!.ToString()!); return null; } catch (Exception e) { throw new VSRuntimeError($"file_append error: {e.Message}"); } });
        Reg("file_exists", args => (object?)File.Exists(SafePath(args[0]!.ToString()!)));
        Reg("file_delete", args => { try { File.Delete(SafePath(args[0]!.ToString()!)); return null; } catch (Exception e) { throw new VSRuntimeError($"file_delete error: {e.Message}"); } });
        Reg("list_files", args => { try { return (object?)Directory.GetFiles(SafePath(args.Length > 0 ? args[0]!.ToString()! : "scripts")).Select(Path.GetFileName).Select(x => (object?)x).ToList(); } catch (Exception e) { throw new VSRuntimeError($"list_files error: {e.Message}"); } });
        Reg("file_size", args => { try { return (object?)((double)new FileInfo(SafePath(args[0]!.ToString()!)).Length); } catch (Exception e) { throw new VSRuntimeError($"file_size error: {e.Message}"); } });
        Reg("dir_exists", args => (object?)Directory.Exists(SafePath(args[0]!.ToString()!)));
        Reg("make_dir", args => { Directory.CreateDirectory(SafePath(args[0]!.ToString()!)); return (object?)true; });
        Reg("list_dir", args => { try { return (object?)Directory.GetDirectories(SafePath(args.Length > 0 ? args[0]!.ToString()! : "scripts")).Select(Path.GetFileName).Select(x => (object?)x).ToList(); } catch (Exception e) { throw new VSRuntimeError($"list_dir error: {e.Message}"); } });
        Reg("file_append_line", args => { File.AppendAllText(SafePath(args[0]!.ToString()!), ValueHelpers.VsRepr(args[1]) + "\n"); return (object?)true; });
        Reg("csv_read", args => { try { return (object?)File.ReadAllLines(SafePath(args[0]!.ToString()!)).Select(line => (object?)line.Split(',').Select(x => (object?)x).ToList()).ToList(); } catch (Exception e) { throw new VSRuntimeError($"csv_read error: {e.Message}"); } });
        Reg("csv_write", args => { var rows = (List<object?>)args[1]!; File.WriteAllLines(SafePath(args[0]!.ToString()!), rows.Select(r => string.Join(",", ((List<object?>)r!).Select(ValueHelpers.VsRepr)))); return (object?)true; });

        
        Reg("http_get", args => { try { var req = new HttpRequestMessage(HttpMethod.Get, args[0]!.ToString()!); if (args.Length > 1 && args[1] is Dictionary<object?, object?> hdrs) foreach (var kv in hdrs) req.Headers.TryAddWithoutValidation(kv.Key!.ToString()!, kv.Value!.ToString()!); var resp = httpClient.SendAsync(req).GetAwaiter().GetResult(); string body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult(); return (object?)new Dictionary<object?, object?> { ["status"] = (double)(int)resp.StatusCode, ["body"] = body }; } catch (Exception e) { throw new VSRuntimeError($"http_get error: {e.Message}"); } });
        Reg("http_post", args => { try { var req = new HttpRequestMessage(HttpMethod.Post, args[0]!.ToString()!); string ct = "application/x-www-form-urlencoded"; string payload; if (args.Length > 2 && ValueHelpers.IsTruthy(args[2])) { payload = JsonSerializer.Serialize(args.Length > 1 && args[1] != null ? args[1] : new object()); ct = "application/json"; } else { if (args.Length > 1 && args[1] is Dictionary<object?, object?> form) payload = string.Join("&", form.Select(kv => $"{Uri.EscapeDataString(kv.Key!.ToString()!)}={Uri.EscapeDataString(kv.Value!.ToString()!)}")); else payload = ""; } req.Content = new StringContent(payload, Encoding.UTF8, ct); var resp = httpClient.SendAsync(req).GetAwaiter().GetResult(); string body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult(); return (object?)new Dictionary<object?, object?> { ["status"] = (double)(int)resp.StatusCode, ["body"] = body }; } catch (Exception e) { throw new VSRuntimeError($"http_post error: {e.Message}"); } });
        Reg("url_encode", args => (object?)Uri.EscapeDataString(args[0]!.ToString()!));

        
        Reg("controller_press", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); if (b is IControllerBackend cb) cb.GamepadPress(Convert.ToInt32(args[0]), args[1]!.ToString()!); return null; });
        Reg("controller_release", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); if (b is IControllerBackend cb) cb.GamepadRelease(Convert.ToInt32(args[0]), args[1]!.ToString()!); return null; });
        Reg("controller_stick", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); if (b is IControllerBackend cb) cb.GamepadStick(Convert.ToInt32(args[0]), args[1]!.ToString()!, Convert.ToDouble(args[2]), Convert.ToDouble(args[3])); return null; });
        Reg("controller_trigger", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); if (b is IControllerBackend cb) cb.GamepadTrigger(Convert.ToInt32(args[0]), args[1]!.ToString()!, Convert.ToDouble(args[2])); return null; });
        Reg("controller_dpad", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); if (b is IControllerBackend cb) cb.GamepadDPad(Convert.ToInt32(args[0]), args[1]!.ToString()!); return null; });
        Reg("controller_click", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); if (b is IControllerBackend cb) { int id = Convert.ToInt32(args[0]); string btn = args[1]!.ToString()!; cb.GamepadPress(id, btn); interp.SleepScaled(0.03); cb.GamepadRelease(id, btn); } return null; });
        Reg("controller_hold", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); if (b is IControllerBackend cb) { int id = Convert.ToInt32(args[0]); string btn = args[1]!.ToString()!; double ms = Convert.ToDouble(args[2]); cb.GamepadPress(id, btn); interp.SleepScaled(ms / 1000.0); cb.GamepadRelease(id, btn); } return null; });
        Reg("controller_button_down", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); if (b is IControllerBackend cb) return (object?)cb.IsGamepadButtonDown(Convert.ToInt32(args[0]), args[1]!.ToString()!); return false; });
        Reg("controller_passthrough", args => {
            bool on = ValueHelpers.IsTruthy(args[0]);
            int physId = args.Length > 1 && args[1] != null ? Convert.ToInt32(args[1]) : 0;
            
            
            
            
            
            if (on)
                ControllerPassthrough.Start(physId);
            else
                ControllerPassthrough.Stop();
            return null;
        });
        Reg("controller_stick_x", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); if (b is IControllerBackend cb) return (object?)cb.GetStickX(Convert.ToInt32(args[0]), args[1]!.ToString()!); return 0.0; });
        Reg("controller_stick_y", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); if (b is IControllerBackend cb) return (object?)cb.GetStickY(Convert.ToInt32(args[0]), args[1]!.ToString()!); return 0.0; });
        Reg("controller_trigger_value", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); if (b is IControllerBackend cb) return (object?)cb.GetTriggerValue(Convert.ToInt32(args[0]), args[1]!.ToString()!); return 0.0; });
        Reg("controller_connected", args => { var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend"); if (b is IControllerBackend cb) return (object?)cb.IsGamepadConnected(Convert.ToInt32(args[0])); return false; });

        
        
        
        
        
        Reg("vjoy_enabled", _ => (object?)VJoyVirtualControllerBackend.StaticIsAvailable());
        
        
        
        
        Reg("virtual_controller_button_down", args =>
        {
            var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend");
            if (ControllerPassthrough.Active) return ControllerPassthrough.VirtualButtonDown(args[1]!.ToString()!);
            if (b is IControllerBackend cb) return (object?)cb.IsGamepadButtonDown(Convert.ToInt32(args[0]), args[1]!.ToString()!);
            return false;
        });
        Reg("virtual_controller_trigger_value", args =>
        {
            var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend");
            if (ControllerPassthrough.Active) return ControllerPassthrough.VirtualTriggerValue(args[1]!.ToString()!);
            if (b is IControllerBackend cb) return (object?)cb.GetTriggerValue(Convert.ToInt32(args[0]), args[1]!.ToString()!);
            return 0.0;
        });
        Reg("virtual_controller_stick_x", args =>
        {
            var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend");
            if (ControllerPassthrough.Active) return ControllerPassthrough.VirtualStickValue(args[1]!.ToString()!, false);
            if (b is IControllerBackend cb) return (object?)cb.GetStickX(Convert.ToInt32(args[0]), args[1]!.ToString()!);
            return 0.0;
        });
        Reg("virtual_controller_stick_y", args =>
        {
            var b = interp.ActionBackend ?? throw new VSRuntimeError("No backend");
            if (ControllerPassthrough.Active) return ControllerPassthrough.VirtualStickValue(args[1]!.ToString()!, true);
            if (b is IControllerBackend cb) return (object?)cb.GetStickY(Convert.ToInt32(args[0]), args[1]!.ToString()!);
            return 0.0;
        });
        Reg("virtual_controller_count", _ => (object?)(new VJoyVirtualControllerBackend()).VirtualDeviceCount);
        Reg("virtual_controller_connected", args =>
        {
            
            
            if (ControllerPassthrough.Active)
                return (object?)(ControllerPassthrough.VirtualPadName != "");
            var vcb = new VJoyVirtualControllerBackend();
            if (!vcb.IsAvailable) return false;
            int dev = args.Length > 0 && args[0] != null ? Convert.ToInt32(args[0]) : 1;
            return (object?)(dev >= 1 && dev <= vcb.VirtualDeviceCount);
        });
        Reg("virtual_controller_set_enabled", args =>
        {
            var vcb = new VJoyVirtualControllerBackend();
            if (!vcb.IsAvailable) return false;
            int dev = args.Length > 0 && args[0] != null ? Convert.ToInt32(args[0]) : 1;
            bool en = ValueHelpers.IsTruthy(args[1]);
            return vcb.SetEnabled(dev, en);
        });
        Reg("virtual_controller_set_state", args =>
        {
            var vcb = new VJoyVirtualControllerBackend();
            if (!vcb.IsAvailable) return null;
            int dev = args.Length > 0 && args[0] != null ? Convert.ToInt32(args[0]) : 1;
            var s = VJoyVirtualControllerBackend.ParseVJoyState(args.Skip(1).ToArray());
            vcb.SetState(dev, s);
            return null;
        });
        static string DriverChain() =>
            ViGEmVirtualBackend.StaticIsAvailable() ? (HidHideControl.IsAvailable ? "xinput+vigem+hidhide" : "xinput+vigem") :
            VJoyVirtualControllerBackend.StaticIsAvailable() ? (HidHideControl.IsAvailable ? "xinput+vjoy+hidhide" : "xinput+vjoy") :
            HidHideControl.IsAvailable ? "xinput+hidhide" : "xinput";
        Reg("controller_driver", _ => (object?)DriverChain());
        static string Paths(IEnumerable<string> paths)
        {
            var list = paths.ToList();
            return list.Count == 0 ? "(none)" : string.Join(" | ", list);
        }
        Reg("controller_passthrough_status", _ => (object?)new Dictionary<object?, object?>
        {
            ["active"] = ControllerPassthrough.Active,
            ["hid_hidden"] = ControllerPassthrough.HidHidden,
            ["virtual_pad"] = ControllerPassthrough.VirtualPadName,
            ["note"] = ControllerPassthrough.Active
                ? (ControllerPassthrough.PassthroughNote ?? (ControllerPassthrough.HidHidden ? null : "hiding unavailable — run as administrator for full isolation"))
                : ControllerPassthrough.PassthroughNote,
            ["physical_paths"] = Paths(DualSenseHid.EnumeratePhysicalControllerInstancePaths()),
            ["vjoy_paths"] = Paths(DualSenseHid.EnumerateVJoyInstancePaths()),
            ["hidden_paths"] = Paths(HidHideControl.HiddenPaths),
            ["hidden_at_arm"] = Paths(ControllerPassthrough.HiddenAtArm),
        });

        
        
        
        
        Reg("controller_hide_report", _ =>
        {
            var physical = DualSenseHid.EnumeratePhysicalControllerInstancePaths();
            var vjoy = DualSenseHid.EnumerateVJoyInstancePaths();
            var hidden = HidHideControl.HiddenPaths.ToList();
            
            
            var missed = physical.Where(p => !hidden.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList();

            var xinput = DualSenseHid.EnumerateXInputDevnodes();
            var physicalXInput = DualSenseHid.EnumeratePhysicalXInputInstancePaths();
            string verdict;
            if (physicalXInput.Count > 0)
            {
                int ours = ControllerPassthrough.VirtualXInputSlot;
                int phys = ControllerPassthrough.PhysicalXInputSlot;
                verdict = (ours == 0 && (phys < 0 || phys > 0))
                    ? $"XINPUT PAD, PLAYER 1 HELD: the virtual pad owns XInput player 1 and the physical pad sits behind it (player {phys + 1}) — games read the pad your scripts change. Both stay listed, because XInput itself cannot be cloaked."
                    : (phys >= 0 && ours >= 0 && phys < ours)
                        ? $"XINPUT PAD, LOSING PLAYER 1: the physical pad owns player {phys + 1}, the virtual pad is player {ours + 1} — a game reading the first pad reads the UNMODIFIED controller. XInput slots follow arrival order: arm the passthrough BEFORE plugging the controller, or let FerroLite take player 1 for you (see took_player_one)."
                        : "XINPUT PAD: the physical controller is an Xbox/XInput device (XnaComposite), which no HID-class cloak can mask — games and test sites list it next to the virtual pad. Slot order (see the two slot fields) decides which pad a game reads.";
            }
            else if (physical.Count == 0 && xinput.Count > 0)
                verdict = "XInput-ONLY physical pad: it exposes no HID interface, so the driver cannot hide it — games and sites will list it alongside the virtual pad";
            else if (physical.Count == 0)
                verdict = "no physical controller detected — wake it (PS/Xbox button) and re-arm";
            else if (missed.Count > 0)
                verdict = "LEAK: " + missed.Count + " of " + physical.Count + " physical interfaces are not hidden — other apps can still list your real pad";
            else if (!ControllerPassthrough.Active)
                verdict = "nothing armed right now — the listing above is the un-hidden truth; arm the passthrough to compare";
            else
                verdict = "OK: every physical interface is hidden; only the virtual pad should be visible";

            return (object?)new Dictionary<object?, object?>
            {
                ["chain"] = DriverChain(),
                ["elevated"] = System.Environment.IsPrivilegedProcess,
                ["passthrough_active"] = ControllerPassthrough.Active,
                ["hidhide_available"] = HidHideControl.IsAvailable,
                ["hidhide_mode"] = HidHideControl.Mode.ToString(),
                ["hidhide_last_failure"] = HidHideControl.LastHideFailure ?? "(none)",
                ["physical_pads"] = Paths(physical),
                ["physical_xinput_pads"] = Paths(physicalXInput),
                ["xinput_devnodes"] = Paths(xinput),
                ["virtual_xinput_slot"] = ControllerPassthrough.VirtualXInputSlot,
                ["physical_xinput_slot"] = ControllerPassthrough.PhysicalXInputSlot,
                ["took_player_one"] = ControllerPassthrough.TookPlayerOneSlot,
                ["auto_switched_pad_type"] = ControllerPassthrough.AutoSwitchedPadType,
                ["slot_priority_failure"] = ControllerPassthrough.SlotPriorityFailure ?? "(none)",
                ["vjoy_device"] = Paths(vjoy),
                ["hidden_now"] = Paths(hidden),
                ["hidden_at_arm"] = Paths(ControllerPassthrough.HiddenAtArm),
                ["not_hidden"] = Paths(missed),
                ["verdict"] = verdict,
            };
        });

        
        
        
        
        object? GuiBridge()
        {
            var bridge = interp.ScriptGuiBridge;
            if (bridge is IScriptGuiBridge b)
                return (object)b;
            return null;
        }

        Reg("gui_create_overlay", args =>
        {
            var g = GuiBridge();
            if (g is null) throw new VSRuntimeError("gui_create_overlay: no GUI bridge available. Run this script from the Ferrolite Studio window.");
            return ((IScriptGuiBridge)g).CreateOverlay(args.Length > 0 && args[0] != null ? args[0]!.ToString()! : "Overlay", args.Length > 1 ? Convert.ToInt32(args[1]) : 220, args.Length > 2 ? Convert.ToInt32(args[2]) : 140);
        });
        Reg("gui_add_label", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("gui_add_label: no GUI bridge available.");
            return ((IScriptGuiBridge)g).AddLabel(Convert.ToInt32(args[0]), args[1]!.ToString()!, Convert.ToInt32(args[2]), Convert.ToInt32(args[3]));
        });
        Reg("gui_add_button", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("gui_add_button: no GUI bridge available.");
            return ((IScriptGuiBridge)g).AddButton(Convert.ToInt32(args[0]), args[1]!.ToString()!, Convert.ToInt32(args[2]), Convert.ToInt32(args[3]));
        });
        Reg("gui_add_toggle", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("gui_add_toggle: no GUI bridge available.");
            return ((IScriptGuiBridge)g).AddToggle(Convert.ToInt32(args[0]), args[1]!.ToString()!, Convert.ToInt32(args[2]), Convert.ToInt32(args[3]), args.Length > 4 && ValueHelpers.IsTruthy(args[4]));
        });
        Reg("gui_set_text", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("gui_set_text: no GUI bridge available.");
            ((IScriptGuiBridge)g).SetText(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), args[2]!.ToString()!);
            return null;
        });
        Reg("gui_set_element_position", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("gui_set_element_position: no GUI bridge available.");
            ((IScriptGuiBridge)g).SetElementPosition(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), Convert.ToInt32(args[2]), Convert.ToInt32(args[3]));
            return null;
        });
        Reg("gui_button_clicked", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("gui_button_clicked: no GUI bridge available.");
            return (object?)((IScriptGuiBridge)g).ButtonClicked(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]));
        });
        Reg("gui_get_toggle", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("gui_get_toggle: no GUI bridge available.");
            return (object?)((IScriptGuiBridge)g).GetToggle(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]));
        });
        Reg("gui_remove_element", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("gui_remove_element: no GUI bridge available.");
            ((IScriptGuiBridge)g).RemoveElement(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]));
            return null;
        });
        Reg("gui_set_always_on_top", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("gui_set_always_on_top: no GUI bridge available.");
            ((IScriptGuiBridge)g).SetAlwaysOnTop(Convert.ToInt32(args[0]), ValueHelpers.IsTruthy(args[1]));
            return null;
        });
        Reg("gui_set_position", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("gui_set_position: no GUI bridge available.");
            ((IScriptGuiBridge)g).SetPosition(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), Convert.ToInt32(args[2]));
            return null;
        });
        Reg("gui_set_visible", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("gui_set_visible: no GUI bridge available.");
            ((IScriptGuiBridge)g).SetVisible(Convert.ToInt32(args[0]), ValueHelpers.IsTruthy(args[1]));
            return null;
        });
        Reg("gui_close_overlay", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("gui_close_overlay: no GUI bridge available.");
            ((IScriptGuiBridge)g).CloseOverlay(Convert.ToInt32(args[0]));
            return null;
        });

        
        
        
        Reg("begin_session", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("begin_session: no GUI bridge available.");
            ((IScriptGuiBridge)g).BeginSession(Convert.ToInt32(args[0]));
            return null;
        });
        Reg("close_session", args =>
        {
            var g = GuiBridge(); if (g is null) throw new VSRuntimeError("close_session: no GUI bridge available.");
            ((IScriptGuiBridge)g).CloseSession(Convert.ToInt32(args[0]));
            return null;
        });

        
        
        
        void Alias(string simple, string canonical)
        {
            try { env.Define(simple, env.Get(canonical)); } catch (VSRuntimeError) {  }
        }
        Alias("reverse_text", "reverse_str"); 
        Alias("ord", "char_code");           
        Alias("chr", "char_from");           
        Alias("count_text", "count_str");    
        Alias("contains_text", "contains_str");
        Alias("map_values", "map_map");      
        Alias("filter_map", "map_filter");
        Alias("entries", "to_pairs");        
        Alias("remove_key", "delete_key");
        Alias("mouse_move_relative", "move_relative"); 
    }

    private static object? JsonElementToObject(JsonElement element) => element.ValueKind switch {
        JsonValueKind.String => (object?)element.GetString(),
        JsonValueKind.Number => element.TryGetInt32(out int i) ? (object?)((double)i) : (object?)element.GetDouble(),
        JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null,
        JsonValueKind.Array => (object?)element.EnumerateArray().Select(JsonElementToObject).ToList(),
        JsonValueKind.Object => (object?)element.EnumerateObject().ToDictionary(p => (object?)p.Name, p => JsonElementToObject(p.Value)),
        _ => null,
    };

    private static object? CallUserFn(Interpreter interp, object? fn, object?[] args) {
        if (fn is NativeFunction nf) return nf.Fn(args);
        if (fn is VsFunction vsf) return interp.CallVsFunction(vsf, args.ToList(), 0);
        throw new VSRuntimeError($"Expected a function, got {ValueHelpers.VsTypeName(fn)}");
    }

    private static long GCD(long a, long b) { while (b != 0) { (a, b) = (b, a % b); } return a; }
}
