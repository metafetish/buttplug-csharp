// <copyright file="ButtplugClient.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Logging;
using Buttplug.Core.Messages;
using JetBrains.Annotations;

namespace Buttplug.Client
{
    public class ButtplugClient
    {
        /// <summary>
        /// Name of the client, used for server UI/permissions.
        /// </summary>
        [NotNull]
        public readonly string Name;

        /// <summary>
        /// Event fired on Buttplug device added, either after connect or while scanning for devices.
        /// </summary>
        [CanBeNull]
        public event EventHandler<DeviceAddedEventArgs> DeviceAdded;

        /// <summary>
        /// Event fired on Buttplug device removed. Can fire at any time after device connection.
        /// </summary>
        [CanBeNull]
        public event EventHandler<DeviceRemovedEventArgs> DeviceRemoved;

        /// <summary>
        /// Fires when an error that was not provoked by a client action is received from the server,
        /// such as a device exception, message parsing error, etc... Server may possibly disconnect
        /// after this event fires.
        /// </summary>
        [CanBeNull]
        public event EventHandler<ButtplugExceptionEventArgs> ErrorReceived;

        /// <summary>
        /// Event fired when the server has finished scanning for devices.
        /// </summary>
        [CanBeNull]
        public event EventHandler ScanningFinished;

        /// <summary>
        /// Event fired when a server ping timeout has occured.
        /// </summary>
        [CanBeNull]
        public event EventHandler PingTimeout;

        /// <summary>
        /// Event fired when a server disconnect has occured.
        /// </summary>
        [CanBeNull]
        public event EventHandler ServerDisconnect;

        /// <summary>
        /// Event fired when the client receives a Log message. Should only fire if the client has
        /// requested that log messages be sent.
        /// </summary>
        [CanBeNull]
        public event EventHandler<LogEventArgs> Log;

        /// <summary>
        /// Gets list of devices currently connected to the server.
        /// </summary>
        /// <value>
        /// A list of connected Buttplug devices.
        /// </value>
        public ButtplugClientDevice[] Devices => _devices.Values.ToArray();

        /// <summary>
        /// Gets a value indicating whether the client is connected to a server.
        /// </summary>
        /// <value>
        /// Value indicating whether the client is connected to a server.
        /// </value>
        public bool Connected => _connector != null && _connector.Connected;

        /// <summary>
        /// Ping timer.
        /// </summary>
        /// <remarks>
        /// Sends a ping message to the server whenever the timer triggers. Usually runs at
        /// (requested ping interval / 2).
        /// </remarks>
        [CanBeNull]
        [UsedImplicitly]
        protected Timer _pingTimer;

        /// <summary>
        /// Global logger instance for the client.
        /// </summary>
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        [NotNull]
        private readonly IButtplugLog _bpLogger;

        [NotNull]
        private readonly IButtplugLogManager _bpLogManager;

        /// <summary>
        /// Stores information about devices currently connected to the server.
        /// </summary>
        [NotNull]
        private readonly ConcurrentDictionary<uint, ButtplugClientDevice> _devices =
            new ConcurrentDictionary<uint, ButtplugClientDevice>();

        /// <summary>
        /// Connector to use for the client. Can be local (server embedded), IPC, Websocket, etc...
        /// </summary>
        [NotNull]
        private readonly IButtplugClientConnector _connector;

