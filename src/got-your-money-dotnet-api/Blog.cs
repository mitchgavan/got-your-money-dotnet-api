using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace got_your_money_dotnet_api
{
    public class Blog
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Content { get; set; }
        public DateTime CreatedTimestamp { get; set; }
    }
}
