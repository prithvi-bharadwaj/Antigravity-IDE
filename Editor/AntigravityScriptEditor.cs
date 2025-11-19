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
    static readonly string[] KnownPaths =
    {
        "/Applications/Antigravity.app",
        "/Applications/Antigravity.app/Contents/MacOS/Antigravity",
        // Add other potential paths here
    };

    static AntigravityScriptEditor()
    {
        CodeEditor.Register(new AntigravityScriptEditor());
        // Auto-set as default if installed and not already set
        string current = EditorPrefs.GetString("kScriptsDefaultApp");
        if (IsAntigravityInstalled() && !current.Contains(EditorName))
        {
            // Optional: Force set or just notify. For now, we just register.
            // To force set: EditorPrefs.SetString("kScriptsDefaultApp", FoundPath);
        }
    }

    private static bool IsAntigravityInstalled()
    {
        return KnownPaths.Any(p => File.Exists(p) || Directory.Exists(p));
    }

    private static string GetExecutablePath(string path)
    {
        if (path.EndsWith(".app"))
        {
            // On Mac, point to the executable inside the .app
            // Adjust this if Antigravity has a specific CLI entry point
            string executable = Path.Combine(path, "Contents", "MacOS", "Antigravity");
            if (File.Exists(executable)) return executable;
            
            // Fallback: maybe the .app itself is handled by 'open' command, 
            // but for Process.Start we usually need the binary.
            return path; 
        }
        return path;
    }

    public CodeEditor.Installation[] Installations
    {
        get
        {
            var installations = new List<CodeEditor.Installation>();
            foreach (var path in KnownPaths)
            {
                if (File.Exists(path) || Directory.Exists(path))
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

    public void Initialize(string editorInstallationPath)
    {
        // Perform any initialization here
    }

    public void OnGUI()
    {
        // Custom GUI for Preferences > External Tools
        GUILayout.Label("Antigravity IDE Settings", EditorStyles.boldLabel);
        // Add settings here if needed
    }

    public bool OpenProject(string filePath, int line, int column)
    {
        string installation = CodeEditor.CurrentEditorInstallation;
        
        // If no specific file, just open the project folder
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
            // Format: file:line:column (Standard for many editors, adjust for Antigravity)
            arguments = $"\"{filePath}:{line}:{column}\"";
        }

        try
        {
            Process process = new Process();
            
            // Handle macOS .app bundles specifically
            if (installation.EndsWith(".app") && Application.platform == RuntimePlatform.OSXEditor)
            {
                process.StartInfo.FileName = "/usr/bin/open";
                process.StartInfo.Arguments = $"-a \"{installation}\" -n --args {arguments}";
            }
            else
            {
                process.StartInfo.FileName = GetExecutablePath(installation);
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
        if (editorPath.Contains("Antigravity"))
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
