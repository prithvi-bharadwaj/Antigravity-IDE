# Antigravity IDE Support for Unity

Integrates **Antigravity** as an external script editor for Unity, providing seamless file opening and project generation for Intellisense.

## Features
- **Auto-Discovery**: Automatically detects Antigravity installation on macOS.
- **Auto-Default**: Attempts to set Antigravity as the default editor upon first load.
- **Intellisense Support**: Generates `.csproj` and `.sln` files compatible with Antigravity's language server.
- **Smart Opening**: Opens files at specific lines and columns.

## Installation

### via Package Manager (Git URL)
1.  Open Unity.
2.  Go to **Window > Package Manager**.
3.  Click the **+** icon > **Add package from git URL...**.
4.  Enter the URL of this repository: `https://github.com/prithvi-bharadwaj/Antigravity-IDE.git`

### via Disk
1.  Open **Window > Package Manager**.
2.  Click **+** > **Add package from disk...**.
3.  Select the `package.json` file in this folder.

## Usage
Once installed, go to **Unity > Preferences > External Tools**.
- **External Script Editor**: Select **Antigravity**.
- If it doesn't appear automatically, ensure Antigravity is installed in `/Applications/Antigravity.app`.

## Troubleshooting
- **Editor not found**: The package looks for `/Applications/Antigravity.app`. If installed elsewhere, ensure the path is correct or manually select the executable.
- **No Intellisense**: Ensure `Generate .csproj files` is checked in Unity Preferences.
