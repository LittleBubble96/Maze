using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic.Maze
{
    /// <summary>Prefab-backed scene content. No UI Graphic, RectTransform or per-cell GameObjects.</summary>
    public sealed class MazeSceneView : MonoBehaviour
    {
        private Camera sceneCamera => Camera.main;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private MeshFilter[] sections;
        [SerializeField] private Transform foldRegion;
        [SerializeField] private GameObject ballPrefab, goalPrefab;
        [SerializeField] private Material hintMaterial;
        [SerializeField] private Transform _ball, _goal;
        private MazeMap _map;

        private readonly List<Mesh> _meshes=new List<Mesh>();
        private readonly List<GameObject> _hints=new List<GameObject>();
        private float _foldAmount,_foldFrom,_foldTo,_foldTime=1,_snapTime=1;
        private Vector3 _snapFrom,_snapTo;
        private int _hintRevision=-1;
        public bool Busy=>_foldTime<1||_snapTime<1;
        public Vector3 BallPosition=>_ball.localPosition;
        public Camera SceneCamera=>sceneCamera;

        public void Configure(Transform root, MeshFilter[] parts, Transform fold, GameObject ball, GameObject goal, Material hint)
        {
            contentRoot=root;
            sections=parts;
            foldRegion=fold;
            ballPrefab=ball;
            goalPrefab=goal;
            hintMaterial=hint;
        }
        public void SetTokenInstances(Transform ball,Transform goal){_ball=ball;_goal=goal;}
        public void Bind(MazeMap map,bool folded)
        {
            bool rebuild=!ReferenceEquals(_map,map);
            _map=map; if(rebuild)ClearMeshes();
            if(_ball==null)_ball=Instantiate(ballPrefab,contentRoot).transform;
            if(_goal==null)_goal=Instantiate(goalPrefab,contentRoot).transform;
            _ball.name="Ball";_goal.name="Goal";_ball.localScale=Vector3.one;

                if(rebuild)for(int i=0;i<sections.Length;i++){var mesh=MazeMeshBuilder.Board(map,i);sections[i].sharedMesh=mesh;_meshes.Add(mesh);}
            _foldAmount=_foldTo=folded?1:0;_foldTime=_snapTime=1;_hintRevision=-1;
            foldRegion.gameObject.SetActive(true);
            foldRegion.localPosition=new Vector3(map.FoldColumn+(map.FoldWidth-1)*.5f-(map.Columns-1)*.5f,0,.2f);
            foldRegion.localScale=new Vector3(map.FoldWidth,map.Rows,1);
            Fit(); ApplyFold();
            _ball.localPosition=Position(map.Start,-.3f); _goal.localPosition=Position(map.Exit,-.15f);
        }
        private void Fit()
        {
            float availableWidth=sceneCamera.orthographicSize*2*sceneCamera.aspect*.86f;
            float availableHeight=sceneCamera.orthographicSize*2*.48f;
            float scale=Mathf.Min(availableWidth/(_map.Columns+1),availableHeight/(_map.Rows+1));
            contentRoot.localScale=Vector3.one*scale;
            contentRoot.localPosition=new Vector3(0,.45f,0);
        }
        public void AnimateFold(bool folded){_foldFrom=_foldAmount;_foldTo=folded?1:0;_foldTime=0;}
        public void Snap(Vector3 from,int cell){_snapFrom=from;_snapTo=Position(cell,-.3f);_snapTime=0;}
        public void ResetAnimation(bool folded){_foldAmount=_foldTo=folded?1:0;_foldTime=_snapTime=1;ApplyFold();}
        public void Render(FoldMazeGameplay game)
        {
            bool folding=_foldTime<1;
            if(folding){_foldTime=Mathf.Min(1,_foldTime+Time.unscaledDeltaTime/.35f);_foldAmount=Mathf.Lerp(_foldFrom,_foldTo,Mathf.SmoothStep(0,1,_foldTime));ApplyFold();}
            if(_snapTime<1){_snapTime=Mathf.Min(1,_snapTime+Time.unscaledDeltaTime/.12f);_ball.localPosition=Vector3.Lerp(_snapFrom,_snapTo,Mathf.SmoothStep(0,1,_snapTime));}
            else _ball.localPosition=Vector3.Lerp(Position(game.Anchor.Cell,-.3f),Position(game.Target.Cell,-.3f),game.Fraction);
            _goal.localPosition=Position(_map.Exit,-.15f);
            if(_hintRevision!=game.HintRevision){_hintRevision=game.HintRevision;RebuildHints(game);}
            if(game.Completed)_ball.localScale=Vector3.MoveTowards(_ball.localScale,Vector3.zero,Time.unscaledDeltaTime*2.5f);
        }
        private void ApplyFold()
        {


            float offset=_foldAmount*_map.FoldWidth*.5f;
            sections[0].transform.localPosition=new Vector3(offset,0,0);
            sections[2].transform.localPosition=new Vector3(-offset,0,0);
            sections[1].transform.localScale=new Vector3(1-_foldAmount,1,1);
            float centre=_map.FoldColumn+(_map.FoldWidth-1)*.5f-(_map.Columns-1)*.5f;
            sections[1].transform.localPosition=new Vector3(centre*_foldAmount,0,0);
            sections[1].gameObject.SetActive(_foldAmount<1);
            foldRegion.gameObject.SetActive(_foldAmount<1);
            foldRegion.localScale=new Vector3(_map.FoldWidth*(1-_foldAmount),_map.Rows,1);
        }
        private Vector3 Position(int cell,float z)
        {
            Vector2 p=MazeMeshBuilder.Position(_map,cell);int section=_map.Section(cell);
            p.x+=section==0?_foldAmount*_map.FoldWidth*.5f:section==2?-_foldAmount*_map.FoldWidth*.5f:0;
            return new Vector3(p.x,p.y,z);
        }
        public bool TryPointer(Vector2 screen,out Vector2 board)
        {
            var world=sceneCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,-sceneCamera.transform.position.z));
            Vector3 local=contentRoot.InverseTransformPoint(world);board=local;
            return _map!=null;
        }
        private void RebuildHints(FoldMazeGameplay game)
        {
            foreach(var go in _hints){Destroy(go.GetComponent<MeshFilter>().sharedMesh);Destroy(go);}_hints.Clear();
            int previous=game.Stable.Cell;
            foreach(var node in game.Hint)
            {
                if(node.Folded!=game.Stable.Folded)break;
                var go=new GameObject("Hint",typeof(MeshFilter),typeof(MeshRenderer));go.layer=gameObject.layer;
                go.transform.SetParent(contentRoot,false);go.transform.localPosition=new Vector3(0,0,-.2f);
                go.GetComponent<MeshFilter>().sharedMesh=MazeMeshBuilder.Arrow(Position(previous,0),Position(node.Cell,0));
                go.GetComponent<MeshRenderer>().sharedMaterial=hintMaterial;_hints.Add(go);previous=node.Cell;
            }
        }
        private void ClearMeshes(){foreach(var mesh in _meshes)if(mesh!=null)Destroy(mesh);_meshes.Clear();}
        private void OnDestroy(){ClearMeshes();foreach(var go in _hints)if(go!=null)Destroy(go.GetComponent<MeshFilter>().sharedMesh);}
    }
}
