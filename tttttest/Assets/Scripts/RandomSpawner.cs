using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Database;

public class RandomSpawner : MonoBehaviour
{
    [Header("Parent Container")]
    public Transform prefabParent;

    [Header("Prefab")]
    public GameObject[] prefabs;
    public float spawnInterval = 1f;
    public int maxCount = 100;

    [Header("Firebase Settings")]
    public string streamPath = "jiggle/stream";
    public string firebaseDatabaseUrl = "https://desktopsystem-40436-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("Phase Settings")]
    public int maxCountPhase1 = 5;
    public int maxCountPhase2 = 6;
    public int maxCountPhase3 = 8;

    private List<GameObject> spawnedObjects = new List<GameObject>();
    private float timer = 0f;
    private int spawnCount = 0;
    private bool spawning = true;

    private int count1 = 0, count2 = 0, count3 = 0;
    private int currentPhase = 0;

    private DatabaseReference streamRef;

    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                FirebaseApp app = FirebaseApp.DefaultInstance;
                FirebaseDatabase db = FirebaseDatabase.GetInstance(app, firebaseDatabaseUrl);

                streamRef = db.GetReference(streamPath);
                streamRef.LimitToLast(1).ChildAdded += OnChildAdded;
            }
            else
            {
                Debug.LogError("Firebase dependency error: " + task.Result);
            }
        });
    }

    void Update()
    {
        if (!spawning || spawnCount >= maxCount) return;

        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0f;

            int index = Random.Range(0, prefabs.Length);
            float x = Random.Range(-17f, 17f);
            float y = Random.Range(-9f, 9f);
            Vector3 spawnPosition = new Vector3(x, y, 0);

            GameObject obj = Instantiate(prefabs[index], spawnPosition, Quaternion.identity, prefabParent);
            spawnedObjects.Add(obj);
            spawnCount++;
        }
    }

    void OnDestroy()
    {
        if (streamRef != null)
        {
            streamRef.LimitToLast(1).ChildAdded -= OnChildAdded;
        }
    }

    private void OnChildAdded(object sender, ChildChangedEventArgs args)
    {
        int v = System.Convert.ToInt32(args.Snapshot.Value);

        // リセット（今までどおり）
        if (v == 9)
        {
            ResetAll();
            StartCoroutine(ClearStreamNextFrame());
            return;
        }

        // ⬇️ 生成再開トリガー：8を受け取ったら生成を再開
        if (v == 8)
        {
            spawning = true;
            Debug.Log("Resumed spawning by receiving value 8");
            return;
        }

        // 範囲外は無視
        if (v < 1 || v > 3) return;

        if (currentPhase == 0 || v == currentPhase + 1)
        {
            currentPhase = v;
        }
        else if (v != currentPhase)
        {
            ResetCounts();
            currentPhase = v;
        }

        if (currentPhase == 1)
        {
            count1 = Mathf.Min(count1 + 1, maxCountPhase1);
            if (count1 == maxCountPhase1)
            {
                spawning = false;
                Debug.Log("Spawning stopped! Total spawned: " + spawnedObjects.Count);
            }
        }
        else if (currentPhase == 2)
        {
            count2 = Mathf.Min(count2 + 1, maxCountPhase2);
            if (count2 == maxCountPhase2)
            {
                DeleteHalf();
                spawning = false;
                Debug.Log("Half deleted and spawning paused");
            }
        }
        else if (currentPhase == 3)
        {
            count3 = Mathf.Min(count3 + 1, maxCountPhase3);
            if (count3 == maxCountPhase3)
            {
                DeleteAll();
                spawning = false;
                Debug.Log("All deleted and spawning paused");
            }
        }
    }

    void DeleteHalf()
    {
        int half = spawnedObjects.Count / 2;
        for (int i = 0; i < half; i++)
        {
            int index = Random.Range(0, spawnedObjects.Count);
            Destroy(spawnedObjects[index]);
            spawnedObjects.RemoveAt(index);
        }
    }

    void DeleteAll()
    {
        foreach (var obj in spawnedObjects)
        {
            Destroy(obj);
        }
        spawnedObjects.Clear();
    }

    void ResetAll()
    {
        currentPhase = 0;
        count1 = count2 = count3 = 0;
        DeleteAll();
        spawning = true;
        spawnCount = 0;
    }

    void ResetCounts()
    {
        count1 = count2 = count3 = 0;
    }

    IEnumerator ClearStreamNextFrame()
    {
        yield return null;
        streamRef.RemoveValueAsync();
    }
}