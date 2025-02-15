#module nuget:?package=Cake.LongPath.Module&version=0.5.0

#addin nuget:?package=Cake.FileHelpers&version=3.2.1
#addin nuget:?package=Cake.Powershell&version=0.4.8

using System;
using System.Linq;
using System.Text.RegularExpressions;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");

//////////////////////////////////////////////////////////////////////
// VERSIONS
//////////////////////////////////////////////////////////////////////

var gitVersioningVersion = "2.1.65";
var inheritDocVersion = "1.1.1.1";

//////////////////////////////////////////////////////////////////////
// VARIABLES
//////////////////////////////////////////////////////////////////////

var baseDir = MakeAbsolute(Directory("../")).ToString();
var buildDir = baseDir + "/build";
var win32Solution = baseDir + "/Microsoft.Toolkit.Win32.sln";
var toolsDir = buildDir + "/tools";

var binDir = baseDir + "/bin";
var nupkgDir = binDir + "/nupkg";

var styler = toolsDir + "/XamlStyler.Console/tools/xstyler.exe";
var stylerFile = baseDir + "/settings.xamlstyler";

var versionClient = toolsDir + "/nerdbank.gitversioning/tools/Get-Version.ps1";
string Version = null;

var inheritDoc = toolsDir + "/InheritDoc/tools/InheritDoc.exe";
var inheritDocExclude = "Foo.*";

//////////////////////////////////////////////////////////////////////
// METHODS
//////////////////////////////////////////////////////////////////////

void VerifyHeaders(bool Replace)
{
    var header = FileReadText("header.txt") + "\r\n";
    bool hasMissing = false;

    Func<IFileSystemInfo, bool> exclude_objDir =
        fileSystemInfo => !fileSystemInfo.Path.Segments.Contains("obj");

    var files = GetFiles(baseDir + "/**/*.cs", exclude_objDir).Where(file =>
    {
        var path = file.ToString();
        return !(path.EndsWith(".g.cs") || path.EndsWith(".i.cs") || System.IO.Path.GetFileName(path).Contains("TemporaryGeneratedFile"));
    });

    Information("\nChecking " + files.Count() + " file header(s)");
    foreach(var file in files)
    {
        var oldContent = FileReadText(file);
        if(oldContent.Contains("// <auto-generated>"))
        {
           continue;
        }
        var rgx = new Regex("^(//.*\r?\n)*\r?\n");
        var newContent = header + rgx.Replace(oldContent, "");

        if(!newContent.Equals(oldContent, StringComparison.Ordinal))
        {
            if(Replace)
            {
                Information("\nUpdating " + file + " header...");
                FileWriteText(file, newContent);
            }
            else
            {
                Error("\nWrong/missing header on " + file);
                hasMissing = true;
            }
        }
    }

    if(!Replace && hasMissing)
    {
        throw new Exception("Please run UpdateHeaders.bat or '.\\build.ps1 -target=UpdateHeaders' and commit the changes.");
    }
}

//////////////////////////////////////////////////////////////////////
// DEFAULT TASK
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Description("Clean the output folder")
    .Does(() =>
{
    if(DirectoryExists(binDir))
    {
        Information("\nCleaning Working Directory");
        CleanDirectory(binDir);
    }
    else
    {
        CreateDirectory(binDir);
    }
});

Task("Verify")
    .Description("Run pre-build verifications")
    .IsDependentOn("Clean")
    .Does(() =>
{
    VerifyHeaders(false);

    StartPowershellFile("./Find-WindowsSDKVersions.ps1");
});

Task("Version")
    .Description("Updates the version information in all Projects")
    .IsDependentOn("Verify")
    .Does(() =>
{
    Information("\nDownloading NerdBank GitVersioning...");
    var installSettings = new NuGetInstallSettings {
        ExcludeVersion  = true,
        Version = gitVersioningVersion,
        OutputDirectory = toolsDir
    };

    NuGetInstall(new []{"nerdbank.gitversioning"}, installSettings);

    Information("\nRetrieving version...");
    var results = StartPowershellFile(versionClient);
    Version = results[1].Properties["NuGetPackageVersion"].Value.ToString();
    Information("\nBuild Version: " + Version);
});

