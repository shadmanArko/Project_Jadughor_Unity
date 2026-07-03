using UnityEngine;

namespace Systems.MineSystem.Mine.Service.VisualizerService
{
    public sealed class CaveFormationContactRelay : MonoBehaviour
    {
        private CaveFormationRuntime _runtime;

        public void Configure(CaveFormationRuntime runtime)
        {
            _runtime = runtime;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            _runtime?.HandleTriggerEnter(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider != null)
                _runtime?.HandleTriggerEnter(collision.collider);
        }
    }
}
