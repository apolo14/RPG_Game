using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;

/* Upgrade Manager script created by Oussama Bouanani, SoumiDelRio.
 * This script is part of the Unity RTS Engine */

namespace RTSEngine
{
    public class UpgradeManager : MonoBehaviour
    {
        //holds unit upgrade tasks info that need to be synced when a new task launcher is added
        private struct UpgradedUnitTask
        {
            public int factionID;
            public string upgradedUnitCode;
            public Unit targetUnitPrefab;
            public Upgrade.NewTaskInfo newTaskInfo;
        }
        private List<UpgradedUnitTask> upgradedUnitTasks = new List<UpgradedUnitTask>();

        GameManager gameMgr;

        public void Init(GameManager gameMgr)
        {
            this.gameMgr = gameMgr;
        }

        private void OnEnable()
        {
            //start listening to custom events:
            CustomEvents.TaskLauncherAdded += OnTaskLauncherAdded;
        }

        private void OnDisable()
        {
            //stop listening to custom events:
            CustomEvents.TaskLauncherAdded -= OnTaskLauncherAdded;
        }

        //method called when a unit/building upgrade is launched
        public void LaunchUpgrade (Upgrade upgrade, int upgradeID, FactionEntity upgradeLauncher, bool oneInstance)
        {
            int factionID = upgradeLauncher.FactionID;
            string sourceCode = upgrade.Source.GetCode();
            EntityTypes sourceType = upgrade.Source.Type;

            Assert.IsTrue(upgrade.GetTarget(upgradeID).Type == sourceType, "[UpgradeManager] The upgrade target doesn't have the same type as the upgrade source!");

            if (oneInstance) //if this is a one instance upgrade type
            {
                //if this is a one instance upgrade, make sure that the upgrade source code and the task holder are the same
                if (upgradeLauncher.Type != sourceType || upgradeLauncher.GetCode() != sourceCode)
                {
                    Debug.LogError("[UpgradeManager] Can not launch a one instance upgrade where the upgrade source and the source task launcher are different!");
                    return;
                }

                UpgradeInstance(upgradeLauncher, upgrade.GetTarget(upgradeID), factionID, 
                    gameMgr.GetFaction(factionID).PlayerControlled ? upgrade.GetUpgradeEffect() : null);
            }
            else if(upgrade.CanUpgradeSpawnedInstances()) //if we can upgrade all spawned instances of the source upgrade
            {
                List<FactionEntity> currEntities = upgradeLauncher.FactionMgr.GetFactionEntities().ToList();
                //go through the spawned instances list of the faction:
                foreach (FactionEntity instance in currEntities)
                {
                    //if this building/unit matches the instance to be upgraded
                    if (instance.GetCode() == sourceCode)
                        //upgrade it
                        UpgradeInstance(instance, upgrade.GetTarget(upgradeID), factionID, 
                            gameMgr.GetFaction(factionID).PlayerControlled ? upgrade.GetUpgradeEffect() : null);
                }
            }

            switch(sourceType) //depending on the type of the source
            {
                case EntityTypes.building:

                    if (!oneInstance) //if this is not a one instance upgrade then update the placable buildings list for the player/NPC faction
                    {
                        //is this the local player's faction:
                        if (gameMgr.GetFaction(factionID).PlayerControlled == true)
                            //search for the building instance inside the buildings list that the player is able to place.
                            gameMgr.PlacementMgr.ReplaceBuilding(sourceCode, (Building)upgrade.GetTarget(upgradeID));
                        //& if the faction belongs is NPC:
                        else if (gameMgr.GetFaction(factionID).GetNPCMgrIns() != null)
                            LaunchNPCBuildingUpgrade(
                                gameMgr.GetFaction(factionID).GetNPCMgrIns(),
                                (Building)upgrade.Source, 
                                (Building)upgrade.GetTarget(upgradeID));
                    }

                    //trigger the upgrade event:
                    CustomEvents.OnBuildingUpgraded(upgrade);

                    break;

                case EntityTypes.unit:

                    if (!oneInstance) //if this is not a one instance upgrade then update all source unit type creation tasks
                    {
                        //search for a task that creates the unit to upgrade inside the task launchers
                        List<TaskLauncher> taskLaunchers = gameMgr.GetFaction(factionID).FactionMgr.TaskLaunchers;

                        //go through the active task launchers:
                        foreach (TaskLauncher tl in taskLaunchers)
                        {
                            //and sync the upgraded tasks
                            UpdateUnitCreationTask(tl, sourceCode, (Unit)upgrade.GetTarget(upgradeID), upgrade.GetNewTaskInfo());
                        }

                        //register the upgraded unit creation task:
                        UpgradedUnitTask uut = new UpgradedUnitTask()
                        {
                            factionID = factionID,
                            upgradedUnitCode = sourceCode,
                            targetUnitPrefab = (Unit)upgrade.GetTarget(upgradeID),
                            newTaskInfo = upgrade.GetNewTaskInfo()
                        };
                        //add it to the list:
                        upgradedUnitTasks.Add(uut);

                        //if the faction belongs is NPC:
                        if (gameMgr.GetFaction(factionID).GetNPCMgrIns() != null)
                        {
                            LaunchNPCUnitUpgrade(
                                gameMgr.GetFaction(factionID).GetNPCMgrIns(), 
                                (Unit)upgrade.Source, 
                                (Unit)upgrade.GetTarget(upgradeID));
                        }
                    }

                    //trigger the upgrade event:
                    CustomEvents.OnUnitUpgraded(upgrade);

                    break;
            }

            //trigger upgrades?
            LaunchTriggerUpgrades(upgrade.GetTriggerUpgrades(), upgradeLauncher);
        }

