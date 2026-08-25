using System;
using UnityEngine;

namespace ThreeKingdoms
{
    [Serializable]
    public sealed class CharacterVisualScale
    {
        [SerializeField, Min(.1f)] private float character = 2.38f;
        [SerializeField] private Vector2 shadow = new Vector2(1.95f, 1.22f);

        public float Character => character;
        public Vector2 Shadow => shadow;

        public void Apply(Transform visualRoot, Transform shadowRoot)
        {
            if (visualRoot != null) visualRoot.localScale = Vector3.one * character;
            if (shadowRoot != null) shadowRoot.localScale = new Vector3(shadow.x, shadow.y, 1f);
        }
    }
}
