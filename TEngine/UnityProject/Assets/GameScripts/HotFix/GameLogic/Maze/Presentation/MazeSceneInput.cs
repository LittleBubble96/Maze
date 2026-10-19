using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic.Maze
{
    public sealed class MazeSceneInput : MonoBehaviour
    {
        public MazeSceneView View;
        public Func<bool> CanInput;
        public Action<MazeDirection> Begin;
        public Action<MazeDirection,float> Move;
        public Action End;
        private int _pointer=int.MinValue;
        private Vector2 _origin,_last;
        private bool _started,_horizontal;
        private float _sign;
        private void Update()
        {
            if(Input.touchCount>0)
            {
                for(int i=0;i<Input.touchCount;i++)
                {
                    var t=Input.GetTouch(i);
                    if(t.phase==TouchPhase.Began)Down(t.fingerId,t.position);
                    else if(t.fingerId==_pointer)
                    {
                        if(t.phase==TouchPhase.Ended||t.phase==TouchPhase.Canceled)Up();
                        else if(t.phase==TouchPhase.Moved)Drag(t.position);
                    }
                }
            }
            else
            {
                if(Input.GetMouseButtonDown(0))Down(-1,Input.mousePosition);
                if(_pointer==-1&&Input.GetMouseButton(0))Drag(Input.mousePosition);
                if(_pointer==-1&&Input.GetMouseButtonUp(0))Up();
            }
        }
        private void Down(int id,Vector2 screen)
        {
            if(_pointer!=int.MinValue||CanInput==null||!CanInput()||
                (EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject(id))||!View.TryPointer(screen,out _origin))return;
            _pointer=id;_last=_origin;_started=false;
        }
        private void Drag(Vector2 screen)
        {
            View.TryPointer(screen,out var p);Vector2 total=p-_last;
            if(Mathf.Max(Mathf.Abs(total.x),Mathf.Abs(total.y))<(_started?.025f:.12f))return;
            _horizontal=Mathf.Abs(total.x)>Mathf.Abs(total.y);_sign=Mathf.Sign(_horizontal?total.x:total.y);
            var direction=_horizontal?(_sign>0?MazeDirection.Right:MazeDirection.Left):(_sign>0?MazeDirection.Up:MazeDirection.Down);
            if(!_started)
            {
                _horizontal=Mathf.Abs(total.x)>Mathf.Abs(total.y);_sign=Mathf.Sign(_horizontal?total.x:total.y);
                Begin?.Invoke(_horizontal?(_sign>0?MazeDirection.Right:MazeDirection.Left):(_sign>0?MazeDirection.Up:MazeDirection.Down));_started=true;
            }
            Move?.Invoke(direction,Mathf.Abs(_horizontal?total.x:total.y));_last=p;
        }
        private void Up(){if(_started)End?.Invoke();ResetPointer();}
        public void ResetPointer(){_pointer=int.MinValue;_started=false;}
        private void OnDisable(){ResetPointer();}
    }
}
