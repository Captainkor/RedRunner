using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using RedRunner.Characters;

namespace RedRunner.DDA
{

    /// <summary>
    /// MONITOR phase of the MAPE-K loop.
    /// Collects per-run player performance metrics by subscribing to existing game events.
    /// </summary>
    public class PlayerMetricsCollector : MonoBehaviour
    {

        #region Fields

        [Header("References")]
        [Space]
        [SerializeField]
        private Character m_Character;

        [Header("Debug")]
        [Space]
        [SerializeField]
        private bool m_LogMetrics = false;

        private float m_DistanceTraveled = 0f;
        private int m_DeathCount = 0;
        private float m_TotalRunTime = 0f;
        private float m_LastDeathTime = 0f;
        private float m_TimeBetweenDeathsSum = 0f;
        private int m_CoinsCollected = 0;
        private int m_CoinCountAtRunStart = 0;
        private int m_JumpsCount = 0;
        private float m_RunStartTime = 0f;
        private bool m_IsRunning = false;

        #endregion

        #region Properties

        public float DistanceTraveled
        {
            get { return m_DistanceTraveled; }
        }

        public int DeathCount
        {
            get { return m_DeathCount; }
        }

        public float TotalRunTime
        {
            get { return m_TotalRunTime; }
        }

        public float AvgTimeBetweenDeaths
        {
            get
            {
                if (m_DeathCount <= 0) return m_TotalRunTime;
                return m_TimeBetweenDeathsSum / m_DeathCount;
            }
        }

        public int CoinsCollected
        {
            get { return m_CoinsCollected; }
        }

        public int JumpsCount
        {
            get { return m_JumpsCount; }
        }

        public float JumpsPerSecond
        {
            get
            {
                if (m_TotalRunTime <= 0f) return 0f;
                return m_JumpsCount / m_TotalRunTime;
            }
        }

        #endregion

        #region MonoBehaviour Messages

        void Awake()
        {
            GameManager.OnScoreChanged += GameManager_OnScoreChanged;
            GameManager.OnReset += GameManager_OnReset;
        }

        void Start()
        {
            if (m_Character != null)
            {
                m_Character.IsDead.AddEvent(Character_OnDeathChanged, this, true);
            }
            if (GameManager.Singleton != null)
            {
                m_CoinCountAtRunStart = GameManager.Singleton.m_Coin.Value;
                GameManager.Singleton.m_Coin.AddEvent(Coin_OnValueChanged, this, true);
            }
        }

        void Update()
        {
            if (m_IsRunning && GameManager.Singleton != null && GameManager.Singleton.gameRunning)
            {
                m_TotalRunTime += Time.deltaTime;

                // Count jumps via input polling (same input system as RedCharacter)
                if (UnityStandardAssets.CrossPlatformInput.CrossPlatformInputManager.GetButtonDown("Jump"))
                {
                    m_JumpsCount++;
                }
            }
        }

        void OnDestroy()
        {
            GameManager.OnScoreChanged -= GameManager_OnScoreChanged;
            GameManager.OnReset -= GameManager_OnReset;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns current metrics as a JSON string for LLM prompt input.
        /// </summary>
        public string GetMetricsJson()
        {
            string json = string.Format(
                "{{\"distanceTraveled\":{0},\"deathCount\":{1},\"totalRunTime\":{2},\"avgTimeBetweenDeaths\":{3},\"coinsCollected\":{4},\"jumpsCount\":{5},\"jumpsPerSecond\":{6}}}",
                m_DistanceTraveled.ToString("F1"),
                m_DeathCount,
                m_TotalRunTime.ToString("F1"),
                AvgTimeBetweenDeaths.ToString("F1"),
                m_CoinsCollected,
                m_JumpsCount,
                JumpsPerSecond.ToString("F2"));

            if (m_LogMetrics)
            {
                Debug.Log("[PlayerMetricsCollector] Metrics: " + json);
            }

            return json;
        }

        /// <summary>
        /// Resets all metrics for a fresh session. Called when starting evaluation from scratch.
        /// </summary>
        public void ResetAllMetrics()
        {
            m_DistanceTraveled = 0f;
            m_DeathCount = 0;
            m_TotalRunTime = 0f;
            m_LastDeathTime = 0f;
            m_TimeBetweenDeathsSum = 0f;
            m_CoinsCollected = 0;
            m_JumpsCount = 0;
            m_RunStartTime = Time.time;
            m_IsRunning = false;

            if (GameManager.Singleton != null)
            {
                m_CoinCountAtRunStart = GameManager.Singleton.m_Coin.Value;
            }

            if (m_LogMetrics)
            {
                Debug.Log("[PlayerMetricsCollector] All metrics reset.");
            }
        }

        #endregion

        #region Events

        void GameManager_OnScoreChanged(float newScore, float highScore, float lastScore)
        {
            m_DistanceTraveled = newScore;
            m_IsRunning = true;
        }

        void GameManager_OnReset()
        {
            // Per-run reset: keep cumulative death count and coins, reset distance and time
            m_RunStartTime = Time.time;

            if (GameManager.Singleton != null)
            {
                m_CoinCountAtRunStart = GameManager.Singleton.m_Coin.Value;
            }

            if (m_LogMetrics)
            {
                Debug.Log("[PlayerMetricsCollector] Run reset. Cumulative deaths: " + m_DeathCount);
            }
        }

        void Character_OnDeathChanged(bool isDead)
        {
            if (isDead)
            {
                m_DeathCount++;
                float timeSinceLastDeath = Time.time - m_LastDeathTime;
                if (m_LastDeathTime > 0f)
                {
                    m_TimeBetweenDeathsSum += timeSinceLastDeath;
                }
                else
                {
                    // First death: time from run start
                    m_TimeBetweenDeathsSum += Time.time - m_RunStartTime;
                }
                m_LastDeathTime = Time.time;
                m_IsRunning = false;

                if (m_LogMetrics)
                {
                    Debug.Log("[PlayerMetricsCollector] Death #" + m_DeathCount +
                              " at distance " + m_DistanceTraveled.ToString("F1"));
                }
            }
        }

        void Coin_OnValueChanged(int newValue)
        {
            m_CoinsCollected = newValue - m_CoinCountAtRunStart;
        }

        #endregion

    }

}
