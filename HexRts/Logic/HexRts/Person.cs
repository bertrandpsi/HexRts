using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HexRts.Logic.HexRts
{
    public enum PersonTask : int
    {
        Idle = 0,
        Working = 1,
        Constructing = 2,
        Transport = 3,
        PlayerDestination = 4,
        PlayerIdle = 5,
        Attack = 6,
        Defend = 7
    }

    public class Person : IPosition
    {
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        internal int WalkSpeed => 7 - game.CellWalkSpeed(Position.ScreenToCell(this));
        public PlayerColor Player { get; set; }
        public string ConnectionId { get; set; }
        public PersonTask Task { get; set; } = PersonTask.Idle;
        public int MaxLife { get; set; } = 100;
        public int Life { get; set; } = 100;
        internal int ActionTimer { get; set; } = 0;
        internal Func<IPosition, IPosition, List<Position>> solver { get; set; }
        internal Game game { get; set; }
        internal bool IsAttacked { get; set; } = false;

        internal bool IsOnCell
        {
            get
            {
                if (toAttack != null)
                    return Position.Same(Position.ScreenToCell(this), Position.ScreenToCell(toAttack));
                return assignedToBuilding == null ? false : Position.Same(Position.ScreenToCell(this), assignedToBuilding);
            }
        }

        private Building assignedToBuilding = null;

        internal Building AssignedToBuilding
        {
            get
            {
                return assignedToBuilding;
            }
            set
            {
                if (value == null && assignedToBuilding != null)
                    assignedToBuilding.Assigned.Remove(this);
                else if (value != null && assignedToBuilding == null)
                    value.Assigned.Add(this);
                else if (value != null && assignedToBuilding != value && assignedToBuilding != null)
                {
                    assignedToBuilding.Assigned.Remove(this);
                    value.Assigned.Add(this);
                }
                assignedToBuilding = value;
            }
        }

        const int AttackTime = 20;
        Person toAttack = null;
        internal Person ToAttack
        {
            get
            {
                return toAttack;
            }
            set
            {
                toAttack = value;
                playerAssignedBuilding = null;
                AssignedToBuilding = null;
                if (value == null)
                    Task = PersonTask.Idle;
                else
                {
                    Task = PersonTask.Attack;
                    ActionTimer = AttackTime;
                }
            }
        }

        IPosition cellDestination = null;
        internal void SetDestination(IPosition cellDestination)
        {
            if (cellDestination == null)
            {
                this.cellDestination = null;
                stepsToWalk = null;
                return;
            }
            if (this.cellDestination != null && Position.Same(this.cellDestination, cellDestination))
                return;
            this.cellDestination = cellDestination;
            stepsToWalk = null;
        }

        internal bool HasDestination => cellDestination != null;

        public Resource Transporting { get; set; } = null;

        List<Position> stepsToWalk = null;
        internal bool Walk()
        {
            if (stepsToWalk == null)
                stepsToWalk = solver(Position.ScreenToCell(this), cellDestination);
            if (stepsToWalk == null)
                return false;
            if (stepsToWalk.Count == 0)
            {
                stepsToWalk = null;
                return false;
            }
            var s = Position.CellToScreen(stepsToWalk[0]);

            if (s.X < this.X && s.Y > this.Y)
            {
                if (Math.Abs(s.X - this.X) < WalkSpeed)
                    this.X = s.X;
                else
                    this.X -= WalkSpeed;
                if (Math.Abs(s.Y - this.Y) < WalkSpeed)
                    this.Y = s.Y;
                else
                    this.Y += WalkSpeed;
            }
            else if (s.X < this.X && s.Y < this.Y)
            {
                if (Math.Abs(s.X - this.X) < WalkSpeed)
                    this.X = s.X;
                else
                    this.X -= WalkSpeed;
                if (Math.Abs(s.Y - this.Y) < WalkSpeed)
                    this.Y = s.Y;
                else
                    this.Y -= WalkSpeed;
            }
            else if (s.X > this.X && s.Y > this.Y)
            {
                if (Math.Abs(s.X - this.X) < WalkSpeed)
                    this.X = s.X;
                else
                    this.X += WalkSpeed;
                if (Math.Abs(s.Y - this.Y) < WalkSpeed)
                    this.Y = s.Y;
                else
                    this.Y += WalkSpeed;
            }
            else if (s.X > this.X && s.Y < this.Y)
            {
                if (Math.Abs(s.X - this.X) < WalkSpeed)
                    this.X = s.X;
                else
                    this.X += WalkSpeed;
                if (Math.Abs(s.Y - this.Y) < WalkSpeed)
                    this.Y = s.Y;
                else
                    this.Y -= WalkSpeed;
            }
            else if (s.X < this.X)
            {
                if (Math.Abs(s.X - this.X) < WalkSpeed)
                    this.X = s.X;
                else
                    this.X -= WalkSpeed;
            }
            else if (s.X > this.X)
            {
                if (Math.Abs(s.X - this.X) < WalkSpeed)
                    this.X = s.X;
                else
                    this.X += WalkSpeed;
            }
            else if (s.Y < this.Y)
            {
                if (Math.Abs(s.Y - this.Y) < WalkSpeed)
                    this.Y = s.Y;
                else
                    this.Y -= WalkSpeed;
            }
            else if (s.Y > this.Y)
            {
                if (Math.Abs(s.Y - this.Y) < WalkSpeed)
                    this.Y = s.Y;
                else
                    this.Y += WalkSpeed;
            }
            else if (Position.Same(s, this))
            {
                stepsToWalk.RemoveAt(0);
                if (stepsToWalk.Count == 0)
                    this.cellDestination = null;
            }
            return (stepsToWalk.Count != 0);
        }

        private Player MyPlayer => game.Players.FirstOrDefault(row => row.ConnectionId == ConnectionId);

        Building playerAssignedBuilding = null;
        internal Building PlayerAssignedBuilding
        {
            get
            {
                return playerAssignedBuilding;
            }
            set
            {
                toAttack = null;
                playerAssignedBuilding = value;
                AssignedToBuilding = value;
            }
        }
        public bool HasPlayerAssignedBuilding => playerAssignedBuilding != null;

        internal int Damages { get; set; } = 10;

        internal void Handle(ref bool buildingChanged)
        {
            if (ToAttack != null && !IsOnCell && !IsAttacked)
                SetDestination(Position.ScreenToCell(ToAttack));
            if (PlayerAssignedBuilding != null && IsOnCell && PlayerAssignedBuilding.HasTransport == false && PlayerAssignedBuilding.State == BuildingState.Idle) // Set as working if no transport
                PlayerAssignedBuilding.State = BuildingState.Producing;
            // Need to move person on the cell
            if (AssignedToBuilding != null && !IsOnCell)
                SetDestination(AssignedToBuilding);
            if (HasDestination)
                Walk();
            else if (Task == PersonTask.PlayerDestination)
            {
                Task = PersonTask.PlayerIdle;
                ActionTimer = 150;
            }
            else if (Task == PersonTask.PlayerIdle)
            {
                ActionTimer--;
                if (ActionTimer <= 0)
                    Task = PersonTask.Idle;
            }
            else if (Task == PersonTask.Transport && Transporting == null) // Pickup
            {
                var building = game.BuildingOn(Position.ScreenToCell(this));
                if (building != null && building.HasTransport)
                {
                    building.HasTransport = false;
                    Transporting = new Resource(building.Resource);
                    SetDestination(game.NearestHouse(ConnectionId, Position.ScreenToCell(this)));
                }
                else
                {
                    Transporting = null;
                    Task = PersonTask.Idle;
                }
            }
            else if (Task == PersonTask.Transport && Transporting != null) // Drop
            {
                MyPlayer?.SetResource(Transporting.Name, MyPlayer.GetResource(Transporting.Name) + Transporting.Value);
                MyPlayer?.Connection?.SendAsync("Resources", MyPlayer?.Resources);
                Transporting = null;
                Task = PersonTask.Idle;
            }
            else if (IsOnCell)
            {
                if (ToAttack != null)
                {
                    if (ToAttack.ToAttack == null) // Fight back!
                    {
                        ToAttack.ToAttack = this;
                        ToAttack.IsAttacked = true;
                    }
                    ActionTimer--;
                    if (ActionTimer <= 0)
                    {
                        if((IsAttacked && game.rnd.Next(0, 50) < 40) || (!IsAttacked && game.rnd.Next(0, 50) < 35))
                        {
                            ToAttack.Life -= Damages;
                            if (ToAttack.Life <= 0)
                            {
                                game.RemovePerson(ToAttack);
                                IsAttacked = false;
                                ToAttack = null;
                            }
                        }
                        ActionTimer = AttackTime;
                    }
                }
                else if (AssignedToBuilding != null) switch (AssignedToBuilding.State)
                    {
                        case BuildingState.InConstruction:
                            AssignedToBuilding.TaskInProgress++;
                            buildingChanged = true;
                            if (AssignedToBuilding.TaskInProgress >= AssignedToBuilding.ConstructionTime)
                            {
                                AssignedToBuilding.State = BuildingState.Idle;
                                AssignedToBuilding.TaskInProgress = 0;
                                if (game.GameGrid[AssignedToBuilding.X, AssignedToBuilding.Y] == 7) // Game finished!
                                {
                                    game.End();
                                    return;
                                }
                                AssignedToBuilding = PlayerAssignedBuilding;
                                if (AssignedToBuilding == null)
                                    Task = PersonTask.Idle;
                                else
                                {
                                    if (AssignedToBuilding.Resource != null)
                                        Task = PersonTask.Working;
                                    else
                                    {
                                        PlayerAssignedBuilding = null;
                                        Task = PersonTask.Idle;
                                    }
                                }

                            }
                            break;
                        case BuildingState.Producing:
                            AssignedToBuilding.TaskInProgress++;
                            buildingChanged = true;
                            if (AssignedToBuilding.TaskInProgress >= AssignedToBuilding.ProductionTime)
                            {
                                AssignedToBuilding.State = BuildingState.Idle;
                                AssignedToBuilding.HasTransport = true;
                                AssignedToBuilding.TaskInProgress = 0;
                                AssignedToBuilding = PlayerAssignedBuilding;
                                if (AssignedToBuilding == null)
                                    Task = PersonTask.Idle;
                            }
                            break;
                        default:
                            break;
                    }
            }

        }

        internal void ResetPath()
        {
            /*if(!IsOnCell) // Force re-calculate
                stepsToWalk = null;*/
        }
    }
}