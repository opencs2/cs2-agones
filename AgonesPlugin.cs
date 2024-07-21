using Agones;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CSSTimer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CSSTimerFlags = CounterStrikeSharp.API.Modules.Timers.TimerFlags;

namespace AgonesPlugin;

public class AgonesPlugin : BasePlugin
{
    public override string ModuleName => "AgonesPlugin";
    public override string ModuleVersion => "0.0.1";
    public override string ModuleAuthor => "krbtgt";
    public override string ModuleDescription => "Provides integration with Agones";
    private AgonesSDK agones = new AgonesSDK();
    private CSSTimer? shutdownTimer;
    private CSSTimer? healthTimer;
    public override void Load(bool hotReload)
    {
        healthTimer ??= AddTimer(1, () => {
            Task.Run(async () => await agones.HealthAsync());
        }, CSSTimerFlags.REPEAT);
    }

    [GameEventHandler]
    public HookResult OnServerSpawn(EventServerSpawn @event, GameEventInfo info)
    {
        Task.Run(async () => await agones.ReadyAsync());
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        shutdownTimer?.Kill();
        shutdownTimer = null;
        Task.Run(async () => await agones.Beta().IncrementCounterAsync("players", 1));

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        Task.Run(async () => await agones.Beta().DecrementCounterAsync("players", 1));
        if (!Utilities.GetPlayers().Any()) {
            shutdownTimer = AddTimer(60, () => {
                Task.Run(async () => await agones.ShutDownAsync());
            });
        }
        
        return HookResult.Continue;
    }
}
