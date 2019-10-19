using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/* NPC Attack Manager script created by Oussama Bouanani, SoumiDelRio.
 * This script is part of the Unity RTS Engine */

namespace RTSEngine
{
    public class NPCAttackManager : NPCComponent
    {
        //picking an attack target:
        public bool canAttack = true; //can this faction attack?
        public bool pickWeakestFaction = true; //will this faction pick the weakest opponent to attack?
        public FloatRange setTargetFactionDelay = new FloatRange(10, 15); //target faction will only be set after this delay is done
        private float setTargetFactionTimer;
        private FactionManager targetFaction; //the faction manager component of the target faction.

        //launching the attack:
        private bool isAttacking = false; //is the faction currently in an attack?

        public bool IsAttacking() { return isAttacking; } //get if the attack manager is attacking or not.

        //timer at which the faction decides to attack target faction or not:
        public FloatRange launchAttackReloadRange = new FloatRange(10.0f, 15.0f);
        private float launchAttackTimer;

        //the launch attack power is required to have as the current attack power in order to launch an attack on another faction.
        public IntRange launchAttackPowerRange = new IntRange(300, 400);

        //attacking:
        //whenever the below timer is through, this component will point army units to a target.
        public FloatRange attackOrderReloadRange = new FloatRange(3.0f, 7.0f);
        private float attackOrderTimer;

        private Vector3 lastAttackPos; //the last position of the target building in the attack
        //list of units that will participate in the attack:
        private List<Unit> currentAttackUnits = new List<Unit>();

        //a list of the buildings codes that this faction will attempt to attack:
        public List<Building> targetBuildings = new List<Building>();
        private List<string> targetBuildingCodes = new List<string>(); //codes of the above buildings will be saved here.

        private FactionEntity currentTarget; //the current faction entity that this faction is attempting to destroy.

        //when the faction's army attack power goes below this value while the faction attacking another one, then a retreat will take place:
        public IntRange surrenderAttackPowerRange = new IntRange(100, 200);

        public override void Init(GameManager gameMgr, NPCManager npcMgr, FactionManager factionMgr)
        {
            base.Init(gameMgr, npcMgr, factionMgr);

            //if we can't attack then disable this component:
            if (canAttack == false)
                enabled = false;
            else
            {
                targetFaction = null;
                setTargetFactionTimer = setTargetFactionDelay.getRandomValue(); //start the set attack target timer.
            }

            //start timers:
            setTargetFactionTimer = setTargetFactionDelay.getRandomValue();
            launchAttackTimer = launchAttackReloadRange.getRandomValue();

            //assign target buildings codes:
            UpdateTargetBuildingCodes();

            //start listening to events:
            CustomEvents.UnitDead += OnUnitDead;
            CustomEvents.UnitConversionComplete += OnUnitConverted;
            CustomEvents.FactionEliminated += OnFactionEliminated;
        }

        private void OnDisable()
        {
            //stop listening to events:
            CustomEvents.UnitDead -= OnUnitDead;
            CustomEvents.UnitConversionComplete -= OnUnitConverted;
            CustomEvents.FactionEliminated -= OnFactionEliminated;
        }

        //called whenever a unit is dead:
        void OnUnitDead (Unit unit)
        {
            //if the unit is in the current attack units list, it will be removed:
            currentAttackUnits.Remove(unit);
        }

        //called whenever a unit is converted:
        void OnUnitConverted (Unit converter, Unit target)
        {
            //if the unit is in the current attack units list, it will be removed:
            currentAttackUnits.Remove(target);
        }

        //called whenever a faction is eliminated:
        void OnFactionEliminated (FactionSlot factionInfo)
        {
            //if this is the current target faction?
            if(factionInfo.FactionMgr == targetFaction)
            {
                CancelAttack(); //cancel the attack
            }
        }

