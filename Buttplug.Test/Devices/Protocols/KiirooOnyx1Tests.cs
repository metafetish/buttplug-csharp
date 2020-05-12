// <copyright file="KiirooOnyx1Tests.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

// Test file, disable ConfigureAwait checking.
// ReSharper disable ConsiderUsingConfigureAwait

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
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
    public class KiirooOnyx1Tests
    {
        [NotNull]
        private ProtocolTestUtils testUtil;

        [SetUp]
        public async Task Init()
        {
            testUtil = new ProtocolTestUtils();
            await testUtil.SetupTest<KiirooGen1Protocol>("ONYX", new List<DeviceConfiguration>());
        }

        [Test]
        public void TestAllowedMessages()
        {
            testUtil.TestDeviceAllowedMessages(new Dictionary<System.Type, uint>()
            {
                { typeof(StopDeviceCmd), 0 },
                { typeof(FleshlightLaunchFW12Cmd), 0 },
                { typeof(LinearCmd), 1 },
                { typeof(KiirooCmd), 0 },
            });
        }

        [Test]
        public async Task TestInitialize()
        {
            await testUtil.TestDeviceInitialize(new List<(byte[], string)>()
            {
                (new byte[] { 0x01, 0x00 }, Endpoints.Command),
                (new byte[] { 0x30, 0x2c }, Endpoints.Tx),
            }, true, false);
        }

        // StopDeviceCmd test handled in GeneralDeviceTests

        // In all device message tests, expect WriteWithResponse to be false.
        [Test]
        [Ignore("Intermittent failure due to timing issues.")]
        public async Task TestFleshlightLaunchFW12Cmd()
        {
            var msg = new FleshlightLaunchFW12Cmd(4, 35, 23);
            await testUtil.TestDeviceMessageDelayed(msg,
                new List<(byte[], string)>()
                {
                    (Encoding.ASCII.GetBytes("1,\n"), Endpoints.Tx),
                    (Encoding.ASCII.GetBytes("2,\n"), Endpoints.Tx),
                    (Encoding.ASCII.GetBytes("3,\n"), Endpoints.Tx),
                }, false, 400);

            msg = new FleshlightLaunchFW12Cmd(4, 30, 51);
            await testUtil.TestDeviceMessageDelayed(msg,
                new List<(byte[], string)>()
                {
                    (Encoding.ASCII.GetBytes("2,\n"), Endpoints.Tx),
                }, false, 500);
        }

        [Test]
        public async Task TestStopDeviceCmd()
        {
            var expected =
                new List<(byte[], string)>()
                {
                    (Encoding.ASCII.GetBytes("0,\n"), Endpoints.Tx),
                };

            await testUtil.TestDeviceMessageDelayed(new StopDeviceCmd(4), expected, false, 1000);
        }

        [Test]
        [Ignore("Times out intermittently, needs a rewrite.")]
        public async Task TestVectorCmd()
        {
            var msg = new LinearCmd(4, new List<LinearCmd.VectorSubcommand>
            {
                new LinearCmd.VectorSubcommand(0, 500, 0.25),
            });
            await testUtil.TestDeviceMessageDelayed(msg,
                new List<(byte[], string)>()
                {
                    (Encoding.ASCII.GetBytes("1,\n"), Endpoints.Tx),
                    (Encoding.ASCII.GetBytes("2,\n"), Endpoints.Tx),
                    (Encoding.ASCII.GetBytes("3,\n"), Endpoints.Tx),
                }, false, 500);

            msg = new LinearCmd(4, new List<LinearCmd.VectorSubcommand>
            {
                new LinearCmd.VectorSubcommand(0, 400, 0.5),
            });
            await testUtil.TestDeviceMessageDelayed(msg,
                new List<(byte[], string)>()
                {
                    (Encoding.ASCII.GetBytes("2,\n"), Endpoints.Tx),
                }, false, 500);
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

        [Test]
        public async Task TestKiirooCmd()
        {
            var expected =
                new List<(byte[], string)>()
                {
                    (Encoding.ASCII.GetBytes("3,\n"), Endpoints.Tx),
                };

            await testUtil.TestDeviceMessage(new KiirooCmd(4, 3), expected, false);
        }
    }
}