using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Biocrowds.Core;

public class SceneController : MonoBehaviour
{
    public World world;
    public static System.Random SpawnerRandom;
    public int initialSeed;

    private void Awake()
    {
        SpawnerRandom = new System.Random(initialSeed);
    }
    void Start()
    {
        Debug.Log("Press 1 to load world");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("Reloading Scene");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("Loading World");
            world.LoadWorld();
        }
    }
}