        //a method to assign codes of the target buildings list in another list:
        void UpdateTargetBuildingCodes ()
        {
            targetBuildingCodes.Clear();

            //go through the target buildings
            foreach(Building b in targetBuildings)
            {
                if (targetBuildingCodes.Contains(b.GetCode()) == false) //if the building's code doesn't already exist.
                    targetBuildingCodes.Add(b.GetCode());
            }
        }

        void Update()
        {
            //this component is only active if the peace time is over:
            if (gameMgr.InPeaceTime())
                return; 

            SetTargetProgress();

            LaunchAttackProgress();

            AttackProgress();
        }

        //a method that runs the set target faction timer in order to assign a new target faction
        void SetTargetProgress ()
        {
            //as long as there's no target faction assigned & peace time is over:
            if (targetFaction == null && !gameMgr.InPeaceTime())
            {
                //setting the attack target timer:
                if (setTargetFactionTimer > 0)
                    setTargetFactionTimer -= Time.deltaTime;
                else
                {
                    setTargetFactionTimer = setTargetFactionDelay.getRandomValue(); //reload the set attack target timer.
                    SetTargetFaction(); //find a target faction.
                }
            }
        }

        //a method to set a target faction:
        void SetTargetFaction()
        {
            //first get the factions that are not yet defeated in a list:
            List<FactionSlot> activeFactions = gameMgr.GetFactions().Where(faction => !faction.Lost && faction.FactionMgr != factionMgr).ToList();

            //remove the defeated factions and pick the weakest:
            FactionManager weakestFaction = null;

            if (pickWeakestFaction == true) //if we're picking the weakest faction as the target:
            {
                foreach(FactionSlot faction in activeFactions)
                {
                    //look for weakest faction:
                    if (weakestFaction == null || weakestFaction.GetCurrentAttackPower() > faction.FactionMgr.GetCurrentAttackPower())
                        weakestFaction = faction.FactionMgr;
                }
            }

            //pick weakest faction or random faction:
            targetFaction = (pickWeakestFaction == true) ? weakestFaction : activeFactions[Random.Range(0, activeFactions.Count)].FactionMgr;
        }

        void LaunchAttackProgress ()
        {
            //launching an attack:
            //if we aren't currently in an attack and there's a valid target faction and the faction isn't defending its territory:
            if (IsAttacking() == false && targetFaction != null && npcMgr.defenseManager_NPC.IsDefending() == false)
            {
                //launch attack timer:
                if (launchAttackTimer > 0)
                    launchAttackTimer -= Time.deltaTime;
                else
                {
                    //reload timer:
                    launchAttackTimer = launchAttackReloadRange.getRandomValue();

                    //does the NPC faction has enough attacking power to launch attack?
                    if(factionMgr.GetCurrentAttackPower() > launchAttackPowerRange.getRandomValue())
                    {
                        //launch attack:
                        LaunchAttack();
                    }
                }
            }
        }

        //method to launch the attack:
        void LaunchAttack ()
        {
            //making sure there's a valid target faction:
            if (targetFaction == null)
                return;

            //mark as attacking:
            isAttacking = true;

            //clear the current attack units list:
            currentAttackUnits.Clear();
            currentAttackUnits.AddRange(factionMgr.GetAttackUnits(1- npcMgr.defenseManager_NPC.defenseRatioRange.getRandomValue())); //get the required units for this attack.

            //we'll be searching for the next building to attack starting from the last attack pos, initially set it as the capital building
            lastAttackPos = gameMgr.GetFaction(factionMgr.FactionID).CapitalPosition;

            currentTarget = null;
            //pick a target building:
            SetTargetEntity(targetFaction.GetBuildings().Cast<FactionEntity>());

            //start the attack order timer:
            attackOrderTimer = attackOrderReloadRange.getRandomValue();
        }

