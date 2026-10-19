using System;
using System.IO;
using System.Linq;
using GameLogic.Maze;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace PaperFold.Editor
{
    /// <summary>Creates real editable scene/UI prefabs, mesh assets and a complete Chinese font atlas.</summary>
    [InitializeOnLoad]
    public static class MazeResourceBuilder
    {
        public const string ScenePath="Assets/AssetRaw/Actor/Maze/MazeSceneRoot.prefab";
        public const string HudPath="Assets/AssetRaw/UI/UICorePlay/UICorePlay.prefab";
        public const string FontPath="Assets/AssetRaw/Fonts/Maze/MazeChinese.asset";
        private const string MeshPath="Assets/AssetRaw/Actor/Maze/Meshes/";
        static MazeResourceBuilder(){EditorApplication.delayCall+=BuildMissing;}
        private static void BuildMissing()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!File.Exists(ScenePath)||!File.Exists(HudPath)||!File.Exists(FontPath))Build();
        }
        [MenuItem("Tools/PaperFold/构建场景与UI资源")]
        public static void Build()
        {
            if(EditorApplication.isPlaying){Debug.LogWarning("请停止 Play 模式后构建资源");return;}
            Directory.CreateDirectory(MeshPath);Directory.CreateDirectory(Path.GetDirectoryName(HudPath));
            Directory.CreateDirectory(Path.GetDirectoryName(FontPath));AssetDatabase.Refresh();
            var font=BuildFont();
            var road=Material("Road",new Color(.98f,.99f,.96f));
            var ball=Material("Ball",new Color(.08f,.43f,.86f));
            var goal=Material("Goal",new Color(.59f,.28f,.86f));
            var shade=Material("FoldRegion",new Color(0,.58f,.53f));
            var hint=Material("Hint",new Color(1,.61f,.13f));
            var disk=new MazeMeshBuilder();disk.Disk(Vector2.zero,.22f,32);var ballMesh=StoreMesh("Ball",disk.Finish("Ball"));
            var target=new MazeMeshBuilder();target.Disk(Vector2.zero,.3f,32);var goalMesh=StoreMesh("Goal",target.Finish("Goal"));
            var quad=new MazeMeshBuilder();quad.Quad(new Vector2(-.5f,-.5f),new Vector2(.5f,.5f));var quadMesh=StoreMesh("Quad",quad.Finish("Quad"));
            var ballPrefab=TokenPrefab("MazeBall",ballMesh,ball);
            var goalPrefab=TokenPrefab("MazeGoal",goalMesh,goal);
            var root=new GameObject("MazeSceneRoot");root.layer=30;
            try
            {
                var content=new GameObject("Map").transform;content.SetParent(root.transform,false);content.gameObject.layer=30;
                var map=new MazeMap(FoldMazeGenerator.FirstTutorial());
                var parts=new MeshFilter[3];
                for(int i=0;i<3;i++)parts[i]=MeshObject(new[]{"Left","Middle","Right"}[i],content,StoreMesh("TutorialSection"+i,MazeMeshBuilder.Board(map,i)),road);
                var fold=MeshObject("FoldRegion",content,quadMesh,shade).transform;fold.localPosition=new Vector3(0,0,.2f);fold.gameObject.SetActive(false);
                var ballInstance=(GameObject)PrefabUtility.InstantiatePrefab(ballPrefab,content);ballInstance.name="Ball";ballInstance.transform.localPosition=(Vector3)MazeMeshBuilder.Position(map,map.Start)+Vector3.back*.3f;
                var goalInstance=(GameObject)PrefabUtility.InstantiatePrefab(goalPrefab,content);goalInstance.name="Goal";goalInstance.transform.localPosition=(Vector3)MazeMeshBuilder.Position(map,map.Exit)+Vector3.back*.15f;
                var view=root.AddComponent<MazeSceneView>();
                view.Configure(content,parts,fold,ballPrefab,goalPrefab,hint);
                view.SetTokenInstances(ballInstance.transform,goalInstance.transform);
                PrefabUtility.SaveAsPrefabAsset(root,ScenePath);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            BuildHud(font);AssetDatabase.SaveAssets();AssetDatabase.Refresh();ValidateAssets();
            Debug.Log("[PaperFold] 场景、小球、目标、UICorePlay 和中文字体资源已构建。");
        }
        private static TMP_FontAsset BuildFont()
        {
            var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);if(existing!=null)return existing;
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/AssetArt/Font/AlibabaPuHuiTi-3-95-ExtraBold.ttf");
            if(source==null)throw new InvalidOperationException("缺少中文源字体");
            var font=TMP_FontAsset.CreateFontAsset(source,64,6,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            font.name="MazeChinese";AssetDatabase.CreateAsset(font,FontPath);
            font.material.name="MazeChinese Material";AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);
            string chars=Characters();font.TryAddCharacters(chars,out string missing);
            if(!string.IsNullOrEmpty(missing))Debug.LogWarning("[PaperFold] 字体缺字: "+missing);
            foreach(var texture in font.atlasTextures)if(!AssetDatabase.Contains(texture))AssetDatabase.AddObjectToAsset(texture,font);
            EditorUtility.SetDirty(font);return font;
        }
        private static string Characters()
        {
            var text="0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz（） ·，。！？：；、紫色蓝色目标起点道路折叠迷宫提示视频免费返回关卡成功上下左右滑动重新开始已未跳过解锁选择加载中进度保存失败这一关不需要再最后到";
            foreach(string file in Directory.GetFiles("Assets/GameScripts/HotFix/GameLogic/Maze","*.cs",SearchOption.AllDirectories))text+=File.ReadAllText(file);
            return new string(text.Where(c=>!char.IsControl(c)).Distinct().ToArray());
        }
        private static Material Material(string name,Color color)
        {
            string path="Assets/AssetRaw/Materials/Maze/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("PaperFold/MazeSolid"));AssetDatabase.CreateAsset(material,path);}
            material.color=color;EditorUtility.SetDirty(material);return material;
        }
        private static Mesh StoreMesh(string name,Mesh mesh)
        {
            string path=MeshPath+"Maze"+name+"Mesh.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;
        }
        private static MeshFilter MeshObject(string name,Transform parent,Mesh mesh,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=30;go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.GetComponent<MeshFilter>();
        }
        private static GameObject TokenPrefab(string name,Mesh mesh,Material material)
        {
            var go=MeshObject(name,null,mesh,material).gameObject;
            try{return PrefabUtility.SaveAsPrefabAsset(go,"Assets/AssetRaw/Actor/Maze/"+name+".prefab");}
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        private static void BuildHud(TMP_FontAsset font)
        {
            var root=new GameObject("UICorePlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(MazeHudView));root.layer=5;
            try
            {
                root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(720,1280);scaler.matchWidthOrHeight=1;
                var panel=Rect("Panel",root.transform,Vector2.zero,new Vector2(720,1280));
                Text("Title",panel,new Vector2(0,480),new Vector2(640,70),40,"第 1 关",font);
                Text("Status",panel,new Vector2(0,380),new Vector2(640,140),26,"向右滑动，再向上，最后向右到紫色目标\n这一关不需要折叠",font);
                var props=Rect("Props",panel,Vector2.zero,new Vector2(720,1280));
                Button("Fold","折叠",props,new Vector2(0,-360),new Vector2(330,76),font);
                Button("Hint","提示（免费）",props,new Vector2(-170,-470),new Vector2(280,72),font);
                Button("Restart","重新开始",props,new Vector2(170,-470),new Vector2(280,72),font);
                Button("Select","返回选关",props,new Vector2(-170,-570),new Vector2(280,58),font);
                Button("Skip","跳过本关",props,new Vector2(170,-570),new Vector2(280,58),font);
                PrefabUtility.SaveAsPrefabAsset(root,HudPath);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {var go=new GameObject(name,typeof(RectTransform));go.layer=5;var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
        private static void Text(string name,Transform parent,Vector2 pos,Vector2 size,int fontSize,string value,TMP_FontAsset font)
        {
            var text=Rect(name,parent,pos,size).gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=fontSize;
            text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;
        }
        private static void Button(string name,string label,Transform parent,Vector2 pos,Vector2 size,TMP_FontAsset font)
        {
            var rect=Rect(name,parent,pos,size);rect.gameObject.AddComponent<Image>().color=new Color(0,.47f,.43f);rect.gameObject.AddComponent<Button>();
            Text("Label",rect,Vector2.zero,size,26,label,font);
        }
        [MenuItem("Tools/PaperFold/验证场景资源")]
        public static void ValidateAssets()
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(ScenePath);var hud=AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            if(root==null||hud==null||root.GetComponent<MazeSceneView>()==null||hud.GetComponent<MazeHudView>()==null)throw new InvalidOperationException("场景或 UI 预制体无效");
            if(root.GetComponentsInChildren<Canvas>(true).Length!=0)throw new InvalidOperationException("游戏内容不允许存在 Canvas");
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))if(renderer.sharedMaterial==null||renderer.GetComponent<MeshFilter>().sharedMesh==null)throw new InvalidOperationException("场景网格资源缺失");
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if(!font.HasCharacters("第1关向右滑动再向上最后到紫色目标这一关不需要折叠提示免费重新开始返回选关"))throw new InvalidOperationException("教学字体缺字");
            File.WriteAllText("../Tests/maze-scene-validation.txt","PASS: separate scene/UI prefabs, meshes/materials, Chinese tutorial glyphs.");
        }
    }
}

