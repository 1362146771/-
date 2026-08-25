using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class VisualAnchorFixSetup
{
    private const string FrameRoot="Assets/Art/Characters";

    public static void Apply()
    {
        string[] textures=AssetDatabase.FindAssets("t:Texture2D",new[]{FrameRoot})
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path=>path.Contains("/Frames/")&&path.EndsWith(".png",StringComparison.OrdinalIgnoreCase))
            .OrderBy(path=>path).ToArray();
        int anchored=0,fallback=0;
        foreach(string path in textures)
        {
            if(!(AssetImporter.GetAtPath(path) is TextureImporter importer))continue;
            Vector2 pivot=DetectBakedShadowPivot(path,out bool detected);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=pivot;
            importer.SetTextureSettings(settings);
            importer.filterMode=FilterMode.Point;
            importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            if(detected)anchored++;else fallback++;
        }

        DisableDuplicateRuntimeShadow("Assets/Prefabs/Characters/PF_Diaochan.prefab");
        DisableDuplicateRuntimeShadow("Assets/Prefabs/Characters/PF_CommonSoldier.prefab");
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("VISUAL_ANCHOR_FIX_OK textures="+textures.Length+" bakedShadowAnchors="+anchored+" fallback="+fallback+" runtimeShadowsDisabled=2");
    }

    private static Vector2 DetectBakedShadowPivot(string assetPath,out bool detected)
    {
        string absolute=Path.GetFullPath(assetPath);byte[] bytes=File.ReadAllBytes(absolute);
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if(!ImageConversion.LoadImage(texture,bytes,false)){UnityEngine.Object.DestroyImmediate(texture);detected=false;return new Vector2(.5f,0f);}
        Color32[] pixels=texture.GetPixels32();int width=texture.width,height=texture.height;
        int bestCount=0,bestMin=0,bestMax=0,bestY=0;
        int maxY=Mathf.Max(1,Mathf.FloorToInt(height*.46f));
        for(int y=0;y<maxY;y++)
        {
            int count=0,minX=width,maxX=-1;
            for(int x=0;x<width;x++)
            {
                Color32 c=pixels[y*width+x];
                if(c.a<220||c.r>72||c.g>72||c.b>72)continue;
                count++;minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);
            }
            int span=maxX>=minX?maxX-minX+1:0;float density=span==0?0f:(float)count/span;
            bool checkerShadow=count>=18&&span>=32&&density>=.34f&&density<=.76f;
            if(checkerShadow&&(count>bestCount||(count==bestCount&&y<bestY))){bestCount=count;bestMin=minX;bestMax=maxX;bestY=y;}
        }
        detected=bestCount>0;
        Vector2 result=detected
            ?new Vector2(((bestMin+bestMax)*.5f)/Mathf.Max(1,width-1),(float)bestY/Mathf.Max(1,height-1))
            :new Vector2(.5f,0f);
        UnityEngine.Object.DestroyImmediate(texture);return result;
    }

    private static void DisableDuplicateRuntimeShadow(string prefabPath)
    {
        GameObject root=PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform shadow=root.transform.Find("Shadow");
            if(shadow==null)throw new MissingReferenceException("Shadow child missing: "+prefabPath);
            shadow.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
