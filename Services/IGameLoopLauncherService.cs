using System.Collections.Generic;
using System.Threading.Tasks;
using XenomOptimizer.Models;

namespace XenomOptimizer.Services;

public interface IGameLoopLauncherService
{
    Task<bool> LaunchAppAsync(string packageName);
    Task<bool> LaunchSettingsAsync();
    Task<bool> LaunchPubgAsync();
    Task<bool> LaunchAppMarketAsync();
    Task<bool> InstallLocalApkAsync(string apkPath);
    Task<List<GameLoopAppItem>> GetLauncherAppsAsync();
    Task<string> DetectActivePubgPackageAsync();
}
