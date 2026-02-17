using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using RedRunner.Characters;
using RedRunner.Enemies;
using RedRunner.TerrainGeneration;

namespace RedRunner.DDA
{

    /// <summary>
    /// EXECUTE phase of the MAPE-K loop.
    /// Applies difficulty profile values to actual game systems (RedCharacter, TerrainGeneration, Blocks).
    /// </summary>
    public class DifficultyEffector : MonoBehaviour
    {

        #region Fields

        [Header("References")]
        [Space]
        [SerializeField]
        private RedCharacter m_Character;
        [SerializeField]
        private TerrainGenerationSettings m_TerrainSettings;

        [Header("Debug")]
        [Space]
        [SerializeField]
        private bool m_LogChanges = true;

        // Stores original block probabilities so we can rescale without drift
        private Dictionary<Block, float> m_OriginalProbabilities = new Dictionary<Block, float>();
        private bool m_ProbabilitiesCached = false;

        #endregion

        #region Properties

        public RedCharacter Character
        {
            get { return m_Character; }
        }

        public TerrainGenerationSettings TerrainSettings
        {
            get { return m_TerrainSettings; }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies all difficulty variables from the profile to game systems.
        /// Should be called between runs (after death, before reset).
        /// </summary>
        public void ApplyProfile(DifficultyProfile profile)
        {
            if (profile == null)
            {
                Debug.LogWarning("[DifficultyEffector] Cannot apply null profile.");
                return;
            }

            CacheOriginalProbabilities();

            ApplyCharacterSettings(profile);
            ApplyBlockProbabilities(profile);

            if (m_LogChanges)
            {
                Debug.Log("[DifficultyEffector] Profile applied: " + profile.ToJson());
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Cache the original block probabilities on first use so we can scale from baseline.
        /// </summary>
        private void CacheOriginalProbabilities()
        {
            if (m_ProbabilitiesCached || m_TerrainSettings == null)
                return;

            CacheBlockArray(m_TerrainSettings.StartBlocks);
            CacheBlockArray(m_TerrainSettings.MiddleBlocks);
            CacheBlockArray(m_TerrainSettings.EndBlocks);

            m_ProbabilitiesCached = true;
        }

        private void CacheBlockArray(Block[] blocks)
        {
            if (blocks == null) return;
            foreach (var block in blocks)
            {
                if (block != null && !m_OriginalProbabilities.ContainsKey(block))
                {
                    m_OriginalProbabilities[block] = block.Probability;
                }
            }
        }

        /// <summary>
        /// Applies runSpeed and jumpStrength to the player character.
        /// NOTE: RedCharacter.m_RunSpeed and m_JumpStrength are protected fields.
        /// Students must add public setters to RedCharacter for this to work.
        /// As a scaffold, we use reflection as a fallback.
        /// </summary>
        private void ApplyCharacterSettings(DifficultyProfile profile)
        {
            if (m_Character == null)
            {
                Debug.LogWarning("[DifficultyEffector] No character assigned.");
                return;
            }

            // Try direct setter methods first (students should add these to RedCharacter)
            // SetRunSpeed(float) and SetJumpStrength(float)
            var runSpeedMethod = m_Character.GetType().GetMethod("SetRunSpeed");
            if (runSpeedMethod != null)
            {
                float oldSpeed = m_Character.RunSpeed;
                runSpeedMethod.Invoke(m_Character, new object[] { profile.RunSpeed });
                LogChange("runSpeed", oldSpeed, profile.RunSpeed);
            }
            else
            {
                // Fallback: reflection on the protected field
                var field = m_Character.GetType().GetField("m_RunSpeed",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.FlattenHierarchy);
                if (field != null)
                {
                    float oldSpeed = (float)field.GetValue(m_Character);
                    field.SetValue(m_Character, profile.RunSpeed);
                    LogChange("runSpeed", oldSpeed, profile.RunSpeed);
                }
                else
                {
                    Debug.LogWarning("[DifficultyEffector] Cannot set runSpeed. Add SetRunSpeed(float) to RedCharacter.");
                }
            }

            var jumpMethod = m_Character.GetType().GetMethod("SetJumpStrength");
            if (jumpMethod != null)
            {
                float oldJump = m_Character.JumpStrength;
                jumpMethod.Invoke(m_Character, new object[] { profile.JumpStrength });
                LogChange("jumpStrength", oldJump, profile.JumpStrength);
            }
            else
            {
                var field = m_Character.GetType().GetField("m_JumpStrength",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.FlattenHierarchy);
                if (field != null)
                {
                    float oldJump = (float)field.GetValue(m_Character);
                    field.SetValue(m_Character, profile.JumpStrength);
                    LogChange("jumpStrength", oldJump, profile.JumpStrength);
                }
                else
                {
                    Debug.LogWarning("[DifficultyEffector] Cannot set jumpStrength. Add SetJumpStrength(float) to RedCharacter.");
                }
            }
        }

        /// <summary>
        /// Adjusts block selection probabilities based on the difficulty profile.
        /// Scales enemy-containing blocks by enemyDensity, saw/spike blocks individually.
        /// </summary>
        private void ApplyBlockProbabilities(DifficultyProfile profile)
        {
            if (m_TerrainSettings == null)
            {
                Debug.LogWarning("[DifficultyEffector] No TerrainSettings assigned.");
                return;
            }

            ApplyToBlockArray(m_TerrainSettings.MiddleBlocks, profile);

            // Start and end blocks typically don't need DDA, but can be extended
        }

        private void ApplyToBlockArray(Block[] blocks, DifficultyProfile profile)
        {
            if (blocks == null) return;

            foreach (var block in blocks)
            {
                if (block == null) continue;

                float originalProb = m_OriginalProbabilities.ContainsKey(block)
                    ? m_OriginalProbabilities[block]
                    : block.Probability;

                // Check if this block contains enemies
                bool hasSaw = block.GetComponentInChildren<Saw>(true) != null;
                bool hasSpike = block.GetComponentInChildren<Spike>(true) != null;
                bool hasEnemy = block.GetComponentInChildren<Enemy>(true) != null;

                float scale = 1f;

                if (hasSaw)
                {
                    scale = profile.SawProbability / 0.5f; // Normalize around default 0.5
                    LogChange("block[" + block.name + "] (saw)", block.Probability, originalProb * scale);
                }
                else if (hasSpike)
                {
                    scale = profile.SpikeProbability / 0.5f;
                    LogChange("block[" + block.name + "] (spike)", block.Probability, originalProb * scale);
                }
                else if (hasEnemy)
                {
                    scale = profile.EnemyDensity / 0.5f;
                    LogChange("block[" + block.name + "] (enemy)", block.Probability, originalProb * scale);
                }

                // Apply via reflection since Probability has no public setter on Block
                // Students should add: public virtual float Probability { set { m_Probability = value; } }
                if (hasEnemy)
                {
                    SetBlockProbability(block, originalProb * scale);
                }
            }
        }

        private void SetBlockProbability(Block block, float probability)
        {
            var field = typeof(Block).GetField("m_Probability",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(block, Mathf.Max(0.01f, probability));
            }
        }

        private void LogChange(string variable, float oldValue, float newValue)
        {
            if (m_LogChanges && !Mathf.Approximately(oldValue, newValue))
            {
                Debug.Log(string.Format("[DifficultyEffector] {0}: {1:F2} -> {2:F2} (delta: {3:+0.00;-0.00})",
                    variable, oldValue, newValue, newValue - oldValue));
            }
        }

        #endregion

    }

}
