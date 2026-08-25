using UnityEngine;

namespace ThreeKingdoms
{
    [DisallowMultipleComponent]
    public sealed class FinalStageBackground:MonoBehaviour
    {
        [SerializeField] private Sprite[] sections;
        [SerializeField] private float firstCenterX=4.2f,sectionWorldWidth=18.12f,worldHeight=10.2f;
        public int SectionCount=>sections==null?0:sections.Length;
        public void Rebuild()
        {
            Transform root=transform.Find("FinalStageFlatBackground");
            if(root==null){var go=new GameObject("FinalStageFlatBackground");root=go.transform;root.SetParent(transform,false);}
            int required=0;
            if(sections!=null)for(int i=0;i<sections.Length;i++)if(sections[i]!=null)required++;
            // The setup tool serializes the panels into the scene. Keep those exact
            // renderers at runtime instead of creating a duplicate set for one frame.
            if(Application.isPlaying&&root.childCount==required)return;
            for(int i=root.childCount-1;i>=0;i--){if(Application.isPlaying)Destroy(root.GetChild(i).gameObject);else DestroyImmediate(root.GetChild(i).gameObject);}
            if(sections==null)return;
            for(int i=0;i<sections.Length;i++)
            {
                Sprite sprite=sections[i];if(sprite==null)continue;
                var panel=new GameObject("Final_Background_"+(i+1));panel.transform.SetParent(root,false);
                panel.transform.position=new Vector3(firstCenterX+sectionWorldWidth*i,0f,0f);
                panel.transform.localScale=new Vector3(sectionWorldWidth/sprite.bounds.size.x,worldHeight/sprite.bounds.size.y,1f);
                var renderer=panel.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=-100;renderer.drawMode=SpriteDrawMode.Simple;
            }
        }
    }
}
