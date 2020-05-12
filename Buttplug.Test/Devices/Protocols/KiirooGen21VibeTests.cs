// <copyright file="KiirooGen2VibeTests.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

// Test file, disable ConfigureAwait checking.
// ReSharper disable ConsiderUsingConfigureAwait

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Buttplug.Core.Messages;
using Buttplug.Devices;
using Buttplug.Devices.Configuration;
using Buttplug.Devices.Protocols;
using Buttplug.Test.Devices.Protocols.Utils;
using FluentAssertions;
using NUnit.Framework;

namespace Buttplug.Test.Devices.Protocols
{
    // This info class represents multiple device types, so we can't call setup for our test utils
    // here, they need to be generated per-loop.
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Test classes can skip documentation requirements")]
    [TestFixture]
    public class KiirooGen21VibeTests
    {
        [Test]
        public async Task TestAllowedMessages()
        {
            foreach (var item in KiirooGen21Protocol.DevInfos)
            {
                if (item.Value.VibeCount == 0)
                {
                    continue;
                }

                var testUtil = new ProtocolTestUtils();
                await testUtil.SetupTest<KiirooGen21Protocol>(item.Key, new List<DeviceConfiguration>());
                var expected = new Dictionary<Type, uint>()
                {
                    { typeof(StopDeviceCmd), 0 },
                    { typeof(SingleMotorVibrateCmd), 0 },
                    { typeof(VibrateCmd), item.Value.VibeCount },
                };

                if (item.Value.HasLinear)
                {
                    expected.Add(typeof(LinearCmd), 1);
                    expected.Add(typeof(FleshlightLaunchFW12Cmd), 0);
                }

                testUtil.TestDeviceAllowedMessages(expected);
            }
        }

        // StopDeviceCmd test handled in GeneralDeviceTests

        [Test]
        public async Task TestSingleMotorVibrateCmd()
        {
            foreach (var item in KiirooGen21Protocol.DevInfos)
            {
                if (item.Value.VibeCount == 0)
                {
                    continue;
                }

                var testUtil = new ProtocolTestUtils();
                await testUtil.SetupTest<KiirooGen21Protocol>(item.Key, new List<DeviceConfiguration>());
                var expected = new byte[] { 1, 0 };
                for (var i = 0u; i < item.Value.VibeCount; ++i)
                {
                    item.Value.VibeOrder.Should().Contain(i);
                    expected[Array.IndexOf(item.Value.VibeOrder, i) + 1] = 50;
                }

                await testUtil.TestDeviceMessage(new SingleMotorVibrateCmd(4, 0.5),
                    new List<(byte[], string)>()
                    {
                        (expected, Endpoints.Tx),
                    }, false);
            }
        }

        [Test]
        public async Task TestVibrateCmd()
        {
            foreach (var item in KiirooGen21Protocol.DevInfos)
            {
                if (item.Value.VibeCount == 0)
                {
                    continue;
                }

                var testUtil = new ProtocolTestUtils();
                await testUtil.SetupTest<KiirooGen21Protocol>(item.Key, new List<DeviceConfiguration>());
                var speeds = new[] { 0.25, 0.5, 0.75 };
                var features = new List<VibrateCmd.VibrateSubcommand>();
                for (var i = 0u; i < item.Value.VibeCount; ++i)
                {
                    features.Add(new VibrateCmd.VibrateSubcommand(i, speeds[i]));
                }

                var expected = new byte[] { 1, 0 };
                for (var i = 0u; i < item.Value.VibeCount; ++i)
                {
                    item.Value.VibeOrder.Should().Contain(i);
                    expected[Array.IndexOf(item.Value.VibeOrder, i) + 1] = (byte)(speeds[i] * 100);
                }

                await testUtil.TestDeviceMessage(new VibrateCmd(4, features),
                    new List<(byte[], string)>()
                    {
                        (expected, Endpoints.Tx),
                    }, false);
            }
        }

        [Test]
        public async Task TestInvalidCmds()
        {
            foreach (var item in KiirooGen21Protocol.DevInfos)
            {
                if (item.Value.VibeCount == 0)
                {
                    continue;
                }

                var testUtil = new ProtocolTestUtils();
                await testUtil.SetupTest<KiirooGen21Protocol>(item.Key, new List<DeviceConfiguration>());
                testUtil.TestInvalidVibrateCmd(item.Value.VibeCount);
            }
        }
    }
}