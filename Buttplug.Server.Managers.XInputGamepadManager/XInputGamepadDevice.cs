﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Messages;
using SharpDX.XInput;

namespace Buttplug.Server.Managers.XInputGamepadManager
{
    internal class XInputGamepadDevice : ButtplugDevice
    {
        private Controller _device;
        private double[] _vibratorSpeeds = { 0, 0 };

        public XInputGamepadDevice(IButtplugLogManager aLogManager, Controller aDevice)
            : base(aLogManager, "XBox Compatible Gamepad (XInput)", aDevice.UserIndex.ToString())
        {
            _device = aDevice;
            MsgFuncs.Add(typeof(SingleMotorVibrateCmd), new ButtplugDeviceMessageHandler(HandleSingleMotorVibrateCmd));
            MsgFuncs.Add(typeof(VibrateCmd), new ButtplugDeviceMessageHandler(HandleVibrateCmd, new MessageAttributes() { FeatureCount = 2 }));
            MsgFuncs.Add(typeof(StopDeviceCmd), new ButtplugDeviceMessageHandler(HandleStopDeviceCmd));
        }

        private Task<ButtplugMessage> HandleStopDeviceCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            BpLogger.Debug("Stopping Device " + Name);
            return HandleSingleMotorVibrateCmd(new SingleMotorVibrateCmd(aMsg.DeviceIndex, 0, aMsg.Id), aToken);
        }

        private Task<ButtplugMessage> HandleSingleMotorVibrateCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            if (!(aMsg is SingleMotorVibrateCmd cmdMsg))
            {
                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
            }

            var speeds = new List<VibrateCmd.VibrateSubcommand>();
            for (uint i = 0; i < 2; i++)
            {
                speeds.Add(new VibrateCmd.VibrateSubcommand(i, cmdMsg.Speed));
            }

            return HandleVibrateCmd(new VibrateCmd(cmdMsg.DeviceIndex, speeds, cmdMsg.Id), aToken);
        }

        private Task<ButtplugMessage> HandleVibrateCmd(ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            if (!(aMsg is VibrateCmd cmdMsg))
            {
                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, "Wrong Handler"));
            }

            if (cmdMsg.Speeds.Count < 1 || cmdMsg.Speeds.Count > 2)
            {
                return Task.FromResult<ButtplugMessage>(new Error(
                    "VibrateCmd requires between 1 and 2 vectors for this device.",
                    Error.ErrorClass.ERROR_DEVICE,
                    cmdMsg.Id));
            }

            foreach (var vi in cmdMsg.Speeds)
            {
                if (vi.Index < 0 || vi.Index >= 2)
                {
                    return Task.FromResult<ButtplugMessage>(new Error(
                        $"Index {vi.Index} is out of bounds for VibrateCmd for this device.",
                        Error.ErrorClass.ERROR_DEVICE,
                        cmdMsg.Id));
                }

                _vibratorSpeeds[vi.Index] = _vibratorSpeeds[vi.Index] < 0 ? 0
                                          : _vibratorSpeeds[vi.Index] > 1 ? 1
                                                                          : vi.Speed;
            }

            var v = new Vibration
            {
                LeftMotorSpeed = (ushort)(_vibratorSpeeds[0] * ushort.MaxValue),
                RightMotorSpeed = (ushort)(_vibratorSpeeds[1] * ushort.MaxValue),
            };

            try
            {
                _device?.SetVibration(v);
            }
            catch (Exception e)
            {
                if (_device?.IsConnected != true)
                {
                    InvokeDeviceRemoved();

                    // Don't throw a spanner in the works
                    return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
                }

                return Task.FromResult<ButtplugMessage>(BpLogger.LogErrorMsg(aMsg.Id, Error.ErrorClass.ERROR_DEVICE, e.Message));
            }

            return Task.FromResult<ButtplugMessage>(new Ok(aMsg.Id));
        }

        public override void Disconnect()
        {
            var v = new Vibration
            {
                LeftMotorSpeed = 0,
                RightMotorSpeed = 0,
            };
            try
            {
                _device?.SetVibration(v);
                _device = null;
            }
            finally
            {
                _device = null;
            }
        }
    }
}
