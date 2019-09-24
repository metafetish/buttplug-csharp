﻿// <copyright file="TestDeviceSubtypeManager.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

// Test file, disable ConfigureAwait checking.
// ReSharper disable ConsiderUsingConfigureAwait

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Buttplug.Core.Logging;
using Buttplug.Devices;
using Buttplug.Server;
using JetBrains.Annotations;

namespace Buttplug.Test
{
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Test classes can skip documentation requirements")]
    public class TestDeviceSubtypeManager : DeviceSubtypeManager
    {
        [CanBeNull]
        public readonly ButtplugDevice Device;

        public bool StartScanningCalled { get; private set; }

        public bool StopScanningCalled { get; private set; }

        public TestDeviceSubtypeManager()
            : base(new ButtplugLogManager())
        {
        }

        public TestDeviceSubtypeManager([NotNull] ButtplugDevice aDevice)
            : base(new ButtplugLogManager())
        {
            Device = aDevice;
        }

        public override Task StartScanning()
        {
            StartScanningCalled = true;
            StopScanningCalled = false;
            if (!(Device is null))
            {
                InvokeDeviceAdded(new DeviceAddedEventArgs(Device));
            }

            return Task.CompletedTask;
        }

        public override Task StopScanning()
        {
            StopScanningCalled = true;
            StartScanningCalled = false;
            InvokeScanningFinished();

            return Task.CompletedTask;
        }

        public override bool IsScanning()
        {
            return StartScanningCalled && !StopScanningCalled;
        }

        public void AddDevice(ButtplugDevice dev)
        {
            InvokeDeviceAdded(new DeviceAddedEventArgs(dev));
        }
    }
}