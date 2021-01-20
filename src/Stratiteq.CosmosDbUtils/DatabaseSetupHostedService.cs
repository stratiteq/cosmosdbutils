// Copyright (c) Stratiteq Sweden AB. All rights reserved.
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stratiteq.CosmosDbUtils
{
    /// <summary>
    /// A hosted service that runs the registered <see cref="IDatabaseSetup.SetupDbAsync()"/> method.
    /// </summary>
    public class DatabaseSetupHostedService : IHostedService
    {
        private readonly IServiceProvider serviceProvider;

        public DatabaseSetupHostedService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Create a new scope to retrieve scoped services
            using var scope = serviceProvider.CreateScope();

            // Get the CosmosDbSetup instance
            var cosmosDbSetup = scope.ServiceProvider.GetRequiredService<IDatabaseSetup>();

            // Setup database asynchronously
            await cosmosDbSetup.SetupDbAsync();
        }

        // noop
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
