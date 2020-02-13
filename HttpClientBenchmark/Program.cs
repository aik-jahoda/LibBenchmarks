// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Benchmarks;

namespace HttpClientBenchmark
{
    public class Program
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        private static string _serverUri;

        private static int _warmup;

        private static int _duration;

        static async Task Main(string serverUri, int warmup = 15, int duration = 15)
        {
            Console.WriteLine($"Server URI: {serverUri}");
            Console.WriteLine($"Warmup: {warmup}");
            Console.WriteLine($"Duration: {duration}");

            _serverUri = serverUri;
            _warmup = warmup;
            _duration = duration;
            BenchmarksEventSource.Log.Metadata("httpclient/requests", "max", "sum", "Requests", "Total number of requests", "n0");
            await Run();
        }


        static async Task Run()
        {
            await SendRequests(1000 * _warmup, new SimpleRequestProvider(_serverUri));
            await SendRequests(1000 * _duration, new SimpleRequestProvider(_serverUri));
        }

        static async Task SendRequests(int duration, IHttpRequestMessageProvider httpRequestMessageProvider)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(duration);

            for (int i = 0; !cts.IsCancellationRequested; i++)
            {
                var requestMessage = httpRequestMessageProvider.CreateHttpRequestMessage(i);
                await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            }

            BenchmarksEventSource.Measure("httpclient/requests", 1);
        }
    }


    class SimpleRequestProvider : IHttpRequestMessageProvider
    {
        readonly Uri _serverUri;

        public SimpleRequestProvider(string serverUri)
        {
            _serverUri = new Uri(serverUri);
        }

        public HttpRequestMessage CreateHttpRequestMessage(int number)
        {
            return new HttpRequestMessage()
            {
                RequestUri = _serverUri,
                Method = HttpMethod.Get,
            };
        }
    }

    interface IHttpRequestMessageProvider
    {
        HttpRequestMessage CreateHttpRequestMessage(int number);
    }

}
