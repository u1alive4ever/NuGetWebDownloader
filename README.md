# NuGetWebDownloader
**NuGetWebDownloader** is a simple command-line utility that helps you download NuGet packages and their dependencies directly from [www.nuget.org](https://www.nuget.org). This tool is particularly useful for scenarios where the corporate firewall or network policies block access to `api.nuget.org`, but you can access the NuGet website.

## Features
- Downloads specific versions of NuGet packages.
- Resolves and downloads all dependencies for a package.
- Supports selecting a target framework for dependency resolution.
- Saves all downloaded packages in a local directory (`NuGetPackages`) for offline use.

## Why NuGetWebDownloader?
In some corporate environments, access to NuGet API endpoints may be restricted, preventing developers from retrieving packages and dependencies through traditional means (e.g., Visual Studio or `dotnet` CLI). This tool works by parsing the NuGet website, bypassing API restrictions and enabling package retrieval via standard HTTP requests.

## How to Use
1. Download and build the project.
2. Run the executable with the following syntax: `NuGetWebDownloader.exe [PackageName] [Version]`
   - `PackageName` (optional): The name of the package to download.
   - `Version` (optional): The version of the package.
   - If no arguments are provided, the tool will prompt for input.
3. Follow the instructions to select a target framework (if applicable).
4. All packages will be saved in the `NuGetPackages` folder within the application's directory.

### Example
To download `Newtonsoft.Json` version `13.0.1`: `NuGetWebDownloader.exe Newtonsoft.Json 13.0.1`

### Output
The tool will create a folder named `NuGetPackages` in the working directory, containing the `.nupkg` files for the specified package and its dependencies.

## Dependencies
- [.NET 6.0+](https://dotnet.microsoft.com/download)
- [HtmlAgilityPack](https://html-agility-pack.net/) for HTML parsing.

## Building the Project
1. Clone this repository: `git clone https://github.com/yourusername/NuGetWebDownloader.git`
2. Navigate to the project directory: `cd NuGetWebDownloader`
3. Build the project: `dotnet build`

## License
This project is licensed under the MIT License. See the [LICENSE](https://github.com/u1alive4ever/NuGetWebDownloader/blob/main/LICENSE) file for details.

## Contributing
Contributions are welcome! If you encounter any issues or have suggestions for improvement, feel free to open an issue or submit a pull request.

## Disclaimer
This tool is intended for use in environments where NuGet API access is restricted but access to the NuGet website is available. Always ensure compliance with your organization's policies when using this tool.
   
