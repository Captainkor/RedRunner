using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RedRunner.DDA
{

    /// <summary>
    /// Performance symptom levels matching the paper's classification.
    /// Ordered from worst (player struggling) to best (player dominating).
    /// </summary>
    public enum PerformanceSymptom
    {
        VeryLow,
        Low,
        SlightlyLow,
        Normal,
        SlightlyHigh,
        High,
        SharplyHigh
    }

    /// <summary>
    /// ANALYZE phase of the MAPE-K loop.
    /// Classifies player performance into a symptom level based on collected metrics.
    /// </summary>
    public class DDAAnalyzer : MonoBehaviour
    {

        #region Fields

        [Header("References")]
        [Space]
        [SerializeField]
        private PlayerMetricsCollector m_MetricsCollector;

        [Header("Death Rate Thresholds")]
        [Space]
        [SerializeField]
        [Tooltip("Deaths per 100 units of distance. Below this = performing well.")]
        private float m_DeathRateSharplyHigh = 0.5f;
        [SerializeField]
        private float m_DeathRateHigh = 1f;
        [SerializeField]
        private float m_DeathRateSlightlyHigh = 2f;
        [SerializeField]
        private float m_DeathRateSlightlyLow = 4f;
        [SerializeField]
        private float m_DeathRateLow = 6f;
        [SerializeField]
        [Tooltip("Above this = player severely struggling.")]
        private float m_DeathRateVeryLow = 8f;

        [Header("Average Survival Time Thresholds (seconds)")]
        [Space]
        [SerializeField]
        [Tooltip("Surviving longer than this per life = dominating.")]
        private float m_SurvivalTimeSharplyHigh = 60f;
        [SerializeField]
        private float m_SurvivalTimeHigh = 40f;
        [SerializeField]
        private float m_SurvivalTimeSlightlyHigh = 25f;
        [SerializeField]
        private float m_SurvivalTimeSlightlyLow = 10f;
        [SerializeField]
        private float m_SurvivalTimeLow = 5f;
        [SerializeField]
        [Tooltip("Surviving less than this = severely struggling.")]
        private float m_SurvivalTimeVeryLow = 3f;

        [Header("Debug")]
        [Space]
        [SerializeField]
        private bool m_LogAnalysis = false;

        #endregion

        #region Properties

        public PlayerMetricsCollector MetricsCollector
        {
            get { return m_MetricsCollector; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Analyzes collected metrics and returns a performance symptom.
        /// Combines death rate and survival time into a single classification.
        /// </summary>
        public PerformanceSymptom AnalyzePerformance()
        {
            if (m_MetricsCollector == null)
            {
                Debug.LogWarning("[DDAAnalyzer] No MetricsCollector assigned. Returning Normal.");
                return PerformanceSymptom.Normal;
            }

            PerformanceSymptom deathRateSymptom = ClassifyByDeathRate();
            PerformanceSymptom survivalSymptom = ClassifyBySurvivalTime();

            // Combine both signals: average the enum values (simple fusion)
            int combined = Mathf.RoundToInt(((int)deathRateSymptom + (int)survivalSymptom) / 2f);
            PerformanceSymptom result = (PerformanceSymptom)Mathf.Clamp(combined, 0, 6);

            if (m_LogAnalysis)
            {
                Debug.Log(string.Format(
                    "[DDAAnalyzer] DeathRate={0}, Survival={1}, Combined={2}",
                    deathRateSymptom, survivalSymptom, result));
            }

            return result;
        }

        /// <summary>
        /// Returns the symptom as a string matching the paper's format.
        /// e.g., "very.low", "slightly.high", "sharply.high"
        /// </summary>
        public string GetSymptomString()
        {
            return SymptomToString(AnalyzePerformance());
        }

        /// <summary>
        /// Converts a PerformanceSymptom enum to the paper's string format.
        /// </summary>
        public static string SymptomToString(PerformanceSymptom symptom)
        {
            switch (symptom)
            {
                case PerformanceSymptom.VeryLow:
                    return "very.low";
                case PerformanceSymptom.Low:
                    return "low";
                case PerformanceSymptom.SlightlyLow:
                    return "slightly.low";
                case PerformanceSymptom.Normal:
                    return "normal";
                case PerformanceSymptom.SlightlyHigh:
                    return "slightly.high";
                case PerformanceSymptom.High:
                    return "high";
                case PerformanceSymptom.SharplyHigh:
                    return "sharply.high";
                default:
                    return "normal";
            }
        }

        #endregion

        #region Private Methods

        private PerformanceSymptom ClassifyByDeathRate()
        {
            float distance = m_MetricsCollector.DistanceTraveled;
            int deaths = m_MetricsCollector.DeathCount;

            if (distance <= 0f)
            {
                // No distance covered yet — can't classify
                return deaths > 0 ? PerformanceSymptom.VeryLow : PerformanceSymptom.Normal;
            }

            // Deaths per 100 units of distance
            float deathRate = (deaths / distance) * 100f;

            if (deathRate <= m_DeathRateSharplyHigh) return PerformanceSymptom.SharplyHigh;
            if (deathRate <= m_DeathRateHigh) return PerformanceSymptom.High;
            if (deathRate <= m_DeathRateSlightlyHigh) return PerformanceSymptom.SlightlyHigh;
            if (deathRate <= m_DeathRateSlightlyLow) return PerformanceSymptom.Normal;
            if (deathRate <= m_DeathRateLow) return PerformanceSymptom.SlightlyLow;
            if (deathRate <= m_DeathRateVeryLow) return PerformanceSymptom.Low;
            return PerformanceSymptom.VeryLow;
        }

        private PerformanceSymptom ClassifyBySurvivalTime()
        {
            float avgSurvival = m_MetricsCollector.AvgTimeBetweenDeaths;

            if (m_MetricsCollector.DeathCount <= 0)
            {
                // No deaths yet — check how long they've been alive
                float totalTime = m_MetricsCollector.TotalRunTime;
                if (totalTime >= m_SurvivalTimeSharplyHigh) return PerformanceSymptom.SharplyHigh;
                if (totalTime >= m_SurvivalTimeHigh) return PerformanceSymptom.High;
                return PerformanceSymptom.Normal;
            }

            if (avgSurvival >= m_SurvivalTimeSharplyHigh) return PerformanceSymptom.SharplyHigh;
            if (avgSurvival >= m_SurvivalTimeHigh) return PerformanceSymptom.High;
            if (avgSurvival >= m_SurvivalTimeSlightlyHigh) return PerformanceSymptom.SlightlyHigh;
            if (avgSurvival >= m_SurvivalTimeSlightlyLow) return PerformanceSymptom.Normal;
            if (avgSurvival >= m_SurvivalTimeLow) return PerformanceSymptom.SlightlyLow;
            if (avgSurvival >= m_SurvivalTimeVeryLow) return PerformanceSymptom.Low;
            return PerformanceSymptom.VeryLow;
        }

        #endregion

    }

}
