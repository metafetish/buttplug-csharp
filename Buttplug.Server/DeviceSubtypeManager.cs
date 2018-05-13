﻿using System;
using Buttplug.Core;
using JetBrains.Annotations;

namespace Buttplug.Server
{
    public abstract class DeviceSubtypeManager : IDeviceSubtypeManager
    {
        [NotNull]
        protected readonly IButtplugLog BpLogger;
        [NotNull]
        protected readonly IButtplugLogManager LogManager;

        private bool _verboseDeviceLogging = false;

        public bool VerboseDeviceLogging
        {
            get => _verboseDeviceLogging;
            set => _verboseDeviceLogging = value;
        }

        public event EventHandler<DeviceAddedEventArgs> DeviceAdded;

        public event EventHandler<EventArgs> ScanningFinished;

        protected DeviceSubtypeManager([NotNull] IButtplugLogManager aLogManager)
        {
            LogManager = aLogManager;
            BpLogger = aLogManager.GetLogger(GetType());
            BpLogger.Debug($"Setting up Device Manager {GetType().Name}");
        }

        protected void InvokeDeviceAdded([NotNull] DeviceAddedEventArgs aEventArgs)
        {
            DeviceAdded?.Invoke(this, aEventArgs);
        }

        protected void InvokeScanningFinished()
        {
            ScanningFinished?.Invoke(this, new EventArgs());
        }

        public abstract void StartScanning();

        public abstract void StopScanning();

        public abstract bool IsScanning();
    }
}