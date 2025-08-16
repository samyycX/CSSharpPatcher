using System.Data;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Extensions;
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

public class CSSharpPatcher : BasePlugin, IPluginConfig<PatcherConfig>
{
  public override string ModuleName => "CSSharpPatcher";

  public override string ModuleVersion => "1.0.0";

  public override string ModuleAuthor => "samyyc";

  public PatcherConfig Config { get; set; } = new();

  private Dictionary<nint, List<byte>> _PatchedAddrs = new();

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
    var i = 0;
    var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    foreach (var name in Config.EnabledPatches)
    {
      i++;
      var patch = Config.Patches[name];
      var entry = isWindows ? patch.windows : patch.linux;
      var addr = NativeAPI.FindSignature(GetModulePath(patch.module), entry.signature);
      if (addr == 0)
      {
        Logger.LogError($"[{i}/{Config.Patches.Count()}] Patch '{name}' has invalid signature, skipping...");
        continue;
      }
      var payload = entry.patch.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                   .Select(hex => Convert.ToByte(hex, 16))
                   .ToList();

      List<byte> originalBytes = new();
      MemoryPatch.SetMemAccess(addr, payload.Count());
      for (int j = 0; j < payload.Count(); j++)
      {
        originalBytes.Append(Marshal.ReadByte(addr, j));
        Marshal.WriteByte(addr, j, payload[j]);
      }

      _PatchedAddrs[addr] = originalBytes;

      Logger.LogInformation($"[{i}/{Config.Patches.Count()}] Patch '{name}' successfully patched.");

    }
  }

  public override void Unload(bool hotReload)
  {
    if (hotReload) return;
    if (!Config.RestoreWhenUnload) return;

    foreach (var (addr, originalBytes) in _PatchedAddrs)
    {
      MemoryPatch.SetMemAccess(addr, originalBytes.Count());
      for (int i = 0; i < originalBytes.Count(); i++)
      {
        Marshal.WriteByte(addr, i, originalBytes[i]);
      }
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