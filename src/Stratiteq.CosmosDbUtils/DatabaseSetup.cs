// Copyright (c) Stratiteq Sweden AB. All rights reserved.
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stratiteq.CosmosDbUtils
{
    /// <summary>
    /// Provides a basic module for setting up a database with container(s) if they do not already exists.
    /// </summary>
    public class DatabaseSetup : IDatabaseSetup
    {
        private readonly CosmosClient cosmosClient;
        private readonly DatabaseCreationSettings databaseCreationSettings;
        private readonly IEnumerable<IContainerSetup> cosmosContainers;
        private readonly ILogger<DatabaseSetup> logger;

        public DatabaseSetup(CosmosClient cosmosClient, DatabaseCreationSettings databaseCreationSettings, IEnumerable<IContainerSetup> cosmosContainers, ILogger<DatabaseSetup> logger)
        {
            this.cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
            this.databaseCreationSettings = databaseCreationSettings ?? throw new ArgumentNullException(nameof(databaseCreationSettings));
            this.cosmosContainers = cosmosContainers ?? throw new ArgumentNullException(nameof(cosmosContainers));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SetupDbAsync()
        {
            logger.LogInformation($"Fetching database reference, and creating it if it doesn't exists...");
            var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseCreationSettings.Id, databaseCreationSettings.Throughput);

            if (databaseResponse.StatusCode == System.Net.HttpStatusCode.Created)
            {
                logger.LogInformation($"Database {databaseCreationSettings.Id} didn't exist and was therefore created");
            }
            else if (databaseResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                logger.LogInformation($"Database {databaseCreationSettings.Id} exists.");
            }
            else
            {
                logger.LogError($"CreateDatabaseIfNotExistsAsync with Databasename {databaseCreationSettings.Id} didn't return 200 OK or 201 created.");
            }

            if (databaseResponse.Database == null)
            {
                throw new Exception($"Couldn't create or retrieve database {databaseCreationSettings.Id}");
            }

            foreach (var cosmosContainer in cosmosContainers)
            {
                await cosmosContainer.CreateContainerIfNotExistsAsync(databaseResponse.Database);
            }
        }
    }
}
