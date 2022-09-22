using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Xunit;
using Xunit.Sdk;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace ClientJSApp.IntegrationTests
{
    public class VpnDetectionTests
    {
        [Fact]
        public async Task XForwardedForDefaultSettingsChangeRemoteIpAndPort()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(webHostBuilder =>
                {
                    webHostBuilder
                        .UseTestServer()
                        .Configure(app =>
                        {
                            app.UseForwardedHeaders(new ForwardedHeadersOptions
                            {
                                ForwardedHeaders = ForwardedHeaders.XForwardedFor,
                                ForwardLimit = 2,
                            });
                        });
                }).Build();

            await host.StartAsync();

            var server = host.GetTestServer();

            var context = await server.SendAsync(c =>
            {
                c.Request.Headers["X-Forwarded-For"] = "11.111.111.11:9090, 12.111.111.11:9090, 13.111.111.11:9090";
            });

            Assert.Equal("13.111.111.11", context.Connection.RemoteIpAddress.ToString());
            Assert.Equal(9090, context.Connection.RemotePort);
            // No Original set if RemoteIpAddress started null.
            Assert.False(context.Request.Headers.ContainsKey("X-Original-For"));
            // Should have been consumed and removed
            Assert.True(context.Request.Headers.ContainsKey("X-Forwarded-For"));
        }

        [Theory]
        [InlineData(1, "11.111.111.11:12345", "20.0.0.1", "10.0.0.1", 99, false)]
        [InlineData(1, "11.111.111.11:12345", "20.0.0.1", "10.0.0.1", 99, true)]
        [InlineData(1, "", "10.0.0.1", "10.0.0.1", 99, false)]
        [InlineData(1, "", "10.0.0.1", "10.0.0.1", 99, true)]
        [InlineData(1, "11.111.111.11:12345", "10.0.0.1", "11.111.111.11", 12345, false)]
        [InlineData(1, "11.111.111.11:12345", "10.0.0.1", "11.111.111.11", 12345, true)]
        [InlineData(1, "12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1", "11.111.111.11", 12345, false)]
        [InlineData(1, "12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1", "11.111.111.11", 12345, true)]
        [InlineData(1, "12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11", "11.111.111.11", 12345, false)]
        [InlineData(1, "12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11", "11.111.111.11", 12345, true)]
        [InlineData(2, "12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11", "12.112.112.12", 23456, false)]
        [InlineData(2, "12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11", "12.112.112.12", 23456, true)]
        [InlineData(1, "12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11,12.112.112.12", "11.111.111.11", 12345, false)]
        [InlineData(1, "12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11,12.112.112.12", "11.111.111.11", 12345, true)]
        [InlineData(2, "12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11,12.112.112.12", "12.112.112.12", 23456, false)]
        [InlineData(2, "12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11,12.112.112.12", "12.112.112.12", 23456, true)]
        [InlineData(3, "13.113.113.13:34567, 12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11,12.112.112.12", "13.113.113.13", 34567, false)]
        [InlineData(3, "13.113.113.13:34567, 12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11,12.112.112.12", "13.113.113.13", 34567, true)]
        [InlineData(3, "13.113.113.13:34567, 12.112.112.12;23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11,12.112.112.12", "11.111.111.11", 12345, false)] // Invalid 2nd IP
        [InlineData(3, "13.113.113.13:34567, 12.112.112.12;23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11,12.112.112.12", "11.111.111.11", 12345, true)] // Invalid 2nd IP
        [InlineData(3, "13.113.113.13;34567, 12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11,12.112.112.12", "12.112.112.12", 23456, false)] // Invalid 3rd IP
        [InlineData(3, "13.113.113.13;34567, 12.112.112.12:23456, 11.111.111.11:12345", "10.0.0.1,11.111.111.11,12.112.112.12", "12.112.112.12", 23456, true)] // Invalid 3rd IP
        public async Task XForwardedForForwardKnownIps(int limit, string header, string knownIPs, string expectedIp, int expectedPort, bool requireSymmetry)
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(webHostBuilder =>
                {
                    webHostBuilder
                    .UseTestServer()
                    .Configure(app =>
                    {
                        var options = new ForwardedHeadersOptions
                        {
                            ForwardedHeaders = ForwardedHeaders.XForwardedFor,
                            RequireHeaderSymmetry = requireSymmetry,
                            ForwardLimit = limit,
                        };
                        foreach (var ip in knownIPs.Split(',').Select(text => IPAddress.Parse(text)))
                        {
                            options.KnownProxies.Add(ip);
                        }
                        app.UseForwardedHeaders(options);
                    });
                }).Build();

            await host.StartAsync();

            var listOfKnownIps = knownIPs.Split(',').Select(text => IPAddress.Parse(text).ToString());

            var server = host.GetTestServer();

            var context = await server.SendAsync(c =>
            {
                c.Request.Headers["X-Forwarded-For"] = header;
                c.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
                c.Connection.RemotePort = 99;
            });

            Assert.Equal(expectedIp, context.Connection.RemoteIpAddress.ToString());

            if (!listOfKnownIps.Contains(expectedIp))
            {
                await Assert.ThrowsAsync <Xunit.Sdk.ContainsException>(async () => Assert.Contains(context.Connection.RemoteIpAddress.ToString(), listOfKnownIps));
            }

            Assert.Equal(expectedPort, context.Connection.RemotePort);
       
        }


        [Theory]
        [InlineData("22.33.44.55,::ffff:127.0.0.1", "", "", "22.33.44.55")]
        [InlineData("22.33.44.55,::ffff:172.123.142.121", "172.123.142.121", "", "22.33.44.55")]
        [InlineData("22.33.44.55,::ffff:172.123.142.121", "::ffff:172.123.142.121", "", "22.33.44.55")]
        [InlineData("22.33.44.55,::ffff:172.123.142.121,172.32.24.23", "", "172.0.0.0/8", "22.33.44.55")]
        [InlineData("2a00:1450:4009:802::200e,2a02:26f0:2d:183::356e,::ffff:172.123.142.121,172.32.24.23", "", "172.0.0.0/8,2a02:26f0:2d:183::1/64", "2a00:1450:4009:802::200e")]
        [InlineData("22.33.44.55,2a02:26f0:2d:183::356e,::ffff:127.0.0.1", "2a02:26f0:2d:183::356e", "", "22.33.44.55")]
        public async Task XForwardForIPv4ToIPv6Mapping(string forHeader, string knownProxies, string knownNetworks, string expectedRemoteIp)
        {
            var options = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor,
                ForwardLimit = null,
            };

            foreach (var knownProxy in knownProxies.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries))
            {
                var proxy = IPAddress.Parse(knownProxy);
                options.KnownProxies.Add(proxy);
            }
            foreach (var knownNetwork in knownNetworks.Split(new string[] { "," }, options: StringSplitOptions.RemoveEmptyEntries))
            {
                var knownNetworkParts = knownNetwork.Split('/');
                var networkIp = IPAddress.Parse(knownNetworkParts[0]);
                var prefixLength = int.Parse(knownNetworkParts[1], CultureInfo.InvariantCulture);
                options.KnownNetworks.Add(new IPNetwork(networkIp, prefixLength));
            }

            using var host = new HostBuilder()
                .ConfigureWebHost(webHostBuilder =>
                {
                    webHostBuilder
                    .UseTestServer()
                    .Configure(app =>
                    {
                        app.UseForwardedHeaders(options);
                    });
                }).Build();

            await host.StartAsync();

            var server = host.GetTestServer();

            var context = await server.SendAsync(c =>
            {
                c.Request.Headers["X-Forwarded-For"] = forHeader;
            });

            Assert.Equal(expectedRemoteIp, context.Connection.RemoteIpAddress.ToString());
        }
    }
}
