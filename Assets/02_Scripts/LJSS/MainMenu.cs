using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void StartRandomGame()
    {
        int randomSceneIndex = UnityEngine.Random.Range(0, 2);
        string sceneToLoad = (randomSceneIndex == 0) ? "0516_LJSS_Black" : "0516_LJSS_Slime";
        
        Debug.Log($"[MainMenu] 게임 시작! 랜덤 씬 로드: {sceneToLoad}");
        SceneManager.LoadScene(sceneToLoad);
    }
}
