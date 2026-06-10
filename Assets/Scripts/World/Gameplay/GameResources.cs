using UnityEngine;

namespace Institute.World.Gameplay
{
    /// <summary>
    /// Single read/write point for player resources used by the new gameplay systems.
    /// Prefers <c>ResourceManager</c>, falls back to <c>LevelController</c> (the legacy
    /// resource holder the HUD already reads). No region dependencies.
    /// </summary>
    public static class GameResources
    {
        public static bool Available => ResourceManager.Instance != null || LevelController.Instance != null;

        public static int Sanity =>
            ResourceManager.Instance != null ? ResourceManager.Instance.Sanity :
            LevelController.Instance != null ? LevelController.Instance.Sanity : 0;

        public static int Money =>
            ResourceManager.Instance != null ? ResourceManager.Instance.Money :
            LevelController.Instance != null ? LevelController.Instance.Money : 0;

        public static int Artifacts =>
            ResourceManager.Instance != null ? ResourceManager.Instance.Artifacts :
            LevelController.Instance != null ? LevelController.Instance.Artifacts : 0;

        public static bool CanAfford(int sanityCost, int moneyCost, int artifactsCost)
        {
            return Sanity >= Mathf.Max(0, sanityCost)
                && Money >= Mathf.Max(0, moneyCost)
                && Artifacts >= Mathf.Max(0, artifactsCost);
        }

        /// <summary>Spend the given (non-negative) costs if affordable. Returns false if not.</summary>
        public static bool TrySpend(int sanityCost, int moneyCost, int artifactsCost)
        {
            if (!CanAfford(sanityCost, moneyCost, artifactsCost)) return false;
            Change(-Mathf.Max(0, sanityCost), -Mathf.Max(0, moneyCost), -Mathf.Max(0, artifactsCost));
            return true;
        }

        /// <summary>Apply signed deltas (gains or losses) to resources.</summary>
        public static void Change(int sanityDelta, int moneyDelta, int artifactsDelta)
        {
            if (ResourceManager.Instance != null)
            {
                if (sanityDelta != 0) ResourceManager.Instance.ChangeSanity(sanityDelta);
                if (moneyDelta != 0) ResourceManager.Instance.ChangeMoney(moneyDelta);
                if (artifactsDelta != 0) ResourceManager.Instance.ChangeArtifacts(artifactsDelta);
                return;
            }
            if (LevelController.Instance != null)
            {
                if (sanityDelta != 0) LevelController.Instance.ChangeSanity(sanityDelta);
                if (moneyDelta != 0) LevelController.Instance.ChangeMoney(moneyDelta);
                if (artifactsDelta != 0) LevelController.Instance.ChangeArtifacts(artifactsDelta);
            }
        }
    }
}
