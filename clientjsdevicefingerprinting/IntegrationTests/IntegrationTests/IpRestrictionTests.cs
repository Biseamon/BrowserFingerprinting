using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using ClientJSApp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTests.IntegrationTests
{
    [TestClass]
    public class IpRestrictionTests
    {
        [TestMethod]
        public async Task HttpRequestWithAllowedIpAddressShouldReturn200()
        {
            var factory = new WebApplicationFactory<Startup>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("https_port", "5001");
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IStartupFilter>(new CustomRemoteIpStartupFilter(IPAddress.Parse("127.0.0.1")));
                });
            });
            var client = factory.CreateClient();
            var response = await client.GetAsync("values");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());

            var json = await response.Content.ReadAsStringAsync();
            Assert.AreEqual("[\"value1\",\"value2\"]", json);
        }

        [TestMethod]
        public async Task HttpRequestWithForbiddenIpAddressShouldReturn403()
        {
            var factory = new WebApplicationFactory<Startup>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("https_port", "5001");
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IStartupFilter>(new CustomRemoteIpStartupFilter(IPAddress.Parse("127.168.1.32")));
                });
            });
            var client = factory.CreateClient();
            var response = await client.GetAsync("values");

            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
