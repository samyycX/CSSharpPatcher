# CSSharpPatcher

A Counter-Strike Sharp (CSSharp) plugin that provides runtime memory patching capabilities for CS2 game binaries. This plugin allows you to apply custom patches to the game server without modifying the original game files.

## Installation

1. **Download the plugin** and place it in your CSSharp plugins directory:
   ```
   game/csgo/addons/counterstrikesharp/plugins/CSSharpPatcher/
   ```

2. **Configure the plugin**:
   - Navigate to `game/csgo/addons/counterstrikesharp/configs/plugins/CSSharpPatcher/`
   - **Important**: Rename `CSSharpPatcher.example.json` to `CSSharpPatcher.json` if this is your first time installation
   - Edit the configuration file to enable/disable patches as needed

3. Restart your server

## Configuration

The configuration file (`CSSharpPatcher.json`) uses the following structure:

```json
{
  "Patches": {
    "PatchName": {
      "module": "server",
      "windows": {
        "signature": "memory signature for Windows",
        "patch": "hex bytes to patch"
      },
      "linux": {
        "signature": "memory signature for Linux",
        "patch": "hex bytes to patch"
      }
    }
  },
  "EnabledPatches": ["PatchName1", "PatchName2"],
  "RestoreWhenUnload": true,
  "ConfigVersion": 1
}
```

## Building from Source

1. Clone the repository
2. Install .NET 8.0 SDK
3. Run `dotnet build` in the project directory
4. Copy the built files to your CSSharp plugins directory

## License

This project is provided as-is for educational and development purposes. Use at your own risk.

## Credits

- Memory patching implementation based on work by @xstage