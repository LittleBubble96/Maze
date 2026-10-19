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
        // Quarter-cell polygons keep straight edges exactly on grid boundaries.
        private void RoadQuarter(Vector2 p, int sx, int sy, bool round)
        {
            const float h=.5f, radius=.16f;
            if(!round)
            {
                Quad(p+new Vector2(sx<0?-h:0,sy<0?-h:0),p+new Vector2(sx<0?0:h,sy<0?0:h));
                return;
            }
            int start=_vertices.Count;
            _vertices.Add(p);
            _vertices.Add(p+new Vector2(sx*h,0));
            for(int i=0;i<=4;i++)
            {
                float a=i*Mathf.PI*.5f/4;
                _vertices.Add(p+new Vector2(sx*(h-radius+radius*Mathf.Cos(a)),sy*(h-radius+radius*Mathf.Sin(a))));
            }
            _vertices.Add(p+new Vector2(0,sy*h));
            for(int i=start+2;i<_vertices.Count;i++)Triangle(start,i-1,i);
        }
        private void WallFillet(Vector2 p,int sx,int sy)
        {
            const float h=.5f,r=.16f;
            int start=_vertices.Count;
            _vertices.Add(p+new Vector2(sx*h,sy*h));
            for(int i=0;i<=4;i++)
            {
                float a=i*Mathf.PI*.5f/4;
                _vertices.Add(p+new Vector2(sx*(h-r+r*Mathf.Cos(a)),sy*(h-r+r*Mathf.Sin(a))));
                if(i>0)Triangle(start,start+i,start+i+1);
            }
        }
        public static Vector2 Position(MazeMap map,int cell) => new Vector2(cell%map.Columns-(map.Columns-1)*.5f,(map.Rows-1)*.5f-cell/map.Columns);
        public static Mesh Board(MazeMap map,int section)
        {
            var b=new MazeMeshBuilder();
            for(int cell=0;cell<map.Count;cell++)
            {
                if(map.Section(cell)!=section)continue;
                Vector2 p=Position(map,cell);
                int row=cell/map.Columns,col=cell%map.Columns;
                for(int sx=-1;sx<=1;sx+=2)
                    for(int sy=-1;sy<=1;sy+=2)
                    {
                        bool horizontal=col+sx>=0&&col+sx<map.Columns&&map.IsRoad(cell+sx);
                        bool vertical=row-sy>=0&&row-sy<map.Rows&&map.IsRoad(cell-sy*map.Columns);
                        if(map.IsRoad(cell))b.RoadQuarter(p,sx,sy,!horizontal&&!vertical);
                        else if(horizontal&&vertical&&map.IsRoad(cell+sx-sy*map.Columns))b.WallFillet(p,sx,sy);
                    }
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
