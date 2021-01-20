// Copyright (c) Stratiteq Sweden AB. All rights reserved.
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Stratiteq.CosmosDbUtils
{
    /// <summary>
    /// Provides a convenient base class to specific ContainerRepository classes so that they do not need boilerplate code for setting up the container.
    /// The container reference then be retrieved by inheritors through the Container property as long as the CreateContainerIfNotExistsAsync has been called (typically called once per host during application startup, if configured correctly).
    /// </summary>
    public abstract class ContainerRepositoryBase : IContainerSetup
    {
        private readonly ILogger<ContainerRepositoryBase>? logger;
        private Container? container;

        public ContainerRepositoryBase()
        {
        }

        public ContainerRepositoryBase(ILogger<ContainerRepositoryBase> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the value that decides the throughput of the newly created container (applies only if the container doesn't exist).
        /// </summary>
        public abstract int? ThroughputIfContainerNotExists { get; }

        /// <summary>
        /// Gets the container reference, and throw an exception if it isn't yet configured.
        /// </summary>
        public Container Container =>
            container ?? throw new InvalidOperationException("Can't get container if it has not been initialized during startup. This is probably due to a bug in the application, please analyze the root cause and / or restart the applcation");

        protected ILogger<ContainerRepositoryBase> Logger => logger;

        /// <summary>
        /// Creates the container on the provided database.
        /// </summary>
        /// <param name="database">The database on to which the container will be created.</param>
        /// <param name="cancellationToken">A cancellation token to be able to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task CreateContainerIfNotExistsAsync(Database database, CancellationToken cancellationToken = default)
        {
            var containerProperties = GetContainerCreationProperties();
            var containerResponse = await database.CreateContainerIfNotExistsAsync(
                containerProperties,
                ThroughputIfContainerNotExists);

            if (containerResponse.StatusCode == HttpStatusCode.Created)
            {
                logger?.LogInformation($"Container {containerResponse.Container.Id} didn't exist and was therefore created");
            }
            else if (containerResponse.StatusCode == HttpStatusCode.OK)
            {
                logger?.LogInformation($"Database {containerResponse.Container.Id} exists.");
            }
            else
            {
                logger?.LogError($"CreateContainerIfNotExistsAsync with container {containerProperties.Id} didn't return 200 OK or 201 created.");
            }

            if (containerResponse.Container == null)
            {
                throw new Exception($"Couldn't create or retrieve container {containerProperties.Id}");
            }

            foreach (var storedProcedureProperties in GetStoredProcedureProperties())
            {
                await CreateStoredProcedureIfNotExistsOrUpdateIfDifferentAsync(containerResponse.Container, storedProcedureProperties);
            }

            container = containerResponse.Container;
        }

        /// <summary>
        /// Get items from the database.
        /// </summary>
        /// <typeparam name="T">The type to query for.</typeparam>
        /// <param name="queryDefinition">The query.</param>
        /// <param name="queryRequestOptions">Request options stating how to ask the database for example if should be a partitioned query or cross partition query.</param>
        /// <param name="sessionToken">Optional session token.</param>
        /// <returns></returns>
        public async Task<QueryResult<T>> GetItemsAsync<T>(QueryDefinition queryDefinition, QueryRequestOptions queryRequestOptions, string? sessionToken = default)
        {
            using var queryIterator = Container.GetItemQueryIterator<T>(
                queryDefinition: queryDefinition,
                requestOptions: queryRequestOptions);
            return await GetItemsFromFeedIteratorAsync<T>(queryIterator, sessionToken);
        }

        /// <summary>
        /// Get items from the database reading from a FeedIterator.
        /// </summary>
        /// <typeparam name="T">The type to query for.</typeparam>
        /// <param name="feedIterator">The FeedIterator returned from the query to read documents from.</param>
        /// <param name="sessionToken">Optional session token.</param>
        /// <returns></returns>
        public async Task<QueryResult<T>> GetItemsFromFeedIteratorAsync<T>(FeedIterator<T> feedIterator, string? sessionToken = default)
        {
            var resultSet = new List<T>();
            double totalRequestCharge = 0;
            FeedResponse<T>? response = null;

            while (feedIterator.HasMoreResults)
            {
                response = await feedIterator.ReadNextAsync();
                totalRequestCharge += response.RequestCharge;
                resultSet.AddRange(response);
            }

            return new QueryResult<T>
            {
                ContinuationToken = response?.ContinuationToken,
                Items = resultSet,
                SessionToken = string.IsNullOrEmpty(sessionToken) ? response!.Headers.Session : sessionToken,
                StatusCode = response!.StatusCode,
                TotalRequestCharge = totalRequestCharge
            };
        }

        /// <summary>
        /// Store items in the database. Defaults to using the SDK bulk support.
        /// </summary>
        /// <typeparam name="T">The type to store.</typeparam>
        /// <param name="items">The items to store.</param>
        /// <param name="partitionKey">The partition key for the items to be stored.</param>
        /// <param name="itemRequestOptions">Optional: Cosmos item request options.</param>
        /// <param name="useStoredProcedure">Optional. True if the items should be inserted using a stored procedure. False by default.</param>
        /// <param name="storedProcedureId">If <paramref name="useStoredProcedure"/> is true then this parameter must be set to the id of the stored procedure.</param>
        /// <returns>The count of items stored.</returns>
        public async Task<int> StoreItemsAsync<T>(IEnumerable<T> items, PartitionKey partitionKey, ItemRequestOptions? itemRequestOptions = null, bool useStoredProcedure = false, string storedProcedureId = "")
        {
            int response = 0;
            if (!useStoredProcedure)
            {
                response = await StoreItemsAsync(items, partitionKey, itemRequestOptions);
            }
            else
            {
                var id = storedProcedureId ?? throw new ArgumentException($"{nameof(storedProcedureId)} cannot be null or empty if {nameof(useStoredProcedure)} is true");
                response = await StoreItemsUsingSprocAsync(items, partitionKey, id);
            }

            if (response != items.ToList().Count)
            {
                // Reaching here is wierd, log critical but don't explode.
                Logger.LogCritical($"The number of documents that was posted ({items.ToList().Count}) doesn't match the number of documents actually stored." +
                    $"This will most likely lead to wierd behavior and needs to be thoroughly investigated ASAP.");
            }

            return response;
        }

        protected QueryDefinition CreateInClauseQueryDefinition(IEnumerable<string> values, string queryText)
        {
            var namedParameters = new List<string>();
            var loopIndex = 0;

            foreach (var ticketId in values)
            {
                var paramName = "@n_" + loopIndex++;
                namedParameters.Add(paramName);
            }

            if (namedParameters.Any())
            {
                queryText = string.Format(queryText, string.Join(" , ", namedParameters));
            }

            var queryDefinition = new QueryDefinition(queryText);

            loopIndex = 0;
            foreach (var value in values)
            {
                queryDefinition.WithParameter("@n_" + loopIndex++, value);
            }

            return queryDefinition;
        }

        /// <summary>
        /// In inherited classes, gets the ContainerProperties used while creating a new container (applies only if the container doesn't exist).
        /// </summary>
        /// <returns>An instance of <see cref="ContainerProperties"/></returns>
        protected abstract ContainerProperties GetContainerCreationProperties();

        /// <summary>
        /// In inherited classes, gets a list of StoredProcedureProperties (one for each stored procedure to create) used while creating a new container (applies only if the container doesn't exist).
        /// </summary>
        /// <returns>An instance of <see cref="StoredProcedureProperties"/></returns>
        protected virtual StoredProcedureProperties[] GetStoredProcedureProperties() =>
            Array.Empty<StoredProcedureProperties>();

        // Store items when bulk support is enabled.
        private async Task<int> StoreItemsAsync<T>(IEnumerable<T> items, PartitionKey partitionKey, ItemRequestOptions? itemRequestOptions)
        {
            int itemsCreated = 0;

            var tasks = new List<Task>(items.ToList().Count);
            foreach (var item in items)
            {
                tasks.Add(
                    Container.CreateItemAsync<T>(item, partitionKey, itemRequestOptions)
                    .ContinueWith((Task<ItemResponse<T>> task) =>
                    {
                        if (!task.IsCompletedSuccessfully)
                        {
                            var innerExceptions = task?.Exception?.Flatten();
                            var cosmosException = innerExceptions?.InnerExceptions.FirstOrDefault(innerEx => innerEx is CosmosException) as CosmosException;

                            logger?.LogCritical(cosmosException, $"StoreItemsAsync failed to create item with status code {cosmosException?.StatusCode}");
                        }
                    }));
            }

            await Task.WhenAll(tasks);

            itemsCreated += tasks.Count(task => task.IsCompletedSuccessfully);

            return itemsCreated;
        }

        private async Task<int> StoreItemsUsingSprocAsync<T>(IEnumerable<T> items, PartitionKey partitionKey, string sprocId)
        {
            return await Container.Scripts.ExecuteStoredProcedureAsync<int>(
                sprocId,
                partitionKey,
                new[] { items },
                new StoredProcedureRequestOptions { });
        }

        private async Task CreateStoredProcedureIfNotExistsOrUpdateIfDifferentAsync(Container container, StoredProcedureProperties storedProcedureProperties)
        {
            try
            {
                var storedProcedureResponse = await container.Scripts.ReadStoredProcedureAsync(storedProcedureProperties.Id);

                if (storedProcedureProperties.Body.Equals(storedProcedureResponse.Resource.Body, StringComparison.OrdinalIgnoreCase))
                {
                    logger?.LogInformation($"Stored procedure {storedProcedureResponse.Resource.Id} exists.");
                }
                else
                {
                    storedProcedureResponse = await container.Scripts.ReplaceStoredProcedureAsync(storedProcedureProperties);
                    if (storedProcedureResponse.StatusCode == HttpStatusCode.OK)
                    {
                        logger?.LogInformation($"Stored procedure {storedProcedureResponse.Resource.Id} existed but was different from source code and was therefore updated.");
                    }
                    else
                    {
                        logger?.LogError($"Stored procedure {storedProcedureProperties.Id} couldn't be updated. " +
                            $"Failed with status code {storedProcedureResponse.StatusCode} and diagnostics info: {storedProcedureResponse.Diagnostics.ToString()}");
                    }
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                var storedProcedureResponse = await container.Scripts.CreateStoredProcedureAsync(storedProcedureProperties);
                if (storedProcedureResponse.StatusCode == HttpStatusCode.Created)
                {
                    logger?.LogInformation($"Stored procedure {storedProcedureResponse.Resource.Id} didn't exist and was therefore created");
                }
                else
                {
                    logger?.LogError($"Stored procedure {storedProcedureProperties.Id} couldn't be created. " +
                        $"Failed with status code {storedProcedureResponse.StatusCode} and diagnostics info: {storedProcedureResponse.Diagnostics.ToString()}");
                }
            }
        }
    }
}
