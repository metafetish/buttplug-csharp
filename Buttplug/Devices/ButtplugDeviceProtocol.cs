// <copyright file="ButtplugDeviceProtocol.cs" company="Nonpolynomial Labs LLC">
//     Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
//     Copyright (c) Nonpolynomial Labs LLC. All rights reserved. Licensed under the BSD 3-Clause
//     license. See LICENSE file in the project root for full license information.
// </copyright>

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
    public abstract class ButtplugDeviceProtocol : IButtplugDeviceProtocol
    {
        public string Name { get; protected set; }

        public string DeviceConfigIdentifier { get; protected set; }

        /// <summary>
        /// Gets the message handler functions.
        /// </summary>
        /// <remarks>
        /// This is kept private so that we can regulate keys being added. Otherwise it's easy to
        /// accidentally copy/paste duplicate keys when adding new functions, and that is hell to debug.
        /// </remarks>
        [NotNull]
        protected readonly Dictionary<Type, (Func<ButtplugDeviceMessage, CancellationToken, Task<ButtplugMessage>> Function, MessageAttributes Attrs)> MsgFuncs;

        [NotNull]
        protected readonly IButtplugLog BpLogger;

        [NotNull]
        protected IButtplugDeviceImpl Interface;

        protected bool SentVibration = false;
        protected bool SentLinear = false;
        protected bool SentRotation = false;

        /// <inheritdoc />
        public IEnumerable<Type> AllowedMessageTypes => MsgFuncs.Keys;

        protected ButtplugDeviceProtocol(IButtplugLogManager aLogManager,
            string aName,
            IButtplugDeviceImpl aInterface)
        {
            BpLogger = aLogManager.GetLogger(GetType());
            Name = aName;
            // By default, we'll make the identifier the name of the device as
            // seen by the device subtype manager. Running initialize may change
            // this, for instance, when Lovense gets its device identifier
            // letter during status queries.
            DeviceConfigIdentifier = aInterface.Name;
            Interface = aInterface;
            MsgFuncs =
                new Dictionary<Type, (Func<ButtplugDeviceMessage, CancellationToken, Task<ButtplugMessage>> Function,
                    MessageAttributes Attrs)>();
        }

        public MessageAttributes GetMessageAttrs(Type aMsg)
        {
            if (MsgFuncs.TryGetValue(aMsg, out var wrapper))
            {
                return wrapper.Attrs ?? new MessageAttributes();
            }

            return new MessageAttributes();
        }

        /// <inheritdoc />
        public async Task<ButtplugMessage> ParseMessageAsync([NotNull] ButtplugDeviceMessage aMsg, CancellationToken aToken)
        {
            if (!MsgFuncs.ContainsKey(aMsg.GetType()))
            {
                throw new ButtplugDeviceException(BpLogger, $"{Name} cannot handle message of type {aMsg.GetType().Name}", aMsg.Id);
            }

            // We just checked whether the key exists above, so we're ok.
            // ReSharper disable once PossibleNullReferenceException
            return await MsgFuncs[aMsg.GetType()].Function.Invoke(aMsg, aToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual Task InitializeAsync(CancellationToken aToken)
        {
            return Task.FromResult<ButtplugMessage>(new Ok(ButtplugConsts.SystemMsgId));
        }

        /// <inheritdoc />
        public virtual Task ConfigureAsync(DeviceConfiguration aConfig, CancellationToken aToken)
        {
            // Default behaviour: make sure there's a StopDeviceCmd or errors will be thrown later
            if (!MsgFuncs.ContainsKey(typeof(StopDeviceCmd)))
            {
                AddMessageHandler<StopDeviceCmd>((message, token) =>
                    Task.FromResult<ButtplugMessage>(new Ok(message.Id)));
            }

            return Task.FromResult<ButtplugMessage>(new Ok(ButtplugConsts.SystemMsgId));
        }

        /// <summary>
        /// Used by deriving classes to add ButtplugDeviceMessage handlers to the handler map. Checks
        /// to make sure duplicate entries are not being added.
        /// </summary>
        /// <remarks>
        /// Having this as a generic with a type constraint rather than a type parameter means we get
        /// type checking at compile/roslyn time.
        /// </remarks>
        /// <typeparam name="T">ButtplugDeviceMsg deriving type.</typeparam>
        /// <param name="aFunction">Handler for the message type.</param>
        /// <param name="aAttrs">MessageAttribute parameters, assuming the message type has any.</param>
        protected void AddMessageHandler<T>(Func<ButtplugDeviceMessage, CancellationToken, Task<ButtplugMessage>> aFunction,
            MessageAttributes aAttrs = null) where T : ButtplugDeviceMessage
        {
            MsgFuncs.Add(typeof(T), (aFunction, aAttrs ?? new MessageAttributes()));
        }


        protected T CheckMessageHandler<T>(ButtplugDeviceMessage aMsg)
            where T : ButtplugDeviceMessage
        {
            return (aMsg is T cmdMsg) ? cmdMsg : throw new ButtplugDeviceException(BpLogger, $"Wrong handler for message type {aMsg.GetType()}", aMsg.Id);
        }

        private void CheckGenericSubcommandList<T>(ButtplugDeviceMessage aMsg, IEnumerable<T> aCmdList, uint aLimitValue)
        where T : GenericMessageSubcommand
        {
            if (!aCmdList.Any() || aCmdList.Count() > aLimitValue)
            {
                if (aLimitValue == 1)
                {
                    throw new ButtplugDeviceException(BpLogger, $"{aMsg.GetType().Name} requires 1 subcommand for this device, {aCmdList.Count()} present.", aMsg.Id);
                }

                throw new ButtplugDeviceException(BpLogger, $"{aMsg.GetType().Name} requires between 1 and {aLimitValue} subcommands for this device, {aCmdList.Count()} present.", aMsg.Id);
            }

            foreach (var cmd in aCmdList)
            {
                if (cmd.Index >= aLimitValue)
                {
                    throw new ButtplugDeviceException(BpLogger, $"Index {cmd.Index} is out of bounds for {aMsg.GetType().Name} for this device.", aMsg.Id);
                }
            }
        }

        protected T CheckGenericMessageHandler<T>(ButtplugDeviceMessage aMsg, uint aLimitValue)
            where T : ButtplugDeviceMessage
        {
            var actualMsg = CheckMessageHandler<T>(aMsg);

            // Can't seem to pattern match this, so big ol' if/else chain it is. :(
            if (typeof(T) == typeof(VibrateCmd))
            {
                var cmdMsg = actualMsg as VibrateCmd;
                CheckGenericSubcommandList(cmdMsg, cmdMsg.Speeds, aLimitValue);
                return actualMsg;
            }

            if (typeof(T) == typeof(RotateCmd))
            {
                var cmdMsg = actualMsg as RotateCmd;
                CheckGenericSubcommandList(cmdMsg, cmdMsg.Rotations, aLimitValue);
                return actualMsg;
            }

            if (typeof(T) == typeof(LinearCmd))
            {
                var cmdMsg = actualMsg as LinearCmd;
                CheckGenericSubcommandList(cmdMsg, cmdMsg.Vectors, aLimitValue);
                return actualMsg;
            }

            throw new ButtplugMessageException("CheckGenericMessageHandler only works with generic (VibrateCmd/RotateCmd/etc) messages.", aMsg.Id);
        }
    }
}
