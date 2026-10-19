using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Maze
{
    /// <summary>Only HUD and modal UI. Every main control is stored in UICorePlay.prefab.</summary>
    public sealed class MazeHudView : MonoBehaviour
    {
        private TextMeshProUGUI _title,_status,_foldLabel,_hintLabel;
        private Button _fold,_hint,_restart,_skip,_select;
        private RectTransform _modal;
        private bool _initialized;
        private int _level=-1,_hintRevision=-1;
        private bool _folded;
        public bool Busy=>_modal!=null;
        public Action Fold,Hint,Restart,Skip,Select;
        public void Initialize()
        {
            if(_initialized)return;_initialized=true;
            _title=transform.Find("Panel/Title").GetComponent<TextMeshProUGUI>();
            _status=transform.Find("Panel/Status").GetComponent<TextMeshProUGUI>();
            _fold=Bind("Fold",()=>Fold?.Invoke());_hint=Bind("Hint",()=>Hint?.Invoke());
            _restart=Bind("Restart",()=>Restart?.Invoke());_skip=Bind("Skip",()=>Skip?.Invoke());_select=Bind("Select",()=>Select?.Invoke());
            _foldLabel=_fold.GetComponentInChildren<TextMeshProUGUI>();_hintLabel=_hint.GetComponentInChildren<TextMeshProUGUI>();
        }
        private Button Bind(string name,UnityEngine.Events.UnityAction action)
        {var b=transform.Find("Panel/Props/"+name).GetComponent<Button>();b.onClick.AddListener(action);return b;}
        public void Render(FoldMazeGameplay game,bool canSkip,bool locked)
        {
            if(_level!=game.Map.Id){_level=game.Map.Id;_title.text=$"第 {_level} 关";_hintRevision=-1;}
            _fold.gameObject.SetActive(_level!=1);
            _skip.gameObject.SetActive(canSkip);
            bool active=!locked&&!Busy&&!game.Dragging&&!game.Completed;
            _fold.interactable=_hint.interactable=_restart.interactable=_skip.interactable=active;
            _select.interactable=!locked&&!game.Dragging&&!Busy;
            if(_hintRevision==game.HintRevision&&_folded==game.Stable.Folded)return;
            _hintRevision=game.HintRevision;_folded=game.Stable.Folded;
            _foldLabel.text=_folded?"展开":"折叠";
            _hintLabel.text=game.Hint.Count>0?"提示中":game.FreeHintUsed?"提示（视频）":"提示（免费）";
            _fold.GetComponent<Image>().color=game.Hint.Count>0&&game.Hint[0].Folded!=_folded?new Color(.95f,.6f,.15f):new Color(0,.52f,.48f);
        }
        public void SetMessage(string message){if(_status!=null)_status.text=message;}
        public void ShowDialog(string title,string action,Action accept,bool cancel=true)
        {
            Overlay();Text("Message",new Vector2(0,120),new Vector2(610,190),30).text=title;
            Button(action,new Vector2(0,-40),()=>{CloseOverlay();accept?.Invoke();});
            if(cancel)Button("返回",new Vector2(0,-150),CloseOverlay);
        }
        public void ShowLevels(int total,int unlocked,Func<int,string> state,Action<int> select)
        {
            Overlay();Text("Title",new Vector2(0,440),new Vector2(600,70),36).text="选择关卡";
            for(int i=1;i<=total;i++)
            {
                int id=i;var b=Button(id+"\n"+state(id),new Vector2(-210+((i-1)%3)*210,280-((i-1)/3)*145),()=>{CloseOverlay();select(id);},new Vector2(190,115));
                b.interactable=id<=unlocked;
            }
        }
        public void CloseOverlay(){if(_modal==null)return;_modal.gameObject.SetActive(false);Destroy(_modal.gameObject);_modal=null;}
        private void Overlay()
        {
            CloseOverlay();_modal=Rect("Modal",transform,Vector2.zero,new Vector2(720,1280));
            _modal.gameObject.AddComponent<Image>().color=new Color(.94f,.97f,.95f,.99f);
        }
        private TextMeshProUGUI Text(string name,Vector2 pos,Vector2 size,int fontSize)
        {
            var rect=Rect(name,_modal,pos,size);var t=rect.gameObject.AddComponent<TextMeshProUGUI>();
            t.font=_title.font;t.fontSize=fontSize;t.alignment=TextAlignmentOptions.Center;t.color=new Color(.04f,.24f,.25f);t.raycastTarget=false;return t;
        }
        private Button Button(string label,Vector2 pos,UnityEngine.Events.UnityAction action,Vector2? size=null)
        {
            var rect=Rect(label,_modal,pos,size??new Vector2(440,76));rect.gameObject.AddComponent<Image>().color=new Color(0,.52f,.48f);
            var b=rect.gameObject.AddComponent<Button>();b.onClick.AddListener(action);
            var text=Text("Label",Vector2.zero,rect.sizeDelta,25);text.transform.SetParent(rect,false);text.text=label;text.color=Color.white;return b;
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.gameObject.layer=5;r.SetParent(parent,false);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
        private void OnDestroy(){Fold=Hint=Restart=Skip=Select=null;}
    }
}
