﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

using NCAT.lib.Objects;

namespace NCAT.lib
{
    public static class TCPConnections
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public const string UNKNOWN = "<UNKNOWN>";

        private const string LOCALHOST = "127.0.0.1";

        private static async Task<NetworkConnectionItem> GetReverseLookupAsync(NetworkConnectionItem item)
        {
            if (item.IPAddress != LOCALHOST)
            {
                try
                {
                    var response =
                        await HttpClient.GetAsync(new Uri($"http://ip-api.com/csv/{item.IPAddress}?fields=lat,lon,country,isp"));

                    if (response.IsSuccessStatusCode)
                    {
                        var csv = await response.Content.ReadAsStringAsync();

                        csv = csv.Replace("\n", "");

                        if (string.IsNullOrEmpty(csv))
                        {
                            item.ISP = UNKNOWN;
                            item.Country = UNKNOWN;

                            return item;
                        }

                        var split = csv.Split(',');

                        item.Latitude = Convert.ToDouble(split[1]);
                        item.Longitude = Convert.ToDouble(split[2]);
                        item.Country = split[0];
                        item.ISP = split[3].Replace(@"""", string.Empty);

                        DB.AddToDB(item);

                        return item;
                    }
                }
                catch (HttpRequestException hre)
                {
                    // Log
                }
            }

            return item;
        }

        public static async Task<List<NetworkConnectionItem>> GetConnectionsAsync()
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();

            var connections = properties.GetActiveTcpConnections();

            var activeConnections = new List<NetworkConnectionItem>();

            foreach (var connection in connections.Where(a => a.RemoteEndPoint.Address.ToString() != LOCALHOST))
            {
                var item = DB.CheckDB(connection.RemoteEndPoint.Address.ToString());

                if (item == null)
                {
                    item = new NetworkConnectionItem
                    {
                        IPAddress = connection.RemoteEndPoint.Address.ToString(),
                        Port = connection.RemoteEndPoint.Port,
                        DetectedTime = DateTime.Now
                    };

                    item = await GetReverseLookupAsync(item);
                }
                else
                {
                    item.DetectedTime = DateTime.Now;
                }

                activeConnections.Add(item);
            }

            return activeConnections;
        }
    }
}