// <copyright file="Cueme.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Buttplug.Core;
using Buttplug.Core.Logging;
using Buttplug.Core.Messages;
using Buttplug.Devices.Configuration;

namespace Buttplug.Devices.Protocols
{
    internal class CuemeProtocol : ButtplugDeviceProtocol
    {
        private double[] _vibratorSpeeds = { 0 };
        private double[] _nullSpeed = { 0 };
        private uint _vibeIndex;

        // When alternating vibes, switch twice a second
        private static readonly uint DelayTimeMS = 500;

        private readonly System.Timers.Timer _updateValueTimer = new System.Timers.Timer();
        private CancellationTokenSource _stopUpdateCommandSource = new CancellationTokenSource();

        public CuemeProtocol(IButtplugLogManager aLogManager,
                             IButtplugDeviceImpl aInterface)
            : base(aLogManager,
                   "Cueme Unknown",
                   aInterface)
        {
            var bits = aInterface.Name.Split('_');
            DeviceConfigIdentifier = bits.Length == 3 ? bits[2] : "Unknown";

            // Create a new timer that wont fire any events just yet
            _updateValueTimer.Interval = DelayTimeMS;
            _updateValueTimer.Elapsed += CuemeUpdateHandler;
            _updateValueTimer.Enabled = false;
            aInterface.DeviceRemoved += OnDeviceRemoved;

            AddMessageHandler<StopDeviceCmd>(HandleStopDeviceCmd);
        }

        public override Task ConfigureAsync(DeviceConfiguration aConfig, CancellationToken aToken)
        {
            foreach (var command in aConfig.Attributes)
            {
                switch (command.Key)
                {
                    case "VibrateCmd":
                        // Add messages
                        AddMessageHandler<VibrateCmd>(HandleVibrateCmd, command.Value);
                        AddMessageHandler<SingleMotorVibrateCmd>(HandleSingleMotorVibrateCmd);

                        // Any other logic
                        _nullSpeed = new double[command.Value.FeatureCount ?? 1];
                        _vibratorSpeeds = new double[command.Value.FeatureCount ?? 1];
                        for (var i = 0; i < (command.Value.FeatureCount ?? 1); i++)
                        {
                            _nullSpeed[i] = 0;
                            _vibratorSpeeds[i] = 0;
                        }

                        break;

                    default:
                        BpLogger.Warn($"Protocol {GetType().Name} doesn't have support for configuration specified message {command.Key}. Skipping");
                        break;
                }
            }

            return Task.FromResult<ButtplugMessage>(new Ok(ButtplugConsts.SystemMsgId));
        }

        private void OnDeviceRemoved(object aEvent, EventArgs aArgs)
        {
            // Timer should be turned off on removal.
            _updateValueTimer.Enabled = false;

            // Clean up event handler for that magic day when devices manage to disconnect.
            Interface.DeviceRemoved -= OnDeviceRemoved;
        }

        private async void CuemeUpdateHandler(object aEvent, ElapsedEventArgs aArgs)
        {
            if (_vibratorSpeeds.SequenceEqual(_nullSpeed))
            {
                _updateValueTimer.Enabled = false;
                _vibeIndex = 0;
            }

            var data = Convert.ToByte((int)(_vibratorSpeeds[_vibeIndex] * 15));
            if (data != 0x00)
            {
                data |= (byte)((byte)(_vibeIndex + 1) << 4);
            }

            // We'll have to use an internal token here since this is timer triggered.
            try
            {
                // todo This throw doesn't actually go anywhere. This should bubble upward.
                await Interface.WriteValueAsync(new[] { data }, _stopUpdateCommandSource.Token).ConfigureAwait(false);
            }
            catch (ButtplugDeviceException ex)
            {
                BpLogger.Error($"Cannot send update to Cueme {Name}, device may stick on a single vibrator.");
                _updateValueTimer.Enabled = false;
            }

            if (!_updateValueTimer.Enabled || aArgs == null)
            {
                return;
            }

            // Move to the next active vibe
            var nextVibe = _vibeIndex;
            while (true)
            {
                nextVibe++;

                // Wrap back to 0
                if (nextVibe == _vibratorSpeeds.Length)
                {
                    nextVibe = 0;
                }

                if (Math.Abs(_vibratorSpeeds[nextVibe]) > 0.0)
                {
                    _vibeIndex = nextVibe;
                    break;
                }

                if (nextVibe == _vibeIndex)
                {
                    // Stop the timer: there's only one vibe running
                    _updateValueTimer.Enabled = false;
                    break;
                }
            }
        }

        private async Task<ButtplugMessage> HandleStopDeviceCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            BpLogger.Debug("Stopping Device " + Name);
            return await HandleSingleMotorVibrateCmd(new SingleMotorVibrateCmd(aMsg.DeviceIndex, 0, aMsg.Id), aToken).ConfigureAwait(false);
        }

        private async Task<ButtplugMessage> HandleSingleMotorVibrateCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            var cmdMsg = CheckMessageHandler<SingleMotorVibrateCmd>(aMsg);

            return await HandleVibrateCmd(VibrateCmd.Create(cmdMsg.DeviceIndex, cmdMsg.Id, cmdMsg.Speed, (uint)_vibratorSpeeds.Length), aToken).ConfigureAwait(false);
        }

        private Task<ButtplugMessage> HandleVibrateCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            var cmdMsg = CheckGenericMessageHandler<VibrateCmd>(aMsg, (uint)_vibratorSpeeds.Length);

            var newVibratorSpeeds = (double[])_vibratorSpeeds.Clone();

            foreach (var v in cmdMsg.Speeds)
            {
                if (Math.Abs(v.Speed - newVibratorSpeeds[v.Index]) < 0.001)
                {
                    continue;
                }

                newVibratorSpeeds[v.Index] = v.Speed;
            }

            if (newVibratorSpeeds.SequenceEqual(_vibratorSpeeds) && SentVibration)
            {
                return Task.FromResult(new Ok(aMsg.Id) as ButtplugMessage);
            }

            _vibratorSpeeds = newVibratorSpeeds;

            CuemeUpdateHandler(null, null);

            if (!_updateValueTimer.Enabled)
            {
                _updateValueTimer.Enabled = true;
            }

            SentVibration = true;
            return Task.FromResult(new Ok(aMsg.Id) as ButtplugMessage);
        }
    }
}
