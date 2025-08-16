using System.Data;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Memory;
using Microsoft.Extensions.Logging;

namespace CSSharpPatcher;

public class PatchEntryConfig
{
  public string signature { get; set; } = "";
  public string patch { get; set; } = "";
}
public class PatchConfig
{
  public string module { get; set; } = "server";
  public PatchEntryConfig windows { get; set; } = new();
  public PatchEntryConfig linux { get; set; } = new();

}
public class PatcherConfig : BasePluginConfig
{
  public Dictionary<string, PatchConfig> Patches { get; set; } = new();
  public List<string> EnabledPatches { get; set; } = new();

  public bool RestoreWhenUnload { get; set; } = true;
}

public record PatchInfo(
  string Name,
  nint Addr,
  List<byte> OriginalBytes
);

public class CSSharpPatcher : BasePlugin, IPluginConfig<PatcherConfig>
{
  public override string ModuleName => "CSSharpPatcher";

  public override string ModuleVersion => "1.0.1";

  public override string ModuleAuthor => "samyyc";

  public PatcherConfig Config { get; set; } = new();

  private List<PatchInfo> _PatchedPatchs = new();

  public void OnConfigParsed(PatcherConfig config)
  {
    Config = config;
  }

  private string GetModulePath(string module)
  {
    var dir = new string[] { "server", "host", "matchmaking", "client" }.Contains(module) ? Constants.GameBinaryPath : Constants.RootBinaryPath;
    return Path.Join(dir, Constants.ModulePrefix + module + Constants.ModuleSuffix);
  }

  public override void Load(bool hotReload)
  {
    if (hotReload) return;
    foreach (var name in Config.EnabledPatches)
    {
      ApplyPatch(name);
    }
  }

  public override void Unload(bool hotReload)
  {
    if (hotReload) return;
    if (!Config.RestoreWhenUnload) return;

    foreach (var patch in _PatchedPatchs)
    {
      RestorePatch(patch);
    }

    _PatchedPatchs.Clear();
  }

  public void ApplyPatch(string name)
  {
    var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    var patch = Config.Patches[name];
    var entry = isWindows ? patch.windows : patch.linux;
    var addr = NativeAPI.FindSignature(GetModulePath(patch.module), entry.signature);
    if (addr == 0)
    {
      Logger.LogError($"Patch '{name}' has invalid signature, skipping...");
      return;
    }
    var payload = entry.patch.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                  .Select(hex => Convert.ToByte(hex, 16))
                  .ToList();

    List<byte> originalBytes = new();
    MemoryPatch.SetMemAccess(addr, payload.Count());
    for (int j = 0; j < payload.Count(); j++)
    {
      originalBytes.Add(Marshal.ReadByte(addr, j));
      Marshal.WriteByte(addr, j, payload[j]);
    }

    _PatchedPatchs.Add(new(name, addr, originalBytes));

    Logger.LogInformation($"Patch '{name}' successfully patched at 0x{addr:X}.");
  }

  public void RestorePatch(PatchInfo patch)
  {
    MemoryPatch.SetMemAccess(patch.Addr, patch.OriginalBytes.Count());
    for (int i = 0; i < patch.OriginalBytes.Count(); i++)
    {
      Marshal.WriteByte(patch.Addr, i, patch.OriginalBytes[i]);
    }
    Logger.LogInformation($"Patch '{patch.Name}' successfully restored.");
    
  }

  [ConsoleCommand("css_patcher")]
  [CommandHelper(minArgs: 1, usage: "patch/restore/status [patch name]", CommandUsage.SERVER_ONLY)]
  public void PatcherCommand(CCSPlayerController? player, CommandInfo info)
  {

    void HandleStatusCommand()
    {
      Console.WriteLine("====================[ Patcher ]====================");
      foreach (var (name, p) in Config.Patches)
      {
        if (!_PatchedPatchs.Any(patch => patch.Name == name))
        {
          Console.WriteLine($"× {name}\t (Unpatched)");
        }
        else
        {
          var patch = _PatchedPatchs.First(patch => patch.Name == name);
          Console.WriteLine($"√ {name}\t (0x{patch.Addr:X})");
        }
      }
      Console.WriteLine("===================================================");
    }

    void HandlePatchCommand()
    {
      var name = info.GetArg(2);
      if (_PatchedPatchs.Any(patch => patch.Name == name))
      {
        info.ReplyToCommand($"Patch '{name}' has already patched.");
        return;
      }
      if (!Config.Patches.ContainsKey(name))
      {
        info.ReplyToCommand($"Cannot find patch '{name}'.");
        return;
      }
      ApplyPatch(name);
    }

    void HandleRestoreCommand()
    {
      var name = info.GetArg(2);
      if (!_PatchedPatchs.Any(patch => patch.Name == name))
      {
        info.ReplyToCommand($"Patch '{name}' is not patched.");
        return;
      }
      if (!Config.Patches.ContainsKey(name))
      {
        info.ReplyToCommand($"Cannot find patch '{name}'.");
        return;
      }
      var patch = _PatchedPatchs.First(patch => patch.Name == name);
      RestorePatch(patch);
      _PatchedPatchs.Remove(patch);
    }

    switch (info.GetArg(1))
    {
      case "status":
        HandleStatusCommand();
        break;
      case "patch":
        HandlePatchCommand();
        break;
      case "restore":
        HandleRestoreCommand();
        break;
      default:
        info.ReplyToCommand("Unknown command");
        break;
    }
  }
}

// credits to @xstage
// https://discord.com/channels/1160907911501991946/1297699678556524555
static class MemoryPatch {
  [DllImport("libc", EntryPoint = "mprotect")]
  public static extern int MProtect(nint address, int len, int protect);

  [DllImport("kernel32.dll")]
  public unsafe static extern bool VirtualProtect(nint address, int dwSize, int newProtect, int* oldProtect);

  public unsafe static bool SetMemAccess(nint addr, int size)
  {
    if (addr == nint.Zero)
      throw new ArgumentNullException(nameof(addr));

    const int PAGESIZE = 4096;

    nint LALIGN(nint addr) => addr & ~(PAGESIZE - 1);
    int LALDIF(nint addr) => (int)(addr % PAGESIZE);

    int* oldProtect = stackalloc int[1];

    return RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ?
        MProtect(LALIGN(addr), size + LALDIF(addr), 7) == 0 : VirtualProtect(addr, size, 0x40, oldProtect);
  }
}