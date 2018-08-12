﻿// <copyright file="TestServer.cs" company="Nonpolynomial Labs LLC">
// Buttplug C# Source Code File - Visit https://buttplug.io for more info about the project.
// Copyright (c) Nonpolynomial Labs LLC. All rights reserved.
// Licensed under the BSD 3-Clause license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Generic;
using Buttplug.Core;
using Buttplug.Core.Messages;
using JetBrains.Annotations;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;

namespace Buttplug.Server.Test
{
    public class TestServer : ButtplugServer
    {
        public readonly List<string> OutgoingAsync = new List<string>();

        // Set MaxPingTime to zero (infinite ping/ping checks off) by default for tests
        public TestServer(uint aMaxPingTime = 0, DeviceManager aDevManager = null, bool aInitClient = true)
            : base("Test Server", aMaxPingTime, aDevManager)
        {
            // Build ourselves an NLog manager just so we can see what's going on.
            var dt = new DebuggerTarget();
            LogManager.Configuration = new LoggingConfiguration();
            LogManager.Configuration.AddTarget("debugger", dt);
            LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, dt));
            LogManager.Configuration = LogManager.Configuration;

            if (aInitClient)
            {
                // Send RequestServerInfo message now, to save us having to do it in every test.
                Assert.True(SendMessage(new RequestServerInfo("TestClient")).GetAwaiter().GetResult() is ServerInfo);
            }
        }

        public void OnMessageReceived(object aObj, MessageReceivedEventArgs aEvent)
        {
            OutgoingAsync.Add(Serialize(aEvent.Message));
        }

        // ReSharper disable once UnusedMember.Global
        [NotNull]
        internal DeviceManager GetDeviceManager()
        {
            return _deviceManager;
        }

        internal ButtplugMessage[] Deserialize(string aMsg)
        {
            return _parser.Deserialize(aMsg);
        }
    }
}
