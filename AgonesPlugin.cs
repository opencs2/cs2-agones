using Agones;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CSSTimer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CSSTimerFlags = CounterStrikeSharp.API.Modules.Timers.TimerFlags;
using Microsoft.Extensions.Logging;

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
        RegisterListener<Listeners.OnMapStart>(OnMapStart);

        Logger.LogInformation("[AgonesPlugin] Load: starting SDK Health Check task");
        healthTimer ??= AddTimer(1, () => {
            Task.Run(async () => await agones.HealthAsync());
        }, CSSTimerFlags.REPEAT);

        Logger.LogInformation("[AgonesPlugin] Load: Setting server to Ready state");
        Task.Run(async () => await agones.ReadyAsync());
    }

    [GameEventHandler]
    public HookResult OnServerSpawn(EventServerSpawn @event, GameEventInfo info)
    {
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        Logger.LogInformation("[AgonesPlugin] OnPlayerConnect");
        CCSPlayerController? player = @event.Userid;
        if (player == null || player.IsHLTV || player.IsBot || !player.IsValid) {
            return HookResult.Continue;
        }
        shutdownTimer?.Kill();
        shutdownTimer = null;
        Logger.LogInformation($"[AgonesPlugin] OnPlayerConnect: updating players list: {player.SteamID.ToString()}");
        Task.Run(async () => {
            await agones.Beta().AppendListValueAsync("players", player.SteamID.ToString());
        });

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        Logger.LogInformation("[AgonesPlugin] OnPlayerDisconnect");
        CCSPlayerController? player = @event.Userid;
        if (player == null) {
            return HookResult.Continue;
        }
        if (player.IsBot || player.IsHLTV || !player.IsValid) {
            return HookResult.Continue;
        }
        //Task.Run(async () => await agones.Beta().DecrementCounterAsync("players", 1));
        Logger.LogInformation($"[AgonesPlugin] OnPlayerDisconnect: updating players list: {player.SteamID.ToString()}");
        Task.Run(async () => await agones.Beta().DeleteListValueAsync("players", player.SteamID.ToString()));
        if (PlayersConnectedExcept(player) == 0 && shutdownTimer == null) {
            Logger.LogInformation("[AgonesPlugin] OnPlayerDisconnect: no players remain - starting shutdown timer");
            shutdownTimer = AddTimer(60, () => {
                Task.Run(async () => await agones.ShutDownAsync());
            });
        }

        return HookResult.Continue;
    }

    private void  OnMapStart(string mapName)
    {
        Logger.LogInformation($"[AgonesPlugin] OnMapStart {mapName}");
        Task.Run(async() => await agones.SetLabelAsync("cs2-map", mapName));
    }

    private static int PlayersConnected()
    {
        return Utilities.GetPlayers().Where(player => player.IsValid && !player.IsHLTV && !player.IsBot).Count();
    }

    private static int PlayersConnectedExcept(CCSPlayerController exception)
    {
        return Utilities.GetPlayers().Where(player => player.IsValid && !player.IsHLTV && !player.IsBot && player.SteamID != exception.SteamID).Count();
    }
}
