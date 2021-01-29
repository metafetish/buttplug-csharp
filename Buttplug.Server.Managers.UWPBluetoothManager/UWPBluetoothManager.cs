// <copyright file="UWPBluetoothManager.cs" company="Nonpolynomial Labs LLC">
//     Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
//     Copyright (c) Nonpolynomial Labs LLC. All rights reserved. Licensed under the BSD 3-Clause
//     license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Management;
using System.Threading.Tasks;
using Buttplug.Core.Logging;
using Buttplug.Devices.Configuration;
using JetBrains.Annotations;
using Microsoft.Win32;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Buttplug.Devices;

namespace Buttplug.Server.Managers.UWPBluetoothManager
{
    public class UWPBluetoothManager : DeviceSubtypeManager
    {
        [NotNull]
        private readonly BluetoothLEAdvertisementWatcher _bleWatcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };

        private Task _radioTask;

        [NotNull]
        private readonly List<ulong> _seenAddresses = new List<ulong>();

        public static bool HasRegistryKeysSet()
        {
            var accessPerm = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\AppID\{415579bd-5399-48ef-8521-775ebcd647af}", "AccessPermission", string.Empty);
            if (accessPerm == null || accessPerm.ToString().Length == 0)
            {
                return false;
            }

            // ReSharper disable once PossibleNullReferenceException
            var appId = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\AppID\" + AppDomain.CurrentDomain.FriendlyName, "AppId", string.Empty);
            return appId != null && appId.ToString() == "{415579bd-5399-48ef-8521-775ebcd647af}";
        }

        public UWPBluetoothManager(IButtplugLogManager aLogManager)
            : base(aLogManager)
        {
            BpLogger.Info("Loading UWP Bluetooth Manager");

            // We can't filter device advertisements because you can only add one LocalName filter at
            // a time, meaning we would have to set up multiple watchers for multiple devices. We'll
            // handle our own filtering via the factory classes whenever we receive a device.
            _bleWatcher.Received += OnAdvertisementReceived;
            _bleWatcher.Stopped += OnWatcherStopped;
            var adapterTask = Task.Run(() => BluetoothAdapter.GetDefaultAsync().AsTask());
            adapterTask.Wait();
            var adapter = adapterTask.Result;
            if (adapter == null)
            {
                BpLogger.Warn("No bluetooth adapter available for UWP Bluetooth Manager Connection");
                return;
            }

            if (!adapter.IsLowEnergySupported)
            {
                BpLogger.Warn("Bluetooth adapter available but does not support Bluetooth Low Energy.");
                return;
            }

            BpLogger.Debug("UWP Manager found working Bluetooth LE Adapter");

            // Only run radio information lookup if we're actually logging at the level it will show.
            if (aLogManager.MaxLevel >= ButtplugLogLevel.Debug)
            {
                // Do radio lookup in a background task, as the search query is very slow. TODO
                // Should probably try and cancel this if it's still running on object destruction,
                // but the Get() call is uninterruptable?
                _radioTask = Task.Run(() => LogBluetoothRadioInfo());
            }
        }

