// Copyright (c) Stratiteq Sweden AB. All rights reserved.
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Autofac;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using System;

using ContainerBuilder = Autofac.ContainerBuilder;

namespace Stratiteq.CosmosDbUtils
{
    public static class ContainerBuilderExtensions
    {
        public static ContainerBuilder ConfigureCosmosDb(this ContainerBuilder services, CosmosDbSettings cosmosDbSettings, string connectionString)
        {
            if (cosmosDbSettings == null)
            {
                throw new ArgumentNullException(nameof(cosmosDbSettings));
            }

            services.Register<CosmosClient>(service =>
                new CosmosClientBuilder(connectionString)
                    .WithApplicationName(cosmosDbSettings.ApplicationName)
                    .WithApplicationRegion(Regions.WestEurope)
                    .WithBulkExecution(cosmosDbSettings.AllowBulkExecution)
                    .WithThrottlingRetryOptions(cosmosDbSettings.MaxRetryWaitTimeOnRateLimitedRequests, cosmosDbSettings.MaxRetryAttemptsOnRateLimitedRequests)
                    .WithConnectionModeDirect()
                    .AddCustomHandlers(new AppInsightsRequestHandler(service.Resolve<TelemetryClient>()))
                    .Build())
                .As<CosmosClient>()
                .SingleInstance();

            services.AddCosmosDbSetup(new DatabaseCreationSettings(cosmosDbSettings.DatabaseId));

            return services;
        }

        public static ContainerBuilder AddCosmosDbSetup(this ContainerBuilder services, DatabaseCreationSettings databaseCreationSettings)
        {
            services
                .RegisterInstance<DatabaseCreationSettings>(databaseCreationSettings)
                .SingleInstance();

            services.RegisterType<DatabaseSetup>()
                .As<IDatabaseSetup>()
                .SingleInstance();

            return services;
        }

        public static ContainerBuilder AddContainerRepository<TService, TImplementation>(this ContainerBuilder services)
            where TService : class
            where TImplementation : class, TService, IContainerSetup
        {
            // Add the container class as singleton once
            services.RegisterType<TImplementation>()
                .As<TService>()
                .As<IContainerSetup>()
                .SingleInstance();

            return services;
        }
    }
}
