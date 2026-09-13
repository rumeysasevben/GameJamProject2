using UnityEngine;

namespace TenCandles
{
    // A pad a tower can stand on.
    public class TowerSpot : MonoBehaviour
    {
        [SerializeField] SpriteRenderer pad;
        [SerializeField] Color idleColor = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] Color hoverColor = new Color(1f, 0.9f, 0.5f, 0.7f);

        public Tower Tower { get; set; }
        public bool IsEmpty => Tower == null;

        bool highlighted;

        public void SetPad(SpriteRenderer renderer) => pad = renderer;

        public void SetHighlight(bool value)
        {
            highlighted = value;
        }

        void Update()
        {
            if (pad == null) return;
            Color target = highlighted ? hoverColor : idleColor;
            if (!IsEmpty) target.a *= 0.5f;
            pad.color = Color.Lerp(pad.color, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, 0.45f);
        }
    }
}
