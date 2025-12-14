using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class AntigravityScriptEditor : IExternalCodeEditor
{
    const string EditorName = "Antigravity";
    
    static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static string ProgramFiles => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

    static string[] KnownPaths
    {
        get
        {
            // Windows Paths
            var windowsPaths = new[]
            {
                Path.Combine(LocalAppData, "Programs", "Antigravity", "Antigravity.exe"),
                Path.Combine(ProgramFiles, "Antigravity", "Antigravity.exe")
            };

            // macOS Paths
            var macPaths = new[]
            {
                "/Applications/Antigravity.app",
                "/Applications/Antigravity.app/Contents/MacOS/Antigravity"
            };

            return Application.platform == RuntimePlatform.WindowsEditor ? windowsPaths : macPaths;
        }
    }

    static AntigravityScriptEditor()
    {
        CodeEditor.Register(new AntigravityScriptEditor());

        string current = EditorPrefs.GetString("kScriptsDefaultApp");
        if (IsAntigravityInstalled() && !current.Contains(EditorName))
        {
        }
    }

    private static bool IsAntigravityInstalled()
    {
        return KnownPaths.Any(File.Exists);
    }

    public CodeEditor.Installation[] Installations
    {
        get
        {
            var installations = new List<CodeEditor.Installation>();
            foreach (var path in KnownPaths)
            {
                if (File.Exists(path) || (Application.platform == RuntimePlatform.OSXEditor && Directory.Exists(path)))
                {
                    installations.Add(new CodeEditor.Installation
                    {
                        Name = EditorName,
                        Path = path
                    });
                }
            }
            return installations.ToArray();
        }
    }

    public void Initialize(string editorInstallationPath) { }

    public void OnGUI()
    {
        GUILayout.Label("Antigravity IDE Settings", EditorStyles.boldLabel);
        GUILayout.Label("Version: Windows/Mac Hybrid", EditorStyles.miniLabel);
    }

    public bool OpenProject(string filePath, int line, int column)
    {
        string installation = CodeEditor.CurrentEditorInstallation;

        if (string.IsNullOrEmpty(filePath))
        {
            filePath = Directory.GetCurrentDirectory();
        }

        string arguments;
        if (Directory.Exists(filePath))
        {
            arguments = $"\"{filePath}\"";
        }
        else
        {
            arguments = $"-g \"{filePath}:{line}:{column}\"";
        }

        try
        {
            Process process = new Process();

            // --- WINDOWS LOGIC ---
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                process.StartInfo.FileName = installation; // Direct path to .exe
                process.StartInfo.Arguments = arguments;
            }
            // --- MAC LOGIC ---
            else if (installation.EndsWith(".app") && Application.platform == RuntimePlatform.OSXEditor)
            {
                process.StartInfo.FileName = "/usr/bin/open";
                process.StartInfo.Arguments = $"-a \"{installation}\" -n --args {arguments}";
            }
            else
            {
                process.StartInfo.FileName = installation;
                process.StartInfo.Arguments = arguments;
            }

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            return true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to open Antigravity: {e.Message}");
            return false;
        }
    }

    public void SyncAll()
    {
        ProjectGeneration.Sync();
    }

    public void SyncIfNeeded(string[] addedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, string[] importedAssets)
    {
        ProjectGeneration.SyncIfNeeded(addedAssets, deletedAssets, movedAssets, movedFromAssetPaths, importedAssets);
    }

    public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
    {
        if (editorPath.ToLower().Contains("antigravity"))
        {
            installation = new CodeEditor.Installation
            {
                Name = EditorName,
                Path = editorPath
            };
            return true;
        }

        installation = default;
        return false;
    }
}
