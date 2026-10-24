using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Systems.MineSystem.MineTransitionSystem.View
{
    public class MineTransitionCanvasView : MonoBehaviour
    {
        public GameObject campTransitionPanel;
        public Button campToMineButton;
        public Button campToMuseumButton;
        
        public GameObject mineTransitionPanel;
        public Button sleepForTheNightButton;
        public Button mineToCampButton;
        public Button backToMineButton;
        public TMP_Text sleepButtonLabel;
    }
}
