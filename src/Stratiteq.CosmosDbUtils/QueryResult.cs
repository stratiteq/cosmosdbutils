// Copyright (c) Stratiteq Sweden AB. All rights reserved.
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Net;

namespace Stratiteq.CosmosDbUtils
{
    public sealed class QueryResult<T>
    {
        public string? ContinuationToken { get; set; }

        public string? SessionToken { get; set; }

        public double TotalRequestCharge { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public IEnumerable<T>? Items { get; set; }
    }
}
