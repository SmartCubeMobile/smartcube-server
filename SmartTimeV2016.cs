// Copyright © 2010-2027 Principal Research Corporation Ltd. All rights reserved.
// No part of this software may be reproduced, stored in a retrieval system, or transmitted
// in any form or by any means, electronic, mechanical, photocopying, recording, or otherwise,
// without the prior written permission of Principal Research Corporation Ltd.

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SmartCubeMobile
{
    public static class SmartTimeV2016
    {
        private static readonly string[] NtpServers =
        {
            "pool.ntp.org",
            "time.google.com",
            "time.windows.com"
        };

        public static async Task<DateTime?> GetNetworkTimeAsync(int timeout = 5000)
        {
            foreach (var server in NtpServers)
            {
                try
                {
                    var result = await GetTimeFromServer(server, timeout);
                    if (result != null)
                        return result;
                }
                catch
                {
                    // try next server
                }
            }

            return null;
        }

        public static async Task<DateTime?> GetTimeFromServer(string ntpServer, int timeout)
        {
            byte[] ntpData = new byte[48];
            ntpData[0] = 0x1B;

            var addresses = await Dns.GetHostEntryAsync(ntpServer);

            foreach (var ip in addresses.AddressList)
            {
                try
                {
                    using var udp = new UdpClient(ip.AddressFamily);

                    udp.Connect(ip, 123);

                    await udp.SendAsync(ntpData, ntpData.Length);

                    var receiveTask = udp.ReceiveAsync();

                    if (await Task.WhenAny(receiveTask, Task.Delay(timeout)) != receiveTask)
                        return null;

                    var data = receiveTask.Result.Buffer;

                    ulong intPart =
                        ((ulong)data[40] << 24) |
                        ((ulong)data[41] << 16) |
                        ((ulong)data[42] << 8) |
                        data[43];

                    ulong fractPart =
                        ((ulong)data[44] << 24) |
                        ((ulong)data[45] << 16) |
                        ((ulong)data[46] << 8) |
                        data[47];

                    ulong milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

                    DateTime networkDateTime =
                        new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                        .AddMilliseconds((long)milliseconds);

                    return networkDateTime;
                }
                catch
                {
                    // try next IP
                }
            }

            return null;
        }

        internal static DateTime ConvertDateTime(string datetime)
        {
            DateTime bb = Convert.ToDateTime(datetime,
                    SmartParametersV2016.defaultCulture);
            return bb;
        }
    }
}