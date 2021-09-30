using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HexRts.Logic.HexRts
{
    public class AiLogic
    {
        public AiLogic(Game game,Player aiPlayer)
        {
            this.game = game;
            this.aiPlayer = aiPlayer;
        }

        private readonly Game game;
        private readonly Player aiPlayer;
        private readonly Random rnd = new Random();
        private int waitTimer = 70;

        class Possibility : IPosition
        {
            public int X { get; set; }
            public int Y { get; set; }
            public double DistanceToPlayer { get; set; }
            public double DistanceToWood { get; set; }
            public double DistanceToGrass { get; set; }
            public double DistanceToSwamp { get; set; }
            public double DistanceToMountain { get; set; }
        }


        private void BuildAiTown(Player aiPlayer)
        {
            var possibilities = new List<Possibility>();
            for (int x = 1; x < game.GameGrid.GetLength(0) - 1; x++)
            {
                for (int y = 1; y < game.GameGrid.GetLength(1) - 1; y++)
                {
                    if (game.GameGrid[x, y] != 1)
                        continue;
                    var p = new Position { X = x, Y = y };
                    possibilities.Add(new Possibility
                    {
                        X = x,
                        Y = y,
                        DistanceToMountain = game.MinDistanceToGridType(p, 4),
                        DistanceToPlayer = Position.Distance(p, Position.ScreenToCell(game.People.Where(row => row.ConnectionId == aiPlayer.ConnectionId).OrderBy(row => Position.Distance(row, p)).First())),
                        DistanceToWood = game.MinDistanceToGridType(p, 3),
                        DistanceToSwamp = game.MinDistanceToGridType(p, 6),
                        DistanceToGrass = game.MinDistanceToGridType(p, 2),
                    });
                }
            }

            var b = possibilities.OrderBy(row => row.DistanceToPlayer + row.DistanceToGrass * 4 + row.DistanceToWood * 3 + row.DistanceToSwamp * 2 + row.DistanceToMountain).ToList();
            var best = b.First();
            game.Build(aiPlayer.ConnectionId, "house", best.X, best.Y);
        }

        internal void HandleAI()
        {
            if(waitTimer > 0)
            {
                waitTimer--;
                return;
            }

            var buildings = game.Buildings.Where(row => row.ConnectionId == aiPlayer.ConnectionId);
            var inConstruction = buildings.Count(row => row.State == BuildingState.InConstruction);

            if (buildings.Count() == 0) // Need to build the town first
            {
                BuildAiTown(aiPlayer);
                return;
            }
            else if (buildings.Count(row => row.Resource != null && row.Resource.Name == "Food") == 0 && inConstruction == 0) // No cultivation
            {
                var b = game.Buildings.First(row => row.ConnectionId == aiPlayer.ConnectionId && row.Resource == null);
                var p = game.BestForGridType(b, 2);
                game.Build(aiPlayer.ConnectionId, "cultivation", p.X, p.Y);
            }
            else if (rnd.Next(0, 100) < 30 && inConstruction == 0)  // We skip some turns
            {
                var nbWorkers = game.People.Count(row => row.ConnectionId == aiPlayer.ConnectionId);
                var food = aiPlayer.GetResource("Food");

                if (buildings.Count(row => row.Resource != null && row.Resource.Name == "Wood") == 0 && inConstruction == 0) // No wood lodge
                {
                    var b = game.Buildings.First(row => row.ConnectionId == aiPlayer.ConnectionId && row.Resource == null);
                    var p = game.BestForGridType(b, 3);
                    game.Build(aiPlayer.ConnectionId, "lodge", p.X, p.Y);
                }
                else if (food > 50 + nbWorkers * 10 && rnd.Next(0, 100) < 5) // Could create a new worker
                {
                    var b = game.Buildings.First(row => row.ConnectionId == aiPlayer.ConnectionId && row.Resource == null);
                    game.Build(aiPlayer.ConnectionId, "person", b.X, b.Y);
                }
                else if (buildings.Count(row => row.Resource != null && row.Resource.Name == "Food") < nbWorkers / 1.5) // Build another cultivation
                {
                    var b = game.Buildings.First(row => row.ConnectionId == aiPlayer.ConnectionId && row.Resource == null);
                    var p = game.BestForGridType(b, 2);
                    game.Build(aiPlayer.ConnectionId, "cultivation", p.X, p.Y);
                }
                else if (buildings.Count(row => row.Resource != null && row.Resource.Name == "Bricks") == 0 && rnd.Next(0, 100) < 3) // No bricks production
                {
                    var b = game.Buildings.First(row => row.ConnectionId == aiPlayer.ConnectionId && row.Resource == null);
                    var p = game.BestForGridType(b, 6);
                    game.Build(aiPlayer.ConnectionId, "lodge", p.X, p.Y);
                }
                else if (buildings.Count(row => row.Resource != null && row.Resource.Name == "Iron") == 0 && rnd.Next(0, 100) < 3) // No iron production
                {
                    var b = game.Buildings.First(row => row.ConnectionId == aiPlayer.ConnectionId && row.Resource == null);
                    var p = game.BestForGridType(b, 4);
                    game.Build(aiPlayer.ConnectionId, "lodge", p.X, p.Y);
                }
                else if (buildings.Count(row => row.Resource == null && game.GameGrid[row.X, row.Y] == 7) == 0 && rnd.Next(0, 100) < 3) // Try to build the monument
                {
                    var b = game.Buildings.First(row => row.ConnectionId == aiPlayer.ConnectionId && row.Resource == null);
                    var p = game.BestForGridType(b, 2);
                    game.Build(aiPlayer.ConnectionId, "monument", p.X, p.Y);
                }
            }
        }
    }
}
