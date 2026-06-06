using System;
using System.Collections.Generic;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.Model;
using Systems.MineSystem.CollectableSystem.Scriptable;
using Systems.MineSystem.CollectableSystem.Service;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Service.MineArtifactService.Test;
using Systems.MineSystem.Mine.Service.MineResourceService.Model;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.CollectableSystem.Controller
{
    public sealed class CollectableController : IInitializable, ITickable, IDisposable
    {
        private readonly CollectableFactory _factory;
        private readonly CollectorRegistry _collectors;
        private readonly CollectableSystemConfig _config;
        private readonly List<CollectableModel> _active = new();
        private readonly CompositeDisposable _disposables = new();

        public CollectableController(
            CollectableFactory factory,
            CollectorRegistry collectors,
            CollectableSystemConfig config)
        {
            _factory = factory;
            _collectors = collectors;
            _config = config;
        }

        public void Initialize()
        {
            _factory.Spawned
                .Subscribe(Activate)
                .AddTo(_disposables);
        }

        public void Tick()
        {
            var now = Time.time;
            var step = _config.pullSpeed * Time.deltaTime;

            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var model = _active[i];
                if (!IsTargetValid(model))
                    model.Target = null;

                if (now >= model.NextCollectorScanTime)
                {
                    model.NextCollectorScanTime =
                        now + _config.collectorScanInterval;
                    model.Target = FindNearestCollector(model);
                }

                if (model.Target == null)
                {
                    if (model.IsBeingPulled)
                    {
                        model.IsBeingPulled = false;
                        model.View.EndPull(_config.droppedItemGravityScale);
                    }

                    continue;
                }

                if (!model.IsBeingPulled)
                {
                    model.IsBeingPulled = true;
                    model.View.BeginPull();
                }

                var targetPosition = model.Target.CollectionPoint.position;
                model.View.SetPullPosition(Vector2.MoveTowards(
                    model.View.Transform.position,
                    targetPosition,
                    step));
            }
        }

        private void Activate(CollectableModel model)
        {
            model.NextCollectorScanTime = Time.time;
            model.TriggerSubscription = model.View.TriggerEntered
                .Subscribe(collider => TryCollect(model, collider));
            _active.Add(model);
        }

        private ICollector FindNearestCollector(CollectableModel model)
        {
            ICollector nearest = null;
            var nearestDistance = float.MaxValue;
            var collectors = _collectors.Collectors;
            var position = model.View.Transform.position;

            for (var i = 0; i < collectors.Count; i++)
            {
                var collector = collectors[i];
                if (!collector.CanCollect(model.Item))
                    continue;

                var offset = collector.CollectionPoint.position - position;
                var distance = offset.sqrMagnitude;
                var radius = collector.PullRadius.Value;
                if (distance > radius * radius || distance >= nearestDistance)
                    continue;

                nearest = collector;
                nearestDistance = distance;
            }

            return nearest;
        }

        private static bool IsTargetValid(CollectableModel model)
        {
            if (model.Target == null ||
                model.Target.CollectionPoint == null ||
                !model.Target.CanCollect(model.Item))
                return false;

            var offset = model.Target.CollectionPoint.position -
                         model.View.Transform.position;
            var radius = model.Target.PullRadius.Value;
            return offset.sqrMagnitude <= radius * radius;
        }

        private void TryCollect(CollectableModel model, Collider2D collider)
        {
            if (!_collectors.TryGetCollector(collider, out var collector) ||
                !collector.CanCollect(model.Item) ||
                !collector.TryCollect(model.Item))
                return;

            LogCollectedItem(model.Item);
            var index = _active.IndexOf(model);
            if (index >= 0)
                RemoveAt(index);
        }

        private static void LogCollectedItem(Item item)
        {
            var details = item switch
            {
                Artifact artifact =>
                    $"definition={artifact.DefinitionId}, material={artifact.Material}, " +
                    $"condition={artifact.Condition}, rarity={artifact.Rarity}",
                Resource resource =>
                    $"stackable={resource.IsStackable}, maxStack={resource.MaxStackAmount}",
                CellPlaceable cellPlaceable =>
                    $"scene={cellPlaceable.ScenePath}, size=" +
                    $"{cellPlaceable.ExtraOccupiedDimensionX}x" +
                    $"{cellPlaceable.ExtraOccupiedDimensionY}",
                WallPlaceable wallPlaceable =>
                    $"scene={wallPlaceable.ScenePath}, size=" +
                    $"{wallPlaceable.ExtraOccupiedDimensionX}x" +
                    $"{wallPlaceable.ExtraOccupiedDimensionY}",
                _ => string.Empty
            };

            Debug.LogWarning(
                $"Collected {item.GetType().Name}: id={item.Id}, name={item.Name}, " +
                $"type={item.Type}, category={item.Category}, variant={item.Variant}, " +
                details);
        }

        private void RemoveAt(int index)
        {
            var model = _active[index];
            var last = _active.Count - 1;
            _active[index] = _active[last];
            _active.RemoveAt(last);
            model.Dispose();
            model.PoolHandler.Despawn(model.View);
        }

        public void Dispose()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
                RemoveAt(i);

            _disposables.Dispose();
        }
    }
}
