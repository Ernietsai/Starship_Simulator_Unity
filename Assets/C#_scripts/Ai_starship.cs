using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class RocketAgent : Agent
{
    [Header("Rocket Components")]
    [HideInInspector]
    public Rigidbody rb;
    [HideInInspector]
    public bool UpdateCamera = true;
    public Transform target;
    public Transform routeFloor;

    [Header("Target Random Range")]
    public float minTargetDistance;
    public float maxTargetDistance;

    [Header("Main Engines (6 Engines)")]
    public List<RocketEngine> engines = new List<RocketEngine>(6);
    public float maxEngineThrust = 50f;
    public float maxGimbalAngle = 15f; // 引擎最大偏轉角度

    [Header("RCS System")]
    public float rcsTorqueForce = 10f;  // RCS 旋轉力矩
    public float rcsTranslateForce = 2f; // RCS 微量平移力


    [HideInInspector] public float targetDistance;
    [HideInInspector] public float targetDirection;
    [HideInInspector] public float targetHeight;

    private Vector3 startPos;
    private Quaternion startRot;
    private float thrustSum;
    //private float rewardedPoint = 0;
    private float lastDistance;
    private float minDistanceReached; // 紀錄本回合到目前為止的最短距離
    private float[] prevActions = new float[24]; // 假設有24個動作，用來偵測抖動
    private float actionJitter = 0f; //動作變化量
    private float punishHeight;
    private float endHeight;

    public override void Initialize()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;
        rb = GetComponent<Rigidbody>();
        // 假設模型從 0 到 50，將重心設在 Y = 15 左右 (底部的 1/3)
        // 注意：這是相對於你的父物件 Pivot 的局部座標
        rb.centerOfMass = new Vector3(0, 2f, 0);
        // 如果你的模型 Pivot 在正中央，那就要設為負值
        // rb.centerOfMass = new Vector3(0, -10f, 0); 
    }

    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.localPosition = startPos;
        transform.localRotation = startRot;

        // 建議在重置時給目標點一點隨機位移，訓練出來的模型更強大
        targetDistance = Random.Range(minTargetDistance, maxTargetDistance);
        targetDirection = Random.Range(0, 2*Mathf.PI);
        targetHeight = Random.Range(-30, 30);
        target.transform.localPosition = new Vector3(startPos.x + targetDistance * Mathf.Cos(targetDirection), targetHeight, startPos.y + targetDistance * Mathf.Sin(targetDirection));

        //計算獎勵用變數設定
        lastDistance = Vector3.Distance(transform.localPosition, target.transform.localPosition);
        minDistanceReached = Vector3.Distance(transform.localPosition, target.transform.localPosition);
        punishHeight = Mathf.Min(target.transform.localPosition.y, startPos.y);
        endHeight = punishHeight - 10f;

        // 設定路線地面位置
        routeFloor.transform.localPosition = new Vector3(startPos.x + targetDistance * 0.5f * Mathf.Cos(targetDirection), endHeight, startPos.y + targetDistance * 0.5f * Mathf.Sin(targetDirection));
        routeFloor.transform.LookAt(transform.TransformPoint(new Vector3(startPos.x, routeFloor.transform.localPosition.y, startPos.z)));
        routeFloor.transform.RotateAround(routeFloor.transform.position, routeFloor.transform.up, 90);
        routeFloor.transform.localScale = new Vector3(targetDistance * 0.1f + 2, routeFloor.transform.localScale.y, routeFloor.transform.localScale.z);

        UpdateCamera = true;
}


    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. 相對於火箭的目標位置 (3維) - 這是導航的核心
        Vector3 localTargetPos = transform.InverseTransformPoint(target.position);
        sensor.AddObservation(localTargetPos / 500f);

        // 2. 局部速度 (3維) - 感受側滑
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        sensor.AddObservation(localVelocity / 50f);

        // 3. 角速度 (3維) - 防止瘋狂旋轉
        sensor.AddObservation(rb.angularVelocity / 10f);

        // 4. 火箭在世界中的朝向 (3維) - 讓 AI 知道哪裡是「上」，才能學會直立
        sensor.AddObservation(transform.up);

        // 5. 目標與火箭的向量差 (3維) - 輔助導航
        Vector3 worldDiff = target.position - transform.position;
        sensor.AddObservation(worldDiff / 500f);

        // 6. 距離 (1維) - 簡單明確的目標感
        sensor.AddObservation(Vector3.Distance(transform.position, target.position) / 500f);

        // 總計：3+3+3+3+3+1 = 16 維
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var continuousActions = actions.ContinuousActions;
        thrustSum = 0;
        // ==========================================================
        // 1. 控制 6 具主引擎 (Action Index 0 - 17)
        // 每個引擎現在分配 3 個動作：[0]推力, [1]偏轉幅度, [2]偏轉方位
        // ==========================================================
        for (int i = 0; i < engines.Count; i++)
        {
            int baseIdx = i * 3;

            // 推力控制: 映射 [-1, 1] 到 [0, 1] 再乘上最大推力
            float thrustPercentage = (continuousActions[baseIdx] + 1f) / 2f;
            engines[i].currentThrust = thrustPercentage * maxEngineThrust;
            thrustSum += engines[i].currentThrust;

            // 偏轉控制: 映射 [-1, 1] 到 [-maxGimbalAngle, maxGimbalAngle]
            engines[i].currentGimbal.x = continuousActions[baseIdx + 1] * maxGimbalAngle;
            engines[i].currentGimbal.y = continuousActions[baseIdx + 2] * maxGimbalAngle;

            // 計算推力方向 (在引擎局部座標系中旋轉)
            Quaternion gimbalRotation = Quaternion.Euler(engines[i].currentGimbal.x, 0, engines[i].currentGimbal.y);
            Vector3 thrustDirection = engines[i].engineTransform.rotation * gimbalRotation * Vector3.back;

            // 施加推力 (由引擎位置向火箭上方施力)
            rb.AddForceAtPosition(thrustDirection * engines[i].currentThrust, engines[i].engineTransform.position);

            // 視覺呈現（可選）：可以在這裡畫出 Debug.DrawRay 看看推力方向
            Debug.DrawRay(engines[i].engineTransform.position, -thrustDirection * (thrustPercentage*3f), Color.red);

        }

        // ==========================================================
        // 2. 控制 RCS 系統 (Action Index 18 - 23)
        // [旋轉X, 旋轉Y, 旋轉Z, 平移X, 平移Y, 平移Z]
        // ==========================================================

        // RCS 旋轉 (扭矩)
        Vector3 rcsTorque = new Vector3(continuousActions[18], continuousActions[19], continuousActions[20]) * rcsTorqueForce;
        rb.AddRelativeTorque(rcsTorque);

        // RCS 平移 (微調位置)
        Vector3 rcsTranslate = new Vector3(continuousActions[21], continuousActions[22], continuousActions[23]) * rcsTranslateForce;
        rb.AddRelativeForce(rcsTranslate);

        // ==========================================================
        // 3. 獎勵計算
        // ==========================================================
        float actionJitter = 0f;
        for (int i = 0; i < 24; i++)
        {
            // 累加動作變化量
            actionJitter += Mathf.Abs(actions.ContinuousActions[i] - prevActions[i]);
            prevActions[i] = actions.ContinuousActions[i];
        }
        ApplyRewards();
    }
    
    private void ApplyRewards()
    {
        float distance = Vector3.Distance(transform.localPosition, target.localPosition);
        float uprightness = Vector3.Dot(transform.up, Vector3.up);
        float currentY = transform.localPosition.y;


        // 分離水平距離與高度差
        Vector3 horizontalPos = new Vector3(transform.localPosition.x, 0, transform.localPosition.z);
        Vector3 targetHorizontalPos = new Vector3(target.localPosition.x, 0, target.localPosition.z);
        float horizontalDist = Vector3.Distance(horizontalPos, targetHorizontalPos);
        float heightDiff = transform.localPosition.y - target.localPosition.y;
        
        // 只有水平對齊且高度正確時，才給予最高獎勵引導
        if (horizontalDist < 5f && heightDiff > 0)
        {
            AddReward(0.01f); // 獎勵進入降落走廊
        }
        //=========================================================================
        // 當水平距離接近時（例如 20m 內），強烈獎勵直立狀態
        if (horizontalDist < 20f)
        {
            // 獎勵直立度，這會抵消它想「指向下方」的衝動
            AddReward(uprightness * 0.01f);

            // 如果此時高度低於目標，給予額外懲罰
            if (transform.localPosition.y < target.localPosition.y)
            {
                AddReward(-0.02f);
            }
        }

        //修正「距離進步獎勵」為「位移電位」（Potential Reward）
        if (distance < minDistanceReached)
        {
            float bonus = (minDistanceReached - distance) * 1.0f;
            AddReward(bonus);
            minDistanceReached = distance; // 更新紀錄
        }
        
        //=========================================================================
        // --- 修正傾斜與移動方向的關聯 ---
        // 獲取火箭水平位移的速度方向
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        if (horizontalVelocity.magnitude > 0.5f)
        {
            // 1. 取得傾斜方向（僅方向，不含強度）
            Vector3 tiltProj = new Vector3(transform.up.x, 0, transform.up.z);
            Vector3 tiltDirection = tiltProj.normalized;

            // 2. 計算方向一致性 (-1 到 1)
            float coordination = Vector3.Dot(tiltDirection, horizontalVelocity.normalized);

            // 3. 計算目前的傾斜程度 (0 到 1，1 代表完全躺平)
            // 當 uprightness = 1 (直立) 時，tiltMagnitude = 0
            // 當 uprightness = 0 (水平) 時，tiltMagnitude = 1
            float tiltMagnitude = tiltProj.magnitude;

            if (coordination > 0)
            {
                // 方向正確時：
                // 如果傾斜幅度在合理範圍內 (例如 < 30度，即 tiltMagnitude < 0.5)，給予獎勵
                // 如果傾斜過大，獎勵遞減甚至轉為懲罰
                float tiltFactor = Mathf.Clamp01(1.0f - tiltMagnitude * 2.0f); // 超過 30度後獎勵開始消失
                AddReward(0.002f * coordination * tiltFactor);
            }
            else
            {
                // 方向錯誤（彆扭飛行）：直接扣分
                AddReward(-0.002f);
            }
        }
        //=========================================================================
        // 只要不直立，就會有微小的代價，且越斜越痛
        // (1 - uprightness) 在直立時為 0，在 45度時約為 0.3
        // 讓躺平的代價隨角度指數增加
        if (uprightness < 0.9f)
        {
            float tiltPenalty = Mathf.Pow(1.0f - uprightness, 2) * 0.08f;
            AddReward(-tiltPenalty);
            // 只要不直立，每幀都扣一點點，越歪扣越重
            AddReward(-0.01f * (1.0f - uprightness));
        }
        //=========================================================================
        // 穩定性懲罰：稍微降低，給予它傾斜轉向的空間，但仍防止瘋狂旋轉
        // 使用 sqrMagnitude 會比 magnitude 更嚴厲地懲罰「高速旋轉」
        AddReward(-0.003f * rb.angularVelocity.sqrMagnitude);  //-0.001f
        //=========================================================================
        // 1. 移除你原本那段「前半程」的 if 判斷
        // 2. 改用以下逻辑：

        Vector3 dirToTarget = (target.localPosition - transform.localPosition).normalized;

        // 計算速度在「目標方向」上的投影 (前進速度)
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, dirToTarget);

        // 核心邏輯：理想速度應該隨距離縮小而降低 (類似 V = sqrt(2ad))
        // 距離 500m 時允許 50m/s，距離 10m 時只允許 2m/s
        float idealSpeed;
        if (distance < 10f)
        {
            idealSpeed = 1.0f; // 接近目標時，強行要求慢速
        }
        else
        {
            idealSpeed = Mathf.Sqrt(2f * 0.3f * distance);
        }
        // 關鍵：如果正在遠離目標（forwardSpeed < 0），強制給予負分
        if (forwardSpeed < 0)
        {
            AddReward(-0.002f * Mathf.Abs(forwardSpeed)); // 懲罰「遠離」的行為
        }
        else if (forwardSpeed > idealSpeed)
        {
            AddReward(-0.005f * (forwardSpeed - idealSpeed)); // 加重超速懲罰
        }
        //=========================================================================
        // 懲罰過高的水平速度 (例如超過 15m/s 就開始扣分)
        if (horizontalVelocity.magnitude > 15f)
        {
            float overSpeedPenalty = (horizontalVelocity.magnitude - 15f) * 0.001f;
            AddReward(-overSpeedPenalty);
        }
        //=========================================================================
        // 懲罰抖動：數值需要很小，避免 AI 乾脆不動
        AddReward(-0.0005f * actionJitter);
        //=========================================================================
        // 強化「低速時的直立要求」
        float speed = rb.linearVelocity.magnitude;
        if (speed < 5f) // 當速度降低到接近著陸速度時
        {
            // 強制要求直立，否則扣分
            float uprightPenalty = Mathf.Max(0, 0.95f - uprightness);
            AddReward(-0.01f * uprightPenalty);
        }
        //=========================================================================
        // 縮緊高度保護 (既然你已經有 endHeight，這裡加強警告)
        if (currentY < punishHeight + (punishHeight - endHeight) * 0.2)
        {
            AddReward(-0.05f); // 接近地表警告
        }
        //=========================================================================
        // --- 高度控制補強 ---
        float yDiff = currentY - punishHeight;

        if (yDiff < 0)
        {
            // 如果低於目標高度，給予每幀持續懲罰
            AddReward(-0.01f);
        }
        else if (yDiff > 5f && yDiff < 50f)
        {
            // 鼓勵維持在目標上方一個合理的緩衝區 (例如 5~50公尺)
            AddReward(0.001f);
        }
        //=========================================================================
        // 如果下墜速度過快 (超過 -10m/s)，給予懲罰
        if (rb.linearVelocity.y < -10f)
        {
            float sinkRatePenalty = Mathf.Abs(rb.linearVelocity.y) * 0.001f;
            AddReward(-sinkRatePenalty);
        }
        //=========================================================================
        if (distance < 10f && uprightness > 0.9f)
        {
            float stayBonus = 0.01f * (1f / (rb.linearVelocity.magnitude + 1f));
            AddReward(stayBonus);
        }
        //=========================================================================
        if (distance < 5f)
        {
            // 只要進到 5m 內，每一幀都給予微量獎勵，但前提是必須「慢」
            float stabilityBonus = (1.0f / (rb.linearVelocity.magnitude + 1.0f)) * 0.02f;
            AddReward(stabilityBonus);
        }
        //=========================================================================
        // 只要能維持在目標點上方，且速度在減慢，就給予額外獎勵
        if (horizontalDist < 10f && rb.linearVelocity.y < 0 && rb.linearVelocity.y > -5f)
        {
            AddReward(0.01f); // 這是引導它進入降落姿態的「麵包屑」
        }
        //=========================================================================
        // 失敗判定
        // 如果飛得太遠(假設目標在原點，超過 500 公尺就重置)，失敗
        if (uprightness < -0.5f || distance > maxTargetDistance * 2 || currentY < endHeight)
        {
            // 根據失敗程度給予處罰（越遠、越歪扣越多）
            float penalty = -50f;  //-10f - (distance / 500f);
            SetReward(penalty);
            EndEpisode();
            
            if (uprightness < -0.5f)
                Debug.Log("傾角過大重置！");
            else if (distance > maxTargetDistance * 2)
                Debug.Log("距離過遠重置！");
            else
                Debug.Log("高度過低重置！");
            return;
        }

        // 成功判定 (非常接近目標且速度極低)
        //if (distance < 10f && rb.linearVelocity.magnitude < 3.0f && uprightness > 0.7f && transform.position.y >= target.transform.position.y)
        if (distance < 3.5f && rb.linearVelocity.magnitude < 0.5f && uprightness > 0.95f && transform.position.y >= target.transform.position.y)
            {
            SetReward(500f);
            EndEpisode();
            Debug.Log("成功著陸！");
            return;
        }
    }

    // 讓你可以手動測試 (選用)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        
        var continuousActionsOut = actionsOut.ContinuousActions;
        // 範例：按住 Space 鍵讓所有引擎全開
        if (Input.GetKey(KeyCode.Space))
        {
            for (int i = 0; i < 6; i++) continuousActionsOut[i * 3] = 1.0f;
        }
        else
        {
            for (int i = 0; i < 6; i++) continuousActionsOut[i * 3] = -1.0f;
        }

        if (Input.GetKey(KeyCode.R))
        {
            Debug.Log("已手動強制結束");
            EndEpisode();
        }
        
    }

    
    void OnDrawGizmos()
    {
        /*
         * if (rb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.TransformPoint(rb.centerOfMass), 0.2f);
        }
        */
        /*
        if (engines != null && rb != null)
        {
            foreach (RocketEngine e in engines)
            {
                // 在引擎噴口畫一個藍色小球
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(e.engineTransform.position, 0.1f);

                // 畫一條黃色的線代表推力方向
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(e.engineTransform.position, -e.engineTransform.up * 2f);
            }
        }
        */
    }

    private void FixedUpdate()
    {
        
    }
}