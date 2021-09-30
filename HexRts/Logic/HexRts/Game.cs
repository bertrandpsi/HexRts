using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HexRts.Logic.HexRts
{
    public class Game
    {
        public int[,] GameGrid = new int[14, 14];
        public int[,] BuildingGrid = new int[14, 14];
        public int[,] RoadGrid = new int[14, 14];
        private List<Player> players { get; set; } = new List<Player>();
        public List<Building> Buildings { get; set; } = new List<Building>();
        public List<Person> People { get; set; } = new List<Person>();
        public readonly string GameId = System.Guid.NewGuid().ToString();

        internal Random rnd = new Random();

        public bool IsPvP { get; internal set; } = false;
        int nextId = 1;
        const int TurnLength = 200;
        int turn = TurnLength;

        Thread gameThread;
        private bool gameIsFinished = false;

        public void InitGame()
        {
            InitGrid();
            GenGameMap();

            var player = new Player { Connection = null, ConnectionId = "-GLUM-", Name = "Glum", Color = players.Count == 0 ? PlayerColor.Blue : PlayerColor.Red };
            player.Init();
            players.Add(player);
            IPosition pos = Position.CellToScreen(FindPos(IsWalkable));
            People.Add(new Person { ConnectionId = player.ConnectionId, Id = nextId++, X = pos.X, Y = pos.Y, solver = PathSolver, game = this, Player = player.Color });
        }

        public void InitPvpGame()
        {
            InitGrid();
            GenGameMap();
            IsPvP = true;
        }

        AiLogic aiLogic = null;
        private void GameLoop()
        {
            var aiPlayer = players.FirstOrDefault(row => row.Connection == null);
            if (aiPlayer != null)
                aiLogic = new AiLogic(this, aiPlayer);

            Thread.Sleep(100);
            if(IsPvP)
                SendAll("Playing");

            var sendPos = true;
            while (Players.Count > 0 && gameIsFinished == false)
            {
                Thread.Sleep(100);
                lock (players)
                    HandleGame();
                if (sendPos)
                    SendAll("People", People);
                sendPos = !sendPos;
            }
            foreach (var p in Players)
            {
                p.Connection?.SendAsync("End");
                if (Winner == p)
                    p.Connection?.SendAsync("Won");
                else if (Winner == null)
                    p.Connection?.SendAsync("Tie");
                else
                    p.Connection?.SendAsync("Lost");
            }
        }

        internal Player Winner
        {
            get
            {
                lock (players)
                {
                    var monument = Buildings.FirstOrDefault(r2 => GameGrid[r2.X, r2.Y] == 7 && r2.State == BuildingState.Idle);
                    var playerFinishedConstruction = players.FirstOrDefault(row => row.ConnectionId == monument?.ConnectionId);
                    if (playerFinishedConstruction != null)
                        return playerFinishedConstruction;
                    var playerWithoutWorkers = players.Where(row => People.Count(r2 => r2.ConnectionId == row.ConnectionId) == 0);
                    if (playerWithoutWorkers.Count() == 2)
                        return null;
                    else if (playerWithoutWorkers.Count() == 1)
                        return players.FirstOrDefault(row => People.Count(r2 => r2.ConnectionId == row.ConnectionId) != 0);
                    return null;
                }
            }
        }

        internal void RemovePlayer(string connectionId)
        {
            lock (players)
            {
                players.RemoveAll(row => row.ConnectionId == connectionId);
            }
        }

        private void HandleGame()
        {
            turn--;
            if (turn < 0) // Turn is over, we need to consume food
            {
                foreach (var p in players)
                {
                    var people = People.Where(r => r.ConnectionId == p.ConnectionId).ToList();
                    var nbPeople = People.Count();
                    var food = p.GetResource("Food");
                    if (food < nbPeople * 5)
                    {
                        foreach (var person in people)
                        {
                            if (food > 5)
                                food -= 5;
                            else
                            {
                                person.Life -= 10;
                                if (person.Life < 0)
                                {
                                    People.Remove(person);
                                    p.SetResource("People", People.Count(row => row.ConnectionId == p.ConnectionId));
                                    if (People.Count(r => r.ConnectionId == p.ConnectionId) == 0) // Game finished as there is no more workers
                                    {
                                        End();
                                        return;
                                    }
                                }
                            }
                        }
                        food = 0;
                    }
                    else
                        food -= nbPeople * 5;
                    p.SetResource("Food", food);
                    p.Connection?.SendAsync("Resources", p.Resources);
                }
                turn = TurnLength;
            }

            // AI?
            if (aiLogic != null)
                aiLogic.HandleAI();

            var buildingChanged = false;
            // Search all buildings which needs to be construced without any workers and assign iddle workers
            var toConstruct = Buildings.Where(row => row.State == BuildingState.InConstruction && row.NobodyAssigned);
            foreach (var building in toConstruct)
            {
                var person = GetNearestIdle(building.X, building.Y, building.ConnectionId);
                if (person != null)
                {
                    person.AssignedToBuilding = building;
                    person.Task = PersonTask.Constructing;
                }
            }
            // Search all buildings which needs transport
            var toTransport = Buildings.Where(row => row.HasTransport);
            foreach (var building in toTransport)
            {
                var person = GetNearestIdle(building.X, building.Y, building.ConnectionId);
                if (person != null)
                {
                    if (!Position.Same(Position.ScreenToCell(person), building))
                        person.SetDestination(building);
                    person.Task = PersonTask.Transport;
                }
            }
            // Search all buildings which needs workers
            var toWorkFor = Buildings.Where(row => row.State == BuildingState.Idle && row.NobodyAssigned && row.HasTransport == false && row.Resource != null)
                .OrderBy(row => NeedsPriorities(row));
            foreach (var building in toWorkFor)
            {
                var person = GetNearestIdle(building.X, building.Y, building.ConnectionId);
                if (person != null)
                {
                    person.AssignedToBuilding = building;
                    person.Task = PersonTask.Working;
                    building.State = BuildingState.Producing;
                    buildingChanged = true;
                }
            }

            // Handle all the people
            foreach (var person in People.ToList())
                person.Handle(ref buildingChanged);
            if (buildingChanged)
                SendAll("Buildings", Buildings);
        }

        internal void RemovePerson(Person toAttack)
        {
            People.Remove(toAttack);
            if (Winner != null)
                End();
        }

        internal void End()
        {
            gameIsFinished = true;
        }

        internal Position BestForGridType(IPosition start, int gridType)
        {
            var allCells = new List<Position>();
            for (int x = 0; x < GameGrid.GetLength(0); x++)
                for (int y = 0; y < GameGrid.GetLength(1); y++)
                    if (GameGrid[x, y] == gridType && BuildingGrid[x, y] == 0)
                        allCells.Add(new Position { X = x, Y = y });
            return allCells.OrderBy(r => Position.Distance(start, r)).First();
        }

        internal double MinDistanceToGridType(IPosition pos, int gridType)
        {
            var allCells = new List<Position>();
            for (int x = 0; x < GameGrid.GetLength(0); x++)
                for (int y = 0; y < GameGrid.GetLength(1); y++)
                    if (GameGrid[x, y] == gridType && BuildingGrid[x, y] == 0)
                        allCells.Add(new Position { X = x, Y = y });
            return allCells.Min(r => Position.Distance(pos, r));
        }

        internal int NeedsPriorities(Building building)
        {
            var playerResources = players.FirstOrDefault(row => row.ConnectionId == building.ConnectionId)?.Resources;
            if (playerResources == null)
                return 100;
            return playerResources.First(row => row.Name == building.Resource.Name).Value;
        }

        internal void Build(string connectionId, string what, int x, int y)
        {
            lock (players)
            {
                var p = players.First(row => row.ConnectionId == connectionId);
                switch (what)
                {
                    case "house":
                        if (BuildingGrid[x, y] != 0)
                            return;
                        if (p.GetResource("Bricks") >= 20 && Buildings.Count(b => b.ConnectionId == connectionId) == 0)
                        {
                            BuildingGrid[x, y] = 1;
                            Buildings.Add(Building.Create(nextId++, connectionId, what, GameGrid[x, y], x, y, p.Color));
                            p.SetResource("Bricks", p.GetResource("Bricks") - 20);
                        }
                        break;
                    case "city":
                        if (p.GetResource("Bricks") >= 60 && BuildingGrid[x, y] == 1)
                        {
                            BuildingGrid[x, y] = 2;
                            Buildings.Add(Building.Create(nextId++, connectionId, what, GameGrid[x, y], x, y, p.Color));
                            p.SetResource("Bricks", p.GetResource("Bricks") - 60);
                        }
                        break;
                    case "cultivation":
                        if (BuildingGrid[x, y] != 0)
                            return;
                        if (p.GetResource("Wood") >= 10)
                        {
                            GameGrid[x, y] = 5;
                            Buildings.Add(Building.Create(nextId++, connectionId, what, GameGrid[x, y], x, y, p.Color));
                            p.SetResource("Wood", p.GetResource("Wood") - 10);
                        }
                        break;
                    case "lodge":
                        if (BuildingGrid[x, y] != 0 && GameGrid[x, y] != 0 && GameGrid[x, y] != 1)
                            return;
                        if (p.GetResource("Wood") >= 20)
                        {
                            BuildingGrid[x, y] = 3;
                            Buildings.Add(Building.Create(nextId++, connectionId, what, GameGrid[x, y], x, y, p.Color));
                            p.SetResource("Wood", p.GetResource("Wood") - 20);
                        }
                        break;
                    case "road":
                        if (RoadGrid[x, y] != 0)
                            return;
                        if (p.GetResource("Bricks") >= 10)
                        {
                            RoadGrid[x, y] = 1;
                            p.SetResource("Bricks", p.GetResource("Bricks") - 10);
                            foreach (var person in People) // As it changes the cost to walk, we should re-calculate
                                person.ResetPath();
                        }
                        break;
                    case "person":
                        if (BuildingGrid[x, y] != 1 && BuildingGrid[x, y] != 2)
                            return;
                        var b = Buildings.FirstOrDefault(row => row.ConnectionId == connectionId && row.X == x && row.Y == y);
                        if (b == null || b.State != BuildingState.Idle)
                            return;
                        if (p.GetResource("Food") >= 50 && ((BuildingGrid[x, y] == 1 && People.Count(row => row.ConnectionId == connectionId) < 5) || (BuildingGrid[x, y] == 2 && People.Count(row => row.ConnectionId == connectionId) < 15)))
                        {
                            var pos = Position.CellToScreen(new Position { X = x, Y = y });
                            People.Add(new Person { Id = nextId++, ConnectionId = connectionId, Task = PersonTask.Idle, X = pos.X, Y = pos.Y, solver = PathSolver, game = this, Player = p.Color });
                            p.SetResource("Food", p.GetResource("Food") - 50);
                            p.SetResource("People", People.Count(row => row.ConnectionId == connectionId));
                        }
                        break;
                    case "monument":
                        if (BuildingGrid[x, y] != 0 && GameGrid[x, y] == 2)
                            return;
                        if (p.GetResource("Wood") >= 300 && p.GetResource("Iron") >= 200 && p.GetResource("Food") >= 300)
                        {
                            GameGrid[x, y] = 7;
                            Buildings.Add(Building.Create(nextId++, connectionId, what, GameGrid[x, y], x, y, p.Color));
                            p.SetResource("Wood", p.GetResource("Wood") - 100);
                            p.SetResource("Iron", p.GetResource("Iron") - 50);
                            p.SetResource("Food", p.GetResource("Food") - 100);
                        }
                        break;
                }

                SendAll("GameGrid", SerializeGrid(GameGrid));
                SendAll("RoadGrid", SerializeGrid(RoadGrid));
                SendAll("BuildingGrid", SerializeGrid(BuildingGrid));
                SendAll("Buildings", Buildings);

                p.Connection?.SendAsync("Resources", p.Resources);
            }
        }

        internal IPosition NearestHouse(string connectionId, IPosition position) => Buildings.Where(row => row.ConnectionId == connectionId && row.Resource == null).OrderBy(row => Position.Distance(position, row)).FirstOrDefault();

        internal Building BuildingOn(IPosition position) => Buildings.FirstOrDefault(row => Position.Same(position, row));

        [System.Diagnostics.DebuggerDisplay("X = {X}, Y = {Y}, Cost = {PathCost}, Length = {Steps.Count}")]
        class Path : IPosition
        {
            public int X { get; set; }
            public int Y { get; set; }
            public List<Path> Steps { get; set; } = new List<Path>();
            public Game Game { get; set; }

            private Path()
            {
            }

            public Path(Game game, IPosition pos)
            {
                Game = game;
                X = pos.X;
                Y = pos.Y;
                Steps.Add(this);
            }

            public Path Add(int x, int y)
            {
                var result = new Path { Game = Game, X = X + x, Y = Y + y, Steps = Steps.ToList() };
                result.Steps.Add(result);
                return result;
            }

            public int PathCost => Steps.Sum(s => Game.CellWalkSpeed(s));

            public List<Position> ToPositionList()
            {
                return Steps.Select(row => new Position(row)).ToList();
            }

            internal void OptimizePath(Path n)
            {
                for (var i = 0; i < Steps.Count; i++)
                {
                    var s = Steps[i];
                    if (Position.Same(s, n))
                    {
                        // We found a better path, let's replace it
                        if (n.PathCost < s.PathCost || (n.PathCost == s.PathCost && n.Steps.Count < s.Steps.Count))
                        {
                            var l = n.Steps.ToList();
                            l.AddRange(this.Steps.Skip(i + 1));
                            this.Steps = l;
                        }
                        return;
                    }
                }
            }
        }

        private List<Position> PathSolver(IPosition start, IPosition end)
        {
            if (Position.Same(start, end))
                return null;
            var result = new List<IPosition>();
            var todo = new Queue<Path>();
            var visited = new List<Path>();
            todo.Enqueue(new Path(this, start));

            var possibleResults = new List<Path>();

            while (todo.Count > 0)
            {
                var p = todo.Dequeue();
                if (Position.Same(p, end)) // Reached the goal
                {
                    possibleResults.Add(p);
                }
                else
                {
                    List<Path> possible;

                    if (p.Y % 2 == 0)
                        possible = new List<Path> { p.Add(1, 0), p.Add(-1, 0), p.Add(-1, -1), p.Add(0, -1), p.Add(-1, 1), p.Add(0, 1), };
                    else
                        possible = new List<Path> { p.Add(1, 0), p.Add(-1, 0), p.Add(0, -1), p.Add(1, -1), p.Add(0, 1), p.Add(1, 1), };

                    foreach (var n in possible)
                    {
                        if (!IsWalkable(n))
                            continue;
                        // Is visited
                        if (visited.Any(row => Position.Same(row, n)))
                        {
                            // We may need to glue us as alternate path
                            foreach (var pr in possibleResults)
                                pr.OptimizePath(n);
                            foreach (var t in todo)
                                t.OptimizePath(n);
                        }
                        // Is not yet visited
                        else
                        {
                            todo.Enqueue(n);
                            visited.Add(n);
                        }
                    }
                }
            }
            return possibleResults.OrderBy(row => row.PathCost).ThenBy(row => row.Steps.Count).FirstOrDefault()?.ToPositionList();
        }

        internal int CellWalkSpeed(IPosition s)
        {
            if (RoadGrid[s.X, s.Y] != 0)
                return 1;
            else switch (GameGrid[s.X, s.Y])
                {
                    case 1: // Desert
                        return 3;
                    case 2: // Grass
                    case 5: // Cultivation
                    case 7: // Monument
                        return 2;
                    case 3: // Forest
                        return 4;
                    case 4: // Mountain
                        return 6;
                    case 6: // Swamp
                        return 5;
                    default:
                        return 5;
                }
        }

        private Person GetNearestIdle(int cellX, int cellY, string connectionId)
        {
            var pos = Position.CellToScreen(new Position { X = cellX, Y = cellY });
            return People.Where(row => row.Task == PersonTask.Idle && row.ConnectionId == connectionId).OrderBy(row => Position.Distance(row, pos)).FirstOrDefault();
        }

        public string SerializeGrid(int[,] grid)
        {
            var result = new StringBuilder();
            for (var i = 0; i < 14; i++)
            {
                for (var j = 0; j < 14; j++)
                    result.Append((char)(65 + grid[i, j]));
            }
            return result.ToString();
        }

        private void InitGrid()
        {
            for (var i = 0; i < 14; i++)
            {
                for (var j = 0; j < 14; j++)
                {
                    GameGrid[i, j] = 0;
                    BuildingGrid[i, j] = 0;
                    RoadGrid[i, j] = 0;
                }
            }
        }

        private void GenGameMap()
        {
            var fx = rnd.NextDouble() * 20 + 10;
            var fy = rnd.NextDouble() * 20 + 10;
            int nbSwamp = 0;
            for (var i = 0; i < 14; i++)
            {
                for (var j = 0; j < 14; j++)
                {
                    var x = (int)(i + Math.Sin((i * 14 + j) / fx) * 1);
                    var y = (int)(j + Math.Cos((i * 14 + j) / fy) * 1);
                    var a = 6 - x;
                    var b = 7 - y;
                    var d = Math.Sqrt(a * a + b * b);
                    if (d < 2)
                        GameGrid[i, j] = 4; // Mountains
                    else if (d < 3)
                        GameGrid[i, j] = 3; // Trees
                    else if (d < 4)
                    {
                        if (rnd.NextDouble() * 20 < 2) // Swamp
                        {
                            GameGrid[i, j] = 6;
                            nbSwamp++;
                        }
                        else // Grass
                            GameGrid[i, j] = 2;
                    } // Water
                    else if (d < 5.5)
                        GameGrid[i, j] = 1;
                }
            }

            for (var i = 0; i < 14; i++)
            {
                GameGrid[13, i] = 0;
                GameGrid[12, i] = 0;
            }

            while (nbSwamp < 2) // Ensures there is at least 2 swamps
            {
                var pos = FindPos(p => GameGrid[p.X, p.Y] == 2);
                GameGrid[pos.X, pos.Y] = 6;
                nbSwamp++;
            }
        }

        public void SetPersonDestination(string connectionId, int peopleId, int x, int y)
        {
            lock (players)
            {
                var p = People.FirstOrDefault(row => row.Id == peopleId && row.ConnectionId == connectionId);
                if (p == null)
                    return;
                p.SetDestination(new Position { X = x, Y = y });
                p.PlayerAssignedBuilding = null;
                p.Task = PersonTask.PlayerDestination;
            }
        }

        public void AssignPersonConstruction(string connectionId, int peopleId, int x, int y)
        {
            lock (players)
            {
                var p = People.FirstOrDefault(row => row.Id == peopleId && row.ConnectionId == connectionId);
                if (p == null)
                    return;
                var b = Buildings.FirstOrDefault(row => row.X == x && row.Y == y && row.ConnectionId == connectionId && row.State == BuildingState.InConstruction);
                if (b != null)
                {
                    p.PlayerAssignedBuilding = null;
                    p.AssignedToBuilding = b;
                    p.Task = PersonTask.Constructing;
                }
            }
        }

        public void AssignPersonWorker(string connectionId, int peopleId, int x, int y)
        {
            lock (players)
            {
                var p = People.FirstOrDefault(row => row.Id == peopleId && row.ConnectionId == connectionId);
                if (p == null)
                    return;
                var b = Buildings.FirstOrDefault(row => row.X == x && row.Y == y && row.ConnectionId == connectionId && row.State != BuildingState.InConstruction && row.Resource != null);
                if (b != null)
                {
                    p.PlayerAssignedBuilding = b;
                    p.Task = PersonTask.Working;
                }
            }
        }

        public void PersonAuto(string connectionId, int peopleId)
        {
            lock (players)
            {
                var p = People.FirstOrDefault(row => row.Id == peopleId && row.ConnectionId == connectionId);
                if (p == null)
                    return;
                p.PlayerAssignedBuilding = null;
                p.SetDestination(null);
                p.Task = PersonTask.Idle;
            }
        }

        public void AttackPerson(string connectionId, int peopleId, int attackWho)
        {
            lock (players)
            {
                var p = People.FirstOrDefault(row => row.Id == peopleId && row.ConnectionId == connectionId);
                if (p == null)
                    return;
                var toAttack = People.FirstOrDefault(row => row.Id == attackWho && row.ConnectionId != connectionId);
                if (toAttack == null)
                    return;
                p.ToAttack = toAttack;
                p.IsAttacked = false;
            }
        }

        public void PreparePvP(string firstPlayer, string firstName, string secondPlayer, string secondName)
        {
            lock (players)
            {
                var player = new Player { Name = firstName, PlayerId = firstPlayer, Color = players.Count == 0 ? PlayerColor.Blue : PlayerColor.Red };
                player.Init();
                players.Add(player);

                player = new Player { Name = secondName, PlayerId = secondPlayer, Color = players.Count == 0 ? PlayerColor.Blue : PlayerColor.Red };
                player.Init();
                players.Add(player);
            }
        }

        public void Join(IClientProxy connection, string connectionId, string name, string playerId)
        {
            lock (players)
            {
                var player = players.FirstOrDefault(row => row.PlayerId == playerId);
                if (player == null && players.Count == 2)
                    throw new InvalidOperationException();
                if (player == null)
                {
                    player = new Player { Color = players.Count == 0 ? PlayerColor.Blue : PlayerColor.Red };
                    players.Add(player);
                    player.Init();
                }
                if (player.ConnectionId != null)
                    return;

                player.ConnectionId = connectionId;
                player.Connection = connection;
                player.Name = name;

                IPosition pos = Position.CellToScreen(FindPos(IsWalkable));
                People.Add(new Person { ConnectionId = connectionId, Id = nextId++, X = pos.X, Y = pos.Y, solver = PathSolver, game = this, Player = player.Color });

                connection?.SendAsync("PlayerColor", player.Color);
                connection?.SendAsync("GameGrid", SerializeGrid(GameGrid));
                connection?.SendAsync("RoadGrid", SerializeGrid(RoadGrid));
                connection?.SendAsync("BuildingGrid", SerializeGrid(BuildingGrid));
                connection?.SendAsync("Resources", player.Resources);

                if (gameThread == null && players.Count == 2 && !players.Any(r => r.ConnectionId == null)) // Start the game logic
                {
                    gameThread = new Thread(GameLoop);
                    gameThread.IsBackground = true;
                    gameThread.Start();
                }
            }
        }

        public void Join(IClientProxy connection, string connectionId, string name)
        {
            lock (players)
            {
                var player = new Player { Connection = connection, ConnectionId = connectionId, Name = name, Color = players.Count == 0 ? PlayerColor.Blue : PlayerColor.Red };
                player.Init();
                players.Add(player);
                IPosition pos = Position.CellToScreen(FindPos(IsWalkable));
                People.Add(new Person { ConnectionId = connectionId, Id = nextId++, X = pos.X, Y = pos.Y, solver = PathSolver, game = this, Player = player.Color });

                if (gameThread == null)
                {
                    gameThread = new Thread(GameLoop);
                    gameThread.IsBackground = true;
                    gameThread.Start();
                }

                connection?.SendAsync("PlayerColor", player.Color);
                connection?.SendAsync("GameGrid", SerializeGrid(GameGrid));
                connection?.SendAsync("RoadGrid", SerializeGrid(RoadGrid));
                connection?.SendAsync("BuildingGrid", SerializeGrid(BuildingGrid));
                connection?.SendAsync("Resources", player.Resources);
            }
        }

        internal bool IsWalkable(IPosition pos)
        {
            if (pos.X < 0 || pos.Y < 0 || pos.X > 12 || pos.Y > 12)
                return false;
            return GameGrid[pos.X, pos.Y] != 0;
        }

        private IPosition FindPos(Func<IPosition, bool> check)
        {
            var result = new Position();
            while (true)
            {
                result.X = rnd.Next(0, 14);
                result.Y = rnd.Next(0, 14);
                if (check(result))
                    return result;
            }
        }

        public List<Player> Players
        {
            get
            {
                lock (players)
                    return players.ToList();
            }
        }

        public ulong? Channel { get; set; }

        public void SendAllBut(string connectionId, string message)
        {
            foreach (var s in Players.Where(row => row.ConnectionId != connectionId))
            {
                try
                {
                    s.Connection?.SendAsync(message);
                }
                catch
                {
                }
            }
        }

#nullable enable
        public void SendAllBut(string connectionId, string message, object? obj1)
        {
            foreach (var s in Players.Where(row => row.ConnectionId != connectionId))
            {
                try
                {
                    s.Connection?.SendAsync(message, obj1);
                }
                catch
                {
                }
            }
        }

        public void SendAllBut(string connectionId, string message, object? obj1, object? obj2)
        {
            foreach (var s in Players.Where(row => row.ConnectionId != connectionId))
            {
                try
                {
                    s.Connection?.SendAsync(message, obj1, obj2);
                }
                catch
                {
                }
            }
        }

        public void SendAllBut(string connectionId, string message, object? obj1, object? obj2, object? obj3)
        {
            foreach (var s in Players.Where(row => row.ConnectionId != connectionId))
            {
                try
                {
                    s.Connection?.SendAsync(message, obj1, obj2, obj3);
                }
                catch
                {
                }
            }
        }

        public void SendAllBut(string connectionId, string message, object? obj1, object? obj2, object? obj3, object? obj4)
        {
            foreach (var s in Players)
            {
                try
                {
                    s.Connection?.SendAsync(message, obj1, obj2, obj3, obj4);
                }
                catch
                {
                }
            }
        }

        public void SendAll(string message)
        {
            foreach (var s in Players)
            {
                try
                {
                    s.Connection?.SendAsync(message);
                }
                catch
                {
                }
            }
        }

        public void SendAll(string message, object? obj1)
        {
            foreach (var s in Players)
            {
                try
                {
                    s.Connection?.SendAsync(message, obj1);
                }
                catch
                {
                }
            }
        }

        public void SendAll(string message, object? obj1, object? obj2)
        {
            foreach (var s in Players)
            {
                try
                {
                    s.Connection?.SendAsync(message, obj1, obj2);
                }
                catch
                {
                }
            }
        }

        public void SendAll(string message, object? obj1, object? obj2, object? obj3)
        {
            foreach (var s in Players)
            {
                try
                {
                    s.Connection?.SendAsync(message, obj1, obj2, obj3);
                }
                catch
                {
                }
            }
        }

        public void SendAll(string message, object? obj1, object? obj2, object? obj3, object? obj4)
        {
            foreach (var s in Players)
            {
                try
                {
                    s.Connection?.SendAsync(message, obj1, obj2, obj3, obj4);
                }
                catch
                {
                }
            }
        }
#nullable disable
    }
}
