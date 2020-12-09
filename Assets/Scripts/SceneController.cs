using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Biocrowds.Core;
using System.Linq;

public class SceneController : MonoBehaviour
{
    public World world;
    public static System.Random SpawnerRandom;
    public int initialSeed;

    public bool hideSpawners;

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

            if (hideSpawners)
            {
                List<SpawnArea> _spawners = FindObjectsOfType<SpawnArea>().ToList();
                foreach (SpawnArea s in _spawners)
                    s.meshRenderer.enabled = false;
            }

            world.LoadWorld();
        }
    }
}
