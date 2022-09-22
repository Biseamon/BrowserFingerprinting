using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaxMind.GeoIP2.Model;
using MaxMind.GeoIP2.Responses;

namespace ClientJSApp.Models
{
    public class ConnectionModel
    {
        public string RemoteIP { get; set; }
        public string DummyIp { get; set; }
        public string LocalIP { get; set; }
        public string RemotePort { get; set; }
        public string LocalPort { get; set; }
        public string ClientCert { get; set; }
        public string ISP { get; set; }
        public string ListOfIPs { get; set; }
        public string RemoteIpBehindProxy { get; set; }
        public bool isTor { get; set; }
        public CityResponse CityModel { get; set; }
    }
}