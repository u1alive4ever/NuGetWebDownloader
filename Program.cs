using System.Diagnostics.CodeAnalysis;
using HtmlAgilityPack;

namespace NugetWebDownloader;

internal static class Program
{
    private const string BasePackageUrl = "https://www.nuget.org/packages";
    private static readonly string OutputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "NuGetPackages");

    private static async Task Main(string[] args)
    {
        // Prompt user for package name if not provided in the arguments.
        if (!TryGetPackageName(args, out var packageName))
        {
            Console.WriteLine("Package name cannot be empty.");
            goto end;
        }

        // Prompt user for package version if not provided in the arguments.
        if (!TryGetPackageVersion(args, out var version))
        {
            Console.WriteLine("Version cannot be empty.");
            goto end;
        }

        Directory.CreateDirectory(OutputDirectory);

        try
        {
            // Get the list of available frameworks.
            var targetFramework = await GetUserSelectedFramework(packageName, version);

            // If no framework is selected, terminate the program.
            if (string.IsNullOrEmpty(targetFramework))
            {
                Console.WriteLine("No framework selected. Exiting program.");
                goto end;
            }

            // Download the package and its dependencies.
            await DownloadPackageAndDependencies(packageName, version, targetFramework);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        end:
        Console.ReadKey();
    }

    private static bool TryGetPackageName(IReadOnlyList<string> args, [NotNullWhen(true)] out string? packageName)
    {
        packageName = string.Empty;

        // If package name is provided as argument, use it.
        if (args.Count > 0)
        {
            packageName = args[0];
        }

        // If package name is not provided, prompt user for input.
        if (!string.IsNullOrWhiteSpace(packageName))
        {
            return true;
        }

        packageName = Prompt("Enter the package name: ");
        return !string.IsNullOrWhiteSpace(packageName);
    }

    private static bool TryGetPackageVersion(IReadOnlyList<string> args, [NotNullWhen(true)] out string? packageVersion)
    {
        packageVersion = string.Empty;

        // If version is provided as argument, use it.
        if (args.Count > 1)
        {
            packageVersion = args[1];
        }

        // If version is not provided, prompt user for input.
        if (!string.IsNullOrWhiteSpace(packageVersion))
        {
            return true;
        }

        packageVersion = Prompt("Enter the package version: ");
        return !string.IsNullOrWhiteSpace(packageVersion);
    }

    private static string? Prompt(string message)
    {
        // Display a message and return the user input.
        Console.Write(message);
        return Console.ReadLine()?.Trim();
    }

    private static async Task<string?> GetUserSelectedFramework(string packageName, string version)
    {
        using var client = new HttpClient();
        var packageUrl = $"{BasePackageUrl}/{packageName}/{version}";
        Console.WriteLine($"Fetching data from: {packageUrl}");

        var packagePage = await client.GetStringAsync(packageUrl);

        // Parse HTML to extract framework options.
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(packagePage);

        var dependencyGroups = htmlDoc.DocumentNode.SelectNodes("//ul[@id='dependency-groups']/li");

        // If no dependency groups are found, return null.
        if (dependencyGroups == null || dependencyGroups.Count == 0)
        {
            Console.WriteLine("No available frameworks found for this package.");
            return null;
        }

        // Display available frameworks.
        Console.WriteLine("Available frameworks:");
        var frameworks = new List<string>();

        for (var i = 0; i < dependencyGroups.Count; i++)
        {
            var framework = dependencyGroups[i].SelectSingleNode(".//h4/span")?.InnerText.Trim();

            if (string.IsNullOrEmpty(framework))
            {
                continue;
            }

            frameworks.Add(framework);
            Console.WriteLine($"{i + 1}: {framework}");
        }

        // If no frameworks found, return null.
        if (frameworks.Count == 0)
        {
            Console.WriteLine("No available frameworks.");
            return null;
        }

        // Automatically select the only framework if there is just one.
        if (frameworks.Count == 1)
        {
            Console.WriteLine($"Automatically selected the only available framework: {frameworks[0]}");
            return frameworks[0];
        }

        // Prompt user to select a framework.
        Console.Write("Enter the number of the framework: ");

        // Validate user input for framework selection.
        if (int.TryParse(Console.ReadLine(), out var selectedIndex) && selectedIndex > 0 && selectedIndex <= frameworks.Count)
        {
            return frameworks[selectedIndex - 1];
        }

        Console.WriteLine("Invalid choice.");
        return null;
    }

    private static async Task DownloadPackageAndDependencies(string packageName, string version, string targetFramework)
    {
        var dependencies = new Dictionary<string, Version>();

        Console.WriteLine($"Starting to collect dependencies for package {packageName} version {version}...");

        // Stage 1: Collect dependencies.
        await CollectDependencies(dependencies, packageName, version, targetFramework);

        // Stage 2: Download packages.
        Console.WriteLine("Starting to download packages...");

        var downloadTasks = new List<Task>();

        foreach (var dependency in dependencies)
        {
            var downloadPackageTask = DownloadPackage(dependency.Key, dependency.Value.ToString(), OutputDirectory);
            downloadTasks.Add(downloadPackageTask);
        }

        // Wait for all downloads to complete.
        await Task.WhenAll(downloadTasks);

        Console.WriteLine("Download completed!");
    }

    private static async Task CollectDependencies(IDictionary<string, Version> dependencies, string name, string ver,
                                                  string targetFramework)
    {
        using var client = new HttpClient();
        var requiredVersion = new Version(ver);

        // Check if the package has already been processed and skip if a newer version isn't needed.
        if (dependencies.TryGetValue(name, out var existingVersion))
        {
            if (existingVersion >= requiredVersion)
            {
                Console.WriteLine($"Package {name} version {ver} already included. Skipping.");
                return;
            }
        }

        // Add or update the entry in the dictionary.
        dependencies[name] = requiredVersion;

        // Load the package page.
        var packageUrl = $"{BasePackageUrl}/{name}/{ver}";
        Console.WriteLine($"Fetching data from: {packageUrl}");

        var packagePage = await client.GetStringAsync(packageUrl);

        // Parse HTML to extract dependency information.
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(packagePage);

        var dependencyGroups = htmlDoc.DocumentNode.SelectNodes("//ul[@id='dependency-groups']/li");

        // If no dependencies are found, exit.
        if (dependencyGroups == null)
        {
            Console.WriteLine($"Dependencies for package {name} version {ver} not found.");
            return;
        }

        // Find the section for the selected platform.
        var targetGroup = dependencyGroups
            .FirstOrDefault(group => group.SelectSingleNode(".//h4/span")?.InnerText.Trim() == targetFramework);

        if (targetGroup == null)
        {
            Console.WriteLine($"No dependencies found for {name} version {ver} for platform {targetFramework}.");
            return;
        }

        var dependencyItems = targetGroup.SelectNodes(".//ul[@class='list-unstyled dependency-group']/li");

        foreach (var item in dependencyItems)
        {
            var dependencyName = item.SelectSingleNode(".//a")?.InnerText.Trim();

            var dependencyVersionText = item.SelectSingleNode(".//span")
                ?.InnerText
                .Replace("&gt;", string.Empty)
                .Replace("&lt;", string.Empty)
                .Replace(" ", string.Empty)
                .Trim()
                .Trim('(', ')', '=');

            if (string.IsNullOrEmpty(dependencyName) || string.IsNullOrEmpty(dependencyVersionText))
            {
                continue;
            }

            var dependencyVersion = new Version(dependencyVersionText);

            Console.WriteLine($"Dependency: {dependencyName}, Minimum version: {dependencyVersion}");

            // Recursively collect dependencies.
            await CollectDependencies(dependencies, dependencyName, dependencyVersionText, targetFramework);
        }
    }

    private static async Task DownloadPackage(string name, string ver, string outputDirectory)
    {
        using var client = new HttpClient();

        try
        {
            var packageUrl = $"https://www.nuget.org/packages/{name}/{ver}";
            Console.WriteLine($"Fetching data from: {packageUrl}");

            var packagePage = await client.GetStringAsync(packageUrl);

            // Parse HTML for the download link.
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(packagePage);

            var downloadLink = htmlDoc.DocumentNode
                .SelectSingleNode("//a[@data-track='outbound-manual-download' and text()='Download package']")
                ?.GetAttributeValue("href", null);

            if (downloadLink == null)
            {
                throw new Exception($"Could not find download link for package {name} version {ver}.");
            }

            Console.WriteLine($"Downloading package {name} version {ver} from {downloadLink}");

            var packageData = await client.GetByteArrayAsync(downloadLink);
            var outputPath = Path.Combine(outputDirectory, $"{name.ToLower()}.{ver}.nupkg");

            await File.WriteAllBytesAsync(outputPath, packageData);
            Console.WriteLine($"Package {name} version {ver} downloaded to {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading package {name} version {ver}: {ex.Message}");
        }
    }
}