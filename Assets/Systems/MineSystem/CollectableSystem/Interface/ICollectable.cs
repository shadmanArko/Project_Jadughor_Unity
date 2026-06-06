using System;
using Systems.MineSystem.InventorySystem.Model;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Interface
{
    public interface ICollectable
    {
        Item Item { get; }
        Transform Transform { get; }
        IObservable<Collider2D> TriggerEntered { get; }
    }
}
