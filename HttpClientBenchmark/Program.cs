// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Benchmarks;

namespace HttpClientBenchmark
{

    ///
    public class Program
    {
        private static HttpClient _httpClient;

        private static Uri _serverUri;

        private static int _warmup;

        private static int _duration;

        private static Version _version;

        private const string UNENCRYPTED_HTTP2_ENV_VAR = "DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2UNENCRYPTEDSUPPORT";
        static async Task Main(string serverUri, ProtocolVersion protocol = ProtocolVersion.Http11, int warmup = 15, int duration = 15)
        {
            Console.WriteLine($"Server URI: {serverUri}");
            Console.WriteLine($"Warmup: {warmup}");
            Console.WriteLine($"Duration: {duration}");
            Console.WriteLine($"Protocol: {protocol}");

            _serverUri = new Uri(serverUri);
            _warmup = warmup;
            _duration = duration;
            _version = protocol switch
            {
                ProtocolVersion.Http10 => HttpVersion.Version10,
                ProtocolVersion.Http11 => HttpVersion.Version11,
                ProtocolVersion.Http20 => HttpVersion.Version20,
#if NETCOREAPP5_0
                ProtocolVersion.Http30 => new Version(3, 0),
#endif
                _ => throw new ArgumentException($"Unknown protocol {protocol}")
            };

            if (_serverUri.Scheme == Uri.UriSchemeHttp)
            {
                Environment.SetEnvironmentVariable(UNENCRYPTED_HTTP2_ENV_VAR, "1");
            }

            _httpClient = CreateHttpClient();

            SetupMetadata();
            await Run();
        }

        static async Task Run()
        {
            await TestEndpoint();

            await SendRequests(1000 * _warmup, new SimpleRequestProvider(_serverUri));

            Stopwatch watch = Stopwatch.StartNew();
            var (success, total) = await SendRequests(1000 * _duration, new SimpleRequestProvider(_serverUri));
            watch.Stop();
            MeasureTotalTime(watch.ElapsedMilliseconds);
            MeasureRPS(total > 0 ? total / (watch.ElapsedMilliseconds / (double)1000) : -1);
            MeasureTotalRequests(total);
            MeasureSuccessRequests(success);
        }

        static async Task TestEndpoint()
        {
            HttpClient client = CreateHttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            try
            {
                await client.GetStringAsync(_serverUri);
            }
            catch (TaskCanceledException e)
            {
                if (!e.CancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Connection to {_serverUri} timeouts.");
                }
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Connection to {_serverUri} failed: {e.Message}: {e?.InnerException?.Message ?? ""}");
                throw;
            }
        }

        private static HttpClient CreateHttpClient()
        {
            SocketsHttpHandler handler = new SocketsHttpHandler();

            if (_serverUri.Scheme == Uri.UriSchemeHttps)
            {
                Console.WriteLine($"Disable TLS validation.");
                handler.SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = delegate { return true; }
                };
            }

            return new HttpClient(handler)
            {
                DefaultRequestVersion = _version
            };
        }

        static async Task<(int success, int total)> SendRequests(int duration, IHttpRequestMessageProvider httpRequestMessageProvider)
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(duration);

            int i = 0;
            int success = 0;

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var requestMessage = new HttpRequestMessage()
                    {
                        RequestUri = _serverUri,
                        Method = HttpMethod.Get,
                    };
                    requestMessage.Version = _version;
                    using HttpResponseMessage response = await _httpClient.SendAsync(requestMessage, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        success++;
                    }
                    i++;

                }
            }
            catch (OperationCanceledException)
            {
                if (!cts.IsCancellationRequested)
                    throw;
            }

            return (success, total: i);
        }

        static void SetupMetadata()
        {
            BenchmarksEventSource.Log.Metadata("httpclient/success_requests", "max", "sum", "Requests", "Total number of requests", "n0");
            BenchmarksEventSource.Log.Metadata("httpclient/run_time", "max", "sum", "Time", "Duration of run (ms)", "n0");
            BenchmarksEventSource.Log.Metadata("httpclient/requests_per_seconds", "max", "sum", "RPS", "RPS", "f1");
            BenchmarksEventSource.Log.Metadata("httpclient/total_requests", "max", "sum", "Requests", "Total number of successful requests", "n0");
        }

        static void MeasureTotalTime(long value)
        {
            BenchmarksEventSource.Measure("httpclient/run_time", value);
            Console.WriteLine($"Time (ms) {value:n0}");
        }

        static void MeasureRPS(double value)
        {
            BenchmarksEventSource.Measure("httpclient/requests_per_seconds", value);
            Console.WriteLine($"RPS {value:n0}");
        }

        static void MeasureTotalRequests(int value)
        {
            BenchmarksEventSource.Measure("httpclient/total_requests", value);
            Console.WriteLine($"Total number of successful requests {value:n0}");
        }

        static void MeasureSuccessRequests(int value)
        {
            BenchmarksEventSource.Measure("httpclient/success_requests", value);
            Console.WriteLine($"Total number of requests {value:n0}");
        }
    }


    class SimpleRequestProvider : IHttpRequestMessageProvider
    {
        readonly Uri _serverUri;

        public SimpleRequestProvider(Uri serverUri) => _serverUri = serverUri;

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
