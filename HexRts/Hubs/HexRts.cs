using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HexRts.Logic.HexRts;

namespace HexRts.Hubs
{
    public class HexRts : Hub
    {
        public static readonly List<Game> games = new List<Game>();
        static readonly Dictionary<string, Game> gameConnections = new Dictionary<string, Game>();

        public override Task OnDisconnectedAsync(Exception exception)
        {
            lock (games)
            {
                var game = GetGame();
                if (game == null)
                    return base.OnDisconnectedAsync(exception);
                if (game.IsPvP && game.Players[0].ConnectionId == Context.ConnectionId)
                {
                    if (game.Players.Count > 1)
                        game.Players[1].Connection?.SendAsync("End");
                    game.Players[0].Connection = null;
                }
                else if (game.IsPvP)
                {
                    game.Players[0].Connection?.SendAsync("End");
                    if (game.Players.Count > 1)
                        game.Players[1].Connection = null;
                }
                game.RemovePlayer(Context.ConnectionId);
                if (game.Players.Count == 0)
                    games.Remove(game);
                gameConnections.Remove(Context.ConnectionId);
            }

            return base.OnDisconnectedAsync(exception);
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        private Game GetGame()
        {
            lock (games)
            {
                if (gameConnections.ContainsKey(Context.ConnectionId))
                    return gameConnections[Context.ConnectionId];
                return null;
            }
        }

        public void Init()
        {
            var game = new Game();
            lock (games)
            {
                games.Add(game);
                gameConnections.Add(Context.ConnectionId, game);
            }
            game.InitGame();
            game.IsPvP = false;
            game.Join(Clients.Caller, Context.ConnectionId, Context.User.Identity.IsAuthenticated ? Context.User.Identity.Name : "Guest");
        }

        public void SetPersonDestination(int id, int x, int y)
        {
            var game = GetGame();
            game.SetPersonDestination(Context.ConnectionId, id, x, y);
        }

        public void AssignPersonConstruction(int id, int x, int y)
        {
            var game = GetGame();
            game.AssignPersonConstruction(Context.ConnectionId, id, x, y);
        }

        public void AssignPersonWorker(int id, int x, int y)
        {
            var game = GetGame();
            game.AssignPersonWorker(Context.ConnectionId, id, x, y);
        }

        public void PersonAuto(int id)
        {
            var game = GetGame();
            game.PersonAuto(Context.ConnectionId, id);
        }

        public void AttackPerson(int id, int attackWho)
        {
            var game = GetGame();
            game.AttackPerson(Context.ConnectionId, id, attackWho);
        }

        public void Build(string what, int x, int y)
        {
            var game = GetGame();
            game.Build(Context.ConnectionId, what, x, y);
        }
    }
}
