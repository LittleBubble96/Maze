using Cysharp.Threading.Tasks;
using GameLogic;
using GameSDK;
using TEngine;
#if ENABLE_OBFUZ
using Obfuz;
#endif

/// <summary>PaperFold 热更域入口。</summary>
#if ENABLE_OBFUZ
[ObfuzIgnore(ObfuzScope.TypeName | ObfuzScope.MethodName)]
#endif
public partial class GameApp
{
    public static void Entrance(object[] objects)
    {
        Utility.Unity.AddDestroyListener(Release);
        StartGameLogic().Forget();
    }

    private static async UniTask StartGameLogic()
    {
        await GameLogic.Maze.MazePresenter.LaunchAsync();
        GameEvent.Send(EventDefine.GameStartSuccessEvent);
        SDK.ReportGameStart();
    }

    private static void Release()
    {
        SingletonSystem.Release();
        Log.Info("Release PaperFold GameApp");
    }
}

