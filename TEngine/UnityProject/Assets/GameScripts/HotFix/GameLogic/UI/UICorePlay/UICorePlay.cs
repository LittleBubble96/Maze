using GameLogic.Maze;

namespace GameLogic
{
    /// <summary>Framework-managed props HUD. Gameplay objects are loaded separately as MazeSceneRoot.</summary>
    [Window(UILayer.UI, location: "UICorePlay")]
    public sealed class UICorePlay : UIWindow
    {
        public MazeHudView Hud { get; private set; }
        protected override void OnCreate()
        {
            base.OnCreate();
            Hud=gameObject.GetComponent<MazeHudView>();
            if(Hud==null)throw new System.InvalidOperationException("UICorePlay prefab missing MazeHudView");
            Hud.Initialize();
        }
        protected override void OnClose(){Hud?.CloseOverlay();base.OnClose();}
    }
}
