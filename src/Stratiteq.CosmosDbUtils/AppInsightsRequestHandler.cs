// Copyright (c) Stratiteq Sweden AB. All rights reserved.
//
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Cosmos;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stratiteq.CosmosDbUtils
{
    /// <summary>
    /// Request handler that improves the telemetry logging to Application Insights.
    /// For each request to the database, it publishes a cosmos Db dependency telemetry operation to App Insights with RequestCharge as an added metric.
    /// Any associated HTTP requests to the database (i.e. when the TelemetryClient is in Gateway mode / Http) will be wraped under this dependency telemetry operation.
    /// </summary>
    public class AppInsightsRequestHandler : RequestHandler
    {
        // Needs to be "Azure DocumentDb" to make App Insights UI render cosmosDb icons.
        private const string DependencyTypeName = "Azure DocumentDb";
        private const string RequestChargeName = "reqcharge";
        private const string TelemetryTargetName = "CosmosDb telemetry";
        private const string CollsUriSegment = "/colls";

        private readonly TelemetryClient telemetryClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppInsightsRequestHandler"/> class.
        /// </summary>
        /// <param name="telemetryClient">The TelemetryClient to use when communicating with Application Insights.</param>
        public AppInsightsRequestHandler(TelemetryClient? telemetryClient)
        {
            this.telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            using var operation = telemetryClient.StartOperation<DependencyTelemetry>(
                new DependencyTelemetry(
                    DependencyTypeName,
                    TelemetryTargetName,
                    dependencyName: $"{request.Method.Method} - {GetCompactCosmosRequestUri(request.RequestUri.ToString())}",
                    data: null));

            var response = await base.SendAsync(request, cancellationToken);

            operation.Telemetry.ResultCode = ((int)response.StatusCode).ToString();
            operation.Telemetry.Metrics.Add(RequestChargeName, response.Headers.RequestCharge);
            operation.Telemetry.Success = response.IsSuccessStatusCode;

            telemetryClient.StopOperation(operation);

            return response;
        }

        // Keeps everything after "/colls" in the request uri.
        private static string GetCompactCosmosRequestUri(string requestUri) =>
            requestUri[(requestUri.Contains(CollsUriSegment) ? requestUri.LastIndexOf(CollsUriSegment) + CollsUriSegment.Length : 0)..];
    }
}
