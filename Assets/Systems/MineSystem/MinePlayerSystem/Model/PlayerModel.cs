using System;
using System.Collections.Generic;
using Systems.MineSystem.MinePlayerSystem.Service;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.MinePlayerSystem.Model
{
    [Serializable]
    public sealed class PlayerModel : IFixedTickable
    {
        private readonly PlayerMovementService _movementService;
        private readonly List<IPlayerFixedTickService> _fixedTickServices;

        public PlayerModel(
            PlayerMovementService movementService,
            List<IPlayerFixedTickService> fixedTickServices)
        {
            _movementService = movementService;
            _fixedTickServices = fixedTickServices;
        }

        public void SetMovementInput(Vector2 direction)
        {
            _movementService.SetInput(direction);
        }

        public void FixedTick()
        {
            for (var i = 0; i < _fixedTickServices.Count; i++)
                _fixedTickServices[i].OnFixedTick();
        }
    }
}
