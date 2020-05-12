// <copyright file="BluetoothDeviceTestUtils.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

// Test file, disable ConfigureAwait checking.
// ReSharper disable ConsiderUsingConfigureAwait

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Logging;
using Buttplug.Core.Messages;
using Buttplug.Devices;
using Buttplug.Devices.Configuration;
using FluentAssertions;
using JetBrains.Annotations;

namespace Buttplug.Test.Devices.Protocols.Utils
{
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Test classes can skip documentation requirements")]
    public interface IProtocolTestUtils
    {
        Task SetupTest<T>(string aDeviceName, List<DeviceConfiguration> aConfigs, bool aShouldInitialize = false) where T : IButtplugDeviceProtocol;

        Task TestDeviceMessageNoop(ButtplugDeviceMessage aOutgoingMessage);
    }

    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Test classes can skip documentation requirements")]
    public class ProtocolTestUtils : IProtocolTestUtils
    {
        private TestDeviceImpl _testImpl;
        private IButtplugDevice _testDevice;
        private List<DeviceConfiguration> _configs;

        public async Task SetupTest<T>(string aDeviceName, List<DeviceConfiguration> aConfigs, bool aShouldInitialize = true)
        where T : IButtplugDeviceProtocol
        {
            var logMgr = new ButtplugLogManager();
            _testImpl = new TestDeviceImpl(logMgr, aDeviceName);
            _testDevice = new ButtplugDevice(logMgr, typeof(T), _testImpl);
            _configs = aConfigs;

            if (!aShouldInitialize)
            {
                return;
            }

            await Initialize();
        }

        public async Task Initialize()
        {
            await _testDevice.InitializeAsync(_configs);
            _testImpl.LastWritten.Clear();
        }

        public void AddExpectedRead(string aCharacteristic, byte[] aValue)
        {
            _testImpl.AddExpectedRead(aCharacteristic, aValue);
        }

        public void AddExpectedNotify(string aTriggerString, byte[] aValue)
        {
            _testImpl.AddExpectedNotify(aTriggerString, aValue);
        }

        private void Clear()
        {
            _testImpl.LastWritten.Clear();
        }

        public void TestDeviceName(string aExpectedName)
        {
            _testDevice.Name.Should().Be(aExpectedName);
        }

        public void TestDeviceAllowedMessages([NotNull] Dictionary<Type, uint> aExpectedMessageArgs)
        {
            Clear();

            // If we've updated a class with new messages, but haven't updated the device, fail.
            _testDevice.AllowedMessageTypes.Count().Should().Be(aExpectedMessageArgs.Count);
            foreach (var item in aExpectedMessageArgs)
            {
                _testDevice.AllowedMessageTypes.Should().Contain(item.Key);
                _testDevice.GetMessageAttrs(item.Key).Should().NotBeNull();
                if (item.Value == 0)
                {
                    _testDevice.GetMessageAttrs(item.Key).FeatureCount.Should().BeNull();
                }
                else
                {
                    _testDevice.GetMessageAttrs(item.Key).FeatureCount.Should().Be(item.Value);
                }
            }
        }

