using System;
using System.Collections.Generic;
using System.Net;
using ECommon.Utilities;

namespace ECommon.Extensions
{
    public static class EndPointExtensions
    {
        public static string ToAddress(this EndPoint endpoint)
        {
            Ensure.NotNull(endpoint, "endpoint");
            return ((IPEndPoint)endpoint).ToAddress();
        }
        public static string ToAddress(this IPEndPoint endpoint)
        {
            Ensure.NotNull(endpoint, "endpoint");
            return string.Format("{0}:{1}", endpoint.Address, endpoint.Port);
        }
        public static IPEndPoint ToEndPoint(this string address)
        {
            Ensure.NotNull(address, "address");
            var array = address.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
            if (array.Length != 2)
            {
                throw new Exception("Invalid endpoint address: " + address);
            }
            var ip = IPAddress.Parse(array[0]);
            var port = int.Parse(array[1]);
            return new IPEndPoint(ip, port);
        }
        public static IEnumerable<IPEndPoint> ToEndPoints(this string addresses)
        {
            Ensure.NotNull(addresses, "addresses");
            var array = addresses.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<IPEndPoint>();
            foreach (var item in array)
            {
                list.Add(item.ToEndPoint());
            }
            return list;
        }
    }
}