        //trigger unit/building upgrades locally:
        private void LaunchTriggerUpgrades(IEnumerable<Upgrade> upgrades, FactionEntity upgradeLauncher)
        {
            foreach (Upgrade u in upgrades)
                LaunchUpgrade(u, 0, upgradeLauncher, false); //will trigger the upgrade for the first target only!
        }

        //a method that upgrades a faction entity instance locally
        public void UpgradeInstance (FactionEntity instance, FactionEntity target, int factionID, EffectObj upgradeEffect)
        {
            switch(instance.Type)
            {
                case EntityTypes.building:

                    //create upgraded instance of the building
                    gameMgr.BuildingMgr.CreatePlacedInstance(
                        (Building)target,
                        instance.transform.position,
                        target.transform.rotation.eulerAngles.y, 
                        ((Building)instance).CurrentCenter, 
                        factionID, 
                        true, 
                        ((Building)instance).FactionCapital);

                    CustomEvents.OnBuildingInstanceUpgraded((Building)instance); //trigger custom event
                    break;
                    
                case EntityTypes.unit:

                    //create upgraded instance of the unit
                    gameMgr.UnitMgr.CreateUnit(
                        (Unit)target, 
                        instance.transform.position, 
                        factionID, 
                        null);

                    CustomEvents.OnUnitInstanceUpgraded((Unit)instance); //trigger custom event
                    break;

            }

            //if there's a valid upgrade effect assigned:
            if (upgradeEffect != null)
                //show the upgrade effect for the player:
                gameMgr.EffectPool.SpawnEffectObj(upgradeEffect, instance.transform.position, upgradeEffect.transform.rotation);

            instance.EntityHealthComp.DestroyFactionEntity(true); //destroy the instance
        }

        //a method called that configures NPC faction components in case of a building upgrade:
        private void LaunchNPCBuildingUpgrade (NPCManager npcMgrIns, Building source, Building target)
        {
            //we need access to the NPC Building Creator in order to find the active regulator instance that manages the building type to be upgraded:
            NPCBuildingCreator buildingCreator_NPC = npcMgrIns.buildingCreator_NPC;

            NPCBuildingRegulator buildingRegulator = npcMgrIns.GetBuildingRegulatorAsset(source); //will hold the building's regulator that is supposed to be upgraded.
            NPCBuildingRegulator targetBuildingRegulator = npcMgrIns.GetBuildingRegulatorAsset(target); ; //will hold the target building's regulator.

            //we expect both above regulators to be valid:
            if (buildingRegulator == null)
            {
                Debug.LogError("[Upgrade Manager] Can not find a valid NPC Building Regulator for the upgrade source.");
                return;
            }
            if (targetBuildingRegulator == null)
            {
                Debug.LogError("[Upgrade Manager] Can not find a valid NPC Building Regulator for the upgrade target.");
                return;
            }

            //destroy the old building regulator
            buildingCreator_NPC.DestroyActiveRegulator(buildingRegulator);

            //if the building to be upgraded was either, the main population building or the main center building
            //then we'll update that as well.
            if (buildingRegulator == npcMgrIns.populationManager_NPC.populationBuilding)
            {
                npcMgrIns.populationManager_NPC.populationBuilding = targetBuildingRegulator;
                npcMgrIns.populationManager_NPC.ActivatePopulationBuilding(); //activate the new population building regulator.
            }
            if (buildingRegulator == npcMgrIns.territoryManager_NPC.centerRegulator)
            {
                npcMgrIns.territoryManager_NPC.centerRegulator = targetBuildingRegulator; //assign new regulator for the center building
                npcMgrIns.territoryManager_NPC.ActivateCenterRegulator(); //activate the new regulator.
            }

            //activate the new regulator:
            buildingCreator_NPC.ActivateBuildingRegulator(targetBuildingRegulator);
        }

