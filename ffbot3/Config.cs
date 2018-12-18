using System;
using System.Collections.Generic;
using System.Text;

namespace FFBot2
{
    class Config
    {
        public ServerConfig[] Servers { get; set; }
    }

    class ServerConfig
    {
        public string Name { get; set; }
        public ulong[] ChannelIds { get; set; }
        public bool AllowQuotes { get; set; }
    }
}
