using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Networking;

/* Resource Manager script created by Oussama Bouanani, SoumiDelRio.
 * This script is part of the Unity RTS Engine */

namespace RTSEngine
{

    public class ResourceManager : MonoBehaviour {

        [SerializeField]
        private Transform resourcesParent = null; //All resources must be placed as children of the same object.

        [System.Serializable]
        public struct Resources
        {
            public string Name;
            public int Amount;
        }

        [System.Serializable]
        //This array appears in the inspector, it's where you can create the resources types:
        public class MapResource
        {
            [SerializeField]
            private ResourceTypeInfo type = null; //the resource type info asset file goes here
            public ResourceTypeInfo GetResourceType() { return type; }

            //UI attributes:
            [SerializeField]
            private bool showUI = false; //show this resource in the UI panel? 

            [SerializeField]
            private Image imageUI = null; //Resource UI image.
            [SerializeField]
            private Text textUI = null; //Resource UI text to display the resource amount.

            public void UpdateUI ()
            {
                if(showUI) //if we're allowed to display the resource's amount in UI
                {
                    imageUI.sprite = type.GetIcon();
                    textUI.text = currAmount.ToString();
                }
            }

            //runtime attributes:
            private int currAmount; //the current amount of this resource.
            public void UpdateCurrAmount(int value) { currAmount += value; }
            public int GetCurrAmount() { return currAmount; }
            public void ResetCurrAmount() { currAmount = type.GetStartingAmount(); } //resets the curr amount to the starting amount of the resource type

            //NPC Resource Tasks:
            public int TargetAmount { set; get; } //the target amount that the faction wants to reach.
            public int LastCenterID { set; get; } //Whenever a resource is missing, we start searching for it from a faction center. This variable holds the last ID of the faction center that we started the search from.

            //constructor
            public MapResource (ResourceTypeInfo type)
            {
                this.type = type;
                this.currAmount = this.type.GetStartingAmount();
            }
        }
        [SerializeField]
        private MapResource[] mapResources = new MapResource[0];
        public int GetMapResourcesCount () { return mapResources.Length; }
        public IEnumerable<MapResource> GetMapResources () { return mapResources; }
        public int GetResourceID (string name) { return mapResourcesTable.TryGetValue(name, out int id) ? id : -1; }

        //key = resource_name (string) / value = resource id in above "mapResources" array (int)
        private Dictionary<string, int> mapResourcesTable = new Dictionary<string, int>();

        //This array doesn't appear in the inspector, its values are set by the game manager depending on the number of factions playing
        [System.Serializable]
        public class FactionResources
        {
            public MapResource[] Resources { private set; get; } //For each team, we'll associate all the resources types.
            public void UpdateUI() { foreach (MapResource r in Resources) { r.UpdateUI(); } }
            
            // >= 1.0f, when faction needs to spend amount X of a resource, it must have X * resourceExploitRatio from that resource
            private float resourceNeedRatio = 1.0f;
            public float ResourceNeedRatio {
                set {
                    if (value >= 1.0f) //the value must be >+ 1.0
                        resourceNeedRatio = value;
                }
                get
                {
                    return resourceNeedRatio;
                }
            }

            //constructor
            public FactionResources (MapResource[] mapResources, bool playerFaction, float needRatio)
            {
                if (playerFaction) //if this is the player's faction
                {
                    Resources = mapResources; //because we want to keep the UI elements references
                    foreach (MapResource r in Resources)
                        r.ResetCurrAmount();
                }
                else //if not..
                {
                    //make a deep copy of the map resources
                    Resources = new MapResource[mapResources.Length];
                    for (int i = 0; i < Resources.Length; i++)
                        Resources[i] = new MapResource(mapResources[i].GetResourceType());
                }

                resourceNeedRatio = needRatio;
            }
        }
        private FactionResources[] factionsResources = new FactionResources[0];
        public FactionResources GetFactionResources(int factionID) { return factionsResources[factionID]; }
        public FactionResources PlayerFactionResources { private set; get; }

        [SerializeField]
        private bool autoCollect = true; //Collect resources automatically when true. if false, the unit must drop off the collected resources each time at a building that allow that.
        public bool CanAutoCollect () { return autoCollect; }

        //selection color the resources:
        [SerializeField]
        private Color resourceSelectionColor = Color.green;

        private List<Resource> allResources = new List<Resource>(); //holds a list of all resources
        public IEnumerable<Resource> GetAllResources() { return allResources; }
        public int GetResourcesCount() { return allResources.Count; }
        public void AddResource(Resource r) { if(!allResources.Contains(r)) allResources.Add(r); }
        public void RemoveResource(Resource r) { allResources.Remove(r); }

		private GameManager gameMgr;

        //called before initializing the faction slots
        public void Init (GameManager gameMgr)
        {
            this.gameMgr = gameMgr;

            //add all map resource types to the hashtable
            for (int i = 0; i < mapResources.Length; i++)
                mapResourcesTable.Add(mapResources[i].GetResourceType().GetName(), i);
        }

        //called by the game manager after initializing the faction slots
        public void OnFactionSlotsInitialized ()
        {
            allResources = new List<Resource>(); //go through all the resources that are children of the Resources Parent game object and init them by adding them to the all resources list
            foreach (Resource r in resourcesParent.GetComponentsInChildren<Resource>(true))
                r.Init(this.gameMgr);

            InitFactionResources();
        }

