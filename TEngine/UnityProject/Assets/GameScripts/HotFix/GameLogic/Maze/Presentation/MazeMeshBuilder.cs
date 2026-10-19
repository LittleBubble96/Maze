using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Maze
{
    /// <summary>Scene-space geometry. Built once per map/section; never attached to a Canvas.</summary>
    public sealed class MazeMeshBuilder
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<int> _indices = new List<int>();
        public Mesh Finish(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(_vertices); mesh.SetTriangles(_indices, 0); mesh.RecalculateBounds();
            return mesh;
        }
        public void Quad(Vector2 min, Vector2 max)
        {
            int i = _vertices.Count;
            _vertices.Add(new Vector3(min.x,min.y)); _vertices.Add(new Vector3(min.x,max.y));
            _vertices.Add(new Vector3(max.x,max.y)); _vertices.Add(new Vector3(max.x,min.y));
            Triangle(i,i+1,i+2); Triangle(i,i+2,i+3);
        }
        private void Triangle(int a,int b,int c) { _indices.Add(a); _indices.Add(b); _indices.Add(c); }
        public void Disk(Vector2 centre, float radius, int segments = 24)
        {
            int start = _vertices.Count; _vertices.Add(centre);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                _vertices.Add(centre + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)) * radius);
                if (i > 0) Triangle(start,start+i,start+i+1);
            }
        }
        private void InnerCorner(Vector2 centre, float half,float radius,int sx,int sy)
        {
            int start = _vertices.Count; _vertices.Add(centre + new Vector2(sx*half,sy*half));
            for (int i=0;i<=4;i++)
            {
                float a=i*Mathf.PI*.5f/4;
                _vertices.Add(centre+new Vector2(sx*(half+radius-radius*Mathf.Cos(a)),sy*(half+radius-radius*Mathf.Sin(a))));
                if(i>0) Triangle(start,start+i,start+i+1);
            }
        }
        public static Vector2 Position(MazeMap map,int cell) => new Vector2(cell%map.Columns-(map.Columns-1)*.5f,(map.Rows-1)*.5f-cell/map.Columns);
        public static Mesh Board(MazeMap map,int section,bool folded = false)
        {
            var b=new MazeMeshBuilder(); const float h=.36f;
            for(int cell=0;cell<map.Count;cell++)
            {
                if(!map.IsRoad(cell)||map.Section(cell)!=section) continue;
                Vector2 p=Position(map,cell); b.Disk(p,h,16);
                int row=cell/map.Columns,col=cell%map.Columns;
                bool l=col>0&&map.IsRoad(cell-1), r=col+1<map.Columns&&map.IsRoad(cell+1);
                bool u=row>0&&map.IsRoad(cell-map.Columns), d=row+1<map.Rows&&map.IsRoad(cell+map.Columns);
                if(folded&&section==0&&col==map.FoldColumn-1) r=map.IsRoad(cell+map.FoldWidth+1);
                if(folded&&section==2&&col==map.FoldColumn+map.FoldWidth) l=map.IsRoad(cell-map.FoldWidth-1);
                if(l)b.Quad(p+new Vector2(-.5f,-h),p+new Vector2(0,h));
                if(r)b.Quad(p+new Vector2(0,-h),p+new Vector2(.5f,h));
                if(u)b.Quad(p+new Vector2(-h,0),p+new Vector2(h,.5f));
                if(d)b.Quad(p+new Vector2(-h,-.5f),p+new Vector2(h,0));
                if(l&&u)b.InnerCorner(p,h,.12f,-1,1);
                if(r&&u)b.InnerCorner(p,h,.12f,1,1);
                if(l&&d)b.InnerCorner(p,h,.12f,-1,-1);
                if(r&&d)b.InnerCorner(p,h,.12f,1,-1);
            }
            return b.Finish("MazeSection"+section);
        }
        public static Mesh Arrow(Vector2 from,Vector2 to)
        {
            var b=new MazeMeshBuilder(); Vector2 direction=(to-from).normalized;
            Vector2 normal=new Vector2(-direction.y,direction.x);
            b._vertices.Add(to+direction*.16f); b._vertices.Add(to-direction*.12f+normal*.12f); b._vertices.Add(to-direction*.12f-normal*.12f);
            b.Triangle(0,1,2); return b.Finish("HintArrow");
        }
    }
}
