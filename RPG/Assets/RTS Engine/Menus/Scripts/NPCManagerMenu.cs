using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;

namespace RTSEngine
{
    [System.Serializable]
    public class NPCManagerMenu
    {
        [SerializeField]
        private NPCManager[] prefabs = new NPCManager[0]; //list of available NPC Manager prefabs that can be assigned to a NPC faction
        public List<string> GetNames () { return prefabs.Select(prefab => prefab.Name).ToList(); }

        public NPCManager Get(int ID) { return prefabs[ID]; }
        public IEnumerable<NPCManager> GetAll() { return prefabs; }

        public void Validate (string source)
        {
            Assert.IsTrue(prefabs.Length > 0, $"[{source}] At least one NPC Manager prefab must be assigned.");
            Assert.IsTrue(prefabs.All(prefab => prefab != null), $"[{source}] Make sure all NPC Manager prefabs are not null.");
        }
    }
}