        //a method that initializes resources for factions.
        public void InitFactionResources ()
        {
            //Create as many faction  slots as the amount of the spawned factions.
            factionsResources = new FactionResources[gameMgr.GetFactionCount()];

            //Loop through all the factions:
            for (int i = 0; i < factionsResources.Length; i++)
            {
                factionsResources[i] = new FactionResources(
                    mapResources, 
                    i == GameManager.PlayerFactionID,
                    i == GameManager.PlayerFactionID || GameManager.MultiplayerGame ? 1.0f : gameMgr.GetFaction(i).GetNPCMgrIns().resourceManager_NPC.resourceNeedRatioRange.getRandomValue()); //init the faction resource var

                if (i == GameManager.PlayerFactionID)
                    PlayerFactionResources = factionsResources[i];

                //go through the buildings with border components for each faction and refresh the resources in their territory
                foreach (Building center in gameMgr.GetFaction(i).FactionMgr.GetBuildingCenters())
                    center.BorderComp.CheckAllResources();
            }

            PlayerFactionResources.UpdateUI(); //right after setting up the resource settings above, refresh the resource UI.
        }

		//a method that adds/removes a certain amount to a faction's resources.
		public void UpdateResource (int factionID, string name, int amount)
		{
            if(mapResourcesTable.TryGetValue(name, out int id)) //if the resource name corresponds to a valid ID
            {
                //Add the resource amount.
                factionsResources[factionID].Resources[id].UpdateCurrAmount(amount);
                if (factionID == GameManager.PlayerFactionID) //if this is the player faction, update the UI
                    PlayerFactionResources.UpdateUI();

                CustomEvents.OnFactionResourceUpdate(factionsResources[factionID].Resources[id].GetResourceType(), factionID, amount);
			}
            else
                Debug.LogError($"[Resource Manager] The resource type with name: {name} is not defined in the mapResources array.");
		}

        //a method that gets the resource amount by providing the faction ID and name of the resource.
		public int GetResourceAmount (int factionID, string name)
		{
            if (mapResourcesTable.TryGetValue(name, out int id))
                return factionsResources[factionID].Resources[id].GetCurrAmount();
            else
            {
                Debug.LogError($"[Resource Manager] The resource type with name: {name} is not defined in the mapResources array.");
                return 0;
            }
		}

		//a method that gets called to check whether a faction has the resoureces defined in the first param or not
		public bool HasRequiredResources (Resources[] requiredResources, int factionID)
		{
            //if this is the local player and god mode is enabled
            if (factionID == GameManager.PlayerFactionID && GodMode.Enabled == true)
                return true;

            foreach(Resources r in requiredResources) //go through all required resources
            {
                //if required resource amount can not be provided by the faction
                if (GetResourceAmount(factionID, r.Name) < r.Amount * factionsResources[factionID].ResourceNeedRatio)
                    return false;
            }

            return true; //reaching this point means that the faction has all required resources
		}

        //a method that adds/removes resources using the required resources param
        public void UpdateRequiredResources (Resources[] requiredResources, bool add, int factionID)
        {
            //if we're taking resources from the player's faction while god mode is enabled
            if (!add && factionID == GameManager.PlayerFactionID && GodMode.Enabled)
                return; //do not take nothing

            foreach(Resources r in requiredResources) //go through all required resources
                UpdateResource(factionID, r.Name, (add ? 1 : -1) * r.Amount); //add or remove resources
        }

        //a method that spawns a resource instance:
        public void CreateResource(Resource resourcePrefab, Vector3 spawnPosition)
        {
            if (resourcePrefab == null) //invalid prefab
                return;

            if (GameManager.MultiplayerGame == false) //single player game:
                CreateResourceLocal(resourcePrefab, spawnPosition);
            else
            {
                NetworkInput newInput = new NetworkInput()
                {
                    sourceMode = (byte)InputMode.create,
                    targetMode = (byte)InputMode.resource,

                    initialPosition = spawnPosition
                };
                InputManager.SendInput(newInput, resourcePrefab, null);
            }
        }

        //a method that creates a resource instance locally:
        public Resource CreateResourceLocal (Resource resourcePrefab, Vector3 spawnPosition)
        {
            if (resourcePrefab == null) //invalid prefab
                return null;

            Resource newResource = Instantiate(resourcePrefab.gameObject, spawnPosition, resourcePrefab.transform.rotation).GetComponent<Resource>(); //spawn the new resource

            if (GameManager.MultiplayerGame) //if this a multiplayer game:
                InputManager.instance.RegisterObject(newResource); //register the new resource

            newResource.Init(gameMgr); //initiate resource settings

            //we need to determine whether the resource has spawned in an area controlled by a faction or not
            foreach(Border border in gameMgr.BuildingMgr.GetAllBorders())
                border.CheckAllResources();

            return newResource;
        }

        //static help methods regarding resources:

        //filter a resource list depending on a certain name
        public static List<Resource> FilterResourceList(IEnumerable<Resource> resourceList, string name)
        {
            //result list:
            List<Resource> filteredResourceList = new List<Resource>();
            //go through the input resource list:
            foreach(Resource r in resourceList)
            {
                if (r.GetResourceType().GetName() == name) //if it has the name we need
                    filteredResourceList.Add(r); //add it
            }

            return filteredResourceList;
        }
	}
}