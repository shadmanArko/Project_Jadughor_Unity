using TMPro;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.CoordinateService
{
    public sealed class MineCoordinateCanvasView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI coordinateText;

        public void Present(int x, int depth)
        {
            if (coordinateText != null)
                coordinateText.text = $"(X,Y) = ({x},{depth})";
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void Awake()
        {
            if (coordinateText == null)
                coordinateText =
                    GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}
