using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;

namespace HexRts.Logic.HexRts
{
    public enum PlayerColor : int
    {
        Red = 0,
        Blue = 1
    }

    public class Player
    {
        public List<Resource> Resources = new List<Resource>();
        public IClientProxy Connection { get; internal set; }
        public string ConnectionId { get; internal set; }
        public string Name { get; internal set; }
        internal PlayerColor Color { get; set; }
        internal string PlayerId { get; set; }

        public void Init()
        {
            Resources = new List<Resource> { new Resource { Name="Wood", Value=30},
            new Resource{Name="Bricks",Value=20 },
            new Resource{ Name="Food",Value=80},
            new Resource{ Name="Iron",Value=0},
            new Resource{ Name="People",Value=1} };
        }

        internal void SetResource(string name, int value) => Resources.Find(row => row.Name == name).Value = value;

        internal int GetResource(string name) => Resources.Find(row => row.Name == name).Value;
    }
}