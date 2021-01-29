using Buttplug.Client;
using Buttplug.Core.Logging;
using Buttplug.Core.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Devices;
using Buttplug.Core;

namespace Buttplug.Devices.Protocols
{
    internal class WitMotionProtocol : ButtplugDeviceProtocol
    {
        private SensorData lastSensorData = null;

        public event SensorEventHandler OnSensorUpdate;

        public WitMotionProtocol(IButtplugLogManager aLogManager,
                                 IButtplugDeviceImpl aInterface)
            : base(aLogManager,
                  $"witmotion {aInterface.Name}",
                  aInterface)
        {
            AddMessageHandler<ReadSensorCmd>(HandleReadSensorCmd);
            AddMessageHandler<SubscribeSensorCmd>(HandleSubscribeSensorCmd);
        }

        private async Task<ButtplugMessage> HandleSubscribeSensorCmd(ButtplugDeviceMessage aMsg, CancellationToken arg2)
        {
            var cmdMsg = aMsg as SubscribeSensorCmd;

            if (cmdMsg.Subscribe)
            {
                OnSensorUpdate += cmdMsg.Callback;
            }
            else
            {
                OnSensorUpdate -= cmdMsg.Callback;
            }

            return new Ok(cmdMsg.Id);
        }

        public override async Task InitializeAsync(CancellationToken aToken)
        {
            // Start listening for incoming
            Interface.DataReceived += OnDataReceived;
            await Interface.SubscribeToUpdatesAsync().ConfigureAwait(false);

            await base.InitializeAsync(aToken);
        }

        private async Task<ButtplugMessage> HandleReadSensorCmd(ButtplugDeviceMessage aMsg, CancellationToken arg2)
        {
            var cmdMsg = aMsg as ReadSensorCmd;
            return new SensorResult(aMsg.Id, this.lastSensorData);
        }

        private void OnDataReceived(object sender, ButtplugDeviceDataEventArgs e)
        {
            this.BpLogger.Debug($"{this.Name} OnDataReceived");

            const double g = 9.8;

            const double gRange = 4; //Range configured on WitMotionDevice
            const double aRange = 500; //Angular accelleration configured on WitMotion Device

            const double gScalar = g * gRange;

            //units of gravity
            double x_Accel = (double)(Int16)(((int)e.Bytes[3] << 8) + e.Bytes[2]) / 32768.0 * gScalar;
            double y_Accel = (double)(Int16)(((int)e.Bytes[5] << 8) + e.Bytes[4]) / 32768.0 * gScalar;
            double z_Accel = (double)(Int16)(((int)e.Bytes[7] << 8) + e.Bytes[6]) / 32768.0 * gScalar;

            //Degrees/sec
            double x_Angle_vel = (double)(Int16)(((int)e.Bytes[9] << 8) + e.Bytes[8]) / 32768.0 * aRange;
            double y_Angle_vel = (double)(Int16)(((int)e.Bytes[11] << 8) + e.Bytes[10]) / 32768.0 * aRange;
            double z_Angle_vel = (double)(Int16)(((int)e.Bytes[13] << 8) + e.Bytes[12]) / 32768.0 * aRange;

            //Degrees
            double roll = (double)(Int16)(((int)e.Bytes[15] << 8) + e.Bytes[14]) / 32768.0 * 180.0;
            double pitch = (double)(Int16)(((int)e.Bytes[17] << 8) + e.Bytes[16]) / 32768.0 * 180.0;
            double yaw = (double)(Int16)(((int)e.Bytes[19] << 8) + e.Bytes[18]) / 32768.0 * 180.0;

            SensorData data = new SensorData()
            {
                { StandardSensorModalities.Acceleration, new Vector3(x_Accel, y_Accel, z_Accel) },
                { StandardSensorModalities.AngularAcceleration, new Vector3(x_Angle_vel, y_Angle_vel, z_Angle_vel) },
                { StandardSensorModalities.Inclination, new Vector3(roll, pitch, yaw) },
            };

            this.BpLogger.Debug($"{data}");

            lastSensorData = data;

            if (OnSensorUpdate != null)
            {
                OnSensorUpdate(this, data);
            }

            //Trace.WriteLine($"{data}");
        }
    }
}
