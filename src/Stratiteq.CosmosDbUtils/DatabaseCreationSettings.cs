// Copyright (c) Stratiteq Sweden AB. All rights reserved.
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Stratiteq.CosmosDbUtils
{
    /// <summary>
    /// Settings deciding the name and throughput of the Cosmos database
    /// </summary>
    public class DatabaseCreationSettings
    {
        public DatabaseCreationSettings(string? id, int? throughput = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Throughput = throughput;
        }

        public string Id { get; }

        public int? Throughput { get; }
    }
}
