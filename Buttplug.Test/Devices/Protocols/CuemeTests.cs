﻿// <copyright file="MysteryVibeTests.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

// Test file, disable ConfigureAwait checking.
// ReSharper disable ConsiderUsingConfigureAwait

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Buttplug.Core.Messages;
using Buttplug.Devices;
using Buttplug.Devices.Configuration;
using Buttplug.Devices.Protocols;
using Buttplug.Test.Devices.Protocols.Utils;
using JetBrains.Annotations;
using NUnit.Framework;

namespace Buttplug.Test.Devices.Protocols
{
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Test classes can skip documentation requirements")]
    [TestFixture]
    public class CuemeTests
    {
        [NotNull] private readonly ProtocolTestUtils _testUtil = new ProtocolTestUtils();

        [SetUp]
        public async Task Init()
        {
            DeviceConfigurationManager.LoadBaseConfigurationFromResource();
            var mgr = DeviceConfigurationManager.Manager;
            var pconfigs = mgr.GetProtocolConfig("cueme");
            var configs = pconfigs.First().DeviceConfigs;

            await _testUtil.SetupTest<CuemeProtocol>("FUNCODE_ABABABABAB_3", configs);
        }

        [Test]
        public void TestAllowedMessages()
        {
            _testUtil.TestDeviceAllowedMessages(new Dictionary<System.Type, uint>()
            {
                { typeof(StopDeviceCmd), 0 },
                { typeof(SingleMotorVibrateCmd), 0 },
                { typeof(VibrateCmd), 4 },
            });
        }

        // StopDeviceCmd noop test handled in GeneralDeviceTests

        [Test]
        [Ignore("Fails, possibly due to timer issues?")]
        public async Task TestStopDeviceCmd()
        {
            var expected =
                new List<(byte[], string)>
                {
                    (new byte[] { 0x17 }, Endpoints.Tx),
                };

            await _testUtil.TestDeviceMessage(new SingleMotorVibrateCmd(4, 0.5), expected, false);

            expected =
                new List<(byte[], string)>
                {
                    (new byte[] { 0x00 }, Endpoints.Tx),
                };

            await _testUtil.TestDeviceMessageOnWrite(new StopDeviceCmd(4), expected, false);
        }

        [Test]
        public async Task TestSingleMotorVibrateCmd()
        {
            var expected =
                new List<(byte[], string)>()
                {
                    (new byte[] { 0x17 }, Endpoints.Tx),
                };

            await _testUtil.TestDeviceMessage(new SingleMotorVibrateCmd(4, 0.5), expected, false);
        }

        [Test]
        public async Task TestVibrateCmd()
        {
            var expected =
                new List<(byte[], string)>()
                {
                    (new byte[] { 0x17 }, Endpoints.Tx),
                };

            await _testUtil.TestDeviceMessage(VibrateCmd.Create(4, 1, 0.5, 4), expected, false);
        }

        [Test]
        public void TestInvalidVibrateCmd()
        {
            _testUtil.TestInvalidVibrateCmd(6);
        }
    }
}