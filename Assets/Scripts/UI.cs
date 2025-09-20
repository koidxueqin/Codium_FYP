using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UI : MonoBehaviour
{

    [HideInInspector]
    public int sceneNum;


    //STARTUP PAGE BUTTONS
    public void QuitGame()
    {
        Debug.Log("Quit Game");
        Application.Quit();
    }

    public void ChangeScene(int sceneNum)
    {
        Debug.Log("Load Scene " + sceneNum);
        SceneManager.LoadScene(sceneNum);
    }



}
