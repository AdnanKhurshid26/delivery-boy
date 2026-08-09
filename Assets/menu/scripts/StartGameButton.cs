using UnityEngine;

public class StartGameButton : MonoBehaviour
{
    public GameObject levelMenu;

    public void StartGame()
    {
        levelMenu.SetActive(true);
    }
}
