using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Core.Messages;
using Buttplug.Devices.Configuration;
using JetBrains.Annotations;

namespace Buttplug.Devices
{
    public interface IButtplugDeviceProtocol
    {
        /// <summary>
        /// Name of the Device the Protocol represents.
        /// </summary>
        /// <remarks>
        /// Protocols hold the name of the device they're currently representing, as they may be
        /// required to calculate the name via device communication on initialization.
        /// </remarks>
        string Name { get; }

        string DeviceConfigIdentifier { get; }

        /// <summary>
        /// Allowed message types for this device.
        /// </summary>
        IEnumerable<Type> AllowedMessageTypes { get; }

        /// <summary>
        /// Checks to see whether a message is supported by the device that implements this
        /// interface. If the message is supported, executes the handler for that message.
        /// </summary>
        /// <param name="aMsg">Device message to handle.</param>
        /// <param name="aToken">Cancellation token to stop message parsing action externally.</param>
        /// <returns>Response, usually <see cref="Ok"/> or <see cref="Error"/>, but can be other types.</returns>
        [NotNull]
        Task<ButtplugMessage> ParseMessageAsync(ButtplugDeviceMessage aMsg, CancellationToken aToken = default(CancellationToken));

        /// <summary>
        /// Initializes a device. Required for devices that may require connection handshakes or
        /// similar on-connection setups.
        /// </summary>
        /// <returns>Response, usually <see cref="Ok"/> or <see cref="Error"/>.</returns>
        [NotNull]
        Task InitializeAsync(CancellationToken aToken = default(CancellationToken));

        /// <summary>
        /// Configures a device. Required for devices that may share protocol definitions with
        /// varying numbers of device features, etc.
        /// </summary>
        /// <returns>Response, usually <see cref="Ok"/> or <see cref="Error"/>.</returns>
        [NotNull]
        Task ConfigureAsync(DeviceConfiguration aConfig, CancellationToken aToken = default(CancellationToken));

        /// <summary>
        /// Retrieves the message attributes for the device associated with this message. Used for
        /// retrieving information about feature counts in device command messages, etc...
        /// </summary>
        /// <param name="aMsg">Message type to fetch attributes for.</param>
        [NotNull]
        MessageAttributes GetMessageAttrs(Type aMsg);
    }
}
