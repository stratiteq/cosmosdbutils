// Copyright (c) Stratiteq Sweden AB. All rights reserved.
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Stratiteq.CosmosDbUtils
{
    public class PagedResult<T>
    {
        public IList<T> Items { get; } = new List<T>();

        public string? ContinuationToken { get; set; }
    }
}
