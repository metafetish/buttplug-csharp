// <copyright file="LovenseProtocol.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Logging;
using Buttplug.Core.Messages;
using Buttplug.Devices.Configuration;

namespace Buttplug.Devices.Protocols
{
    internal class LovenseProtocol : ButtplugDeviceProtocol
    {
        private double[] _vibratorSpeeds = { 0 };
        private bool _clockwise = true;
        private double _rotateSpeed;
        private string _lastNotifyReceived = string.Empty;
        private TaskCompletionSource<string> _notificationWaiter = new TaskCompletionSource<string>();

        public LovenseProtocol(IButtplugLogManager aLogManager,
                       IButtplugDeviceImpl aInterface)
            : base(aLogManager,
                   "Lovense Device (Uninitialized)",
                   aInterface)
        {
            AddMessageHandler<StopDeviceCmd>(HandleStopDeviceCmd);
            AddMessageHandler<LovenseCmd>(HandleLovenseCmd);
        }

        public override async Task InitializeAsync(CancellationToken aToken)
        {
            BpLogger.Trace($"Initializing {Name}");

            // Subscribing to read updates
            await Interface.SubscribeToUpdatesAsync().ConfigureAwait(false);
            Interface.DataReceived += NotifyReceived;

            // Retrieving device type info for identification.
            await Interface.WriteValueAsync(Encoding.ASCII.GetBytes("DeviceType;"),
                new ButtplugDeviceWriteOptions { WriteWithResponse = true },
                aToken).ConfigureAwait(false);

            await Task.WhenAny(_notificationWaiter.Task, Task.Delay(4000, aToken));
            var deviceInfoString = _notificationWaiter.Task.Result;

            BpLogger.Debug($"Received device query return for {Name}");

            // Expected Format X:YY:ZZZZZZZZZZZZ X is device type leter YY is firmware version Z is
            // bluetooth address
            var deviceInfo = deviceInfoString.Split(':');

            // If we don't get back the amount of tokens we expect, identify as unknown, log, bail.
            if (deviceInfo.Length != 3 || deviceInfo[0].Length != 1)
            {
                throw new ButtplugDeviceException(BpLogger,
                    $"Unknown Lovense DeviceType of {deviceInfoString} found. Please report to Buttplug Developers by filing an issue at https://github.com/buttplugio/buttplug/");
            }

            var deviceTypeLetter = deviceInfo[0][0];
            if (deviceTypeLetter == 'C')
            {
                deviceTypeLetter = 'A';
            }

            DeviceConfigIdentifier = deviceTypeLetter.ToString();

            /* This block is now handled by the configuration logic

            int.TryParse(deviceInfo[1], out var deviceVersion);
            BpLogger.Trace($"Lovense DeviceType Return: {deviceTypeLetter}");
            if (!Enum.IsDefined(typeof(LovenseDeviceType), (uint)deviceTypeLetter))
            {
                // If we don't know what device this is, just use the defaults
                // todo: Exception here? Won't that stop the device being configured?
                throw new ButtplugDeviceException(BpLogger, $"Unknown Lovense Device of Type {deviceTypeLetter} found. Please report to Buttplug Developers by filing an issue at https://github.com/buttplugio/buttplug/");
            }

            Name = $"Lovense {Enum.GetName(typeof(LovenseDeviceType), (uint)deviceTypeLetter)} v{deviceVersion}";

            _deviceType = (LovenseDeviceType)deviceTypeLetter;

            if (_deviceType == LovenseDeviceType.Unknown)
            {
                BpLogger.Error("Lovense device type unknown, treating as single vibrator device. Please contact developers for more info.");
            }
            */
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
                        _vibratorSpeeds = new double[command.Value.FeatureCount ?? 1];
                        for (var i = 0; i < (command.Value.FeatureCount ?? 1); i++)
                        {
                            _vibratorSpeeds[i] = 0;
                        }

                        break;

                    case "RotateCmd":
                        // Add messages
                        AddMessageHandler<RotateCmd>(HandleRotateCmd, command.Value);

                        break;

                    default:
                        BpLogger.Warn($"Protocol {GetType().Name} doesn't have support for configuration specified message {command.Key}. Skipping");
                        break;
                }
            }

