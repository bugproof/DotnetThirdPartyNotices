using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DotnetThirdPartyNotices.Extensions;
using DotnetThirdPartyNotices.Models;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;


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

            MSBuildLocator.RegisterDefaults();

            app.OnExecute(async () =>
            {
                var scanDirectory = app.RemainingArguments.SingleOrDefault() ?? Directory.GetCurrentDirectory();
                var outputFilename = optionOutputFilename.ParsedValue ?? "third-party-notices.txt";

                var projectFilePath = Directory.GetFiles(scanDirectory, "*.*", SearchOption.TopDirectoryOnly)
                    .SingleOrDefault(s => s.EndsWith(".csproj") || s.EndsWith(".fsproj"));
                if (projectFilePath == null)
                {
                    Console.WriteLine("No C# or F# project file found in the current directory.");
                    return;
                }

                var project = new Project(projectFilePath);
                project.SetProperty("DesignTimeBuild", "true");

                Console.WriteLine("Resolving files...");

                var stopwatch = new Stopwatch();

                stopwatch.Start();

                var licenseContents = new Dictionary<string, List<ResolvedFileInfo>>();
                var resolvedFiles = project.ResolveFiles().ToList();

                Console.WriteLine($"Resolved files count: {resolvedFiles.Count}");

                var unresolvedFiles = new List<ResolvedFileInfo>();

                foreach (var resolvedFileInfo in resolvedFiles)
                {
                    Console.WriteLine($"Resolving license for {resolvedFileInfo.RelativeOutputPath}");
                    Console.WriteLine(resolvedFileInfo.NuSpec != null
                        ? $"  Package: {resolvedFileInfo.NuSpec.Id}"
                        : " NOT FOUND");

                    var licenseContent = await resolvedFileInfo.ResolveLicense();
                    if (licenseContent == null)
                    {
                        unresolvedFiles.Add(resolvedFileInfo);
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine(
                            $"No license found for {resolvedFileInfo.RelativeOutputPath}. Source path: {resolvedFileInfo.SourcePath}. Verify this manually.");
                        Console.ResetColor();
                        continue;
                    }

                    if (!licenseContents.ContainsKey(licenseContent))
                        licenseContents[licenseContent] = new List<ResolvedFileInfo>();

                    licenseContents[licenseContent].Add(resolvedFileInfo);
                }


                stopwatch.Stop();
                
                
                Console.WriteLine($"Resolved {licenseContents.Count} licenses for {licenseContents.Values.Sum(v => v.Count)}/{resolvedFiles.Count} files in {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"Unresolved files: {unresolvedFiles.Count}");

                stopwatch.Start();

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

                stopwatch.Stop();

                if (stringBuilder.Length > 0)
                {
                    Console.WriteLine($"Writing to {outputFilename}...");
                    await File.WriteAllTextAsync(outputFilename, stringBuilder.ToString());

                    Console.WriteLine($"Done in {stopwatch.ElapsedMilliseconds}ms");
                }
            });

            app.Execute(args);
        }
    }
}