        private void LogBluetoothRadioInfo()
        {
            // Log all bluetooth radios on the system, in case we need the information from the user later.
            var objSearcher = new ManagementObjectSearcher("Select * from Win32_PnPSignedDriver where DeviceName like '%Bluetooth%'");

            var objCollection = objSearcher.Get();

            BpLogger.Debug("Bluetooth Radio Information:");
            foreach (var obj in objCollection)
            {
                var info =
                    $"Device='{obj["DeviceName"]}',Manufacturer='{obj["Manufacturer"]}',DriverVersion='{obj["DriverVersion"]}' ";
                BpLogger.Debug(info);
            }
        }

        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher aObj,
                                                   BluetoothLEAdvertisementReceivedEventArgs aEvent)
        {
            if (aEvent?.Advertisement == null)
            {
                BpLogger.Debug("Null BLE advertisement received: skipping");
                return;
            }

            var advertName = aEvent.Advertisement.LocalName ?? string.Empty;
            var advertGUIDs = new List<Guid>();
            advertGUIDs.AddRange(aEvent.Advertisement.ServiceUuids ?? new Guid[] { });
            var btAddr = aEvent.BluetoothAddress;

            // BpLogger.Trace($"Got BLE Advertisement for device: {aEvent.Advertisement.LocalName} / {aEvent.BluetoothAddress}");
            if (_seenAddresses.Contains(btAddr))
            {
                // BpLogger.Trace($"Ignoring advertisement for already connecting device:
                // {aEvent.Advertisement.LocalName} / {aEvent.BluetoothAddress}");
                return;
            }

            BpLogger.Trace("BLE device found: " + advertName);

            // We always need a name to match against.
            if (advertName == string.Empty)
            {
                return;
            }
            
            _seenAddresses.Add(btAddr);

            // todo Add advertGUIDs back in. Not sure that ever really gets used though.
            var deviceCriteria = new BluetoothLEProtocolConfiguration(advertName);

            var deviceFactory = DeviceConfigurationManager.Manager.Find(deviceCriteria);

            // If we don't have a protocol to match the device, we can't do anything with it.
            if (deviceFactory == null || !(deviceFactory.Config is BluetoothLEProtocolConfiguration bleConfig))
            {
                BpLogger.Debug($"No usable device factory available for {advertName}.");
                // If we've got an actual name this time around, and we don't have any factories
                // available that match the info we have, keep the device in our seen list so we won't keep
                // rechecking. If a device does have a factory, but doesn't connect, we still want to
                // try again.
                return;
            }

            var fromBluetoothAddressAsync = BluetoothLEDevice.FromBluetoothAddressAsync(btAddr);

            // Can return null if the device no longer exists, for instance if it turned off between
            // advertising and us getting here. Since we didn't get a chance to try to connect,
            // remove it from seen devices, since the user may turn it back on during this scanning period.
            if (fromBluetoothAddressAsync == null)
            {
                _seenAddresses.Remove(btAddr);
                return;
            }

            var dev = await fromBluetoothAddressAsync;

            // If a device is turned on after scanning has started, windows seems to lose the device
            // handle the first couple of times it tries to deal with the advertisement. Just log the
            // error and hope it reconnects on a later retry.
            IButtplugDeviceImpl bleDevice = null;
            IButtplugDevice btDevice = null; 
            try
            {
                bleDevice = await UWPBluetoothDeviceInterface.Create(LogManager, bleConfig, dev).ConfigureAwait(false);
                btDevice = await deviceFactory.CreateDevice(LogManager, bleDevice).ConfigureAwait(false);
                InvokeDeviceAdded(new DeviceAddedEventArgs(btDevice));
            }
            catch (Exception ex)
            { 
                btDevice?.Disconnect(); 
                 
                bleDevice?.Disconnect(); 
                
                BpLogger.Error(
                    $"Cannot connect to device {advertName} {btAddr}: {ex.Message}\n{ex.StackTrace}");
            }

            // Remove the device from the seen list so that if we error'ed or disconnect, we can try again
            _seenAddresses.Remove(btAddr);
        }

        private void OnWatcherStopped(BluetoothLEAdvertisementWatcher aObj,
                                      BluetoothLEAdvertisementWatcherStoppedEventArgs aEvent)
        {
            BpLogger.Info("Stopped BLE Scanning");
            InvokeScanningFinished();
        }

        public override void StartScanning()
        {
            BpLogger.Info("Starting BLE Scanning");
            _seenAddresses.Clear();
            _bleWatcher.Start();
        }

        public override void StopScanning()
        {
            BpLogger.Info("Stopping BLE Scanning");
            _bleWatcher.Stop();
        }

        public override bool IsScanning()
        {
            return _bleWatcher.Status == BluetoothLEAdvertisementWatcherStatus.Started;
        }
    }
}
