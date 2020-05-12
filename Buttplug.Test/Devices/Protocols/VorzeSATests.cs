// <copyright file="VorzeSATests.cs" company="Nonpolynomial Labs LLC">
//     Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
//     Copyright (c) Nonpolynomial Labs LLC. All rights reserved. Licensed under the BSD 3-Clause
//     license. See LICENSE file in the project root for full license information.
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
    public class VorzeSATests
    {
        [NotNull]
        private ProtocolTestUtils testUtil;

        internal async Task TestAllowedMessages(string aDeviceName, VorzeSAProtocol.CommandType aCommandType)
        {
            testUtil = new ProtocolTestUtils();
            await testUtil.SetupTest<VorzeSAProtocol>(aDeviceName, new List<DeviceConfiguration>());

            if (aCommandType == VorzeSAProtocol.CommandType.Rotate)
            {
                testUtil.TestDeviceAllowedMessages(new Dictionary<System.Type, uint>()
                {
                    {typeof(StopDeviceCmd), 0},
                    {typeof(VorzeA10CycloneCmd), 0},
                    {typeof(RotateCmd), 1},
                });
            }
            else if (aCommandType == VorzeSAProtocol.CommandType.Vibrate)
            {
                testUtil.TestDeviceAllowedMessages(new Dictionary<System.Type, uint>()
                {
                    {typeof(StopDeviceCmd), 0},
                    {typeof(SingleMotorVibrateCmd), 0},
                    {typeof(VibrateCmd), 1},
                });
            }
            else
            {
                Assert.Fail("Unknown command type");
            }
        }

        // StopDeviceCmd noop test handled in GeneralDeviceTests

        internal async Task TestStopDeviceCmd(string aDeviceName, byte aPrefix, VorzeSAProtocol.CommandType aCommandType)
        {
            testUtil = new ProtocolTestUtils();
            await testUtil.SetupTest<VorzeSAProtocol>(aDeviceName, new List<DeviceConfiguration>());
            var expected = new byte[] { aPrefix, (byte)aCommandType, 50 };

            if (aCommandType == VorzeSAProtocol.CommandType.Rotate)
            {
                await testUtil.TestDeviceMessage(new VorzeA10CycloneCmd(4, 50, false),
                    new List<(byte[], string)>()
                    {
                        (expected, Endpoints.Tx),
                    }, false);
            }
            else if (aCommandType == VorzeSAProtocol.CommandType.Vibrate)
            {
                await testUtil.TestDeviceMessage(new SingleMotorVibrateCmd(4, 0.5),
                    new List<(byte[], string)>()
                    {
                        (expected, Endpoints.Tx),
                    }, false);
            }
            else
            {
                Assert.Fail("Unknown command type");
            }

            expected = new byte[] { aPrefix, (byte)aCommandType, 0 };

            await testUtil.TestDeviceMessage(new StopDeviceCmd(4),
                new List<(byte[], string)>()
                {
                        (expected, Endpoints.Tx),
                }, false);
        }

        public async Task TestVorzeA10CycloneCmd(string aDeviceName, byte aPrefix)
        {
            testUtil = new ProtocolTestUtils();
            await testUtil.SetupTest<VorzeSAProtocol>(aDeviceName, new List<DeviceConfiguration>());
            var expected = new byte[] { aPrefix, 0x1, 50 };

            await testUtil.TestDeviceMessage(new VorzeA10CycloneCmd(4, 50, false),
                new List<(byte[], string)>()
                {
                    (expected, Endpoints.Tx),
                }, false);

            expected = new byte[] { aPrefix, 0x1, 50 + 128 };

            await testUtil.TestDeviceMessage(new VorzeA10CycloneCmd(4, 50, true),
                new List<(byte[], string)>()
                {
                    (expected, Endpoints.Tx),
                }, false);
        }

        public async Task TestRotateCmd(string aDeviceName, byte aPrefix)
        {
            testUtil = new ProtocolTestUtils();
            await testUtil.SetupTest<VorzeSAProtocol>(aDeviceName, new List<DeviceConfiguration>());
            var expected = new byte[] { aPrefix, 0x1, 50 };

            await testUtil.TestDeviceMessage(
                RotateCmd.Create(4, 1, 0.5, false, 1),
                new List<(byte[], string)>
                {
                    (expected, Endpoints.Tx),
                }, false);

            expected = new byte[] { aPrefix, 0x1, 50 + 128 };

            await testUtil.TestDeviceMessage(
                RotateCmd.Create(4, 1, 0.5, true, 1),
                new List<(byte[], string)>
                {
                    (expected, Endpoints.Tx),
                }, false);
        }

        public async Task TestSingleMotorVibrateCmd(string aDeviceName, byte aPrefix)
        {
            testUtil = new ProtocolTestUtils();
            await testUtil.SetupTest<VorzeSAProtocol>(aDeviceName, new List<DeviceConfiguration>());
            var expected = new byte[] { aPrefix, 0x03, 50 };

            await testUtil.TestDeviceMessage(new SingleMotorVibrateCmd(4, 0.5),
                new List<(byte[], string)>()
                {
                    (expected, Endpoints.Tx),
                }, false);

            expected = new byte[] { aPrefix, 0x03, 25 };

            await testUtil.TestDeviceMessage(new SingleMotorVibrateCmd(4, 0.25),
                new List<(byte[], string)>()
                {
                    (expected, Endpoints.Tx),
                }, false);
        }

        public async Task TestVibrateCmd(string aDeviceName, byte aPrefix)
        {
            testUtil = new ProtocolTestUtils();
            await testUtil.SetupTest<VorzeSAProtocol>(aDeviceName, new List<DeviceConfiguration>());
            var expected = new byte[] { aPrefix, 0x3, 50 };

            await testUtil.TestDeviceMessage(
                VibrateCmd.Create(4, 1, 0.5, 1),
                new List<(byte[], string)>
                {
                    (expected, Endpoints.Tx),
                }, false);

            expected = new byte[] { aPrefix, 0x3, 25 };

            await testUtil.TestDeviceMessage(
                VibrateCmd.Create(4, 1, 0.25, 1),
                new List<(byte[], string)>
                {
                    (expected, Endpoints.Tx),
                }, false);
        }

        internal async Task TestInvalidCmds(string aDeviceName, VorzeSAProtocol.CommandType aCommandType)
        {
            testUtil = new ProtocolTestUtils();
            await testUtil.SetupTest<VorzeSAProtocol>(aDeviceName, new List<DeviceConfiguration>());
            if (aCommandType == VorzeSAProtocol.CommandType.Rotate)
            {
                testUtil.TestInvalidDeviceMessage(RotateCmd.Create(4, 1, 0.5, false, 0));
                testUtil.TestInvalidDeviceMessage(RotateCmd.Create(4, 1, 0.5, false, 2));
                testUtil.TestInvalidDeviceMessage(
                    new RotateCmd(4, new List<RotateCmd.RotateSubcommand>()
                    {
                        new RotateCmd.RotateSubcommand(0xffffffff, 0.5, true),
                    }));
            }
            else if (aCommandType == VorzeSAProtocol.CommandType.Vibrate)
            {
                testUtil.TestInvalidDeviceMessage(VibrateCmd.Create(4, 1, 0.5, 0));
                testUtil.TestInvalidDeviceMessage(VibrateCmd.Create(4, 1, 0.5, 2));
                testUtil.TestInvalidDeviceMessage(
                    new VibrateCmd(4, new List<VibrateCmd.VibrateSubcommand>()
                    {
                        new VibrateCmd.VibrateSubcommand(0xffffffff, 0.5),
                    }));
            }
            else
            {
                Assert.Fail("Unknown command type");
            }
        }
    }
}