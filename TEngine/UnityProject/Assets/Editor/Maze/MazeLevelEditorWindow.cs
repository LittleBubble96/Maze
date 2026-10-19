using System;
using System.IO;
using GameLogic.Maze;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace PaperFold.Editor
{
    /// <summary>Authoring uses exactly the same map and solver as HotFix.</summary>
    public sealed class MazeLevelEditorWindow : EditorWindow
    {
        private const string CatalogPath = "Assets/AssetRaw/Configs/MazeCampaign.json";
        private MazeCampaignData _campaign;
        private int _selected, _paint;
        private bool _folded, _dirty;
        private string _message = "从关卡列表选择关卡，手动绘制道路并校验。";
        private Vector2 _scroll;
        [MenuItem("Tools/PaperFold/迷宫关卡编辑器")]
        public static void Open() { GetWindow<MazeLevelEditorWindow>("迷宫关卡").minSize = new Vector2(650,740); }
        private void OnEnable() { Load(); }
        private void Load()
        {
            if (File.Exists(CatalogPath)) _campaign = JsonUtility.FromJson<MazeCampaignData>(File.ReadAllText(CatalogPath));
            if (_campaign?.levels == null) _campaign = new MazeCampaignData { levels = new[] { new MazeLevelData { cells = new int[63], start=0, exit=8 } } };
            _selected = Mathf.Clamp(_selected, 0, _campaign.levels.Length - 1); _dirty = false;
        }
        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("PaperFold · 手动搭关 / 关卡列表 / 校验", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("修改的是地图数据，不是视图对象。发布前必须校验可解性；预览不写玩家存档。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新读取") && (!_dirty || EditorUtility.DisplayDialog("放弃未保存修改", "重新读取会覆盖当前编辑内容。", "重新读取", "取消"))) Load();
                if (GUILayout.Button("校验全部")) ValidateAll();
                if (GUILayout.Button("保存目录")) Save();
            }
            EditorGUILayout.LabelField("关卡列表", EditorStyles.boldLabel);
            var labels = new string[_campaign.levels.Length];
            for(int i=0;i<labels.Length;i++)labels[i]=$"{i+1:00}  {_campaign.levels[i]?.title}";
            int selection = GUILayout.SelectionGrid(_selected, labels, 1);
            if (_selected != selection) { _selected = selection; _folded = false; }
            var level = _campaign.levels[_selected];
            if (level != null)
            {
                EditorGUI.BeginChangeCheck();
                level.title = EditorGUILayout.TextField("关卡名称", level.title);
                EditorGUILayout.LabelField("设计意图 / 卡点 / 解法");
                level.designNotes = EditorGUILayout.TextArea(level.designNotes, GUILayout.MinHeight(60));
                level.version = EditorGUILayout.IntField("内容版本（改图后递增）", level.version);
                level.tutorial = EditorGUILayout.Toggle("教学关", level.tutorial);
                level.initialFolded = EditorGUILayout.Toggle("初始折叠", level.initialFolded);
                level.foldColumn = EditorGUILayout.IntSlider("折叠起始列（从 0 开始）", level.foldColumn, 1, level.columns - 2);
                level.foldWidth = EditorGUILayout.IntSlider("折叠列数", level.foldWidth, 1, level.columns - level.foldColumn - 1);
                if (EditorGUI.EndChangeCheck()) _dirty = true;
                _folded = EditorGUILayout.Toggle("预览折叠状态", _folded);
                _paint = GUILayout.Toolbar(_paint, new[] { "画道路", "画墙", "设置起点", "设置出口" });
                DrawMap(level);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("校验当前关")) ValidateCurrent();
                    if (GUILayout.Button("Play 模式试玩当前关")) Preview(level);
                }
            }
            EditorGUILayout.HelpBox((_dirty ? "未保存 · " : "") + _message, MessageType.None);
            EditorGUILayout.EndScrollView();
        }
        private void DrawMap(MazeLevelData data)
        {
            float size = Mathf.Min(48, (position.width - 50) / data.columns);
            Rect rect = GUILayoutUtility.GetRect(data.columns * size, data.rows * size);
            for (int row = 0; row < data.rows; row++)
            {
                int visibleColumn = 0;
                for (int col = 0; col < data.columns; col++)
                {
                    bool middle = col >= data.foldColumn && col < data.foldColumn + data.foldWidth;
                    if (_folded && middle) continue;
                    int cell = row * data.columns + col;
                    var tile = new Rect(rect.x + visibleColumn++ * size, rect.y + row * size, size - 2, size - 2);
                    Color old = GUI.backgroundColor;
                    GUI.backgroundColor = cell == data.start ? Color.cyan : cell == data.exit ? new Color(.7f,.5f,1) :
                        data.cells[cell] == 1 ? (middle ? new Color(.7f,1,.7f) : Color.white) : new Color(.1f,.45f,.4f);
                    GUI.Box(tile, cell == data.start ? "S" : cell == data.exit ? "E" : middle ? "·" : "");
                    var input = Event.current;
                    if ((input.type == EventType.MouseDown || input.type == EventType.MouseDrag) && input.button == 0 && tile.Contains(input.mousePosition))
                    {
                        if (_paint < 2) data.cells[cell] = _paint == 0 ? 1 : 0;
                        else if (_paint == 2) { data.start = cell; data.cells[cell] = 1; }
                        else { data.exit = cell; data.cells[cell] = 1; }
                        _dirty = true; input.Use(); Repaint();
                    }
                    GUI.backgroundColor = old;
                }
            }
        }
        private void ValidateCurrent()
        {
            var data = _campaign.levels[_selected];
            string error = MazeLevelValidator.Validate(data);
            if (error != null) { _message = error; return; }
            var map = new MazeMap(data); var rules = new FoldMazeRules(map);
            var start = new MazeNode(map.Start,map.InitialFolded);
            _message = $"关卡 {data.id} 可解；最短逻辑操作 {rules.Solve(start).Count}，最少必要切换 {rules.MinimumSwitches(start)}。";
        }
        private bool ValidateAll()
        {
            for (int i = 0; i < _campaign.levels.Length; i++)
            {
                var data = _campaign.levels[i];
                string error = MazeLevelValidator.Validate(data);
                if (error == null && data.id != i + 1) error = "关卡 ID 顺序不一致";
                if (error != null) { _message = $"关卡 {i + 1}：{error}"; return false; }
            }
            _message = "全部手工关卡通过可解性与道路可逆性校验。"; return true;
        }
        private void Save()
        {
            if (!ValidateAll()) return;
            File.WriteAllText(CatalogPath, JsonUtility.ToJson(_campaign, true));
            AssetDatabase.ImportAsset(CatalogPath); MazeResourceBuilder.SyncAuthoredMap(); _dirty = false; _message = "已保存并导入 YooAsset Configs 目录。";
        }
        private void Preview(MazeLevelData level)
        {
            if (!EditorApplication.isPlaying) { _message = "请先进入 Play 模式，再点击试玩。"; return; }
            if (MazeLevelValidator.Validate(level) != null) { ValidateCurrent(); return; }
            var scenePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MazeResourceBuilder.ScenePath);
            var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MazeResourceBuilder.HudPath);
            var data = level.Copy(); data.id = 1;
            var existing = UnityEngine.Object.FindObjectsOfType<MazePresenter>();
            foreach (var presenter in existing) presenter.gameObject.SetActive(false);
            var scene = UnityEngine.Object.Instantiate(scenePrefab);
            var hud = UnityEngine.Object.Instantiate(hudPrefab);
            var preview = MazePresenter.Create(new MazeCampaignData { levels = new[] { data } }, scene.GetComponent<MazeSceneView>(), hud.GetComponent<MazeHudView>(), new PreviewStore(), null);
            preview.gameObject.AddComponent<PreviewHudLifetime>().Hud = hud;
            preview.gameObject.AddComponent<PreviewLifetime>().Restore = existing;
            _message = "试玩使用独立内存存档；停止 Play 模式退出预览。";
        }
        private sealed class PreviewStore : IMazeProgressStore
        {
            private MazeProgressData _data = new MazeProgressData();
            public MazeProgressData Load() => _data;
            public void Save(MazeProgressData data) { _data = JsonUtility.FromJson<MazeProgressData>(JsonUtility.ToJson(data)); }
        }
        private sealed class PreviewHudLifetime : MonoBehaviour
        {
            public GameObject Hud;
            private void OnDestroy() { if (Hud != null) Destroy(Hud); }
        }
        private sealed class PreviewLifetime : MonoBehaviour
        {
            public MazePresenter[] Restore;
            private void OnDestroy() { if (Restore == null) return; foreach (var view in Restore) if (view != null) view.gameObject.SetActive(true); }
        }
    }
}

