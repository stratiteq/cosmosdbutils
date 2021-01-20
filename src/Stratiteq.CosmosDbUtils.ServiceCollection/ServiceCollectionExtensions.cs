// Copyright (c) Stratiteq Sweden AB. All rights reserved.
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.ApplicationInsights;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stratiteq.CosmosDbUtils
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureCosmosDb(this IServiceCollection services, string? applicationName, string? accountEndpoint, string? authKey)
        {
            // There should be only one instance of CosmosClient
            services.AddSingleton<CosmosClient>(serviceProvider =>
                new CosmosClientBuilder(accountEndpoint, authKey)
                .WithConnectionModeGateway(maxConnectionLimit: int.MaxValue)
                .WithApplicationName(applicationName)
                .WithApplicationRegion(Regions.WestEurope)
                .WithBulkExecution(true)
                .AddCustomHandlers(new AppInsightsRequestHandler(serviceProvider.GetService<TelemetryClient>()))
                .Build());

            services

                // Add the module that sets up the cosmos database if it doesn't exists.
                .AddCosmosDbSetup(new DatabaseCreationSettings(applicationName));

            // Add the hosted service that sets up the cosmos db during application startup
            services.AddHostedService<DatabaseSetupHostedService>();

            return services;
        }

        /// <summary>
        /// Adds the default CosmosDbSetup-implementation
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="databaseCreationSettings">Settings deciding the name and throughput of the Cosmos database.</param>
        /// <returns>A <see cref="IServiceCollection"/> that can be used to further configure the services.</returns>
        public static IServiceCollection AddCosmosDbSetup(this IServiceCollection services, DatabaseCreationSettings databaseCreationSettings)
        {
            services.TryAddSingleton<DatabaseCreationSettings>(databaseCreationSettings);
            services.TryAddSingleton<IDatabaseSetup, DatabaseSetup>();

            return services;
        }

        /// <summary>
        /// Adds a ContainerRepository to the <see cref="IServiceCollection" />.
        /// </summary>
        /// <typeparam name="TService">The interface used by the ContainerRepository that exposes the supported database operations for the container.</typeparam>
        /// <typeparam name="TImplementation">The implementation of the ContainerRepository that implements the supported database operations for the container.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <returns>A <see cref="IServiceCollection"/> that can be used to further configure the services.</returns>
        public static IServiceCollection AddContainerRepository<TService, TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService, IContainerSetup
        {
            // Add the container class as singleton once
            services.TryAddSingleton<TImplementation>();

            // Set it up so that both TService (whatever container interface is being used) and ICosmosContainer resolves to this exact same instance
            services.TryAddSingleton<TService>(x => x.GetRequiredService<TImplementation>());

            // And since all ContainerRepositories will implement IContainerSetup, we can't use TryAdd here. All the instances can then be retrieved with serviceProvider.GetServices (plural).
            services.AddSingleton<IContainerSetup>(x => x.GetRequiredService<TImplementation>());

            return services;
        }
    }
}
