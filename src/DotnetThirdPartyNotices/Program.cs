using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DotnetThirdPartyNotices.Extensions;
using DotnetThirdPartyNotices.Models;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

namespace DotnetThirdPartyNotices
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var app = new CommandLineApplication(false);

            app.HelpOption();
            var optionOutputFilename = app.Option<string>("--output-filename <FILENAME>", "Output filename (default: third-party-notices.txt)",
                CommandOptionType.SingleValue);
            
            app.OnExecute(async () =>
            {
                SetMsBuildExePath();

                var scanDirectory = app.RemainingArguments.SingleOrDefault() ?? Directory.GetCurrentDirectory();
                var outputFilename = optionOutputFilename.ParsedValue ?? "third-party-notices.txt";

                var projectFilePath = Directory.GetFiles(scanDirectory, "*.*", SearchOption.TopDirectoryOnly)
                    .SingleOrDefault(s => s.EndsWith(".csproj") || s.EndsWith(".fsproj"));
                if (projectFilePath == null)
                {
                    Console.WriteLine("No C# or F# project file found in the current directory.");
                    return;
                }

                var project = Project.FromFile(projectFilePath, new ProjectOptions());
                project.SetProperty("DesignTimeBuild", "true");

                Console.WriteLine("Resolving files...");

                var licenseContents = new Dictionary<string, List<ResolvedFileInfo>>();
                var resolvedFiles = project.ResolveFiles().ToList();

                Console.WriteLine($"Resolved files count: {resolvedFiles.Count}");

                foreach (var resolvedFileInfo in resolvedFiles)
                {
                    Console.WriteLine($"Resolving license for {resolvedFileInfo.RelativeOutputPath}");
                    Console.WriteLine(resolvedFileInfo.NuSpec != null
                        ? $"  Package: {resolvedFileInfo.NuSpec.Id}"
                        : " NOT FOUND");

                    var licenseContent = await resolvedFileInfo.ResolveLicense();
                    if (licenseContent == null)
                    {
                        Console.WriteLine(
                            $"No license found for {resolvedFileInfo.RelativeOutputPath}. Source path: {resolvedFileInfo.SourcePath}. Verify this manually.");
                        continue;
                    }

                    if (!licenseContents.ContainsKey(licenseContent))
                        licenseContents[licenseContent] = new List<ResolvedFileInfo>();

                    licenseContents[licenseContent].Add(resolvedFileInfo);
                }

                var stringBuilder = new StringBuilder();

                foreach (var kv in licenseContents)
                {
                    var licenseContent = kv.Key;
                    var resolvedFileInfos = kv.Value;

                    var longestNameLen = 0;
                    foreach (var resolvedFileInfo in resolvedFileInfos)
                    {
                        var strLen = resolvedFileInfo.RelativeOutputPath.Length;
                        if (strLen > longestNameLen)
                            longestNameLen = strLen;

                        stringBuilder.AppendLine(resolvedFileInfo.RelativeOutputPath);
                    }

                    stringBuilder.AppendLine(new string('-', longestNameLen));

                    stringBuilder.AppendLine(licenseContent);
                    stringBuilder.AppendLine();
                }

                if (stringBuilder.Length > 0)
                {
                    Console.WriteLine($"Writing to {outputFilename}...");
                    await File.WriteAllTextAsync(outputFilename, stringBuilder.ToString());

                    Console.WriteLine("Done.");
                }
            });

            app.Execute(args);
        }

        // Thanks to Rico Suter: https://blog.rsuter.com/missing-sdk-when-using-the-microsoft-build-package-in-net-core/
        private static void SetMsBuildExePath()
        {
            try
            {
                // See https://github.com/Microsoft/msbuild/issues/2532#issuecomment-381096259

                var process = Process.Start(new ProcessStartInfo("dotnet", "--list-sdks")
                    {RedirectStandardOutput = true});
                process.WaitForExit(1000);

                var output = process.StandardOutput.ReadToEnd();
                var sdkPaths = Regex.Matches(output, "([0-9]+.[0-9]+.[0-9]+) \\[(.*)\\]")
                    .Select(m => Path.Combine(m.Groups[2].Value, m.Groups[1].Value, "MSBuild.dll"));

                var sdkPath = sdkPaths.Last();
                Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", sdkPath);
            }
            catch (Exception exception)
            {
                Console.Write("Could not set MSBUILD_EXE_PATH: " + exception + "\n\n");
            }
        }
    }
}