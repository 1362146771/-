using System.IO;
using ThreeKingdoms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Iteration02BackgroundSetup
{
    private const string SourceRoot="Assets/AgentGenerated/Art/Backgrounds/Iteration02/";
    private const string LayerRoot="Assets/Art/Backgrounds/Iteration02Layers/";
    private const string CompositePath=LayerRoot+"fortress_stage_composite.png";
    private const int SourceWidth=1664,Height=928,Overlap=160,SectionWidth=1557;
    private static readonly string[] SourceNames={"fortress_approach","fortress_courtyard","fortress_command"};
    private static readonly string[] BandNames={"BG","Mid","Ground","FG"};

    [MenuItem("Three Kingdoms/Iteration 02/Setup Fortress Background")]
    public static void Setup()
    {
        EnsureFolder("Assets/Art");EnsureFolder("Assets/Art/Backgrounds");EnsureFolder(LayerRoot.TrimEnd('/'));
        var sources=new Texture2D[3];
        for(int i=0;i<3;i++)
        {
            string path=SourceRoot+SourceNames[i]+".png";ConfigureTexture(path,true);
            sources[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(sources[i]==null)throw new FileNotFoundException("Background source is not imported",path);
        }
        BuildComposite(sources);ConfigureTexture(CompositePath,true);var composite=AssetDatabase.LoadAssetAtPath<Texture2D>(CompositePath);

        var layers=new Sprite[4][];for(int band=0;band<4;band++)layers[band]=new Sprite[3];
        for(int section=0;section<3;section++)for(int band=0;band<4;band++)
        {
            int x=section*SectionWidth,width=section==2?composite.width-x:SectionWidth;
            Rect rect=BandRect(band,x,width);string assetPath=LayerRoot+"section_"+(section+1)+"_"+BandNames[band]+".asset";
            if(AssetDatabase.LoadAssetAtPath<Object>(assetPath)!=null)AssetDatabase.DeleteAsset(assetPath);
            var sprite=Sprite.Create(composite,rect,new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect);sprite.name="FortressSection"+(section+1)+"_"+BandNames[band];
            AssetDatabase.CreateAsset(sprite,assetPath);layers[band][section]=sprite;
        }

        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SC_Stage01_AncientStreet.unity",OpenSceneMode.Single);
        var builder=Object.FindFirstObjectByType<StageVisualBuilder>();if(builder==null)throw new MissingComponentException("StageVisualBuilder not found in stage scene");
        var serialized=new SerializedObject(builder);Assign(serialized.FindProperty("backgroundSections"),layers[0]);Assign(serialized.FindProperty("midgroundSections"),layers[1]);
        Assign(serialized.FindProperty("gameplaySections"),layers[2]);Assign(serialized.FindProperty("foregroundSections"),layers[3]);serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(builder);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("ITERATION02_BACKGROUND_SETUP_OK sections=3 layers=4 compositeWidth="+composite.width+" overlap="+Overlap+" provider=qwen-image-3.0-pro");
    }

    private static void BuildComposite(Texture2D[] sources)
    {
        int width=SourceWidth*3-Overlap*2;var output=new Texture2D(width,Height,TextureFormat.RGBA32,false);var pixels=new Color32[width*Height];
        for(int section=0;section<3;section++)
        {
            Color32[] source=sources[section].GetPixels32();int start=section*(SourceWidth-Overlap);
            for(int y=0;y<Height;y++)for(int x=0;x<SourceWidth;x++)
            {
                int targetX=start+x,targetIndex=y*width+targetX,sourceIndex=y*SourceWidth+x;
                if(section>0&&x<Overlap){float t=(x+.5f)/Overlap;pixels[targetIndex]=Color32.Lerp(pixels[targetIndex],source[sourceIndex],t);}
                else pixels[targetIndex]=source[sourceIndex];
            }
        }
        output.SetPixels32(pixels);output.Apply();File.WriteAllBytes(CompositePath,output.EncodeToPNG());Object.DestroyImmediate(output);AssetDatabase.ImportAsset(CompositePath,ImportAssetOptions.ForceSynchronousImport);
    }

    private static Rect BandRect(int band,int x,int width)
    {
        if(band==0)return new Rect(x,590,width,338);if(band==1)return new Rect(x,310,width,280);if(band==2)return new Rect(x,0,width,928);return new Rect(x,0,width,100);
    }
    private static void ConfigureTexture(string path,bool readable)
    {
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)return;
        importer.textureType=TextureImporterType.Default;importer.isReadable=readable;importer.sRGBTexture=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.filterMode=FilterMode.Bilinear;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=8192;importer.SaveAndReimport();
    }
    private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
    private static void Assign(SerializedProperty property,Sprite[] sprites){property.arraySize=sprites.Length;for(int i=0;i<sprites.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=sprites[i];}
}
