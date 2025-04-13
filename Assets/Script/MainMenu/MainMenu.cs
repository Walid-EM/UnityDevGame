using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour{
    
    //Launch the game
    public void Play(){
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    //Quit the game
    public void Quit(){
        Application.Quit();
        Debug.Log("Player Has Quit The Game");
    }

}
