using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedRunner.DDA
{

    [System.Serializable]
    public struct DifficultyVariable
    {
        public string description;
        public float value;
        public float thresholdMin;
        public float thresholdMax;

        public DifficultyVariable(string description, float value, float min, float max)
        {
            this.description = description;
            this.value = value;
            this.thresholdMin = min;
            this.thresholdMax = max;
        }

        public void Clamp()
        {
            value = Mathf.Clamp(value, thresholdMin, thresholdMax);
        }
    }

    [CreateAssetMenu(menuName = "RedRunner/Create Difficulty Profile")]
    public class DifficultyProfile : ScriptableObject
    {

        #region Fields

        [Header("Enemy Settings")]
        [Space]
        [SerializeField]
        [Range(0f, 1f)]
        private float m_EnemyDensity = 0.5f;
        [SerializeField]
        [Range(0f, 1f)]
        private float m_SawProbability = 0.4f;
        [SerializeField]
        [Range(0f, 1f)]
        private float m_SpikeProbability = 0.3f;

        [Header("Terrain Settings")]
        [Space]
        [SerializeField]
        [Range(0f, 0.8f)]
        private float m_GapFrequency = 0.3f;
        [SerializeField]
        [Range(0f, 3f)]
        private float m_PlatformHeightVariance = 0.5f;

        [Header("Player Settings")]
        [Space]
        [SerializeField]
        [Range(3f, 12f)]
        private float m_RunSpeed = 5f;
        [SerializeField]
        [Range(6f, 15f)]
        private float m_JumpStrength = 10f;

        [Header("Collectable Settings")]
        [Space]
        [SerializeField]
        [Range(0.1f, 1f)]
        private float m_CoinDensity = 0.5f;

        #endregion

        #region Properties

        public float EnemyDensity
        {
            get { return m_EnemyDensity; }
            set { m_EnemyDensity = Mathf.Clamp(value, 0f, 1f); }
        }

        public float SawProbability
        {
            get { return m_SawProbability; }
            set { m_SawProbability = Mathf.Clamp(value, 0f, 1f); }
        }

        public float SpikeProbability
        {
            get { return m_SpikeProbability; }
            set { m_SpikeProbability = Mathf.Clamp(value, 0f, 1f); }
        }

        public float GapFrequency
        {
            get { return m_GapFrequency; }
            set { m_GapFrequency = Mathf.Clamp(value, 0f, 0.8f); }
        }

        public float PlatformHeightVariance
        {
            get { return m_PlatformHeightVariance; }
            set { m_PlatformHeightVariance = Mathf.Clamp(value, 0f, 3f); }
        }

        public float RunSpeed
        {
            get { return m_RunSpeed; }
            set { m_RunSpeed = Mathf.Clamp(value, 3f, 12f); }
        }

        public float JumpStrength
        {
            get { return m_JumpStrength; }
            set { m_JumpStrength = Mathf.Clamp(value, 6f, 15f); }
        }

        public float CoinDensity
        {
            get { return m_CoinDensity; }
            set { m_CoinDensity = Mathf.Clamp(value, 0.1f, 1f); }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns all difficulty variables as a list with thresholds for LLM input.
        /// </summary>
        public List<DifficultyVariable> GetVariables()
        {
            return new List<DifficultyVariable>
            {
                new DifficultyVariable("enemyDensity", m_EnemyDensity, 0f, 1f),
                new DifficultyVariable("gapFrequency", m_GapFrequency, 0f, 0.8f),
                new DifficultyVariable("runSpeed", m_RunSpeed, 3f, 12f),
                new DifficultyVariable("jumpStrength", m_JumpStrength, 6f, 15f),
                new DifficultyVariable("sawProbability", m_SawProbability, 0f, 1f),
                new DifficultyVariable("spikeProbability", m_SpikeProbability, 0f, 1f),
                new DifficultyVariable("coinDensity", m_CoinDensity, 0.1f, 1f),
                new DifficultyVariable("platformHeightVariance", m_PlatformHeightVariance, 0f, 3f),
            };
        }

        /// <summary>
        /// Serializes the profile to a JSON string for LLM request input.
        /// Format matches the paper's JSON structure with thresholds.
        /// </summary>
        public string ToJson()
        {
            var variables = GetVariables();
            var parts = new List<string>();
            foreach (var v in variables)
            {
                parts.Add(string.Format(
                    "{{\"description\":\"{0}\",\"threshold\":[{1},{2}],\"value\":{3}}}",
                    v.description,
                    v.thresholdMin.ToString("F1"),
                    v.thresholdMax.ToString("F1"),
                    v.value.ToString("F2")));
            }
            return "{\"game_variables\":[" + string.Join(",", parts) + "]}";
        }

        /// <summary>
        /// Parses a JSON response from the LLM and applies values.
        /// Expected format: {"game_variables":[{"description":"enemyDensity","value":0.6},...]}
        /// Returns true if parsing succeeded.
        /// </summary>
        public bool FromJson(string json)
        {
            try
            {
                // Simple JSON parsing without external dependencies.
                // Looks for "description":"<name>" and "value":<number> pairs.
                var variables = GetVariables();
                foreach (var v in variables)
                {
                    string searchKey = "\"description\":\"" + v.description + "\"";
                    int descIndex = json.IndexOf(searchKey, StringComparison.Ordinal);
                    if (descIndex < 0)
                        continue;

                    string valueKey = "\"value\":";
                    int valueStart = json.IndexOf(valueKey, descIndex, StringComparison.Ordinal);
                    if (valueStart < 0)
                        continue;

                    valueStart += valueKey.Length;
                    int valueEnd = valueStart;
                    while (valueEnd < json.Length && (char.IsDigit(json[valueEnd]) || json[valueEnd] == '.' || json[valueEnd] == '-'))
                    {
                        valueEnd++;
                    }

                    string valueStr = json.Substring(valueStart, valueEnd - valueStart);
                    if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                    {
                        SetVariableByName(v.description, parsed);
                    }
                }

                ClampToThresholds();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DifficultyProfile] Failed to parse JSON: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Clamps all values to their defined thresholds.
        /// </summary>
        public void ClampToThresholds()
        {
            m_EnemyDensity = Mathf.Clamp(m_EnemyDensity, 0f, 1f);
            m_GapFrequency = Mathf.Clamp(m_GapFrequency, 0f, 0.8f);
            m_RunSpeed = Mathf.Clamp(m_RunSpeed, 3f, 12f);
            m_JumpStrength = Mathf.Clamp(m_JumpStrength, 6f, 15f);
            m_SawProbability = Mathf.Clamp(m_SawProbability, 0f, 1f);
            m_SpikeProbability = Mathf.Clamp(m_SpikeProbability, 0f, 1f);
            m_CoinDensity = Mathf.Clamp(m_CoinDensity, 0.1f, 1f);
            m_PlatformHeightVariance = Mathf.Clamp(m_PlatformHeightVariance, 0f, 3f);
        }

        /// <summary>
        /// Creates a runtime copy of this profile that can be modified without affecting the asset.
        /// </summary>
        public DifficultyProfile CreateRuntimeCopy()
        {
            var copy = ScriptableObject.CreateInstance<DifficultyProfile>();
            copy.m_EnemyDensity = m_EnemyDensity;
            copy.m_GapFrequency = m_GapFrequency;
            copy.m_RunSpeed = m_RunSpeed;
            copy.m_JumpStrength = m_JumpStrength;
            copy.m_SawProbability = m_SawProbability;
            copy.m_SpikeProbability = m_SpikeProbability;
            copy.m_CoinDensity = m_CoinDensity;
            copy.m_PlatformHeightVariance = m_PlatformHeightVariance;
            return copy;
        }

        #endregion

        #region Private Methods

        private void SetVariableByName(string name, float value)
        {
            switch (name)
            {
                case "enemyDensity":
                    m_EnemyDensity = value;
                    break;
                case "gapFrequency":
                    m_GapFrequency = value;
                    break;
                case "runSpeed":
                    m_RunSpeed = value;
                    break;
                case "jumpStrength":
                    m_JumpStrength = value;
                    break;
                case "sawProbability":
                    m_SawProbability = value;
                    break;
                case "spikeProbability":
                    m_SpikeProbability = value;
                    break;
                case "coinDensity":
                    m_CoinDensity = value;
                    break;
                case "platformHeightVariance":
                    m_PlatformHeightVariance = value;
                    break;
                default:
                    Debug.LogWarning("[DifficultyProfile] Unknown variable: " + name);
                    break;
            }
        }

        #endregion

    }

}
