// <copyright file="FleshlightLaunchTests.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

// Test file, disable ConfigureAwait checking.
// ReSharper disable ConsiderUsingConfigureAwait

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    public class FleshlightLaunchTests
    {
        [NotNull]
        private ProtocolTestUtils testUtil;

        [SetUp]
        public async Task Init()
        {
            testUtil = new ProtocolTestUtils();
            await testUtil.SetupTest<KiirooGen2Protocol>("Launch", new List<DeviceConfiguration>());
        }

        [Test]
        public void TestAllowedMessages()
        {
            testUtil.TestDeviceAllowedMessages(new Dictionary<System.Type, uint>()
            {
                { typeof(StopDeviceCmd), 0 },
                { typeof(FleshlightLaunchFW12Cmd), 0 },
                { typeof(LinearCmd), 1 },
            });
        }

        [Test]
        public async Task TestInitialize()
        {
            await testUtil.TestDeviceInitialize(new List<(byte[], string)>()
            {
                (new byte[] { 0x0 }, Endpoints.Firmware),
            }, true);
        }

        // StopDeviceCmd test handled in GeneralDeviceTests

        // In all device message tests, expect WriteWithResponse to be false.
        [Test]
        public async Task TestFleshlightLaunchFW12Cmd()
        {
            await testUtil.TestDeviceMessage(new FleshlightLaunchFW12Cmd(4, 50, 50),
                new List<(byte[], string)>()
                {
                    (new byte[] { 50, 50 }, Endpoints.Tx),
                }, false);
        }

        // TODO Test currently fails because we will send repeated packets to the launch. See #402.
        [Test]
        [Ignore("Fails because we don't do what we say we're going to. See #402.")]
        public async Task TestRepeatedFleshlightLaunchFW12Cmd()
        {
            await testUtil.TestDeviceMessage(new FleshlightLaunchFW12Cmd(4, 50, 50),
                new List<(byte[], string)>()
                {
                    (new byte[2] { 50, 50 }, Endpoints.Tx)
                }, false);
            await testUtil.TestDeviceMessageNoop(new FleshlightLaunchFW12Cmd(4, 50, 50));
        }

        [Test]
        public async Task TestVectorCmd()
        {
            var msg = new LinearCmd(4, new List<LinearCmd.VectorSubcommand>
            {
                new LinearCmd.VectorSubcommand(0, 500, 0.5),
            });
            await testUtil.TestDeviceMessage(msg,
                new List<(byte[], string)>()
                {
                    (new byte[] { 50, 20 }, Endpoints.Tx),
                }, false);
        }

        [Test]
        public void TestInvalidVectorCmdTooManyFeatures()
        {
            var msg = LinearCmd.Create(4, 0, 500, 0.75, 2);
            testUtil.TestInvalidDeviceMessage(msg);
        }

        [Test]
        public void TestInvalidVectorCmdWrongFeatures()
        {
            var msg = new LinearCmd(4,
                new List<LinearCmd.VectorSubcommand>
                {
                    new LinearCmd.VectorSubcommand(0xffffffff, 500, 0.75),
                });
            testUtil.TestInvalidDeviceMessage(msg);
        }

        [Test]
        public void TestInvalidVectorNotEnoughFeatures()
        {
            var msg = LinearCmd.Create(4, 0, 500, 0.75, 0);
            testUtil.TestInvalidDeviceMessage(msg);
        }
    }
}