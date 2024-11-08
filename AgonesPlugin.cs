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
    private AgonesSDK agones;
    private CSSTimer? shutdownTimer;
    private CSSTimer? healthTimer;

    public AgonesPlugin()
    {
        agones = new AgonesSDK(logger: Logger);
    }

    public override void Load(bool hotReload)
    {
        healthTimer ??= AddTimer(1, () => {
            Task.Run(async () => await agones.HealthAsync());
        }, CSSTimerFlags.REPEAT);
        Task.Run(async () => await agones.ReadyAsync());
    }

    [GameEventHandler]
    public HookResult OnServerSpawn(EventServerSpawn @event, GameEventInfo info)
    {
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null) {
            // TODO: LOG THIS! NEED TO FIGURE OUT IF / WHY THIS HAPPENS
            return HookResult.Continue;
        }
        shutdownTimer?.Kill();
        shutdownTimer = null;
        //Task.Run(async () => await agones.Beta().IncrementCounterAsync("players", 1));
        Task.Run(async () => await agones.Beta().AppendListValueAsync("players", player.AuthorizedSteamID?.SteamId64.ToString()));

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null) {
            // TODO: LOG THIS! NEED TO FIGURE OUT IF / WHY THIS HAPPENS
            return HookResult.Continue;
        }
        if (player.IsBot || player.IsHLTV || !player.IsValid) {
            return HookResult.Continue;
        }
        //Task.Run(async () => await agones.Beta().DecrementCounterAsync("players", 1));
        Task.Run(async () => await agones.Beta().DeleteListValueAsync("players", player.AuthorizedSteamID?.SteamId64.ToString()));
        if (PlayersConnected() == 0 && shutdownTimer == null) {
            shutdownTimer = AddTimer(60, () => {
                Task.Run(async () => await agones.ShutDownAsync());
            });
        }

        return HookResult.Continue;
    }

    private static int PlayersConnected()
    {
        return Utilities.GetPlayers().Where(player => player.IsValid && !player.IsHLTV && !player.IsBot).Count();
    }
}
