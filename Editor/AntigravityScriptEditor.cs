using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public class AntigravityScriptEditor : IExternalCodeEditor
{
    const string EditorName = "Antigravity";
    static readonly string[] KnownPaths =
    {
        "/Applications/Antigravity.app",
        "/Applications/Antigravity.app/Contents/MacOS/Antigravity",
        // [Anmol V] Windows paths
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/Programs/Antigravity/Antigravity.exe",
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "/Antigravity/Antigravity.exe",
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "/Antigravity/Antigravity.exe",
        // [Anmol V] Linux paths
        "/usr/bin/antigravity",
        "/usr/local/bin/antigravity",
        "/snap/bin/antigravity"
    };

    static AntigravityScriptEditor()
    {
        CodeEditor.Register(new AntigravityScriptEditor());

        string current = EditorPrefs.GetString("kScriptsDefaultApp");
        if (IsAntigravityInstalled() && !current.Contains(EditorName))
        {
            // Registration handles availability; user preference is respected unless explicitly changed.
        }
    }

    private static bool IsAntigravityInstalled()
    {
        return KnownPaths.Any(p => File.Exists(p) || Directory.Exists(p));
    }

    // [Anmol V] Updated to dynamically find the actual executable within the .app bundle
    // This ensures we target the binary directly, which is required for proper instance reuse.
    private static string GetExecutablePath(string path)
    {
        if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            string macOsDir = Path.Combine(path, "Contents", "MacOS");
            if (!Directory.Exists(macOsDir)) return path;

            // 1. Try exact match "Antigravity"
            string candidate = Path.Combine(macOsDir, "Antigravity");
            if (File.Exists(candidate)) return candidate;

            // 2. Try match with .app name (e.g. "Antigravity IDE")
            string appName = Path.GetFileNameWithoutExtension(path);
            candidate = Path.Combine(macOsDir, appName);
            if (File.Exists(candidate)) return candidate;

            // 3. Fallback: take the first file found in MacOS dir
            var files = Directory.GetFiles(macOsDir);
            if (files.Length > 0) return files[0];

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
        // Perform any initialization here if needed
    }

    // [Anmol V] Added GUI for advanced project generation settings
    public void OnGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.Label("Antigravity IDE Settings", EditorStyles.boldLabel);

        GUILayout.Label("Generate .csproj files for:");
        EditorGUI.indentLevel++;
        
        ProjectGeneration.Settings.Embedded = EditorGUILayout.Toggle("Embedded packages", ProjectGeneration.Settings.Embedded);
        ProjectGeneration.Settings.Local = EditorGUILayout.Toggle("Local packages", ProjectGeneration.Settings.Local);
        ProjectGeneration.Settings.Registry = EditorGUILayout.Toggle("Registry packages", ProjectGeneration.Settings.Registry);
        ProjectGeneration.Settings.Git = EditorGUILayout.Toggle("Git packages", ProjectGeneration.Settings.Git);
        ProjectGeneration.Settings.BuiltIn = EditorGUILayout.Toggle("Built-in packages", ProjectGeneration.Settings.BuiltIn);
        ProjectGeneration.Settings.LocalTarball = EditorGUILayout.Toggle("Local tarball", ProjectGeneration.Settings.LocalTarball);
        ProjectGeneration.Settings.Unknown = EditorGUILayout.Toggle("Packages from unknown sources", ProjectGeneration.Settings.Unknown);
        ProjectGeneration.Settings.PlayerProjects = EditorGUILayout.Toggle("Player projects", ProjectGeneration.Settings.PlayerProjects);

        EditorGUI.indentLevel--;

        if (GUILayout.Button("Regenerate project files"))
        {
            ProjectGeneration.SyncSolution();
        }

        GUILayout.EndVertical();
    }

    public bool OpenProject(string filePath, int line, int column)
    {
        // [Anmol V] Fix: Ignore non-script assets so Unity handles them internally
        // - .inputactions: Unity Input System Editor
        // - .asmdef / .asmref: Unity Inspector (Assembly Definitions)
        // - .uxml / .uss: Unity UI Builder
        if (!string.IsNullOrEmpty(filePath))
        {
            string ext = Path.GetExtension(filePath);
            if (ext.Equals(".inputactions", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".asmdef", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".asmref", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".uxml", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".uss", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // 1. Figure out which Antigravity binary / bundle we’re using
        string installation = CodeEditor.CurrentEditorInstallation;

        if (string.IsNullOrEmpty(installation))
        {
            installation = KnownPaths.FirstOrDefault(p => File.Exists(p) || Directory.Exists(p));
            if (string.IsNullOrEmpty(installation))
            {
                Debug.LogError("Antigravity installation could not be found.");
                return false;
            }
        }

        // 2. Make sure we have up-to-date .csproj / .sln files
        // [Anmol V] Updated to call the renamed SyncSolution method to avoid CS0111 error
        ProjectGeneration.SyncSolution();

        // Unity project root – in the Editor this is the project directory
        string projectRoot = Directory.GetCurrentDirectory();

        bool hasFile = !string.IsNullOrEmpty(filePath);

        if (hasFile)
        {
            // Unity may give relative paths; normalize them against the project root
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.GetFullPath(Path.Combine(projectRoot, filePath));
            }

            if (!File.Exists(filePath))
            {
                // If something weird happened and the file doesn't exist, just open the project
                hasFile = false;
            }
        }

        // 3. Build command-line args for Antigravity (VS Code-style)
        // Always open the project root as the workspace
        string arguments;

        if (hasFile)
        {
            int safeLine = Math.Max(1, line);
            int safeColumn = Math.Max(1, column);

            // Open the project folder AND jump to specific file/line
            // Antigravity is a VS Code fork, so it should support --goto "file:line:column"
            arguments =
                $"\"{projectRoot}\" --goto \"{filePath}:{safeLine}:{safeColumn}\"";
        }
        else
        {
            // Just open the project workspace
            arguments = $"\"{projectRoot}\"";
        }


        try
        {
            Process process = new Process();

            // [Anmol V] Use direct binary execution for both Mac and Windows
            // This allows the application (Electron) to handle single-instance logic correctly (reusing existing window)
            process.StartInfo.FileName = GetExecutablePath(installation);
            process.StartInfo.Arguments = arguments;

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            // [Anmol V] Force focus on macOS using AppleScript
            // This ensures the editor comes to the foreground after opening
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                string appName = Path.GetFileNameWithoutExtension(installation);
                // Use a separate process to run the AppleScript to avoid blocking or complex argument escaping issues
                Process.Start("osascript", $"-e \"tell application \\\"{appName}\\\" to activate\"");
            }

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
        // [Anmol V] Updated to call the renamed SyncSolution method
        ProjectGeneration.SyncSolution();
    }

    public void SyncIfNeeded(
        string[] addedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths,
        string[] importedAssets)
    {
        // [Anmol V] Updated to call the renamed SyncSolutionIfNeeded method
        ProjectGeneration.SyncSolutionIfNeeded(
            addedAssets, deletedAssets, movedAssets, movedFromAssetPaths, importedAssets);
    }

    public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
    {
        if (!string.IsNullOrEmpty(editorPath) && editorPath.Contains("Antigravity", StringComparison.OrdinalIgnoreCase))
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
