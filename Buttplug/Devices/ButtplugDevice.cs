using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Core;
using Buttplug.Core.Logging;
using Buttplug.Core.Messages;
using Buttplug.Devices.Configuration;
using JetBrains.Annotations;

namespace Buttplug.Devices
{
    /// <summary>
    /// Representation of a device. Presents a unified view of an IButtplugDeviceImpl and
    /// IButtplugProtocol, while also handling the Buttplug specific info like device indexes.
    /// </summary>
    public class ButtplugDevice : IButtplugDevice
    {
        [NotNull]
        protected readonly IButtplugDeviceProtocol _protocol;

        [NotNull]
        protected readonly IButtplugDeviceImpl _device;

        /// <inheritdoc />
        public string Name { get; protected set; }

        /// <inheritdoc />
        public string Identifier => _device.Address;

        /// <inheritdoc />
        public uint Index { get; set; }

        /// <inheritdoc />
        public bool Connected => _device.Connected;

        /// <inheritdoc />
        [CanBeNull]
        public event EventHandler DeviceRemoved;

        /// <inheritdoc />
        public IEnumerable<Type> AllowedMessageTypes => _protocol.AllowedMessageTypes;

        [NotNull]
        protected readonly IButtplugLog BpLogger;

        /// <inheritdoc />
        [CanBeNull]
        public event EventHandler<MessageReceivedEventArgs> MessageEmitted;

        /// <summary>
        /// Initializes a new instance of the <see cref="ButtplugDevice"/> class.
        /// </summary>
        /// <param name="aLogManager">The log manager.</param>
        /// <param name="aDevice">The device implementation (Bluetooth, USB, etc).</param>
        /// <param name="aProtocol">The device protocol (Lovense, Launch, etc).</param>
        public ButtplugDevice([NotNull] IButtplugLogManager aLogManager,
            [NotNull] IButtplugDeviceProtocol aProtocol,
            [NotNull] IButtplugDeviceImpl aDevice)
        {
            // Protocol can be null if activator construction from type constructor fails
            ButtplugUtils.ArgumentNotNull(aProtocol, nameof(aProtocol));
            _protocol = aProtocol;
            // To start, make this resolve from the protocol. This may change
            // once we have an identifier.
            Name = _protocol.Name;
            _device = aDevice;
            BpLogger = aLogManager.GetLogger(GetType());
            _device.DeviceRemoved += OnDeviceRemoved;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ButtplugDevice"/> class.
        /// </summary>
        /// <param name="aLogManager">The log manager.</param>
        /// <param name="aDevice">The device implementation (Bluetooth, USB, etc).</param>
        /// <param name="aProtocolType">A Type for a protocol, which we will create an instance of.</param>
        public ButtplugDevice([NotNull] IButtplugLogManager aLogManager,
            [NotNull] Type aProtocolType,
            [NotNull] IButtplugDeviceImpl aDevice)
        : this(aLogManager,
            // A lot of trust happening in the structure of protocol constructors here.
            // todo should probably document the many ways this can throw.
            (IButtplugDeviceProtocol)Activator.CreateInstance(aProtocolType, aLogManager, aDevice),
            aDevice)
        {
        }

        public static ButtplugDevice Create<T>(IButtplugLogManager aLogManager,
            IButtplugDeviceImpl aDevice,
            Func<IButtplugLogManager, IButtplugDeviceImpl, T> aProtocolCreationFunc)
        where T : IButtplugDeviceProtocol, new()
        {
            var p = aProtocolCreationFunc(aLogManager, aDevice);
            return new ButtplugDevice(aLogManager, p, aDevice);
        }

        /// <inheritdoc />
        public MessageAttributes GetMessageAttrs(Type aMsg)
        {
            return _protocol.GetMessageAttrs(aMsg);
        }

        /// <inheritdoc />
        public async Task<ButtplugMessage> ParseMessageAsync([NotNull] ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            if (!Connected)
            {
                throw new ButtplugDeviceException(BpLogger, $"{Name} has disconnected and can no longer process messages.", aMsg.Id);
            }

            return await _protocol.ParseMessageAsync(aMsg, aToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual async Task InitializeAsync(List<DeviceConfiguration> aConfigurations, CancellationToken aToken)
        {
            // Run initialize in order to set the DeviceConfigIdentifier, if needed.
            await _protocol.InitializeAsync(aToken);

            // Look up the identifier in the device configuration records
            var ident = (from config in aConfigurations
                where config.Identifiers.Contains(_protocol.DeviceConfigIdentifier)
                select config).ToList();

            // If all we have is one configuration, it may be the default. If
            // nothing else was found, select it. This will happen for cases
            // like XInput.
            if (!ident.Any() && aConfigurations.Count == 1)
            {
                ident.Add(aConfigurations.First());
            }

            if (ident.Any())
            {
                // This will usually be en-us for now, until we get more languages in.
                Name = ident.First().Names.First().Value;
            }

            // Grab the first config, assume it's just defaults for now
            // todo: get a pure default config
            if (!ident.Any() && aConfigurations.Any())
            {
                ident.Add(aConfigurations.First());
            }

            await _protocol.ConfigureAsync(ident.Any() ? ident.First() : null, aToken);
        }

        /// <inheritdoc />
        public void Disconnect()
        {
            _device.Disconnect();
        }

        protected void OnDeviceRemoved(object aObj, EventArgs aArgs)
        {
            InvokeDeviceRemoved();
        }

        /// <summary>
        /// Invokes the DeviceRemoved event handler.
        /// Required to disconnect devices from the lower levels.
        /// </summary>
        protected void InvokeDeviceRemoved()
        {
            DeviceRemoved?.Invoke(this, new EventArgs());
        }
    }
}
