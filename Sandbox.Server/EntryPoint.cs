﻿using System.Diagnostics;
using SS14.Server;
using SS14.Server.Interfaces;
using SS14.Server.Interfaces.Chat;
using SS14.Server.Interfaces.Maps;
using SS14.Server.Interfaces.Player;
using SS14.Server.Player;
using SS14.Shared.Console;
using SS14.Shared.ContentPack;
using SS14.Shared.Enums;
using SS14.Shared.Interfaces.Map;
using SS14.Shared.Interfaces.Timers;
using SS14.Shared.Interfaces.Timing;
using SS14.Shared.IoC;
using SS14.Shared.Log;
using SS14.Shared.Map;
using SS14.Shared.Timers;

namespace Sandbox.Server
{
    /// <inheritdoc />
    public class EntryPoint : GameServer
    {
        private IBaseServer _server;
        private IPlayerManager _players;

        private bool _countdownStarted;

        /// <inheritdoc />
        public override void Init()
        {
            base.Init();

            _server = IoCManager.Resolve<IBaseServer>();
            _players = IoCManager.Resolve<IPlayerManager>();

            _server.RunLevelChanged += HandleRunLevelChanged;
            _players.PlayerStatusChanged += HandlePlayerStatusChanged;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _server.RunLevelChanged -= HandleRunLevelChanged;
            _players.PlayerStatusChanged -= HandlePlayerStatusChanged;

            base.Dispose();
        }

        private void HandleRunLevelChanged(object sender, RunLevelChangedEventArgs args)
        {
            switch (args.NewLevel)
            {
                case ServerRunLevel.PreGame:
                    IoCManager.Resolve<IPlayerManager>().FallbackSpawnPoint = new LocalCoordinates(0, 0, GridId.DefaultGrid, new MapId(2));

                    var timing = IoCManager.Resolve<IGameTiming>();
                    var mapLoader = IoCManager.Resolve<IMapLoader>();
                    var mapMan = IoCManager.Resolve<IMapManager>();

                    var startTime = timing.RealTime;
                    {
                        var newMap = mapMan.CreateMap(new MapId(2));

                        mapLoader.LoadBlueprint(newMap, new GridId(4), "Maps/Demo/DemoGrid.yaml");
                    }
                    var timeSpan = timing.RealTime - startTime;
                    Logger.Info($"Loaded map in {timeSpan.TotalMilliseconds:N2}ms.");

                    IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Round loaded!");
                    break;
                case ServerRunLevel.Game:
                    IoCManager.Resolve<IPlayerManager>().SendJoinGameToAll();
                    IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Round started!");
                    break;
                case ServerRunLevel.PostGame:
                    IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Round over!");
                    break;
            }
        }

        private void HandlePlayerStatusChanged(object sender, SessionStatusEventArgs args)
        {
            switch (args.NewStatus)
            {
                case SessionStatus.Connected:
                {
                    // timer time must be > tick length
                    IoCManager.Resolve<ITimerManager>().AddTimer(new Timer(250, false, () =>
                    {
                        args.Session.JoinLobby();
                    }));
                    IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Player joined server!", args.Session.Index);
                }
                    break;

                case SessionStatus.InLobby:
                {
                    // auto start game when first player joins
                    if (_server.RunLevel == ServerRunLevel.PreGame && !_countdownStarted)
                    {
                        _countdownStarted = true;
                        IoCManager.Resolve<ITimerManager>().AddTimer(new Timer(2000, false, () =>
                        {
                            _server.RunLevel = ServerRunLevel.Game;
                            _countdownStarted = false;
                        }));
                    }

                    IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Player joined Lobby!", args.Session.Index);
                }
                    break;

                case SessionStatus.InGame:
                {
                    //TODO: Check for existing mob and re-attach
                    IoCManager.Resolve<IPlayerManager>().SpawnPlayerMob(args.Session);

                    IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Player joined Game!", args.Session.Index);
                }
                    break;

                case SessionStatus.Disconnected:
                {
                    IoCManager.Resolve<IChatManager>().DispatchMessage(ChatChannel.Server, "Gamemode: Player left!", args.Session.Index);
                }
                    break;
            }
        }
    }
}
