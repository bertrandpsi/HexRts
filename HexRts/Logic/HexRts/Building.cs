using System;
using System.Collections.Generic;
using System.Linq;

namespace HexRts.Logic.HexRts
{
    public enum BuildingState : int
    {
        InConstruction = 0,
        Idle = 1,
        Producing = 2,
        InDestruction = 3
    }

    public class Building : IPosition
    {
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int TaskInProgress { get; set; } = 0;
        public int ConstructionTime { get; set; }
        public int ProductionTime { get; set; }
        public BuildingState State { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public PlayerColor Player { get; set; }
        internal string ConnectionId { get; set; }
        public bool ShouldSerializeAssigned() => false;
        public List<Person> Assigned { get; set; } = new List<Person>();
        public bool ShouldSerializeNobodyAssigned() => false;
        public bool NobodyAssigned => !Assigned.Any();
        public bool HasTransport { get; set; } = false;
        public Resource Resource { get; set; } = null;

        static Dictionary<int, Resource> Resources = new Dictionary<int, Resource>
        {
            { 3, new Resource { Name = "Wood", Value = 10 } },
            { 4, new Resource { Name = "Iron", Value = 5 } },
            { 5, new Resource { Name = "Food", Value = 10 } },
            { 6, new Resource { Name = "Bricks", Value = 5 } }
        };
        static Dictionary<string, int> ConstructionTimes = new Dictionary<string, int> {
            { "house", 40 },
            { "city", 40 },
            { "lodge", 40 },
            { "cultivation", 40 },
            { "monument", 600 }};
        static Dictionary<string, int> ProductionTimes = new Dictionary<string, int> {
            { "house", 0 },
            { "city", 0 },
            { "lodge", 40 },
            { "cultivation", 40 },
            { "monument", 0 },};
        internal static Building Create(int id, string connectionId, string what, int gridCell, int x, int y, PlayerColor color)
        {
            var result = new Building
            {
                Id = id,
                ConnectionId = connectionId,
                X = x,
                Y = y,
                State = BuildingState.InConstruction,
                ConstructionTime = ConstructionTimes[what],
                ProductionTime = ProductionTimes[what],
                Player = color,
                TaskInProgress = 0
            };
            if (what != "city" && what != "house" && what != "road" && what != "monument")
                result.Resource = Resources[gridCell];
            return result;
        }
    }
}