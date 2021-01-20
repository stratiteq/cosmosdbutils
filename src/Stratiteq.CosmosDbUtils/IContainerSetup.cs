// Copyright (c) Stratiteq Sweden AB. All rights reserved.
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stratiteq.CosmosDbUtils
{
    public interface IContainerSetup
    {
        Task CreateContainerIfNotExistsAsync(Database database, CancellationToken cancellationToken = default);

        Task<int> StoreItemsAsync<T>(IEnumerable<T> items, PartitionKey partitionKey, ItemRequestOptions? itemRequestOptions = null, bool useStoredProcedure = false, string sprocId = "");
    }
}
