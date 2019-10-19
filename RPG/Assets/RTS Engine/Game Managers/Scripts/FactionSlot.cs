using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace RTSEngine
{
    //The array that holds all the current teams information.
    [System.Serializable]
    public class FactionSlot
    {
        [SerializeField]
        private string name = "FACTION_NAME"; //Faction's name.
        public string GetName () { return name; }

        [SerializeField]
        private FactionTypeInfo typeInfo = null; //Type of this faction (the type determines which extra buildings/units can this faction use).
        public FactionTypeInfo GetTypeInfo() { return typeInfo; }

        [SerializeField]
        private Color color = Color.blue; //Faction's color.
        public Color GetColor () { return color; }

        [SerializeField]
        private bool playerControlled = false; //Is the team controlled by the player, make sure that only one team is controlled by the player.
        public bool PlayerControlled
        {
            private set
            {
                playerControlled = value;
            }
            get
            {
                return playerControlled;
            }
        }

        [SerializeField]
        private int maxPopulation = 5; //Maximum number of units that can be present at the same time (which can be increased in the game by constructing certain buildings)

        //update the maximum population
        public void UpdateMaxPopulation(int value, bool add = true)
        {
            if (add)
                maxPopulation += value;
            else
                maxPopulation = value;

            //custom event trigger:
            CustomEvents.OnMaxPopulationUpdated(this, value);
        }
        //get the maximum population
        public int GetMaxPopulation() { return maxPopulation; }

        private int currentPopulation; //current number of spawned units.

        //update the current population
        public void UpdateCurrentPopulation(int value)
        {
            currentPopulation += value;
            //custom event trigger:
            CustomEvents.OnCurrentPopulationUpdated(this, value);
        }

        //get the current population
        public int GetCurrentPopulation() { return currentPopulation; }

        //get the amount of free slots:
        public int GetFreePopulation()
        {
            return maxPopulation - currentPopulation;
        }

        [SerializeField]
        private Building capitalBuilding = null; //The capital building that MUST be placed in the map before startng the game.
        public Building GetCapitalBuilding () { return capitalBuilding; }
        public Vector3 CapitalPosition { private set; get; } //The capital building's position is stored in this variable because when it's a new multiplayer game, the capital buildings are re-spawned in order to be synced in all players screens.

        public int ID { private set; get; }
        public FactionManager FactionMgr { private set; get; } //The faction manager is a component that stores the faction data. Each faction is required to have one.

        [SerializeField]
        private NPCManager npcMgr = null; //Drag and drop the NPC manager's prefab here.

        private NPCManager npcMgrIns; //the active instance of the NPC manager prefab.
        //get the NPC Manager instance:
        public NPCManager GetNPCMgrIns() { return npcMgrIns; }

        //init the npc manager:
        public void InitNPCMgr(GameManager gameMgr)
        {
            //make sure there's a npc manager prefab set:
            if (npcMgr == null)
            {
                Debug.LogError("[Faction Slot]: NPC Manager hasn't been set for NPC faction.");
                return;
            }

            npcMgrIns = Object.Instantiate(npcMgr.gameObject).GetComponent<NPCManager>();

            //set the faction manager:
            npcMgrIns.FactionMgr = FactionMgr;

            //init the npc manager:
            npcMgrIns.Init(gameMgr);

            if (typeInfo != null) //if this faction has a valid type.
            {
                //set the building center regulator (if there's any):
                if (typeInfo.GetCenterBuilding() != null)
                    npcMgrIns.territoryManager_NPC.centerRegulator = npcMgrIns.GetBuildingRegulatorAsset(typeInfo.GetCenterBuilding());
                //set the population building regulator (if there's any):
                if (typeInfo.GetPopulationBuilding() != null)
                    npcMgrIns.populationManager_NPC.populationBuilding = npcMgrIns.GetBuildingRegulatorAsset(typeInfo.GetPopulationBuilding());

                //is there extra buildings to add?
                foreach (Building b in typeInfo.GetExtraBuildings())
                {
                    if (b != null)
                        npcMgrIns.buildingCreator_NPC.independentBuildingRegulators.Add(npcMgrIns.GetBuildingRegulatorAsset(b));
                    else
                        Debug.LogError($"[Game Manager]: Faction " + typeInfo.GetName() + " (Code: " + typeInfo.GetCode() + ") has missing Building fields in the 'Extra Buildings' list.");
                }
            }
        }

        public bool IsNPCFaction() //is this faction NPC?
        {
            return PlayerControlled == false && npcMgr != null;
        }

        public bool Lost { private set; get; } //true when the faction is defeated and can no longer have an impact on the game.

        //units/buildings that are spawned by default must be added to the following list
        [System.Serializable]
        public struct DefaultFactionEntity
        {
            public FactionEntity instance;
            public FactionTypeInfo[] factionTypes; //leave empty if you want the faction entity to remain for all faction types, if not, specify the allowed faction types here
        }
        [SerializeField]
        private DefaultFactionEntity[] defaultFactionEntities = new DefaultFactionEntity[0];

        //multiplayer related attributes:
#if RTSENGINE_MIRROR
        //Mirror: 
        public NetworkLobbyFaction_Mirror LobbyFaction_Mirror { private set; get; }
        public NetworkFactionManager_Mirror FactionManager_Mirror { set; get; }
        public int ConnID_Mirror { set; get; }

        public void InitMultiplayer (NetworkLobbyFaction_Mirror lobbyFaction)
        {
            this.LobbyFaction_Mirror = lobbyFaction;
        }
#endif

        //init the faction slot and update the faction attributes
        public void Init(string name, FactionTypeInfo typeInfo, Color color, bool playerControlled, int population, NPCManager npcMgr, FactionManager factionMgr, int factionID, GameManager gameMgr)
        {
            this.name = name;
            this.typeInfo = typeInfo;
            this.color = color;
            this.PlayerControlled = playerControlled;

            this.npcMgr = this.PlayerControlled ? null : npcMgr;

            Init(factionID, factionMgr, gameMgr);

            UpdateMaxPopulation(population, false);
        }

        //init the faction without modifying the faction attributes
        public void Init (int factionID, FactionManager factionMgr, GameManager gameMgr)
        {
            this.ID = factionID;
            this.FactionMgr = factionMgr;

            FactionMgr.Init(gameMgr, ID, typeInfo ? typeInfo.GetLimits() : null); //init the faction manager component of this faction

            //depending on the faction type, add extra units/buildings (if there's actually any) to be created for each faction:
            if (playerControlled == true) //if this faction is player controlled:
            {
                if (typeInfo != null) //if this faction has a valid type.
                    gameMgr.PlacementMgr.AddBuildingRange(typeInfo.GetExtraBuildings()); //add the extra buildings so that this faction can use them.
            }
            else if (IsNPCFaction() == true) //if this is not controlled by the local player but rather NPC.
                //Init the NPC Faction manager:
                InitNPCMgr(gameMgr);

            Lost = false;

            CapitalPosition = capitalBuilding.transform.position;
            if (!GameManager.MultiplayerGame) //if this is no multiplayer game, we'll create the capital buildings and init them here
                SpawnFactionEntities(gameMgr);
        }

        public void SpawnFactionEntities (GameManager gameMgr)
        {
            //if the faction has a valid faction type and a valid capital building assigned to it
            if (typeInfo != null && typeInfo.GetCapitalBuilding() != null)
            {
                Object.DestroyImmediate(this.capitalBuilding.gameObject); //destroy the default capital and spawn another one:

                //create new faction center:
                capitalBuilding = gameMgr.BuildingMgr.CreatePlacedInstanceLocal(
                    typeInfo.GetCapitalBuilding(),
                    CapitalPosition,
                    typeInfo.GetCapitalBuilding().transform.rotation.eulerAngles.y,
                    null, ID, true, true);
            }
            else //if not, just init the pre-placed capital building
                capitalBuilding.Init(gameMgr, ID, false);

            if (ID == GameManager.PlayerFactionID) //if this is the local player? (owner of this capital building)
            {
                //Set the player's initial camera position (looking at the faction's capital building)
                CameraMovement.instance.LookAt(capitalBuilding.transform.position);
                CameraMovement.instance.SetMiniMapCursorPos(capitalBuilding.transform.position);
            }

            foreach(DefaultFactionEntity factionEntity in defaultFactionEntities) //go through the default faction entities
            {
                if (factionEntity.factionTypes.Length == 0 //if not faction types have been assigned
                    || factionEntity.factionTypes.Any(t => t == typeInfo)) //or the faction slot's type is specified in the array
                {
                    factionEntity.instance.Init(gameMgr, ID, false);

                    //if this is a unit, then update the population and update the faction limits:
                    FactionMgr.UpdateLimitsList(factionEntity.instance.GetCode(), factionEntity.instance.GetCategory(), true);
                    if(factionEntity.instance.Type == EntityTypes.unit)
                    {
                        Unit defaultUnit = (Unit)factionEntity.instance;
                        UpdateCurrentPopulation(defaultUnit.GetPopulationSlots());
                    }
                }
                else
                    Object.DestroyImmediate(factionEntity.instance.gameObject); //destroy the faction entity instance if it's not part of this faction type
            }
        }

        //a method called to destroy the faction slot when intializing the game
        public void InitDestroy()
        {
            Object.DestroyImmediate(capitalBuilding.gameObject); //destroy the capital building

            foreach (DefaultFactionEntity factionEntity in defaultFactionEntities) //and destroy all the default faction entities
                Object.DestroyImmediate(factionEntity.instance.gameObject);
        }

        //a method to disable this faction slot
        public void Disable ()
        {
            Lost = true;
        }
    }
}
