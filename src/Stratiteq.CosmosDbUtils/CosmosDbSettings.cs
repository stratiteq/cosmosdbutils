// Copyright (c) Stratiteq Sweden AB. All rights reserved.
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Stratiteq.CosmosDbUtils
{
    public class CosmosDbSettings
    {
        public CosmosDbSettings(
            string applicationName,
            string databaseId,
            string containerId,
            int throughput,
            int maxRetryAttemptsOnRateLimitedRequests,
            int maxRetryWaitTimeOnRateLimitedRequests,
            bool allowBulkExecution)
        {
            ApplicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
            DatabaseId = databaseId ?? throw new ArgumentNullException(nameof(databaseId));
            ContainerId = containerId ?? throw new ArgumentNullException(nameof(containerId));
            Throughput = throughput;
            MaxRetryAttemptsOnRateLimitedRequests = maxRetryAttemptsOnRateLimitedRequests;
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(maxRetryWaitTimeOnRateLimitedRequests);
            AllowBulkExecution = allowBulkExecution;
        }

        public string DatabaseId { get; }

        public string ContainerId { get; }

        public int Throughput { get; } = 400;

        public bool AllowBulkExecution { get; } = true;

        public string ApplicationName { get; }

        public int MaxRetryAttemptsOnRateLimitedRequests { get; }

        public TimeSpan MaxRetryWaitTimeOnRateLimitedRequests { get; }
    }
}
