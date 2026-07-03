using System;
using System.Collections.Generic;
using Systems.MineSystem.CollectableSystem.Service;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.InventorySystem.Scriptable;
using Systems.MineSystem.InventorySystem.View;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

namespace Systems.MineSystem.InventorySystem.Service
{
    public sealed class ItemCollectionVisualizerService :
        IInitializable,
        IDisposable
    {
        private sealed class Entry
        {
            public string Key;
            public Item Representative;
            public ItemCollectableView View;
            public int Count;
            public IDisposable Lifetime;
            public IDisposable Fade;
        }

        private readonly InventoryModel _inventory;
        private readonly InventorySystemConfig _config;
        private readonly CollectableSpriteResolver _spriteResolver;
        private readonly MinePlayerScriptable _player;
        private readonly ItemCollectionVisualizerCanvasView _canvasView;
        private readonly CompositeDisposable _disposables = new();
        private readonly List<Entry> _activeEntries = new();
        private readonly Dictionary<string, Entry> _entryByKey = new();
        private readonly Queue<ItemCollectableView> _availableViews = new();
        private readonly Queue<Item> _pendingItems = new();

        public ItemCollectionVisualizerService(
            InventoryModel inventory,
            InventorySystemConfig config,
            CollectableSpriteResolver spriteResolver,
            MinePlayerScriptable player,
            ItemCollectionVisualizerCanvasView canvasView)
        {
            _inventory = inventory;
            _config = config;
            _spriteResolver = spriteResolver;
            _player = player;
            _canvasView = canvasView;
        }

        public void Initialize()
        {
            _canvasView.SetVisible(false);
            CreatePool();
            _inventory.ItemCollected
                .Subscribe(PresentCollectedItem)
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _disposables.Dispose();
            foreach (var entry in _activeEntries)
            {
                entry.Lifetime?.Dispose();
                entry.Fade?.Dispose();
            }
        }

        private void CreatePool()
        {
            var poolSize = Mathf.Max(
                GetVisibleCapacity(),
                _config.itemCollectionPooledCardCount);

            for (var i = 0; i < poolSize; i++)
            {
                var view = Object.Instantiate(
                    _config.itemCollectableViewPrefab,
                    _canvasView.CardParent);
                view.ResetView();
                _availableViews.Enqueue(view);
            }
        }

        private void PresentCollectedItem(Item item)
        {
            if (item == null)
                return;

            var key = BuildKey(item);
            if (_entryByKey.TryGetValue(key, out var existing))
            {
                existing.Count++;
                existing.Fade?.Dispose();
                existing.Fade = null;
                Present(existing);
                ScheduleRemoval(
                    existing,
                    _config.itemCollectionDisplayDuration);
                return;
            }

            if (!CanShowNewEntry())
            {
                _pendingItems.Enqueue(item);
                ScheduleOverflowRemoval();
                return;
            }

            ShowNewEntry(item, key);
        }

        private void ShowNewEntry(Item item, string key)
        {
            var view = AcquireView();
            if (view == null)
                return;

            var entry = new Entry
            {
                Key = key,
                Representative = item,
                View = view,
                Count = 1
            };
            _activeEntries.Add(entry);
            _entryByKey[key] = entry;
            _canvasView.AttachCard(view);
            Present(entry);
            ScheduleRemoval(
                entry,
                _config.itemCollectionDisplayDuration);
            RefreshStackOrder();
        }

        private bool CanShowNewEntry()
        {
            return _activeEntries.Count < GetVisibleCapacity();
        }

        private void ScheduleOverflowRemoval()
        {
            if (_activeEntries.Count == 0)
                return;

            ScheduleRemoval(
                _activeEntries[0],
                _config.itemCollectionOverflowLowestDuration);
        }

        private ItemCollectableView AcquireView()
        {
            if (_availableViews.Count == 0)
                return null;

            return _availableViews.Dequeue();
        }

        private void Present(Entry entry)
        {
            var sprite = _spriteResolver.Resolve(
                entry.Representative,
                _player.region,
                _player.site);
            entry.View.Present(
                sprite,
                entry.Representative.Name,
                entry.Count);
            _canvasView.SetVisible(true);
        }

        private void ScheduleRemoval(
            Entry entry,
            float visibleDuration)
        {
            if (entry == null)
                return;

            entry.Lifetime?.Dispose();
            entry.Lifetime = Observable
                .Timer(TimeSpan.FromSeconds(Mathf.Max(0f, visibleDuration)))
                .Subscribe(_ => BeginFade(entry))
                .AddTo(_disposables);
        }

        private void BeginFade(Entry entry)
        {
            if (entry == null ||
                !_activeEntries.Contains(entry))
                return;

            entry.Lifetime?.Dispose();
            entry.Lifetime = null;
            entry.Fade?.Dispose();

            var fadeDuration = Mathf.Max(
                0f,
                _config.itemCollectionFadeOutDuration);
            if (fadeDuration <= 0f)
            {
                RemoveEntry(entry);
                return;
            }

            var startTime = Time.time;
            entry.Fade = Observable
                .EveryUpdate()
                .Subscribe(_ =>
                {
                    var elapsed = Time.time - startTime;
                    var progress = Mathf.Clamp01(elapsed / fadeDuration);
                    entry.View.SetAlpha(1f - progress);
                    if (progress < 1f)
                        return;

                    RemoveEntry(entry);
                })
                .AddTo(_disposables);
        }

        private void RemoveEntry(Entry entry)
        {
            if (entry == null ||
                !_activeEntries.Remove(entry))
                return;

            entry.Lifetime?.Dispose();
            entry.Fade?.Dispose();
            _entryByKey.Remove(entry.Key);
            entry.View.ResetView();
            _availableViews.Enqueue(entry.View);
            RefreshStackOrder();
            _canvasView.SetVisible(_activeEntries.Count > 0);
            PresentPendingItems();
        }

        private void PresentPendingItems()
        {
            while (_pendingItems.Count > 0 && CanShowNewEntry())
            {
                var item = _pendingItems.Dequeue();
                var key = BuildKey(item);
                if (_entryByKey.TryGetValue(key, out var existing))
                {
                    existing.Count++;
                    existing.Fade?.Dispose();
                    existing.Fade = null;
                    Present(existing);
                    ScheduleRemoval(
                        existing,
                        _config.itemCollectionDisplayDuration);
                    continue;
                }

                ShowNewEntry(item, key);
            }

            if (_pendingItems.Count > 0)
                ScheduleOverflowRemoval();
        }

        private void RefreshStackOrder()
        {
            var views = new List<ItemCollectableView>(_activeEntries.Count);
            foreach (var entry in _activeEntries)
                views.Add(entry.View);
            _canvasView.ReorderBottomToTop(views);
        }

        private int GetVisibleCapacity()
        {
            return Mathf.Max(1, _config.itemCollectionVisibleCardCount);
        }

        private static string BuildKey(Item item)
        {
            return string.Join(
                "|",
                item.GetType().FullName,
                item.Type ?? string.Empty,
                item.Category ?? string.Empty,
                item.Variant ?? string.Empty);
        }
    }
}
