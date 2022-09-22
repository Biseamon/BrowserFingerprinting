using ClientJSApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MaxMind.GeoIP2;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace ClientJSApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHostingEnvironment _hostingEnvironment;
        private string IPAddress;

        public HomeController(ILogger<HomeController> logger, IHttpContextAccessor httpContextAccessor, IHostingEnvironment hosting)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _hostingEnvironment = hosting;
        }

        public IActionResult Index()
        {

            using (var reader = new DatabaseReader(_hostingEnvironment.ContentRootPath + "\\GeoLite2-City.mmdb"))
            {
                // Determine the IP Address of the request
                var ipAddress = GetIPAddress(_httpContextAccessor.HttpContext);
               // var ipv4address = HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString();
                string dummyIp = "95.128.43.164";

                bool tor = false;

                var ip = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.MapToIPv4().ToString();
                var local_ip = System.Net.Dns.GetHostEntry(ipAddress).AddressList
                    .First(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();
                //var local_ip = _httpContextAccessor.HttpContext?.Connection?.LocalIpAddress?.MapToIPv4().ToString();
                var client_certificate = _httpContextAccessor.HttpContext?.Connection?.ClientCertificate?.ToString();
                var local_port = _httpContextAccessor.HttpContext?.Connection?.LocalPort.ToString();
                var remote_port = _httpContextAccessor.HttpContext?.Connection?.RemotePort.ToString();
                var listOfIps = GetIp()[0];

                //test
                var testRemoteIPBehindProxy = _httpContextAccessor.HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

                // Get the city from the IP Address
                var city = reader.City(System.Net.IPAddress.Parse(dummyIp));

                var checkIp = System.IO.File.ReadAllText("tor-exit-nodes.txt");

                if (checkIp.Contains(dummyIp))
                {
                    tor = true;
                    //throw new Exception("This is a Tor Exit node. Access denied!");
                }

                var model = new ConnectionModel
                {
                    RemoteIP = ip,
                    DummyIp = dummyIp,
                    RemotePort = remote_port,
                    LocalIP = local_ip,
                    LocalPort = local_port,
                    ClientCert = client_certificate,
                    ListOfIPs = listOfIps,
                    ISP = Dns.GetHostEntry(Dns.GetHostName()).ToString(),
                    CityModel = city,
                    isTor = tor,
                    RemoteIpBehindProxy = testRemoteIPBehindProxy
                };

                return View(model);
            }

        }

        public string[] GetIp()
        {
            string ip = Response.HttpContext.Connection.RemoteIpAddress.ToString(); //1st option to get IP
            string ip0 = HttpContext.Features.Get<IHttpConnectionFeature>()?.RemoteIpAddress.ToString(); // 2nd option to get IP

            //https://en.wikipedia.org/wiki/Localhost
            //127.0.0.1    localhost
            //::1          localhost
            if (ip == "::1")
            {
                ip = Dns.GetHostEntry(Dns.GetHostName()).AddressList[1].MapToIPv4() + " and " + GetIPAddress(_httpContextAccessor.HttpContext) + " and " + _httpContextAccessor.HttpContext.Request.Host + " and " + _httpContextAccessor.HttpContext.Request.Scheme;
                /*ip0 = Dns.GetHostEntry(Dns.GetHostName()).AddressList[2].MapToIPv4() + " and " + GetIPAddress(_httpContextAccessor.HttpContext) + " and " + _httpContextAccessor.HttpContext.Request.Host + " and " + _httpContextAccessor.HttpContext.Request.Scheme;*/
            }

            return new string[] { ip, ip0 };
        }

        public  IPAddress GetIPAddress(HttpContext context, bool allowForwarded = true)
        {
            if (allowForwarded)
            {
                string header = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (System.Net.IPAddress.TryParse(header, out IPAddress ip)) 
                {
                        return ip;
                }
            }
            return context.Connection.RemoteIpAddress;
        }


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
