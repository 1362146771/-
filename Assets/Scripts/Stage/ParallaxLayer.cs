using UnityEngine;

namespace ThreeKingdoms
{
    [DisallowMultipleComponent]
    public sealed class ParallaxLayer : MonoBehaviour
    {
        [SerializeField, Range(-.25f, 1f)] private float cameraFollow;
        private Camera stageCamera;
        private Vector3 origin;
        private float cameraOriginX;

        public float CameraFollow => cameraFollow;

        public void Configure(float value)
        {
            cameraFollow=value;
            origin=transform.position;
            stageCamera=Camera.main;
            cameraOriginX=stageCamera==null?0f:stageCamera.transform.position.x;
        }

        private void Awake()=>Configure(cameraFollow);

        private void LateUpdate()
        {
            if(stageCamera==null)stageCamera=Camera.main;
            if(stageCamera==null)return;
            transform.position=origin+Vector3.right*((stageCamera.transform.position.x-cameraOriginX)*cameraFollow);
        }
    }
}