            return Task.FromResult<ButtplugMessage>(new Ok(ButtplugConsts.SystemMsgId));
        }

        private void NotifyReceived(object sender, ButtplugDeviceDataEventArgs args)
        {
            // Only set the waiter if it's not been set yet. Currently we only
            // use this when initializing, but in the future this may need
            // resetting if we need 2 way communication with the device.
            if (!_notificationWaiter.Task.IsCompleted)
            {
                _notificationWaiter.TrySetResult(Encoding.ASCII.GetString(args.Bytes));
            }
        }

        private async Task<ButtplugMessage> HandleStopDeviceCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            BpLogger.Debug("Stopping Device " + Name);

            if (MsgFuncs.ContainsKey(typeof(RotateCmd)))
            {
                await HandleRotateCmd(RotateCmd.Create(aMsg.DeviceIndex, aMsg.Id, 0, _clockwise, 1), aToken).ConfigureAwait(false);
            }

            return await HandleSingleMotorVibrateCmd(new SingleMotorVibrateCmd(aMsg.DeviceIndex, 0, aMsg.Id), aToken).ConfigureAwait(false);
        }

        private async Task<ButtplugMessage> HandleSingleMotorVibrateCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            var cmdMsg = CheckMessageHandler<SingleMotorVibrateCmd>(aMsg);

            return await HandleVibrateCmd(VibrateCmd.Create(cmdMsg.DeviceIndex, cmdMsg.Id, cmdMsg.Speed, (uint)_vibratorSpeeds.Length), aToken).ConfigureAwait(false);
        }

        private async Task<ButtplugMessage> HandleVibrateCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            var cmdMsg = CheckGenericMessageHandler<VibrateCmd>(aMsg, (uint)_vibratorSpeeds.Length);

            foreach (var v in cmdMsg.Speeds)
            {
                if (Math.Abs(v.Speed - _vibratorSpeeds[v.Index]) < 0.0001)
                {
                    continue;
                }

                _vibratorSpeeds[v.Index] = v.Speed;
                var vId = _vibratorSpeeds.Length == 1 ? string.Empty : string.Empty + (v.Index + 1);
                await Interface.WriteValueAsync(
                    Encoding.ASCII.GetBytes($"Vibrate{vId}:{(int)(_vibratorSpeeds[v.Index] * 20)};"), aToken).ConfigureAwait(false);
            }

            return new Ok(aMsg.Id);
        }

        private async Task<ButtplugMessage> HandleLovenseCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            var cmdMsg = CheckMessageHandler<LovenseCmd>(aMsg);

            await Interface.WriteValueAsync(Encoding.ASCII.GetBytes(cmdMsg.Command), aToken).ConfigureAwait(false);
            return new Ok(aMsg.Id);
        }

        private async Task<ButtplugMessage> HandleRotateCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            var cmdMsg = CheckGenericMessageHandler<RotateCmd>(aMsg, 1);

            var vi = cmdMsg.Rotations[0];

            if (_clockwise != vi.Clockwise)
            {
                _clockwise = !_clockwise;
                await Interface.WriteValueAsync(
                    Encoding.ASCII.GetBytes("RotateChange;"), aToken).ConfigureAwait(false);
            }

            if (Math.Abs(_rotateSpeed - vi.Speed) < 0.0001 && SentRotation)
            {
                return new Ok(cmdMsg.Id);
            }

            SentRotation = true;
            _rotateSpeed = vi.Speed;

            await Interface.WriteValueAsync(
                Encoding.ASCII.GetBytes($"Rotate:{(int)(_rotateSpeed * 20)};"), aToken).ConfigureAwait(false);
            return new Ok(aMsg.Id);
        }
    }
}
