using UnityEngine;

public class GameManagerInstance : MonoBehaviour
{
    public static GameManagerInstance Instance;

    void Awake(){
        if(GameManagerInstance.Instance == null) 
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(this);
        }
    }
}
