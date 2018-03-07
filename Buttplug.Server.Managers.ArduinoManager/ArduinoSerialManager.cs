using System;
using System.IO.Ports;
using System.Threading;
using Buttplug.Core;
using static Buttplug.Server.Managers.ArduinoManager.ArduinoDeviceProtocol;

namespace Buttplug.Server.Managers.ArduinoManager
{
    public class ArduinoSerialManager : DeviceSubtypeManager
    {
        private bool _isScanning = false;
        private Thread _scanThread;

        public ArduinoSerialManager(IButtplugLogManager aLogManager)
            : base(aLogManager)
        {
            BpLogger.Info("Loading Arduino Serial Port Manager");
        }

        public override void StartScanning()
        {
            BpLogger.Info("Starting Scanning Serial Ports for Arduino Devices");
            _isScanning = true;
            _scanThread = new Thread(() => ScanSerialPorts());
            _scanThread.Start();
        }

        public override void StopScanning()
        {
            BpLogger.Info("Stopping Scanning Serial Ports for ErosTek Devices");
            _isScanning = false;
        }

        public override bool IsScanning()
        {
            return _isScanning;
        }

        private void ScanSerialPorts()
        {
            string[] comPortsToScan;
            _isScanning = true;
            var count = 5;

            while (_isScanning && count-- > 0)
            {
                // Enumerate Ports
                comPortsToScan = SerialPort.GetPortNames();

                // try to detect devices on all port
                foreach (var port in comPortsToScan)
                {
                    BpLogger.Info("Scanning " + port);

                    SerialPort serialPort = new SerialPort(port)
                    { 
                        ReadTimeout = 2000,
                        WriteTimeout = 2000,
                        BaudRate = 115200,
                    };

                    try
                    {
                        serialPort.Open();
                        serialPort.ReadExisting();
                    }
                    catch (Exception ex)
                    {
                        if (ex is UnauthorizedAccessException
                            || ex is ArgumentOutOfRangeException
                            || ex is System.IO.IOException)
                        {
                            // This port is inaccessible.
                            // Possibly because a device detected earlier is already using it,
                            // or because our required parameters are not supported
                            continue;
                        }

                        throw;
                    }
                    
                    try
                    {
                        serialPort.Write(new byte[] { (byte)SerialCommand.Ack }, 0, 1);
                    
                        if (serialPort.ReadByte() == (byte)SerialCommand.Ack)
                        {
                            // This seems to be a Buttplug Arduino Device! Let's try to create the device.
                            var device = new ArduinoDevice(serialPort, LogManager, "Arduino Device", port);

                            BpLogger.Info("Found device at port " + port);

                            // Device succesfully created!
                            InvokeDeviceAdded(new DeviceAddedEventArgs(device));
                            continue;
                        }
                    }
                    catch (TimeoutException)
                    {
                        // Can't write to this port? Skip to the next one.
                    }

                    // No useful device detected on this port. Close the port.
                    serialPort.Close();
                    serialPort.Dispose();
                }

                System.Threading.Thread.Sleep(3000);

                // _isScanning = false; // Uncomment to disable continuuous serial port scanning
            }

            if (count < 0)
            {
                BpLogger.Info("Automatically Stopping Scanning Serial Ports for Arduino Devices");
            }

            _isScanning = false;
            InvokeScanningFinished();
        }
    }
}
