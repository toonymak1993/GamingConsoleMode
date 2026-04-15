using System.Threading;
using System.Threading.Tasks;

namespace gcmloader.Services;

public interface ILauncherService
{
    Task SwitchToConfiguredLauncherAsync(CancellationToken cancellationToken = default);

    Task SwitchToSpecificLauncherAsync(string launcherId, CancellationToken cancellationToken = default);

    Task StartSteamAsync(bool forceRestart = false, CancellationToken cancellationToken = default);

    Task StartPlayniteAsync(CancellationToken cancellationToken = default);

    Task StartGfnAsync(CancellationToken cancellationToken = default);

    Task StartOtherLauncherAsync(CancellationToken cancellationToken = default);
}