        private void TestPacketMatching(IEnumerable<(byte[], string)> aExpectedBytes, bool aWriteWithResponse, bool aStrict = true)
        {
            // ExpectedBytes should have the same number of packets as LastWritten
            if (aStrict)
            {
                _testImpl.LastWritten.Count.Should().Be(aExpectedBytes.Count());

                // Since the expected values and lastWritten in interface should be lock-stepped, we can
                // merge them and iterate through everything at once.
                var checkSeq = aExpectedBytes.Zip(_testImpl.LastWritten, (first, second) => (first, second));
                foreach (var ((bytes, endpoint), lastWritten) in checkSeq)
                {
                    lastWritten.Value.Should().Equal(bytes);
                    lastWritten.WriteWithResponse.Should().Be(aWriteWithResponse);
                    lastWritten.Endpoint.Should().Be(endpoint);
                }
            }
            else
            {
                foreach (var (bytes, endpoint) in aExpectedBytes)
                {
                    var matched = false;
                    foreach (var lastWritten in _testImpl.LastWritten)
                    {
                        if (!lastWritten.Value.SequenceEqual(bytes) ||
                            lastWritten.WriteWithResponse != aWriteWithResponse ||
                            endpoint != lastWritten.Endpoint)
                        {
                            continue;
                        }

                        matched = true;
                        break;
                    }

                    var msg = "Matched!";
                    if (!matched)
                    {
                        msg = "Expected:\n";
                        foreach (var (bytes2, endpoint2) in aExpectedBytes)
                        {
                            msg += $"{BitConverter.ToString(bytes2)} {endpoint2} {aWriteWithResponse}\n";
                        }

                        msg += "\nActual:\n";
                        foreach (var lastWritten in _testImpl.LastWritten)
                        {
                            msg += $"{BitConverter.ToString(lastWritten.Value)} {lastWritten.Endpoint} {lastWritten.WriteWithResponse}\n";
                        }
                    }

                    matched.Should().BeTrue();
                }
            }
        }

        public async Task TestDeviceInitialize(IEnumerable<(byte[], string)> aExpectedBytes, bool aWriteWithResponse, bool aStrict = true)
        {
            Clear();
            await _testDevice.InitializeAsync(new List<DeviceConfiguration>());

            TestPacketMatching(aExpectedBytes, aWriteWithResponse, aStrict);
        }

        // Testing timing with delays is a great way to get intermittent errors, but here we are. Sadness.
        public async Task TestDeviceMessageOnWrite(ButtplugDeviceMessage aOutgoingMessage, IEnumerable<(byte[], string)> aExpectedBytes, bool aWriteWithResponse)
        {
            Clear();
            (await _testDevice.ParseMessageAsync(aOutgoingMessage)).Should().BeOfType<Ok>();
            var resetEvent = new ManualResetEvent(false);
            _testImpl.ValueWritten += (aObj, aArgs) => { resetEvent.Set(); };
            resetEvent.WaitOne(1000);
            TestPacketMatching(aExpectedBytes, aWriteWithResponse);
        }

        public async Task TestDeviceMessage(ButtplugDeviceMessage aOutgoingMessage, IEnumerable<(byte[], string)> aExpectedBytes, bool aWriteWithResponse)
        {
            Clear();
            (await _testDevice.ParseMessageAsync(aOutgoingMessage)).Should().BeOfType<Ok>();

            TestPacketMatching(aExpectedBytes, aWriteWithResponse);
        }

        public async Task TestDeviceMessageDelayed(ButtplugDeviceMessage aOutgoingMessage, IEnumerable<(byte[], string)> aExpectedBytes, bool aWriteWithResponse, uint aMilliseconds)
        {
            Clear();
            (await _testDevice.ParseMessageAsync(aOutgoingMessage)).Should().BeOfType<Ok>();
            Thread.Sleep(new TimeSpan(0, 0, 0, 0, (int)aMilliseconds));
            TestPacketMatching(aExpectedBytes, aWriteWithResponse, false);
        }

        public async Task TestDeviceMessageNoop(ButtplugDeviceMessage aOutgoingMessage)
        {
            await TestDeviceMessage(aOutgoingMessage, new List<(byte[], string)>(), false);
        }

        public void TestInvalidDeviceMessage(ButtplugDeviceMessage aOutgoingMessage)
        {
            Clear();
            _testDevice.Awaiting(async aDev => await aDev.ParseMessageAsync(aOutgoingMessage)).Should().Throw<ButtplugDeviceException>();
        }

        public void TestInvalidVibrateCmd(uint aNumVibes)
        {
            TestInvalidDeviceMessage(VibrateCmd.Create(4, 1, 0.5, 0));
            TestInvalidDeviceMessage(VibrateCmd.Create(4, 1, 0.5, aNumVibes + 1));
            TestInvalidDeviceMessage(
                new VibrateCmd(4, new List<VibrateCmd.VibrateSubcommand>()
                {
                    new VibrateCmd.VibrateSubcommand(0xffffffff, 0.5),
                }));
        }
    }
}