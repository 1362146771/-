using System;
using UnityEngine;

namespace ThreeKingdoms
{
    [Serializable]
    public struct BlockedZone
    {
        public string id;
        public Rect bounds;
        public BlockedZone(string id,Rect bounds){this.id=id;this.bounds=bounds;}
    }

    [DisallowMultipleComponent]
    public sealed class StageNavigationMap:MonoBehaviour
    {
        [SerializeField] private Vector2[] walkablePolygon;
        [SerializeField] private BlockedZone[] blockedZones;
        public Vector2[] WalkablePolygon=>walkablePolygon;
        public BlockedZone[] BlockedZones=>blockedZones;
        public float ApproximateBeltDepth=>2.18f;

        public void ConfigureIteration03()
        {
            walkablePolygon=new[]{
                new Vector2(-1f,-2.42f),new Vector2(6f,-2.55f),new Vector2(12f,-2.34f),new Vector2(18f,-2.52f),
                new Vector2(24f,-2.28f),new Vector2(30f,-2.52f),new Vector2(40f,-2.36f),new Vector2(40f,-.38f),
                new Vector2(34f,-.28f),new Vector2(29f,-.42f),new Vector2(22f,-.30f),new Vector2(16f,-.40f),
                new Vector2(10f,-.26f),new Vector2(5f,-.42f),new Vector2(-1f,-.34f)};
            blockedZones=new[]{
                new BlockedZone("RearGatePillars",new Rect(2.0f,-.55f,2.1f,.32f)),
                new BlockedZone("RearWeaponRack",new Rect(12.8f,-.54f,2.0f,.30f)),
                new BlockedZone("RearWatchStair",new Rect(20.0f,-.53f,2.2f,.30f)),
                new BlockedZone("CommandDais",new Rect(27.0f,-.55f,3.0f,.32f)),
                new BlockedZone("ExitGateTrim",new Rect(36.0f,-.52f,2.1f,.28f))};
        }

        public Vector2 Constrain(Vector2 current,Vector2 desired)
        {
            Vector2 result=ContainsPolygon(desired)?desired:InsetFromBoundary(ClosestPointOnPolygon(desired),current);
            for(int i=0;i<(blockedZones==null?0:blockedZones.Length);i++)
            {
                Rect zone=blockedZones[i].bounds;if(!zone.Contains(result))continue;
                if(ContainsPolygon(current)&&!zone.Contains(current))result=current;
                else result=ClosestPointOnRect(result,zone);
            }
            return result;
        }

        private Vector2 InsetFromBoundary(Vector2 boundaryPoint,Vector2 current)
        {
            Vector2 target=ContainsPolygon(current)?current:PolygonCenter();
            Vector2 direction=target-boundaryPoint;
            if(direction.sqrMagnitude<.0001f)direction=Vector2.down;
            return boundaryPoint+direction.normalized*.01f;
        }

        private Vector2 PolygonCenter()
        {
            if(walkablePolygon==null||walkablePolygon.Length==0)return Vector2.zero;
            Vector2 sum=Vector2.zero;
            for(int i=0;i<walkablePolygon.Length;i++)sum+=walkablePolygon[i];
            return sum/walkablePolygon.Length;
        }

        public bool IsWalkable(Vector2 point)
        {
            if(!ContainsPolygon(point))return false;
            for(int i=0;i<(blockedZones==null?0:blockedZones.Length);i++)if(blockedZones[i].bounds.Contains(point))return false;
            return true;
        }

        private bool ContainsPolygon(Vector2 point)
        {
            if(walkablePolygon==null||walkablePolygon.Length<3)return true;bool inside=false;
            for(int i=0,j=walkablePolygon.Length-1;i<walkablePolygon.Length;j=i++)
            {
                Vector2 a=walkablePolygon[i],b=walkablePolygon[j];
                if(((a.y>point.y)!=(b.y>point.y))&&point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }
            return inside;
        }

        private Vector2 ClosestPointOnPolygon(Vector2 point)
        {
            Vector2 best=point;float bestDistance=float.MaxValue;
            for(int i=0;i<walkablePolygon.Length;i++)
            {
                Vector2 a=walkablePolygon[i],b=walkablePolygon[(i+1)%walkablePolygon.Length],candidate=ClosestPointOnSegment(point,a,b);
                float distance=(candidate-point).sqrMagnitude;if(distance<bestDistance){bestDistance=distance;best=candidate;}
            }
            return best;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 point,Vector2 a,Vector2 b){Vector2 ab=b-a;float t=ab.sqrMagnitude<.0001f?0f:Mathf.Clamp01(Vector2.Dot(point-a,ab)/ab.sqrMagnitude);return a+ab*t;}
        private static Vector2 ClosestPointOnRect(Vector2 point,Rect rect)
        {
            float left=Mathf.Abs(point.x-rect.xMin),right=Mathf.Abs(rect.xMax-point.x),bottom=Mathf.Abs(point.y-rect.yMin),top=Mathf.Abs(rect.yMax-point.y),min=Mathf.Min(left,right,bottom,top);
            if(min==left)return new Vector2(rect.xMin-.001f,point.y);if(min==right)return new Vector2(rect.xMax+.001f,point.y);
            if(min==bottom)return new Vector2(point.x,rect.yMin-.001f);return new Vector2(point.x,rect.yMax+.001f);
        }
    }
}
