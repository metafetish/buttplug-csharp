// <copyright file="ButtplugClientDevice.cs" company="Nonpolynomial Labs LLC">
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
using Buttplug.Core.Logging;
using Buttplug.Core.Messages;
using JetBrains.Annotations;

namespace Buttplug.Client
{
    public delegate void SensorEventHandler(object sender, object data);

    /// <summary>
    /// The Buttplug Client representation of a Buttplug Device connected to a server.
    /// </summary>
    public class ButtplugClientDevice : IEquatable<ButtplugClientDevice>
    {
        /// <summary>
        /// The device index, which uniquely identifies the device on the server.
        /// </summary>
        /// <remarks>
        /// If a device is removed, this may be the only populated field. If the same device
        /// reconnects, the index should be reused.
        /// </remarks>
        public readonly uint Index;

        /// <summary>
        /// The device name, which usually contains the device brand and model.
        /// </summary>
        [NotNull]
        public readonly string Name;

        /// <summary>
        /// The Buttplug Protocol messages supported by this device, with additional attributes.
        /// </summary>
        [NotNull]
        public Dictionary<Type, MessageAttributes> AllowedMessages { get; }

        private readonly ButtplugClient _owningClient;

        private readonly Func<ButtplugClientDevice, ButtplugDeviceMessage, CancellationToken, Task> _sendClosure;