Task("Build")
    .Description("Build all projects and get the assemblies")
    .IsDependentOn("Version")
    .Does(() =>
{
    Information("\nBuilding Solution");
    EnsureDirectoryExists(nupkgDir);

    {
        var solution = new FilePath(@"..\Microsoft.Toolkit.Win32.UI.XamlApplication\packages.config");
        var nugetRestoreSettings = new NuGetRestoreSettings {
            PackagesDirectory = new DirectoryPath(@"..\packages"),
        };
        Information("\nRestore Native Step");
        NuGetRestore(solution, nugetRestoreSettings);
    }

    {
        var buildSettings = new MSBuildSettings
        {
            PlatformTarget = PlatformTarget.x64,
            MaxCpuCount = 1,
        }
        .SetConfiguration("Release")
        .UseToolVersion(MSBuildToolVersion.VS2019)
        .WithTarget("Restore");

        Information("\nRestore x64 Step");
        MSBuild(win32Solution, buildSettings);
    }

    {
        // Build once with normal dependency ordering
        var buildSettings = new MSBuildSettings
        {
            PlatformTarget = PlatformTarget.x64,
            MaxCpuCount = 1,
        }
        .SetConfiguration("Release")
        .UseToolVersion(MSBuildToolVersion.VS2019)
        .WithTarget("Build");

        Information("\nBuild x64 Step");
        MSBuild(win32Solution, buildSettings);
    }

    {
        var buildSettings = new MSBuildSettings
        {
            PlatformTarget = PlatformTarget.x86,
            MaxCpuCount = 1,
        }
        .SetConfiguration("Release")
        .UseToolVersion(MSBuildToolVersion.VS2019)
        .WithTarget("Restore");

        Information("\nRestore x86 Step");
        MSBuild(win32Solution, buildSettings);
    }

    {
        // Build once with normal dependency ordering
        var buildSettings = new MSBuildSettings
        {
            PlatformTarget = PlatformTarget.x86,
            MaxCpuCount = 1,
        }
        .SetConfiguration("Release")
        .UseToolVersion(MSBuildToolVersion.VS2019)
        .WithTarget("Build");

        Information("\nBuild x86 Step");
        MSBuild(win32Solution, buildSettings);
    }

    {
        var buildSettings = new MSBuildSettings
        {
            PlatformTarget = PlatformTarget.ARM,
            MaxCpuCount = 1,
        }
        .SetConfiguration("Release")
        .UseToolVersion(MSBuildToolVersion.VS2019)
        .WithTarget("Restore");

        Information("\nRestore ARM Step");
        MSBuild(win32Solution, buildSettings);
    }

    {
        // Build once with normal dependency ordering
        var buildSettings = new MSBuildSettings
        {
            PlatformTarget = PlatformTarget.ARM,
            MaxCpuCount = 1,
        }
        .SetConfiguration("Release")
        .UseToolVersion(MSBuildToolVersion.VS2019)
        .WithTarget("Build");

        Information("\nBuild ARM Step");
        MSBuild(win32Solution, buildSettings);
    }

    {
        var buildSettings = new MSBuildSettings
        {
            PlatformTarget = PlatformTarget.ARM64,
            MaxCpuCount = 1,
        }
        .SetConfiguration("Release")
        .UseToolVersion(MSBuildToolVersion.VS2019)
        .WithTarget("Restore");

        Information("\nRestore ARM64 Step");
        MSBuild(win32Solution, buildSettings);
    }

    {
        // Build once with normal dependency ordering
        var buildSettings = new MSBuildSettings
        {
            PlatformTarget = PlatformTarget.ARM64,
            MaxCpuCount = 1,
        }
        .SetConfiguration("Release")
        .UseToolVersion(MSBuildToolVersion.VS2019)
        .WithTarget("Build");

        Information("\nBuild ARM64 Step");
        MSBuild(win32Solution, buildSettings);
    }

});

Task("InheritDoc")
    .Description("Updates <inheritdoc /> tags from base classes, interfaces, and similar methods")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information("\nDownloading InheritDoc...");
    var installSettings = new NuGetInstallSettings {
        ExcludeVersion = true,
        Version = inheritDocVersion,
        OutputDirectory = toolsDir,
    };

    NuGetInstall(new []{"InheritDoc"}, installSettings);

    var args = new ProcessArgumentBuilder()
                .AppendSwitchQuoted("-b", baseDir)
                .AppendSwitch("-o", "")
                .AppendSwitchQuoted("-x", inheritDocExclude);

    var result = StartProcess(inheritDoc, new ProcessSettings { Arguments = args });

    if (result != 0)
    {
        throw new InvalidOperationException("InheritDoc failed!");
    }

    Information("\nFinished generating documentation with InheritDoc");
});

Task("Package")
    .Description("Pack the NuPkg")
    .IsDependentOn("InheritDoc")
    .Does(() =>
{
    var buildSettings = new MSBuildSettings
    {
        PlatformTarget = PlatformTarget.x64,
        MaxCpuCount = 0,
    }
    .SetConfiguration("Release")
    //.WithProperty("GenerateLibraryLayout", "true")
    .UseToolVersion(MSBuildToolVersion.VS2019)
    .WithTarget("Pack");

    Information("\nBuild Step");
    MSBuild(win32Solution, buildSettings);
});


//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Package");

Task("UpdateHeaders")
    .Description("Updates the headers in *.cs files")
    .Does(() =>
{
    VerifyHeaders(true);
});

Task("StyleXaml")
    .Description("Ensures XAML Formatting is Clean")
    .Does(() =>
{
    Information("\nDownloading XamlStyler...");
    var installSettings = new NuGetInstallSettings {
        ExcludeVersion  = true,
        OutputDirectory = toolsDir
    };

    NuGetInstall(new []{"xamlstyler.console"}, installSettings);

    Func<IFileSystemInfo, bool> exclude_objDir =
        fileSystemInfo => !fileSystemInfo.Path.Segments.Contains("obj");

    var files = GetFiles(baseDir + "/**/*.xaml", exclude_objDir);
    Information("\nChecking " + files.Count() + " file(s) for XAML Structure");
    foreach(var file in files)
    {
        StartProcess(styler, "-f \"" + file + "\" -c \"" + stylerFile + "\"");
    }
});



//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