        /// <summary>
        /// Initializes a new instance of the <see cref="ButtplugClient"/> class.
        /// </summary>
        /// <param name="aClientName">The name of the client (used by the server for UI and permissions).</param>
        /// <param name="aConnector">Connector for the client.</param>
        public ButtplugClient([NotNull] string aClientName, [NotNull] IButtplugClientConnector aConnector)
        {
            ButtplugUtils.ArgumentNotNull(aConnector, nameof(aConnector));
            Name = aClientName;
            _connector = aConnector;
            _connector.Disconnected += (aObj, aEventArgs) =>
            {
                ServerDisconnect?.Invoke(aObj, aEventArgs);
            };
            _connector.InvalidMessageReceived += ConnectorErrorHandler;

            _bpLogManager = new ButtplugLogManager();
            _connector.LogManager = _bpLogManager;
            _bpLogger = _bpLogManager.GetLogger(GetType());
            _bpLogger.Info("Finished setting up ButtplugClient");
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ButtplugClient"/> class, closing the connector if
        /// it is still open.
        /// </summary>
        ~ButtplugClient()
        {
            DisconnectAsync().Wait();
        }

        // ReSharper disable once UnusedMember.Global
        public async Task ConnectAsync(CancellationToken aToken = default(CancellationToken))
        {
            if (Connected)
            {
                throw new ButtplugHandshakeException(_bpLogger, "Client already connected to a server.");
            }

            _connector.MessageReceived += MessageReceivedHandler;
            await _connector.ConnectAsync(aToken).ConfigureAwait(false);

            var res = await SendMessageAsync(new RequestServerInfo(Name), aToken).ConfigureAwait(false);
            switch (res)
            {
                case ServerInfo si:
                    if (si.MaxPingTime > 0)
                    {
                        _pingTimer = new Timer(OnPingTimer, null, 0,
                            Convert.ToInt32(Math.Round(((double)si.MaxPingTime) / 2, 0)));
                    }

                    if (si.MessageVersion < ButtplugConsts.CurrentSpecVersion)
                    {
                        await DisconnectAsync().ConfigureAwait(false);
                        throw new ButtplugHandshakeException(_bpLogger,
                            $"Buttplug Server's schema version ({si.MessageVersion}) is less than the client's ({ButtplugConsts.CurrentSpecVersion}). A newer server is required.",
                            res.Id);
                    }

                    // Get full device list and populate internal list
                    var resp = await SendMessageAsync(new RequestDeviceList()).ConfigureAwait(false);
                    if (!(resp is DeviceList))
                    {
                        await DisconnectAsync().ConfigureAwait(false);
                        if (resp is Error errResp)
                        {
                            throw ButtplugException.FromError(_bpLogger, errResp);
                        }

                        throw new ButtplugHandshakeException(_bpLogger,
                            "Received unknown response to DeviceList handshake query");
                    }

                    foreach (var d in (resp as DeviceList).Devices)
                    {
                        if (_devices.ContainsKey(d.DeviceIndex))
                        {
                            continue;
                        }

                        var device = new ButtplugClientDevice(_bpLogManager, this, SendDeviceMessageAsync, d);
                        _devices[d.DeviceIndex] = device;
                        DeviceAdded?.Invoke(this, new DeviceAddedEventArgs(device));
                    }

                    break;

                case Error e:
                    await DisconnectAsync().ConfigureAwait(false);
                    throw ButtplugException.FromError(_bpLogger, e);

                default:
                    await DisconnectAsync().ConfigureAwait(false);
                    throw new ButtplugHandshakeException(_bpLogger, $"Unrecognized message {res.Name} during handshake", res.Id);
            }
        }

        public async Task DisconnectAsync()
        {
            if (!Connected)
            {
                return;
            }

            _connector.MessageReceived -= MessageReceivedHandler;
            await _connector.DisconnectAsync().ConfigureAwait(false);
            ServerDisconnect?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// Instructs the server to start scanning for devices. New devices will be raised as <see
        /// cref="DeviceAdded"/> events. When scanning completes, an <see cref="ScanningFinished"/>
        /// event will be triggered.
        /// </summary>
        /// <param name="aToken">Cancellation token, for cancelling action externally if it is not yet finished.</param>
        /// <returns>
        /// Void on success, throws <see cref="ButtplugClientException" /> otherwise.
        /// </returns>
        // ReSharper disable once UnusedMember.Global
        public async Task StartScanningAsync(CancellationToken aToken = default(CancellationToken))
        {
            await SendMessageExpectOk(new StartScanning(), aToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Instructs the server to stop scanning for devices. If scanning was in progress, a <see
        /// cref="ScanningFinished"/> event will be sent when the server has stopped scanning.
        /// </summary>
        /// <param name="aToken">Cancellation token, for cancelling action externally if it is not yet finished.</param>
        /// <returns>
        /// Void on success, throws <see cref="ButtplugClientException" /> otherwise.
        /// </returns>
        // ReSharper disable once UnusedMember.Global
        public async Task StopScanningAsync(CancellationToken aToken = default(CancellationToken))
        {
            await SendMessageExpectOk(new StopScanning(), aToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Instructs the server to either forward or stop log entries to the client. Log entries
        /// will be raised as <see cref="Log"/> events.
        /// </summary>
        /// <param name="aLogLevel">The maximum log level to send.</param>
        /// <param name="aToken">Cancellation token, for cancelling action externally if it is not yet finished.</param>
        /// <returns>
        /// Void on success, throws <see cref="ButtplugClientException" /> otherwise.
        /// </returns>
        // ReSharper disable once UnusedMember.Global
        public async Task RequestLogAsync(ButtplugLogLevel aLogLevel, CancellationToken aToken = default(CancellationToken))
        {
            await SendMessageExpectOk(new RequestLog(aLogLevel), aToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a DeviceMessage (e.g. <see cref="VibrateCmd"/> or <see cref="LinearCmd"/>). Handles
        /// constructing some parts of the message for the user.
        /// </summary>
        /// <param name="aDevice">The device to be controlled by the message.</param>
        /// <param name="aDeviceMsg">The device message (Id and DeviceIndex will be overriden).</param>
        /// <param name="aToken">Cancellation token, for cancelling action externally if it is not yet finished.</param>
        /// <returns>
        /// Void on success, throws <see cref="ButtplugClientException" /> otherwise.
        /// </returns>
        protected async Task SendDeviceMessageAsync(ButtplugClientDevice aDevice, ButtplugDeviceMessage aDeviceMsg, CancellationToken aToken = default(CancellationToken))
        {
            await SendMessageExpectOk(aDeviceMsg, aToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a message to the server, and handles asynchronously waiting for the reply from the server.
        /// </summary>
        /// <param name="aMsg">Message to send.</param>
        /// <param name="aToken">Cancellation token, for cancelling action externally if it is not yet finished.</param>
        /// <returns>The response, which will derive from <see cref="ButtplugMessage"/>.</returns>
        protected async Task<ButtplugMessage> SendMessageAsync(ButtplugMessage aMsg, CancellationToken aToken = default(CancellationToken))
        {
            if (!Connected)
            {
                throw new ButtplugClientConnectorException(_bpLogger, "Client not connected.");
            }

            return await _connector.SendAsync(aMsg, aToken).ConfigureAwait(false);
        }

        private void ConnectorErrorHandler(object aSender, ButtplugExceptionEventArgs aException)
        {
            ErrorReceived?.Invoke(this, aException);
        }

        /// <summary>
        /// Message Received event handler. Either tries to match incoming messages as replies to
        /// messages we've sent, or fires an event related to an incoming event, like device
        /// additions/removals, log messages, etc.
        /// </summary>
        /// <param name="aSender">Object sending the open event, unused.</param>
        /// <param name="aArgs">Event parameters, including the data received.</param>
        private async void MessageReceivedHandler(object aSender, MessageReceivedEventArgs aArgs)
        {
            var msg = aArgs.Message;

            switch (msg)
            {
                case Log l:
                    Log?.Invoke(this, new LogEventArgs(l));
                    break;

                case DeviceAdded d:
                    var dev = new ButtplugClientDevice(_bpLogManager, this, SendDeviceMessageAsync, d);
                    _devices.AddOrUpdate(d.DeviceIndex, dev, (u, device) => dev);
                    DeviceAdded?.Invoke(this, new DeviceAddedEventArgs(dev));
                    break;

                case DeviceRemoved d:
                    if (!_devices.ContainsKey(d.DeviceIndex))
                    {
                        ErrorReceived?.Invoke(this,
                            new ButtplugExceptionEventArgs(
                                new ButtplugDeviceException(_bpLogger,
                                    "Got device removed message for unknown device.",
                                    msg.Id)));
                        return;
                    }

                    var oldDev = _devices[d.DeviceIndex];
                    if (_devices.TryRemove(d.DeviceIndex, out _))
                    {
                        DeviceRemoved?.Invoke(this, new DeviceRemovedEventArgs(oldDev));
                    }

                    break;

                case ScanningFinished _:
                    // The scanning finished event is self explanatory and doesn't require extra arguments.
                    ScanningFinished?.Invoke(this, new EventArgs());
                    break;

                case Error e:
                    // This will both log the error and fire it from our ErrorReceived event handler.
                    ErrorReceived?.Invoke(this, new ButtplugExceptionEventArgs(ButtplugException.FromError(_bpLogger, e)));

                    if (e.ErrorCode == Error.ErrorClass.ERROR_PING)
                    {
                        PingTimeout?.Invoke(this, EventArgs.Empty);
                        await DisconnectAsync().ConfigureAwait(false);
                    }

                    break;

                default:
                    ErrorReceived?.Invoke(this,
                        new ButtplugExceptionEventArgs(
                            new ButtplugMessageException(_bpLogger,
                                $"Got unhandled message: {msg}",
                                msg.Id)));
                    break;
            }
        }

        /// <summary>
        /// Manages the ping timer, sending pings at the rate faster than requested by the server. If
        /// the ping timer handler does not run, it means the event loop is blocked, and the server
        /// will stop all devices and disconnect.
        /// </summary>
        /// <param name="aState">State of the Timer.</param>
        private async void OnPingTimer(object aState)
        {
            try
            {
                await SendMessageExpectOk(new Ping()).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                ErrorReceived?.Invoke(_bpLogger, new ButtplugExceptionEventArgs(new ButtplugPingException(_bpLogger, "Exception thrown during ping update", ButtplugConsts.SystemMsgId, e)));

                // If SendMessageAsync throws, we're probably already disconnected, but just make sure.
                await DisconnectAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Sends a message, expecting a response of message type <see cref="Ok"/>.
        /// </summary>
        /// <param name="aMsg">Message to send.</param>
        /// <param name="aToken">Cancellation token, for cancelling action externally if it is not yet finished.</param>
        /// <returns>True if successful.</returns>
        private async Task SendMessageExpectOk(ButtplugMessage aMsg, CancellationToken aToken = default(CancellationToken))
        {
            var msg = await SendMessageAsync(aMsg, aToken).ConfigureAwait(false);
            switch (msg)
            {
                case Ok _:
                    return;
                case Error err:
                    throw ButtplugException.FromError(_bpLogger, err);
                default:
                    throw new ButtplugMessageException(_bpLogger, $"Message type {msg.Name} not handled by SendMessageExpectOk", msg.Id);
            }
        }
    }
}