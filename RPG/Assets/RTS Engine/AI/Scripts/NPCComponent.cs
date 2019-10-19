using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RTSEngine
{
    public abstract class NPCComponent : MonoBehaviour {

        protected NPCManager npcMgr;
        protected FactionManager factionMgr;
        protected GameManager gameMgr;

        public virtual void Init(GameManager gameMgr, NPCManager npcMgr, FactionManager factionMgr)
        {
            this.gameMgr = gameMgr;
            this.npcMgr = npcMgr;
            this.factionMgr = factionMgr;
        }
    }
}
