using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HexRts.Logic.HexRts
{
    public class Resource
    {
        public Resource()
        {
        }

        public Resource(Resource resource)
        {
            Name = resource.Name;
            Value = resource.Value;
        }

        public string Name { get; set; }
        public int Value { get; set; }
    }
}