        //a method that picks a target entity to attack:
        private bool SetTargetEntity (IEnumerable<FactionEntity> factionEntities)
        {
            //search the target faction's entities and see if there's a match:
            float lastDistance = 0; //we wanna get the closest entity to the last attack position:

            foreach(FactionEntity entity in factionEntities)
            {
                //if the building is valid and not destroyed yet
                if (entity != null && entity.EntityHealthComp.IsDestroyed == false)
                {
                    //and the building's code matches or this is an eliminate all game:
                    if (gameMgr.GetDefeatCondition() == DefeatConditions.eliminateAll || targetBuildingCodes.Contains(entity.GetCode()))
                    {
                        //get the closest building:
                        if (currentTarget == null || Vector3.Distance(currentTarget.transform.position, lastAttackPos) < lastDistance)
                        {
                            currentTarget = entity;
                            lastDistance = Vector3.Distance(entity.transform.position, lastAttackPos);
                        }
                    }
                }
            }

            return currentTarget != null;
        }

        //when the NPC faction is attacking:
        void AttackProgress ()
        {
            //if we're attacking and there's a valid target faction:
            if(isAttacking == true && targetFaction != null)
            {
                //attack order timer:
                if (attackOrderTimer > 0)
                    attackOrderTimer -= Time.deltaTime;
                else
                {
                    //reload attack order timer:
                    attackOrderTimer = attackOrderReloadRange.getRandomValue();

                    //did the current attack power hit the surrender attack power?
                    if(factionMgr.GetCurrentAttackPower() <= surrenderAttackPowerRange.getRandomValue())
                    {
                        CancelAttack();
                        return; //do not proceed.
                    }

                    //does the faction has a target.
                    if (currentTarget != null)
                    {
                        //attack it:
                        AttackTarget();
                    }
                    //if it doesn't have one yet, start by search for a building to attack, if none is found then look for a target unit (if defeat condition is set to eliminate all)
                    else if (SetTargetEntity(targetFaction.GetBuildings().Cast<FactionEntity>()) == false
                        && gameMgr.GetDefeatCondition() == DefeatConditions.eliminateAll)
                        SetTargetEntity(targetFaction.GetUnits().Cast<FactionEntity>());
                }
            }
        }

        //attack the assigned target.
        void AttackTarget ()
        {
            //making sure that the NPC faction in an attack in progress and that there's a valid target:
            if (currentTarget == null || isAttacking == false)
                return;

            MovementManager.AttackModes attackMode = MovementManager.AttackModes.none;

            Building targetBuilding = currentTarget.Type == EntityTypes.building ? (Building)currentTarget : null; //is this target a building?
            
            //if the current target is a building and it is being constructed:
            if(targetBuilding != null && targetBuilding.WorkerMgr.currWorkers > 0)
            {
                Unit[] workersList = targetBuilding.WorkerMgr.GetAll(); //get all workers in the worker manager
                //attack the workers first: go through the workers positions
                for (int i = 0; i < workersList.Length; i++)
                    //find worker:
                    if (workersList[i] != null && workersList[i].BuilderComp.IsInProgress() == true)
                    {
                        //assign it as target.
                        currentTarget = workersList[i];
                        //force attack units to attack it:
                        attackMode = MovementManager.AttackModes.change;
                    }
            }

            //launch the actual attack:
            gameMgr.MvtMgr.LaunchAttack(currentAttackUnits, currentTarget, currentTarget.GetSelection().transform.position, attackMode, false);
        }

        //a method that checks if a unit is part of the attacking army or not:
        public bool IsUnitDeployed (Unit unit)
        {
            return currentAttackUnits.Contains(unit);
        }

        //a method to cancel the attack:
        public void CancelAttack ()
        {
            //send back units:
            npcMgr.defenseManager_NPC.SendBackUnits(currentAttackUnits);

            //clear the current attack units:
            currentAttackUnits.Clear();

            currentTarget = null; //reset the target.

            targetFaction = null;

            //stop attacking:
            isAttacking = false;
        }
    }
}