        //a method called that configures NPC faction components in case of a unit upgrade:
        private void LaunchNPCUnitUpgrade(NPCManager npcMgrIns, Unit source, Unit target)
        {
            //we need access to the NPC Unit Creator in order to find the active regulator instance that manages the unit type to be upgraded:
            NPCUnitCreator unitCreator_NPC = npcMgrIns.unitCreator_NPC;

            NPCUnitRegulator unitRegulator = npcMgrIns.GetUnitRegulatorAsset(source); //will hold the unit's regulator that is supposed to be upgraded.
            NPCUnitRegulator targetUnitRegulator = npcMgrIns.GetUnitRegulatorAsset(target); ; //will hold the target unit's regulator

            //we expect both above regulators to be valid:
            if (unitRegulator == null)
            {
                Debug.LogError("[Upgrade Manager] Can not find a valid NPC Unit Regulator for the upgrade source.");
                return;
            }
            if (targetUnitRegulator == null)
            {
                Debug.LogError("[Upgrade Manager] Can not find a valid NPC Unit Regulator for the upgrade target.");
                return;
            }

            //destroy the old building regulator
            unitCreator_NPC.DestroyActiveRegulator(unitRegulator);

            //if the unit to be upgraded was either, the main builder, collector or one of the army units
            //then we'll update that as well.
            if (unitRegulator == npcMgrIns.buildingConstructor_NPC.builderRegulator)
            {
                npcMgrIns.buildingConstructor_NPC.builderRegulator = unitRegulator;
                npcMgrIns.buildingConstructor_NPC.ActivateBuilderRegulator(); //activate the new unit regulator
            }
            if (unitRegulator == npcMgrIns.resourceCollector_NPC.collectorRegulator)
            {
                npcMgrIns.resourceCollector_NPC.collectorRegulator = unitRegulator;
                npcMgrIns.resourceCollector_NPC.ActivateCollectorRegulator(); //activate the new unit regulator
            }
            if (npcMgrIns.armyCreator_NPC.armyUnitRegulators.Contains(unitRegulator)) //is the unit to upgrade an army unit?
            {
                npcMgrIns.armyCreator_NPC.armyUnitRegulators.Remove(unitRegulator); //remove old regulator from list
                npcMgrIns.armyCreator_NPC.armyUnitRegulators.Add(targetUnitRegulator); //add new regulator asset
                npcMgrIns.armyCreator_NPC.ActivateArmyUnitRegulators(); //activate army regulators.
            }

            //activate the new regulator:
            unitCreator_NPC.ActivateUnitRegulator(targetUnitRegulator);
        }

        //called whenever a task launcher is added
        private void OnTaskLauncherAdded (TaskLauncher taskLauncher, int taskID, int taskQueueID)
        {
            SyncUnitCreationTasks(taskLauncher); //sync the upgraded unit creation tasks
        }

        //sync all upgraded unit creation tasks for a task launcher:
        private void SyncUnitCreationTasks (TaskLauncher taskLauncher)
        {
            //go through the registered upgraded unit tasks
            foreach(UpgradedUnitTask uut in upgradedUnitTasks)
            {
                //if this task launcher belongs to the faction ID that has the upgraded unit creation task:
                if (uut.factionID == taskLauncher.FactionEntity.FactionID)
                {
                    //sync the unit creation tasks.
                    UpdateUnitCreationTask(taskLauncher, uut.upgradedUnitCode, uut.targetUnitPrefab, uut.newTaskInfo);
                }
            }
        }

        //update an upgraded unit creation task's info:
        private void UpdateUnitCreationTask (TaskLauncher taskLauncher, string upgradedUnitCode, Unit targetUnitPrefab, Upgrade.NewTaskInfo newTaskInfo)
        {
            //go through the tasks:
            for(int i = 0; i < taskLauncher.GetTasksCount(); i++)
                taskLauncher.GetTask(i).Update(upgradedUnitCode, targetUnitPrefab, newTaskInfo);
        }
    }
}

