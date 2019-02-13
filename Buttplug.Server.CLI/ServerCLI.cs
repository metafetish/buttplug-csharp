﻿using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using Buttplug.Client;
using Buttplug.Core.Logging;
using Buttplug.Server.Connectors.WebsocketServer;
using Buttplug.Server.Connectors;
using Google.Protobuf;

namespace Buttplug.Server.CLI
{
    class ServerCLI
    {
        private bool _hasGuiPipe;
        private NamedPipeClientStream _guiPipe;
        public bool ServerReady { get; }
        private DeviceManager _deviceManager;

        // Simple server that exposes device manager, since we'll need to chain device managers
        // through it for this. This is required because Windows 10 has problems disconnecting from
        // BLE devices without completely stopping and restarting processes. :(
        class CLIServer : ButtplugServer
        {
            public DeviceManager DeviceManager => _deviceManager;
            public IButtplugLogManager LogManager => BpLogManager;

            public CLIServer(string aServerName, uint aMaxPingTime, DeviceManager aDevMgr)
            : base(aServerName, aMaxPingTime, aDevMgr)
            {
            }
        }


        public ServerCLI()
        {

        }

        private async Task SendGuiMessage(GuiMessage aMsg)
        {
            if (_guiPipe == null)
            {
                return;
            }
            var arr = aMsg.ToByteArray();
            await _guiPipe.WriteAsync(aMsg.ToByteArray(), 0, arr.Length);
        }

        private async Task PrintGuiLog(string aGuiLogMsg)
        {
            if (_guiPipe != null)
            {
                var msg = new GuiMessage { Guilog = new GuiMessage.Types.GuiLog { Message = aGuiLogMsg } };
                await SendGuiMessage(msg);
            }
            else
            {
                Console.WriteLine(aGuiLogMsg);
            }
        }

        public void RunServer(Options aOptions)
        {
            if (aOptions.Version)
            {
                Console.WriteLine(int.Parse(ThisAssembly.Git.Commits) == 0
                    ? ThisAssembly.Git.BaseTag
                    : ThisAssembly.Git.Tag);
                return;
            }

            if (aOptions.GuiPipe != null)
            {
                // Set up IPC Pipe and wait for connection before continuing, so any errors will show
                // up in the GUI.
                _hasGuiPipe = true;
                _guiPipe =
                    new NamedPipeClientStream(".", aOptions.GuiPipe, PipeDirection.InOut);
                try
                {
                    _guiPipe.Connect(100);
                }
                catch(TimeoutException)
                {
                    Console.WriteLine("Can't connect to GUI pipe! Exiting.");
                    return;
                }

                SendGuiMessage(new GuiMessage {Processstarted = new GuiMessage.Types.ProcessStarted()}).Wait();
            }

            if (aOptions.WebsocketServer && aOptions.IpcServer)
            {
                PrintGuiLog("ERROR: Can't run websocket server and IPC server at the same time!").Wait();
                return;
            }

            if (!aOptions.WebsocketServer && !aOptions.IpcServer)
            {
                PrintGuiLog("ERROR: Must specify either IPC server or Websocket server!").Wait();
                return;
            }

            var logLevel = ButtplugLogLevel.Off;
            if (aOptions.Log != null)
            {
                if (!Enum.TryParse(aOptions.Log, out logLevel))
                {
                    PrintGuiLog("ERROR: Invalid log level!").Wait();
                    return;
                }
            }

            ButtplugServer ServerFactory()
            {
                var server = new CLIServer(aOptions.ServerName, (uint)aOptions.PingTime, _deviceManager);
                if (_deviceManager == null)
                {
                    _deviceManager = server.DeviceManager;
                }

                if (logLevel != ButtplugLogLevel.Off)
                {
                    server.LogManager.AddLogListener(logLevel, async (aLogMsg) =>
                    {
                        await PrintGuiLog(aLogMsg.LogMessage);
                    });
                }

                return server;
            }

            if (aOptions.WebsocketServer)
            {
                var server = new ButtplugWebsocketServer();
                server.StartServerAsync(ServerFactory, 1, aOptions.Port, true, aOptions.WebsocketSecure, aOptions.Host).Wait();
                var wait = new TaskCompletionSource<bool>();
                server.ConnectionClosed += (aSender, aArgs) => { wait.SetResult(true); };
                PrintGuiLog("Websocket Server now running...").Wait();
                wait.Task.Wait();
            }
            else if (aOptions.IpcServer)
            {
                var server = new ButtplugIPCServer();
                server.StartServer(ServerFactory, aOptions.IpcPipe);
                var wait = new TaskCompletionSource<bool>();
                server.ConnectionClosed += (aSender, aArgs) => { wait.SetResult(true); };
                PrintGuiLog("IPC Server now running...").Wait();
                wait.Task.Wait();
            }
        }
    }
}
