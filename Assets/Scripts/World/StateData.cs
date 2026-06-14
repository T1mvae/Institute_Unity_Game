using System.Collections.Generic;

namespace Institute.World
{
    /// <summary>
    /// A feudal state (Kingdom / Duchy / Temple Domain) — a cluster of adjacent
    /// <see cref="RegionData"/> regions under one crown. State-level metrics aggregate the member
    /// regions; <c>influence</c> is the player's (Institute agent's) sway inside the state.
    /// </summary>
    [System.Serializable]
    public class StateData
    {
        public string stateId;
        public string displayName;
        public string colorHex;
        public string capitalRegionId;
        public List<string> regionIds = new List<string>();
        public int stability;     // 0..20 state-level metric (mean of member regions)
        public int development;   // 0..20 state-level metric (mean of member regions)
        public int influence;     // 0..20 player influence inside the state (mean of member regions)

        public StateData() { }

        public StateData(string stateId, string displayName, string colorHex)
        {
            this.stateId = stateId;
            this.displayName = displayName;
            this.colorHex = colorHex;
        }
    }
}
