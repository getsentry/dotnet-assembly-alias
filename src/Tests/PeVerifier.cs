using System.Diagnostics;
using System.Text.RegularExpressions;

public static class PeVerifier
{
    public static readonly bool FoundPeVerify;
    static string? peverifyPath;

    static PeVerifier()
    {
        FoundPeVerify = SdkToolFinder.TryFindTool("peverify", out peverifyPath);
    }

    public static string TrimLineNumbers(string input)
    {
        return Regex.Replace(input, @"\[offset .*\]", "");
    }

  public  static bool Verify(
        string assemblyPath,
        List<string> ignoreCodes,
        out string output)
    {
        ignoreCodes.Add("0x80070002");
        ignoreCodes.Add("0x80131252");
        var workingDirectory = Path.GetDirectoryName(assemblyPath);
        var processStartInfo = new ProcessStartInfo(peverifyPath!)
        {
            Arguments = $"\"{assemblyPath}\" /hresult /nologo /ignore={string.Join(",", ignoreCodes)}",
            WorkingDirectory = workingDirectory!,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(processStartInfo);
        output = process!.StandardOutput.ReadToEnd();
        output = Regex.Replace(output, "^All Classes and Methods.*", "");
        output = output.Trim();
        output = TrimLineNumbers(output);
        if (!process.WaitForExit(10000))
        {
            throw new Exception("PeVerify failed to exit");
        }

        if (process.ExitCode != 0)
        {
            return false;
        }

        return true;
    }
}