using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

public static class ProjectGeneration
{
    public static void Sync()
    {
        // Regenerate everything
        var assemblies = CompilationPipeline.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            GenerateCsproj(assembly);
        }
        GenerateSolution(assemblies);
    }

    public static void SyncIfNeeded(string[] addedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, string[] importedAssets)
    {
        // For now, just simple sync. Optimization can be added later.
        Sync();
    }

    private static void GenerateCsproj(Assembly assembly)
    {
        string projectPath = Path.Combine(Directory.GetCurrentDirectory(), $"{assembly.name}.csproj");
        
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<Project ToolsVersion=\"4.0\" DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
        
        // PropertyGroup
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <Configuration Condition=\" '$(Configuration)' == '' \">Debug</Configuration>");
        sb.AppendLine("    <Platform Condition=\" '$(Platform)' == '' \">AnyCPU</Platform>");
        sb.AppendLine("    <ProductVersion>10.0.20506</ProductVersion>");
        sb.AppendLine("    <SchemaVersion>2.0</SchemaVersion>");
        sb.AppendLine($"    <ProjectGuid>{{{Guid.NewGuid()}}}</ProjectGuid>"); // Ideally stable GUID based on name
        sb.AppendLine("    <OutputType>Library</OutputType>");
        sb.AppendLine($"    <AssemblyName>{assembly.name}</AssemblyName>");
        sb.AppendLine("    <TargetFrameworkVersion>v4.7.1</TargetFrameworkVersion>");
        sb.AppendLine("    <FileAlignment>512</FileAlignment>");
        sb.AppendLine("    <BaseDirectory>.</BaseDirectory>");
        sb.AppendLine("  </PropertyGroup>");

        // References
        sb.AppendLine("  <ItemGroup>");
        foreach (var reference in assembly.compiledAssemblyReferences)
        {
            sb.AppendLine($"    <Reference Include=\"{Path.GetFileNameWithoutExtension(reference)}\">");
            sb.AppendLine($"      <HintPath>{reference}</HintPath>");
            sb.AppendLine("    </Reference>");
        }
        // Add Unity references (simplified)
        // In a real implementation, we'd iterate over assembly.allReferences or similar
        sb.AppendLine("  </ItemGroup>");

        // Compile Items (Source Files)
        sb.AppendLine("  <ItemGroup>");
        foreach (var sourceFile in assembly.sourceFiles)
        {
            sb.AppendLine($"    <Compile Include=\"{sourceFile}\" />");
        }
        sb.AppendLine("  </ItemGroup>");
        
        // Project References
        sb.AppendLine("  <ItemGroup>");
        foreach (var refAssembly in assembly.assemblyReferences)
        {
             sb.AppendLine($"    <ProjectReference Include=\"{refAssembly.name}.csproj\">");
             sb.AppendLine($"      <Project>{{{Guid.NewGuid()}}}</Project>"); // Needs stable GUID
             sb.AppendLine($"      <Name>{refAssembly.name}</Name>");
             sb.AppendLine("    </ProjectReference>");
        }
        sb.AppendLine("  </ItemGroup>");

        sb.AppendLine("  <Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />");
        sb.AppendLine("</Project>");

        File.WriteAllText(projectPath, sb.ToString());
    }

    private static void GenerateSolution(Assembly[] assemblies)
    {
        string solutionPath = Path.Combine(Directory.GetCurrentDirectory(), $"{Path.GetFileName(Directory.GetCurrentDirectory())}.sln");
        var sb = new StringBuilder();
        sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        sb.AppendLine("# Visual Studio 15");
        
        foreach (var assembly in assemblies)
        {
            string guid = Guid.NewGuid().ToString(); // Needs stable GUID
            sb.AppendLine($"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{assembly.name}\", \"{assembly.name}.csproj\", \"{{{guid}}}\"");
            sb.AppendLine("EndProject");
        }
        
        File.WriteAllText(solutionPath, sb.ToString());
    }
}
