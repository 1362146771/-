using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThreeKingdoms
{
    [CreateAssetMenu(menuName = "Three Kingdoms/Combat/Character Action Library", fileName = "LIB_Character")]
    public sealed class CharacterActionLibrary : ScriptableObject
    {
        public string ownerId;
        public List<ActionDefinition> actions = new List<ActionDefinition>();
        public ActionDefinition Find(string actionId)
        {
            return actions == null ? null : actions.Find(a => a != null && string.Equals(a.id, actionId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
