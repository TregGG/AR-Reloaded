using UnityEngine;
using UnityEngine.SceneManagement;

public class ui : MonoBehaviour
{
    public void NextScene()
    {
        // 3. Get the current build index and load index + 1
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
    public void MainMnueScene()
    {
        // 3. Get the current build index and load index + 1
        SceneManager.LoadScene(0);
    }
}
