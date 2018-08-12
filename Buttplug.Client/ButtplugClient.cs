﻿// <copyright file="ButtplugClient.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Core;
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
        public readonly string ClientName;

        /// <summary>
        /// Gets the connected Buttplug devices.
        /// </summary>
        public ButtplugClientDevice[] Devices => _devices.Values.ToArray();

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
        /// Status of the client connection.
        /// </summary>
        /// <returns>True if client is currently connected.</returns>
        public bool Connected => _connector != null && _connector.Connected;

        /// <summary>
        /// Global logger instance for the client.
        /// </summary>
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        [NotNull]
        private readonly IButtplugLog _bpLogger;

        /// <summary>
        /// Stores information about devices currently connected to the server.
        /// </summary>
        [NotNull]
        private readonly Dictionary<uint, ButtplugClientDevice> _devices =
            new Dictionary<uint, ButtplugClientDevice>();

        /// <summary>
        /// Connector to use for the client. Can be local (server embedded), IPC, Websocket, etc...
        /// </summary>
        private IButtplugClientConnector _connector;

        /// <summary>
        /// Initializes a new instance of the <see cref="ButtplugClient"/> class.
        /// </summary>
        /// <param name="aClientName">The name of the client (used by the server for UI and permissions).</param>
        /// <param name="aConnector">Connector for the client</param>
        public ButtplugClient(string aClientName, IButtplugClientConnector aConnector)
        {
            ClientName = aClientName;
            _connector = aConnector;
            IButtplugLogManager bpLogManager = new ButtplugLogManager();
            _bpLogger = bpLogManager.GetLogger(GetType());
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

        /// <summary>
        /// Message Received event handler. Either tries to match incoming messages as replies to
        /// messages we've sent, or fires an event related to an incoming event, like device
        /// additions/removals, log messages, etc.
        /// </summary>
        /// <param name="aSender">Object sending the open event, unused.</param>
        /// <param name="aArgs">Event parameters, including the data received.</param>
        private void MessageReceivedHandler(object aSender, MessageReceivedEventArgs aArgs)
        {
            var msg = aArgs.Message;

            switch (msg)
            {
                case Log l:
                    Log?.Invoke(this, new LogEventArgs(l));
                    break;

                case DeviceAdded d:
                    var dev = new ButtplugClientDevice(d);
                    _devices.Add(d.DeviceIndex, dev);
                    DeviceAdded?.Invoke(this, new DeviceAddedEventArgs(dev));
                    break;

                case DeviceRemoved d:
                    if (!_devices.ContainsKey(d.DeviceIndex))
                    {
                        // TODO: Throw error?
                        return;
                    }

                    var oldDev = _devices[d.DeviceIndex];
                    _devices.Remove(d.DeviceIndex);
                    DeviceRemoved?.Invoke(this, new DeviceRemovedEventArgs(oldDev));
                    break;

                case ScanningFinished sf:
                    // The scanning finished event is self explanatory and doesn't require extra arguments.
                    ScanningFinished?.Invoke(this, new EventArgs());
                    break;
            }
        }

        public async Task ConnectAsync(CancellationToken aToken = default(CancellationToken))
        {
            if (Connected)
            {
                return;
            }

            _connector.MessageReceived += MessageReceivedHandler;
            await _connector.ConnectAsync(aToken);

            // TODO Handle Client/Server Handshake!
            /*
            var res = await SendMessage(new RequestServerInfo(_clientName));
            switch (res)
            {
                case ServerInfo si:
                    if (si.MaxPingTime > 0)
                    {
                        _pingTimer = new Timer(OnPingTimer, null, 0,
                            Convert.ToInt32(Math.Round(((double)si.MaxPingTime) / 2, 0)));
                    }

                    if (si.MessageVersion < ButtplugMessage.CurrentSchemaVersion)
                    {
                        throw new Exception("Buttplug Server's schema version (" + si.MessageVersion +
                                            ") is less than the client's (" + ButtplugMessage.CurrentSchemaVersion +
                                            "). A newer server is required.");
                    }

                    // Get full device list and populate internal list
                    var resp = await SendMessage(new RequestDeviceList());
                    if ((resp as DeviceList)?.Devices == null)
                    {
                        if (resp is Error)
                        {
                            _owningDispatcher.Send(
                                _ => { ErrorReceived?.Invoke(this, new ErrorEventArgs(resp as Error)); }, null);
                        }

                        return;
                    }

                    foreach (var d in (resp as DeviceList).Devices)
                    {
                        if (_devices.ContainsKey(d.DeviceIndex))
                        {
                            continue;
                        }

                        var device = new ButtplugClientDevice(d);
                        if (_devices.TryAdd(d.DeviceIndex, device))
                        {
                            _owningDispatcher.Send(
                                _ => { DeviceAdded?.Invoke(this, new DeviceEventArgs(device, DeviceAction.ADDED)); },
                                null);
                        }
                    }

                    break;

                case Error e:
                    throw new Exception(e.ErrorMessage);

                default:
                    throw new Exception("Unexpecte message returned: " + res.GetType());
            }*/
        }

        public async Task DisconnectAsync()
        {
            if (!Connected)
            {
                return;
            }

            _connector.MessageReceived -= MessageReceivedHandler;
            await _connector.DisconnectAsync();
            ServerDisconnect?.Invoke(this, new EventArgs());
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
                var msg = await SendMessageAsync(new Ping());
                if (!(msg is Error))
                {
                    return;
                }

                PingTimeout?.Invoke(this, new EventArgs());

                // TODO This exception goes nowhere. Why is this even thrown.
                throw new Exception((msg as Error).ErrorMessage);
            }
            catch
            {
                await DisconnectAsync();
            }
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
        public async Task StartScanningAsync(CancellationToken aToken = default(CancellationToken))
        {
            await SendMessageExpectOk(new StartScanning(), aToken);
        }

        /// <summary>
        /// Instructs the server to stop scanning for devices. If scanning was in progress, a <see
        /// cref="ScanningFinished"/> event will be sent when the server has stopped scanning.
        /// </summary>
        /// <param name="aToken">Cancellation token, for cancelling action externally if it is not yet finished.</param>
        /// <returns>
        /// Void on success, throws <see cref="ButtplugClientException" /> otherwise.
        /// </returns>
        public async Task StopScanningAsync(CancellationToken aToken = default(CancellationToken))
        {
            await SendMessageExpectOk(new StopScanning(), aToken);
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
        public async Task RequestLogAsync(string aLogLevel, CancellationToken aToken = default(CancellationToken))
        {
            await SendMessageExpectOk(new RequestLog(aLogLevel), aToken);
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
        public async Task SendDeviceMessageAsync(ButtplugClientDevice aDevice, ButtplugDeviceMessage aDeviceMsg, CancellationToken aToken = default(CancellationToken))
        {
            if (!_devices.TryGetValue(aDevice.Index, out ButtplugClientDevice dev))
            {
                throw new ButtplugClientException(new Error("Device not available.", Error.ErrorClass.ERROR_DEVICE, ButtplugConsts.SystemMsgId));
            }

            if (!dev.AllowedMessages.ContainsKey(aDeviceMsg.GetType().Name))
            {
                throw new ButtplugClientException(new Error("Device does not accept message type: " + aDeviceMsg.GetType().Name, Error.ErrorClass.ERROR_DEVICE, ButtplugConsts.SystemMsgId));
            }

            aDeviceMsg.DeviceIndex = aDevice.Index;
            await SendMessageExpectOk(aDeviceMsg, aToken);
        }

        /// <summary>
        /// Sends a message, expecting a response of message type <see cref="Ok"/>.
        /// </summary>
        /// <param name="aMsg">Message to send.</param>
        /// <param name="aToken">Cancellation token, for cancelling action externally if it is not yet finished.</param>
        /// <returns>True if successful.</returns>
        private async Task SendMessageExpectOk(ButtplugMessage aMsg, CancellationToken aToken = default(CancellationToken))
        {
            var msg = await SendMessageAsync(aMsg, aToken);
            if (!(msg is Ok))
            {
                throw new ButtplugClientException(msg);
            }
        }

        /// <summary>
        /// Sends a message to the server, and handles asynchronously waiting for the reply from the server.
        /// </summary>
        /// <param name="aMsg">Message to send.</param>
        /// <param name="aToken">Cancellation token, for cancelling action externally if it is not yet finished.</param>
        /// <returns>The response, which will derive from <see cref="ButtplugMessage"/>.</returns>
        protected async Task<ButtplugMessage> SendMessageAsync(ButtplugMessage aMsg, CancellationToken aToken = default(CancellationToken))
        {
            return await _connector.SendAsync(aMsg, aToken);
        }
    }
}