using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

using RedRunner.Characters;

namespace RedRunner.DDA
{

    /// <summary>
    /// Orchestrates the full MAPE-K DDA loop:
    /// Monitor (PlayerMetricsCollector) → Analyze (DDAAnalyzer) → Plan (LLMPolicyEngine) → Execute (DifficultyEffector)
    ///
    /// Triggers the adjustment cycle on player death, between runs.
    /// </summary>
    public class DDAManager : MonoBehaviour
    {

        #region Delegates & Events

        public delegate void DifficultyChangedHandler(DifficultyProfile newProfile);

        /// <summary>
        /// Fired after the LLM returns an adjustment and it is applied to the game.
        /// </summary>
        public static event DifficultyChangedHandler OnDifficultyChanged;

        #endregion

        #region Singleton

        private static DDAManager m_Singleton;

        public static DDAManager Singleton
        {
            get
            {
                return m_Singleton;
            }
        }

        #endregion

        #region Fields

        [Header("MAPE-K Components")]
        [Space]
        [SerializeField]
        private PlayerMetricsCollector m_MetricsCollector;
        [SerializeField]
        private DDAAnalyzer m_Analyzer;
        [SerializeField]
        private LLMPolicyEngine m_PolicyEngine;
        [SerializeField]
        private DifficultyEffector m_Effector;

        [Header("Configuration")]
        [Space]
        [SerializeField]
        private bool m_Enabled = true;
        [SerializeField]
        [Tooltip("Minimum seconds between DDA adjustment cycles to prevent rapid-fire changes.")]
        private float m_MinTimeBetweenAdjustments = 5f;
        [SerializeField]
        [Tooltip("Number of deaths before the first DDA adjustment triggers.")]
        private int m_DeathsBeforeFirstAdjustment = 2;

        [Header("Character Reference")]
        [Space]
        [SerializeField]
        private Character m_Character;

        [Header("Session Logging")]
        [Space]
        [SerializeField]
        private bool m_EnableSessionLogging = true;

        [Header("Debug")]
        [Space]
        [SerializeField]
        private bool m_LogCycleEvents = true;

        private float m_LastAdjustmentTime = 0f;
        private int m_TotalDeaths = 0;
        private int m_AdjustmentCount = 0;
        private List<string> m_SessionLog = new List<string>();

        #endregion

        #region Properties

        public bool Enabled
        {
            get { return m_Enabled; }
            set { m_Enabled = value; }
        }

        public DifficultyProfile CurrentProfile
        {
            get
            {
                if (m_PolicyEngine != null)
                    return m_PolicyEngine.CurrentProfile;
                return null;
            }
        }

        public int AdjustmentCount
        {
            get { return m_AdjustmentCount; }
        }

        public PlayerMetricsCollector MetricsCollector
        {
            get { return m_MetricsCollector; }
        }

        public DDAAnalyzer Analyzer
        {
            get { return m_Analyzer; }
        }

        public LLMPolicyEngine PolicyEngine
        {
            get { return m_PolicyEngine; }
        }

        public DifficultyEffector Effector
        {
            get { return m_Effector; }
        }

        #endregion

        #region MonoBehaviour Messages

        void Awake()
        {
            if (m_Singleton != null)
            {
                Destroy(gameObject);
                return;
            }
            m_Singleton = this;
        }

        void Start()
        {
            if (m_Character != null)
            {
                m_Character.IsDead.AddEvent(Character_OnDeathChanged, this, true);
            }
            else
            {
                Debug.LogWarning("[DDAManager] No character reference assigned.");
            }

            LogSession("DDA_SESSION_START", "DDA Manager initialized. Enabled: " + m_Enabled);
        }

