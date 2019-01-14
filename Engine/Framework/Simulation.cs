﻿using System.Collections.Generic;  
using BEPUphysics;
using FixMath.NET;
using Lockstep.Framework.Networking.Messages;
using Lockstep.Framework.Pathfinding;

namespace Lockstep.Framework
{
    public class Simulation
    {                          
        public const int FRAMERATE = 20;

        public Space Space { get; }

        public int FrameDelay { get; set; }

        public uint FrameCounter { get; private set; }


        private ulong _entityCounter;
        private uint _lastFramePointer;

        private Fix64Random _random;
        private readonly ICommandHandler _commandHandler;

        private readonly Dictionary<uint, Frame> _frames = new Dictionary<uint, Frame>();   

        private readonly List<ILockstepEntity> _pendingEntities = new List<ILockstepEntity>();
        private readonly Dictionary<ulong, ILockstepEntity> _entities = new Dictionary<ulong, ILockstepEntity>();

        public bool CanSimulate
        {
            get
            {
                lock (_frames)
                {            
                    return _lastFramePointer - FrameCounter - FrameDelay > 0;
                }
            }
        }                                          

        public Simulation(ICommandHandler commandHandler)
        {
            _random = new Fix64Random(0);
            _commandHandler = commandHandler;
            GridManager.Initialize();
            Space = new Space();                                   
        }

        public void SetSeed(int seed)
        {
            _random = new Fix64Random(seed);
        }

        public void AddFrame(Frame frame)
        {
            lock (_frames)
            {      
                _frames[_lastFramePointer++] = frame;
            }
        }

        public void EnqueueEntity(ILockstepEntity entity)
        {
            lock (_pendingEntities)
            {
                _pendingEntities.Add(entity);
            }
        }

        public ICollection<ILockstepEntity> GetEntities()
        {
            return _entities.Values;
        }

        public T GetEntity<T>(ulong id) where T : ILockstepEntity
        {
            return (T) _entities[id];
        }

        public ulong CalculateChecksum()
        {
            ulong hash = 3;

            foreach (var entity in _entities.Values)
            {
                hash ^= entity.GetHashCode();
            }

            return hash;
        }


        public Fix64 NextRandom()
        {
            return _random.Next();
        }


        public void Simulate()
        {  
            if (!CanSimulate)
            {
                return;
            }

            Frame currentFrame;
            lock (_frames)
            {
                currentFrame = _frames[FrameCounter++];
            }

            lock (_pendingEntities)
            {                                         
                foreach (var entity in _pendingEntities)
                {
                    entity.ID = _entityCounter;
                    _entities[_entityCounter] = entity;
                    if (entity is ILockstepAgent agent)
                    {
                        Space.Add(agent.Body);
                    }

                    _entityCounter++;
                }
                _pendingEntities.Clear();
            } 

            foreach (var command in currentFrame.Commands)
            {
                _commandHandler.Handle(command);   
            }  

            foreach (var entity in _entities.Values)
            {
                entity.Simulate();                        
            }                

            Space.Update();
        }      
    }
}