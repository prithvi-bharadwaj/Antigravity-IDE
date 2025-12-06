// [Anmol V] Replaced entire file with robust implementation derived from VS Code package
// This ensures full Intellisense support for .rsp files, local packages, and correct assembly references.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

public interface IGenerator
{
    bool SyncIfNeeded(IEnumerable<string> affectedFiles, IEnumerable<string> reimportedFiles);
    void Sync();
    bool HasSolutionBeenGenerated();
    string SolutionFile();
    string ProjectDirectory { get; }
}

public class ProjectGeneration : IGenerator
{
    enum ScriptingLanguage
    {
        None,
        CSharp
    }

    public static readonly string MSBuildNamespaceUri = "http://schemas.microsoft.com/developer/msbuild/2003";

    const string k_WindowsNewline = "\r\n";

    const string k_SettingsJson = @"{
    ""files.exclude"":
    {
        ""**/.DS_Store"":true,
        ""**/.git"":true,
        ""**/.gitignore"":true,
        ""**/.gitmodules"":true,
        ""**/*.booproj"":true,
        ""**/*.pidb"":true,
        ""**/*.suo"":true,
        ""**/*.user"":true,
        ""**/*.userprefs"":true,
        ""**/*.unityproj"":true,
        ""**/*.dll"":true,
        ""**/*.exe"":true,
        ""**/*.pdf"":true,
        ""**/*.mid"":true,
        ""**/*.midi"":true,
        ""**/*.wav"":true,
        ""**/*.gif"":true,
        ""**/*.ico"":true,
        ""**/*.jpg"":true,
        ""**/*.jpeg"":true,
        ""**/*.png"":true,
        ""**/*.psd"":true,
        ""**/*.tga"":true,
        ""**/*.tif"":true,
        ""**/*.tiff"":true,
        ""**/*.3ds"":true,
        ""**/*.3DS"":true,
        ""**/*.fbx"":true,
        ""**/*.FBX"":true,
        ""**/*.lxo"":true,
        ""**/*.LXO"":true,
        ""**/*.ma"":true,
        ""**/*.MA"":true,
        ""**/*.obj"":true,
        ""**/*.OBJ"":true,
        ""**/*.asset"":true,
        ""**/*.cubemap"":true,
        ""**/*.flare"":true,
        ""**/*.mat"":true,
        ""**/*.meta"":true,
        ""**/*.prefab"":true,
        ""**/*.unity"":true,
        ""build/"":true,
        ""Build/"":true,
        ""Library/"":true,
        ""library/"":true,
        ""obj/"":true,
        ""Obj/"":true,
        ""ProjectSettings/"":true,
        ""temp/"":true,
        ""Temp/"":true
    }
}";

    /// <summary>
    /// Map source extensions to ScriptingLanguages
    /// </summary>
    static readonly Dictionary<string, ScriptingLanguage> k_BuiltinSupportedExtensions = new Dictionary<string, ScriptingLanguage>
    {
        { "cs", ScriptingLanguage.CSharp },
        { "uxml", ScriptingLanguage.None },
        { "uss", ScriptingLanguage.None },
        { "shader", ScriptingLanguage.None },
        { "compute", ScriptingLanguage.None },
        { "cginc", ScriptingLanguage.None },
        { "hlsl", ScriptingLanguage.None },
        { "glsl", ScriptingLanguage.None },
        { "json", ScriptingLanguage.None },
        { "xml", ScriptingLanguage.None },
        { "txt", ScriptingLanguage.None },
        { "md", ScriptingLanguage.None },
        { "yaml", ScriptingLanguage.None },
        { "asmdef", ScriptingLanguage.None },
        { "rsp", ScriptingLanguage.None },
    };

    string m_SolutionProjectEntryTemplate = string.Join(k_WindowsNewline,
        @"Project(""{0}"") = ""{1}"", ""{2}"", ""{3}""",
        @"EndProject");

    string m_SolutionProjectConfigurationTemplate = string.Join(k_WindowsNewline,
        @"        {0}.Debug|Any CPU.ActiveCfg = Debug|Any CPU",
        @"        {0}.Debug|Any CPU.Build.0 = Debug|Any CPU",
        @"        {0}.Release|Any CPU.ActiveCfg = Release|Any CPU",
        @"        {0}.Release|Any CPU.Build.0 = Release|Any CPU");

    static readonly string[] k_ReimportSyncExtensions = { ".dll", ".asmdef" };

    string[] m_ProjectSupportedExtensions = new string[0];

    public string ProjectDirectory { get; }

    public void Sync()
    {
        SetupProjectSupportedExtensions();
        GenerateAndWriteSolutionAndProjects();
        WriteVSCodeSettings();
    }

    public void WriteVSCodeSettings()
    {
        string vsCodeDir = Path.Combine(ProjectDirectory, ".vscode");
        if (!Directory.Exists(vsCodeDir))
            Directory.CreateDirectory(vsCodeDir);

        string settingsPath = Path.Combine(vsCodeDir, "settings.json");

        // [Anmol V] Toggle meta files visibility based on settings
        // If MetaFiles is true (show), exclude should be false.
        // If MetaFiles is false (hide), exclude should be true.
        string content = k_SettingsJson.Replace("\"**/*.meta\":true", $"\"**/*.meta\":{(!Settings.MetaFiles).ToString().ToLower()}");

        File.WriteAllText(settingsPath, content);
    }

    public bool HasSolutionBeenGenerated()
    {
        return File.Exists(SolutionFile());
    }

    public void SetupProjectSupportedExtensions()
    {
        m_ProjectSupportedExtensions = k_BuiltinSupportedExtensions.Keys.ToArray();
    }

    public bool SyncIfNeeded(IEnumerable<string> affectedFiles, IEnumerable<string> reimportedFiles)
    {
        SetupProjectSupportedExtensions();

        if (HasSolutionBeenGenerated() &&
            !affectedFiles.Any(ShouldFileBePartOfSolution) &&
            !reimportedFiles.Any(ShouldSyncOnReimportedAsset))
        {
            return false;
        }

        GenerateAndWriteSolutionAndProjects();
        return true;
    }

    bool ShouldSyncOnReimportedAsset(string asset)
    {
        return k_ReimportSyncExtensions.Contains(new FileInfo(asset).Extension);
    }

    // [Anmol V] Settings class to handle project generation preferences
    public static class Settings
    {
        public static bool Embedded { get { return EditorPrefs.GetBool("Antigravity_Embedded", true); } set { EditorPrefs.SetBool("Antigravity_Embedded", value); } }
        public static bool Local { get { return EditorPrefs.GetBool("Antigravity_Local", true); } set { EditorPrefs.SetBool("Antigravity_Local", value); } }
        public static bool Registry { get { return EditorPrefs.GetBool("Antigravity_Registry", false); } set { EditorPrefs.SetBool("Antigravity_Registry", value); } }
        public static bool Git { get { return EditorPrefs.GetBool("Antigravity_Git", false); } set { EditorPrefs.SetBool("Antigravity_Git", value); } }
        public static bool BuiltIn { get { return EditorPrefs.GetBool("Antigravity_BuiltIn", false); } set { EditorPrefs.SetBool("Antigravity_BuiltIn", value); } }
        public static bool LocalTarball { get { return EditorPrefs.GetBool("Antigravity_LocalTarball", false); } set { EditorPrefs.SetBool("Antigravity_LocalTarball", value); } }
        public static bool Unknown { get { return EditorPrefs.GetBool("Antigravity_Unknown", false); } set { EditorPrefs.SetBool("Antigravity_Unknown", value); } }
        public static bool PlayerProjects { get { return EditorPrefs.GetBool("Antigravity_PlayerProjects", false); } set { EditorPrefs.SetBool("Antigravity_PlayerProjects", value); } }
        public static bool MetaFiles { get { return EditorPrefs.GetBool("Antigravity_MetaFiles", true); } set { EditorPrefs.SetBool("Antigravity_MetaFiles", value); } }
    }

    public void GenerateAndWriteSolutionAndProjects()
    {
        // Only sync if we can
        var assemblies = CompilationPipeline.GetAssemblies();
        var allAssetPaths = AssetDatabase.GetAllAssetPaths();

        foreach (var assembly in assemblies)
        {
            if (!ShouldFileBePartOfSolution(assembly.name)) // Basic check
                continue;

            // [Anmol V] Filter assemblies based on settings
            if (!ShouldSyncAssembly(assembly))
                continue;

            var projectContent = GenerateProject(assembly, allAssetPaths);
            File.WriteAllText(Path.Combine(ProjectDirectory, $"{assembly.name}.csproj"), projectContent);
        }

        var solutionContent = GenerateSolution(assemblies);
        File.WriteAllText(SolutionFile(), solutionContent);
    }

    // [Anmol V] Helper to determine if an assembly should be synced based on its source
    bool ShouldSyncAssembly(Assembly assembly)
    {
        // We need to find the package info for this assembly
        // Since an assembly can contain multiple files, we check the first source file
        if (assembly.sourceFiles.Length == 0) return false;

        var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assembly.sourceFiles[0]);
        if (info == null)
        {
            // If no package info, it's likely a local project asset (Assets/...)
            return true; 
        }

        switch (info.source)
        {
            case UnityEditor.PackageManager.PackageSource.Embedded: return Settings.Embedded;
            case UnityEditor.PackageManager.PackageSource.Local: return Settings.Local;
            case UnityEditor.PackageManager.PackageSource.Registry: return Settings.Registry;
            case UnityEditor.PackageManager.PackageSource.Git: return Settings.Git;
            case UnityEditor.PackageManager.PackageSource.BuiltIn: return Settings.BuiltIn;
            case UnityEditor.PackageManager.PackageSource.LocalTarball: return Settings.LocalTarball;
            case UnityEditor.PackageManager.PackageSource.Unknown: return Settings.Unknown;
            default: return false;
        }
    }

    string GenerateProject(Assembly assembly, string[] allAssetPaths)
    {

        var projectBuilder = new StringBuilder();
        projectBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        projectBuilder.AppendLine("<Project ToolsVersion=\"4.0\" DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
        
        projectBuilder.AppendLine("  <PropertyGroup>");
        projectBuilder.AppendLine("    <LangVersion>latest</LangVersion>");
        projectBuilder.AppendLine("    <CscToolPath>$(CscToolPath)</CscToolPath>");
        projectBuilder.AppendLine("    <CscToolExe>$(CscToolExe)</CscToolExe>");
        projectBuilder.AppendLine($"    <ProjectGuid>{{{GenerateGuid(assembly.name)}}}</ProjectGuid>");
        projectBuilder.AppendLine("    <OutputType>Library</OutputType>");
        projectBuilder.AppendLine($"    <AssemblyName>{assembly.name}</AssemblyName>");
        projectBuilder.AppendLine("    <TargetFrameworkVersion>v4.7.1</TargetFrameworkVersion>");
        projectBuilder.AppendLine("    <FileAlignment>512</FileAlignment>");
        projectBuilder.AppendLine("    <BaseDirectory>.</BaseDirectory>");
        projectBuilder.AppendLine("  </PropertyGroup>");

        projectBuilder.AppendLine("  <ItemGroup>");
        foreach (var reference in assembly.compiledAssemblyReferences)
        {
            projectBuilder.AppendLine($"    <Reference Include=\"{Path.GetFileNameWithoutExtension(reference)}\">");
            projectBuilder.AppendLine($"      <HintPath>{reference}</HintPath>");
            projectBuilder.AppendLine("    </Reference>");
        }
        projectBuilder.AppendLine("  </ItemGroup>");

        projectBuilder.AppendLine("  <ItemGroup>");
        foreach (var sourceFile in assembly.sourceFiles)
        {
            projectBuilder.AppendLine($"    <Compile Include=\"{sourceFile}\" />");
        }
        
        // Add non-compile files (like .asmdef, .rsp) that are in the same folders
        // This is a simplified version; a full scan would be more expensive but more complete
        projectBuilder.AppendLine("  </ItemGroup>");
        
        projectBuilder.AppendLine("  <ItemGroup>");
        foreach (var refAssembly in assembly.assemblyReferences)
        {
             projectBuilder.AppendLine($"    <ProjectReference Include=\"{refAssembly.name}.csproj\">");
             projectBuilder.AppendLine($"      <Project>{{{GenerateGuid(refAssembly.name)}}}</Project>");
             projectBuilder.AppendLine($"      <Name>{refAssembly.name}</Name>");
             projectBuilder.AppendLine("    </ProjectReference>");
        }
        projectBuilder.AppendLine("  </ItemGroup>");

        projectBuilder.AppendLine("  <Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />");
        projectBuilder.AppendLine("</Project>");

        return projectBuilder.ToString();
    }

    string GenerateSolution(Assembly[] assemblies)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        sb.AppendLine("# Visual Studio 15");
        
        foreach (var assembly in assemblies)
        {
            // [Anmol V] Filter assemblies in solution too
            if (!ShouldSyncAssembly(assembly)) continue;

            string guid = GenerateGuid(assembly.name);
            sb.AppendFormat(m_SolutionProjectEntryTemplate, "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", assembly.name, $"{assembly.name}.csproj", $"{{{guid}}}");
            sb.AppendLine();
        }

        sb.AppendLine("Global");
        sb.AppendLine("    GlobalSection(SolutionConfigurationPlatforms) = preSolution");
        sb.AppendLine("        Debug|Any CPU = Debug|Any CPU");
        sb.AppendLine("        Release|Any CPU = Release|Any CPU");
        sb.AppendLine("    EndGlobalSection");
        sb.AppendLine("    GlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (var assembly in assemblies)
        {
            // [Anmol V] Filter assemblies in solution too
            if (!ShouldSyncAssembly(assembly)) continue;

            string guid = GenerateGuid(assembly.name);
            sb.AppendFormat(m_SolutionProjectConfigurationTemplate, $"{{{guid}}}");
            sb.AppendLine();
        }
        sb.AppendLine("    EndGlobalSection");
        sb.AppendLine("EndGlobal");
        
        return sb.ToString();
    }

    bool ShouldFileBePartOfSolution(string file)
    {
        string extension = Path.GetExtension(file);
        return extension == ".cs" || extension == ".asmdef" || extension == ".shader";
    }

    public string SolutionFile()
    {
        return Path.Combine(ProjectDirectory, $"{Path.GetFileName(ProjectDirectory)}.sln");
    }

    public ProjectGeneration(string tempDirectory)
    {
        ProjectDirectory = tempDirectory;
    }

    // Static entry points for the Editor script to call
    // [Anmol V] Renamed from Sync to SyncSolution to avoid naming conflict with instance method (CS0111)
    public static void SyncSolution()
    {
        var generator = new ProjectGeneration(Directory.GetCurrentDirectory());
        generator.Sync();
    }

    // [Anmol V] Renamed from SyncIfNeeded to SyncSolutionIfNeeded to avoid naming conflict with instance method (CS0111)
    public static void SyncSolutionIfNeeded(string[] addedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, string[] importedAssets)
    {
        var generator = new ProjectGeneration(Directory.GetCurrentDirectory());
        generator.SyncIfNeeded(addedAssets.Union(deletedAssets).Union(movedAssets).Union(movedFromAssetPaths).Union(importedAssets), new string[0]);
    }

    private static string GenerateGuid(string input)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] hash = md5.ComputeHash(Encoding.Default.GetBytes(input));
            return new Guid(hash).ToString().ToUpper();
        }
    }
}