        void OnDestroy()
        {
            m_Singleton = null;

            if (m_EnableSessionLogging && m_SessionLog.Count > 0)
            {
                SaveSessionLog();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Manually trigger a DDA adjustment cycle. Useful for testing.
        /// </summary>
        public void TriggerAdjustmentCycle()
        {
            if (!m_Enabled)
            {
                Debug.LogWarning("[DDAManager] DDA is disabled.");
                return;
            }

            StartCoroutine(RunDDACycle());
        }

        /// <summary>
        /// Resets session metrics. Call when starting a completely new evaluation session.
        /// </summary>
        public void ResetSession()
        {
            m_TotalDeaths = 0;
            m_AdjustmentCount = 0;
            m_LastAdjustmentTime = 0f;

            if (m_MetricsCollector != null)
            {
                m_MetricsCollector.ResetAllMetrics();
            }

            if (m_PolicyEngine != null)
            {
                m_PolicyEngine.ClearExampleBuffer();
            }

            m_SessionLog.Clear();
            LogSession("DDA_SESSION_RESET", "Session metrics reset.");
        }

        #endregion

        #region Private Methods

        private IEnumerator RunDDACycle()
        {
            if (m_MetricsCollector == null || m_Analyzer == null ||
                m_PolicyEngine == null || m_Effector == null)
            {
                Debug.LogError("[DDAManager] MAPE-K components not fully assigned.");
                yield break;
            }

            if (m_PolicyEngine.IsProcessing)
            {
                if (m_LogCycleEvents)
                    Debug.Log("[DDAManager] LLM already processing. Skipping cycle.");
                yield break;
            }

            // === MONITOR ===
            string metricsJson = m_MetricsCollector.GetMetricsJson();

            // === ANALYZE ===
            string symptom = m_Analyzer.GetSymptomString();

            if (m_LogCycleEvents)
            {
                Debug.Log(string.Format("[DDAManager] === DDA Cycle #{0} ===\nSymptom: {1}\nMetrics: {2}",
                    m_AdjustmentCount + 1, symptom, metricsJson));
            }

            LogSession("DDA_CYCLE_START", string.Format(
                "{{\"cycle\":{0},\"symptom\":\"{1}\",\"metrics\":{2}}}",
                m_AdjustmentCount + 1, symptom, metricsJson));

            // === PLAN (LLM) ===
            bool completed = false;
            DifficultyProfile resultProfile = null;

            yield return m_PolicyEngine.RequestAdjustment(metricsJson, symptom, (profile) =>
            {
                resultProfile = profile;
                completed = true;
            });

            // Wait for the callback
            while (!completed)
            {
                yield return null;
            }

            if (resultProfile != null)
            {
                // === EXECUTE ===
                m_Effector.ApplyProfile(resultProfile);
                m_AdjustmentCount++;
                m_LastAdjustmentTime = Time.time;

                LogSession("DDA_CYCLE_COMPLETE", string.Format(
                    "{{\"cycle\":{0},\"profile\":{1}}}",
                    m_AdjustmentCount, resultProfile.ToJson()));

                if (OnDifficultyChanged != null)
                {
                    OnDifficultyChanged(resultProfile);
                }

                if (m_LogCycleEvents)
                {
                    Debug.Log("[DDAManager] === Cycle Complete. Profile applied. ===");
                }
            }
            else
            {
                LogSession("DDA_CYCLE_FAILED", "LLM returned null profile.");
            }
        }

        private bool ShouldTriggerAdjustment()
        {
            if (!m_Enabled)
                return false;

            if (m_TotalDeaths < m_DeathsBeforeFirstAdjustment)
                return false;

            if (Time.time - m_LastAdjustmentTime < m_MinTimeBetweenAdjustments)
                return false;

            return true;
        }

        #endregion

        #region Session Logging

        private void LogSession(string eventType, string data)
        {
            if (!m_EnableSessionLogging)
                return;

            string entry = string.Format(
                "{{\"timestamp\":\"{0}\",\"event\":\"{1}\",\"data\":{2}}}",
                DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                eventType,
                data.StartsWith("{") ? data : "\"" + data + "\"");

            m_SessionLog.Add(entry);
        }

        private void SaveSessionLog()
        {
            try
            {
                string logDir = Path.Combine(Application.persistentDataPath, "DDALogs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                string filename = "dda_session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";
                string filePath = Path.Combine(logDir, filename);

                string json = "[\n  " + string.Join(",\n  ", m_SessionLog) + "\n]";
                File.WriteAllText(filePath, json);

                Debug.Log("[DDAManager] Session log saved to: " + filePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DDAManager] Failed to save session log: " + ex.Message);
            }
        }

        #endregion

        #region Events

        void Character_OnDeathChanged(bool isDead)
        {
            if (isDead)
            {
                m_TotalDeaths++;

                if (m_LogCycleEvents)
                {
                    Debug.Log("[DDAManager] Death #" + m_TotalDeaths + " detected.");
                }

                if (ShouldTriggerAdjustment())
                {
                    StartCoroutine(RunDDACycle());
                }
            }
        }

        #endregion

    }

}
