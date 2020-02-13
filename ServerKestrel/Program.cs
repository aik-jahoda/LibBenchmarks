// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ServerKestrel
{
    public class Program
    {

        public static void Main(LogLevel? logLevel, bool tls = false, int port = 5010, HttpProtocols protocol = HttpProtocols.Http1)
        {

            Console.WriteLine($"Protocol: {protocol}");

            var builder = new WebHostBuilder()
                .ConfigureLogging(loggerFactory =>
                {
                    if (logLevel.HasValue)
                    {
                        Console.WriteLine($"Console Logging enabled with level '{logLevel.GetValueOrDefault()}'");
                        loggerFactory.AddConsole().SetMinimumLevel(logLevel.GetValueOrDefault());
                    }
                })
                .UseKestrel()
                .ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(port, listenOptions =>
                    {
                        listenOptions.Protocols = protocol;
                        if (tls)
                        {
                            using RSA rsa = RSA.Create();
                            var certReq = new CertificateRequest("CN=contoso.com", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                            certReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                            certReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
                            certReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
                            X509Certificate2 cert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow.AddYears(10));

                            cert = new X509Certificate2(cert.Export(X509ContentType.Pfx));
                            listenOptions.UseHttps(cert);
                        }
                    });
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(MapRoutes);
                })
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                });

            var host = builder.Build();
            host.Start();
            Console.WriteLine("Server started.");
            host.WaitForShutdown();
        }


        private static void MapRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/", async context =>
            {
                await context.Response.WriteAsync("ok");
            });
        }
    }
}