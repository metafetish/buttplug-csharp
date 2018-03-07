using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Messages;
using static Buttplug.Server.Managers.ArduinoManager.ArduinoDeviceProtocol;

namespace Buttplug.Server.Managers.ArduinoManager
{
    internal class ArduinoDevice:ButtplugDevice
    {
        private SerialPort serialPort;

        public ArduinoDevice(SerialPort serialPort, IButtplugLogManager logManager, string name, string id)
            :base(logManager, name, id)
        {
            this.serialPort = serialPort;
            SendCommand(SerialCommand.Enable);
            MsgFuncs.Add(typeof(SingleMotorVibrateCmd), new ButtplugDeviceWrapper(HandleSingleMotorVibrateCmd));
            MsgFuncs.Add(typeof(StopDeviceCmd), new ButtplugDeviceWrapper(HandleStopDeviceCmd));
        }

        private async Task<ButtplugMessage> HandleSingleMotorVibrateCmd(ButtplugDeviceMessage msg)
        {
            var cmdMsg = msg as SingleMotorVibrateCmd;
            SetSpeed((byte)(cmdMsg.Speed * 255));
            return new Ok(msg.Id);
        }

        private async Task<ButtplugMessage> HandleStopDeviceCmd(ButtplugDeviceMessage msg)
        {
            SetSpeed(0);
            return new Ok(msg.Id);
        }

        private void SetSpeed(byte speed)
        {
            serialPort.Write(new byte[] { (byte)SerialCommand.Speed, speed }, 0, 2);
        }

        private void SendCommand(SerialCommand cmd)
        {
            serialPort.Write(new byte[] { (byte)cmd }, 0, 1);
        }

        public override void Disconnect()
        {
            SetSpeed(0);
            SendCommand(SerialCommand.Disable);
            serialPort.Close();
            InvokeDeviceRemoved();
        }
    }
}