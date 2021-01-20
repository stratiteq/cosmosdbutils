// Copyright (c) Stratiteq Sweden AB. All rights reserved.
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Stratiteq.CosmosDbUtils
{
    public interface IDatabaseSetup
    {
        /// <summary>
        /// Sets up the database and it's containers if they do not yet exist.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task SetupDbAsync();
    }
}
