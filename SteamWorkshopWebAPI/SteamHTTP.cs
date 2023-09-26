using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamWorkshop.WebAPI
{
    public class SteamHTTP
    {
        private readonly char[] private_key;
        public SteamHTTP (char[] key)
        {
            this.private_key = key;
        }
        internal string RequestKey()
        {
            return $"?key={new string(this.private_key)}";
        }
    }
}
