using UnityEngine;
using UnityEngine.SceneManagement;

// 타이틀 씬의 "Play" 버튼에 연결하세요.
// 버튼 OnClick → TitleSceneManager.OnPlayButtonClicked()
public class TitleSceneManager : MonoBehaviour
{
    private static readonly string[] GameSceneNames =
    {
        "0516_LJSS_Slime",      // index 0 → GiantSlime
        "0516_LJSS_BlackMage",  // index 1 → BlackMage
    };

    public void OnPlayButtonClicked()
    {
        int index = Random.Range(0, GameSceneNames.Length);
        GameConfig.SelectedBigObject = (GameConfig.BigObjectType)index;
        Debug.Log($"[Title] BigObject 선택: {GameConfig.SelectedBigObject} → 씬 '{GameSceneNames[index]}' 로드");
        SceneManager.LoadScene(GameSceneNames[index]);
    }
}
