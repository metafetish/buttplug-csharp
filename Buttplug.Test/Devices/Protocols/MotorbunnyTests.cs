using System.Collections.Generic;
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

    [TestFixture]
    public class MotorbunnyTests
    {
        [NotNull]
        private ProtocolTestUtils testUtil;

        [SetUp]
        public async Task Init()
        {
            testUtil = new ProtocolTestUtils();

            // Just leave name the same as the prefix, we'll set device type via initialize.
            await testUtil.SetupTest<MotorbunnyProtocol>("MB Controller", new List<DeviceConfiguration>(), false);
        }

        [Test]
        public void TestDeviceName()
        {
            testUtil.TestDeviceName("Motorbunny");
        }

        [Test]
        public void TestAllowedMessages()
        {
            testUtil.TestDeviceAllowedMessages(new Dictionary<System.Type, uint>()
            {
                { typeof(StopDeviceCmd), 0 },
                { typeof(SingleMotorVibrateCmd), 0 },
                { typeof(VibrateCmd), 1 },
                { typeof(RotateCmd), 1 },
            });
        }

        // StopDeviceCmd noop test handled in GeneralDeviceTests
        [Test]
        public async Task TestStopDeviceCmd()
        {
            var expected =
                new List<(byte[], string)>()
                {
                    (new byte[] { 0xff, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x05, 0xec }, Endpoints.Tx),
                };

            await testUtil.TestDeviceMessage(new SingleMotorVibrateCmd(4, 0.5), expected, false);

            expected =
                new List<(byte[], string)>()
                {
                    (new byte[] { 0xf0, 0x00, 0x00, 0x00, 0x00, 0xec }, Endpoints.Tx),
                    (new byte[] { 0xa0, 0x00, 0x00, 0x00, 0x00, 0xec }, Endpoints.Tx),
                };

            await testUtil.TestDeviceMessage(new StopDeviceCmd(4), expected, false);
        }

        [Test]
        public async Task TestSingleMotorVibrateCmd()
        {
            var expected =
                new List<(byte[], string)>()
                {
                    (new byte[] { 0xff, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x05, 0xec }, Endpoints.Tx),
                };

            await testUtil.TestDeviceMessage(new SingleMotorVibrateCmd(4, 0.5), expected, false);
        }

        [Test]
        public async Task TestVibrateCmd()
        {
            var expected =
                new List<(byte[], string)>()
                {
                    (new byte[] { 0xff, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x7f, 0x14, 0x05, 0xec }, Endpoints.Tx),
                };

            await testUtil.TestDeviceMessage(VibrateCmd.Create(4, 1, 0.5, 1), expected, false);
        }

        [Test]
        public void TestInvalidVibrateCmd()
        {
            testUtil.TestInvalidVibrateCmd(1);
        }

        [Test]
        public async Task TestRotateCmd()
        {
            var expected =
                new List<(byte[], string)>()
                {
                    (new byte[] { 0xaf, 0x2a, 0x7f, 0x2a, 0x7f, 0x2a, 0x7f, 0x2a, 0x7f, 0x2a, 0x7f, 0x2a, 0x7f, 0x2a, 0x7f, 0x9F, 0xec }, Endpoints.Tx),
                };

            await testUtil.TestDeviceMessage(RotateCmd.Create(4, 1, 0.5, true, 1), expected, false);

        }

        public async Task TestRotateCmdCounterclockwise()
        {
            var expected =
                new List<(byte[], string)>()
                {
                    (new byte[] { 0xaf, 0x29, 0x7f, 0x29, 0x7f, 0x29, 0x7f, 0x29, 0x7f, 0x29, 0x7f, 0x29, 0x7f, 0x29, 0x7f, 0x98, 0xec }, Endpoints.Tx),
                };

            await testUtil.TestDeviceMessage(RotateCmd.Create(4, 1, 0.5, false, 1), expected, false);
        }
    }
}
