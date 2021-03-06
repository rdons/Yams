﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Etg.Yams.Client;
using Etg.Yams.Process.Ipc;
using Etg.Yams.Utils;

namespace Etg.Yams.Process
{
    public class MonitorInitProcessDecorator : AbstractProcessDecorator
    {
        private readonly YamsConfig _config;
        private readonly IIpcConnection _ipcConnection;

        public MonitorInitProcessDecorator(YamsConfig config, IProcess process,
            IIpcConnection ipcConnection) : base(process)
        {
            _config = config;
            _ipcConnection = ipcConnection;
        }

        public override void Dispose()
        {
            base.Dispose();
            _ipcConnection?.Dispose();
        }

        public override async Task Start(string args)
        {
            await _process.Start($"{args} --{nameof(YamsClientOptions.InitializationPipeName)} {_ipcConnection.ConnectionId}");

            try
            {
                await _ipcConnection.Connect().Timeout(_config.IpcConnectTimeout,
                    "Connecting to initialization pipe has timed out, make sure that the app is connecting to the same pipe");

                Trace.TraceInformation("Yams is waiting for the app to finish initializing");
                string msg = await _ipcConnection.ReadMessage()
                    .Timeout(_config.AppInitTimeout, $"Did not receive initialized message from the app {ExePath}");

                if (msg != "[INITIALIZE_DONE]")
                {
                    throw new InvalidOperationException($"Unexpected message received from app: {msg}");
                }
            }
            catch (Exception)
            {
                await Kill();
                throw;
            }

            Trace.TraceInformation($"Received initialized message from App {ExePath}; App is ready to receive requests");
        }

        public override async Task Kill()
        {
            await Task.WhenAll(_ipcConnection.Disconnect(), base.Kill());
        }
    }
}