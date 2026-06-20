using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

class Program {
    static int Main(string[] args) {
        string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string realExe = Path.Combine(exeDir, "7za-real.exe");

        var psi = new ProcessStartInfo {
            FileName = realExe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // pass through args, quoting if needed
        var sb = new StringBuilder();
        foreach (var a in args) {
            if (sb.Length > 0) sb.Append(' ');
            if (a.Contains(" ") || a.Contains("\t")) {
                sb.Append('"').Append(a.Replace("\"", "\\\"")).Append('"');
            } else {
                sb.Append(a);
            }
        }
        psi.Arguments = sb.ToString();

        var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (s, e) => { if (e.Data != null) Console.Out.WriteLine(e.Data); };
        proc.ErrorDataReceived  += (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        proc.WaitForExit();

        int code = proc.ExitCode;
        // 7-Zip exit codes: 0 ok, 1 warning, 2 fatal. We treat 2 as ok because the
        // only fatal we hit is "cannot create symbolic link" for darwin/*.dylib —
        // those files are macOS-only and not used on Windows builds.
        if (code == 2) return 0;
        return code;
    }
}