        [NotNull]
        private readonly IButtplugLog _bpLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ButtplugClientDevice"/> class, using
        /// information received via a DeviceList, DeviceAdded, or DeviceRemoved message from the server.
        /// </summary>
        /// <param name="aDevInfo">
        /// A Buttplug protocol message implementing the IButtplugDeviceInfoMessage interface.
        /// </param>
        public ButtplugClientDevice(IButtplugLogManager aLogManager,
            ButtplugClient aOwningClient,
            Func<ButtplugClientDevice, ButtplugDeviceMessage, CancellationToken, Task> aSendClosure,
            IButtplugDeviceInfoMessage aDevInfo)
           : this(aLogManager, aOwningClient, aSendClosure, aDevInfo.DeviceIndex, aDevInfo.DeviceName, aDevInfo.DeviceMessages)
        {
            ButtplugUtils.ArgumentNotNull(aDevInfo, nameof(aDevInfo));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ButtplugClientDevice"/> class, using
        /// discrete parameters.
        /// </summary>
        /// <param name="aIndex">The device index.</param>
        /// <param name="aName">The device name.</param>
        /// <param name="aMessages">The device allowed message list, with corresponding attributes.</param>
        public ButtplugClientDevice(IButtplugLogManager aLogManager,
            ButtplugClient aOwningClient,
            Func<ButtplugClientDevice, ButtplugDeviceMessage, CancellationToken, Task> aSendClosure,
            uint aIndex,
            string aName,
            Dictionary<string, MessageAttributes> aMessages)
        {
            ButtplugUtils.ArgumentNotNull(aLogManager, nameof(aLogManager));
            ButtplugUtils.ArgumentNotNull(aOwningClient, nameof(aOwningClient));
            ButtplugUtils.ArgumentNotNull(aSendClosure, nameof(aSendClosure));
            _bpLogger = aLogManager.GetLogger(GetType());
            _owningClient = aOwningClient;
            _sendClosure = aSendClosure;
            Index = aIndex;
            Name = aName;
            AllowedMessages = new Dictionary<Type, MessageAttributes>();
            foreach (var msg in aMessages)
            {
                var msgType = ButtplugUtils.GetMessageType(msg.Key);
                if (msgType == null)
                {
                    throw new ButtplugDeviceException($"Message type {msg.Key} does not exist.");
                }

                AllowedMessages[msgType] = msg.Value;
            }
        }

        public MessageAttributes GetMessageAttributes(Type aMsgType)
        {
            ButtplugUtils.ArgumentNotNull(aMsgType, nameof(aMsgType));
            if (!aMsgType.IsSubclassOf(typeof(ButtplugDeviceMessage)))
            {
                throw new ArgumentException("Argument must be subclass of ButtplugDeviceMessage");
            }

            if (!AllowedMessages.ContainsKey(aMsgType))
            {
                throw new ButtplugDeviceException($"Message type {aMsgType.Name} not allowed for device {Name}.");
            }

            return AllowedMessages[aMsgType];
        }

        public MessageAttributes GetMessageAttributes<T>()
        where T : ButtplugDeviceMessage
        {
            return GetMessageAttributes(typeof(T));
        }

        public async Task SendMessageAsync(ButtplugDeviceMessage aMsg, CancellationToken aToken = default(CancellationToken))
        {
            ButtplugUtils.ArgumentNotNull(aMsg, nameof(aMsg));

            if (!_owningClient.Connected)
            {
                throw new ButtplugClientConnectorException(_bpLogger, "Client that owns device is not connected");
            }

            if (!_owningClient.Devices.Contains(this))
            {
                throw new ButtplugDeviceException(_bpLogger, "Device no longer connected or valid");
            }

            if (!AllowedMessages.ContainsKey(aMsg.GetType()))
            {
                throw new ButtplugDeviceException(_bpLogger,
                    $"Device {Name} does not support message type {aMsg.GetType().Name}");
            }

            aMsg.DeviceIndex = Index;

            await _sendClosure(this, aMsg, aToken).ConfigureAwait(false);
        }

        public bool Equals(ButtplugClientDevice aDevice)
        {
            if (_owningClient != aDevice._owningClient ||
                Index != aDevice.Index ||
                Name != aDevice.Name ||
                AllowedMessages.Count != aDevice.AllowedMessages.Count ||
                !AllowedMessages.Keys.SequenceEqual(aDevice.AllowedMessages.Keys))
            {
                return false;
            }

            // If we made it this far, do actual value comparison in the attributes
            try
            {
                // https://stackoverflow.com/a/9547463/4040754
                return !aDevice.AllowedMessages.Where(entry => !AllowedMessages[entry.Key].Equals(entry.Value))
                    .ToDictionary(entry => entry.Key, entry => entry.Value).Any();
            }
            catch
            {
                return false;
            }
        }

        public void CheckGenericSubcommandList<T>(IEnumerable<T> aCmdList, uint aLimitValue)
            where T : GenericMessageSubcommand
        {
            if (!aCmdList.Any() || aCmdList.Count() > aLimitValue)
            {
                if (aLimitValue == 1)
                {
                    throw new ButtplugDeviceException(_bpLogger, $"{typeof(T).Name} requires 1 subcommand for this device, {aCmdList.Count()} present.");
                }

                throw new ButtplugDeviceException(_bpLogger, $"{typeof(T).Name} requires between 1 and {aLimitValue} subcommands for this device, {aCmdList.Count()} present.");
            }

            foreach (var cmd in aCmdList)
            {
                if (cmd.Index >= aLimitValue)
                {
                    throw new ButtplugDeviceException(_bpLogger, $"Index {cmd.Index} is out of bounds for {typeof(T).Name} for this device.");
                }
            }
        }

        private void CheckAllowedMessageType<T>()
        where T : ButtplugDeviceMessage
        {
            if (!AllowedMessages.ContainsKey(typeof(T)))
            {
                throw new ButtplugDeviceException($"Device {Name} does not support message type {typeof(T).Name}");
            }
        }

        public async Task ReadSensorCommand(StandardSensorModalities modality)
        {
            CheckAllowedMessageType<ReadSensorCmd>();
            await SendMessageAsync(ReadSensorCmd.Create(modality)).ConfigureAwait(false);
        }

        public async Task SubscribeSensorCommand(StandardSensorModalities modality, SensorEventHandler callback)
        {
            CheckAllowedMessageType<SubscribeSensorCmd>();
            await SendMessageAsync(SubscribeSensorCmd.Create(modality, callback, true)).ConfigureAwait(false);
        }

        public async Task UnsubscribeSensorCommand(StandardSensorModalities modality, SensorEventHandler callback)
        {
            CheckAllowedMessageType<SubscribeSensorCmd>();
            await SendMessageAsync(SubscribeSensorCmd.Create(modality, callback, false)).ConfigureAwait(false);
        }

        public async Task SendVibrateCmd(double aSpeed)
        {
            CheckAllowedMessageType<VibrateCmd>();
            await SendMessageAsync(VibrateCmd.Create(aSpeed, GetMessageAttributes<VibrateCmd>().FeatureCount.Value)).ConfigureAwait(false);
        }

        public async Task SendVibrateCmd(IEnumerable<double> aCmds)
        {
            CheckAllowedMessageType<VibrateCmd>();
            var msg = VibrateCmd.Create(aCmds);
            CheckGenericSubcommandList(msg.Speeds, GetMessageAttributes<VibrateCmd>().FeatureCount.Value);
            await SendMessageAsync(VibrateCmd.Create(aCmds)).ConfigureAwait(false);
        }

        public async Task SendRotateCmd(double aSpeed, bool aClockwise)
        {
            CheckAllowedMessageType<RotateCmd>();
            await SendMessageAsync(RotateCmd.Create(aSpeed, aClockwise, GetMessageAttributes<RotateCmd>().FeatureCount.Value)).ConfigureAwait(false);
        }

        public async Task SendRotateCmd(IEnumerable<(double, bool)> aCmds)
        {
            CheckAllowedMessageType<RotateCmd>();
            var msg = RotateCmd.Create(aCmds);
            CheckGenericSubcommandList(msg.Rotations, GetMessageAttributes<RotateCmd>().FeatureCount.Value);
            await SendMessageAsync(RotateCmd.Create(aCmds)).ConfigureAwait(false);
        }

        public async Task SendLinearCmd(uint aDuration, double aPosition)
        {
            CheckAllowedMessageType<LinearCmd>();
            await SendMessageAsync(LinearCmd.Create(aDuration, aPosition, GetMessageAttributes<LinearCmd>().FeatureCount.Value)).ConfigureAwait(false);
        }

        public async Task SendLinearCmd(IEnumerable<(uint, double)> aCmds)
        {
            CheckAllowedMessageType<LinearCmd>();
            var msg = LinearCmd.Create(aCmds);
            CheckGenericSubcommandList(msg.Vectors, GetMessageAttributes<LinearCmd>().FeatureCount.Value);
            await SendMessageAsync(LinearCmd.Create(aCmds)).ConfigureAwait(false);
        }

        public async Task SendFleshlightLaunchFW12Cmd(uint aSpeed, uint aPosition)
        {
            CheckAllowedMessageType<FleshlightLaunchFW12Cmd>();
            await SendMessageAsync(new FleshlightLaunchFW12Cmd(Index, aSpeed, aPosition)).ConfigureAwait(false);
        }

        public async Task SendLovenseCmd(string aDeviceCmd)
        {
            CheckAllowedMessageType<LovenseCmd>();
            await SendMessageAsync(new LovenseCmd(Index, aDeviceCmd)).ConfigureAwait(false);
        }

        public async Task SendVorzeA10CycloneCmd(uint aSpeed, bool aClockwise)
        {
            CheckAllowedMessageType<VorzeA10CycloneCmd>();
            await SendMessageAsync(new VorzeA10CycloneCmd(Index, aSpeed, aClockwise)).ConfigureAwait(false);
        }

        public async Task StopDeviceCmd()
        {
            // Every message should support this, but it doesn't hurt to check
            CheckAllowedMessageType<StopDeviceCmd>();
            await SendMessageAsync(new StopDeviceCmd(Index)).ConfigureAwait(false);
        }

        public async Task KiirooCmd(uint aPosition)
        {
            CheckAllowedMessageType<KiirooCmd>();
            await SendMessageAsync(new KiirooCmd(Index, aPosition)).ConfigureAwait(false);
        }
    }
}
