using UnityEngine;
using System.Collections.Generic;

public class AreaManager : MonoBehaviour
{
    [Header("Settings")]
    public GameObject areaPrefab;    // 拖入你的 TrainingArea Prefab
    public float spacing;            // 區域中心點間距 (設為 1200 以確保安全)
    public int numberOfAreas = 16;   // 想要生成的數量
    public int columns = 4;          // 矩陣排列的列數

    //private RocketAgent starship;
    
    [ContextMenu("Generate One Area")]
    public void GenerateOne()
    {
        Generate(1, 1);
    }

    [ContextMenu("Generate Areas")]  // 讓你可以直接在 Inspector 右鍵選單執行
    public void GenerateMultiple()
    {
        Generate(numberOfAreas, columns);
    }

    public void Generate(int numberOfAreasGenerate, int columnsGenerate)
    {
        // 先清除舊的區域
        Clear();

        GameObject firstRocket = null; // 用來儲存第一個生成的火箭

        for (int i = 0; i < numberOfAreasGenerate; i++)
        {
            // 計算矩陣座標
            int row = i / columnsGenerate;
            int col = i % columnsGenerate;

            Vector3 spawnPos = new Vector3(col * spacing, 0, row * spacing);

            // 生成區域
            GameObject newArea = Instantiate(areaPrefab, spawnPos, Quaternion.identity);
            newArea.name = $"Training Area {i}";
            newArea.transform.parent = this.transform;
            // 紀錄第一個區域的火箭
            if (i == 0)
            {
                // 假設你的 RocketAgent 腳本就在 Area 的子物件中
                // 或者是透過標籤 "Player" 或腳本類型找到它
                firstRocket = newArea.GetComponentInChildren<RocketAgent>().gameObject;
            }
        }

        // --- 自動抓取邏輯 ---
        if (firstRocket != null)
        {
            Camera_main_controller camScript = Camera.main.GetComponent<Camera_main_controller>();
            if (camScript != null)
            {
                // 將第一個火箭設定為攝影機跟隨目標
                // 請確認你的 CameraFollow 腳本中變數名稱是否為 target
                camScript.starship = firstRocket;
                Debug.Log($"攝影機已自動對焦至: {firstRocket.name}");
            }
        }

        Debug.Log($"成功生成 {numberOfAreasGenerate} 個訓練區域，間距為 {spacing}。");
    }

    [ContextMenu("Clear Areas")]
    public void Clear()
    {
        // 刪除所有子物件
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }
    void Start()
    {/*
        foreach (RocketAgent child in areaPrefab.transform)
        {
            if (child.name == "星艦 - 實體")
            {
                starship = child;  //.GetComponent<RocketAgent>();
                spacing  = starship.maxTargetDistance * 2;
            }
        }
    */}
}
