using UnityEngine;

namespace ThreeKingdoms
{
    public sealed partial class Hurtbox
    {
        [Header("Battle Editor V0.1 Hurtbox")]
        [SerializeField] private Vector2 localCenter = new Vector2(0f, 1.1f);
        [SerializeField] private Vector2 localSize = new Vector2(.8f, 2.2f);
        public Vector2 LocalCenter => localCenter;
        public Vector2 LocalSize => localSize;
        public void ConfigureShape(Vector2 center, Vector2 size){localCenter=center;localSize=new Vector2(Mathf.Max(.01f,size.x),Mathf.Max(.01f,size.y));}
    }
}
