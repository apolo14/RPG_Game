using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/* Attack Manager script created by Oussama Bouanani, SoumiDelRio.
 * This script is part of the Unity RTS Engine */

namespace RTSEngine
{
    public class AttackManager : MonoBehaviour
    {
        [System.Serializable]
        public class UnitAttackRange
        {
            [SerializeField]
            private string code = "attack_range_code"; //unique code for this unit attack range
            public string GetCode () { return code; }

            [SerializeField]
            private float unitStoppingDistance = 2.0f; //stopping distance for target units
            [SerializeField]
            private float buildingStoppingDistance = 5.0f; //stopping distance when the unit has a target building to attack
            [SerializeField]
            private float noTargetStoppingDistance = 5.0f; //stopping distance when the unit is launching an attack without a target assigned.
            
            //get either the unit/building stopping distance or the no target stopping distance depending on the input target type
            public float GetStoppingDistance (EntityTypes targetType)
            {
                switch(targetType)
                {
                    case EntityTypes.unit:
                        return unitStoppingDistance;
                    case EntityTypes.building:
                        return buildingStoppingDistance;
                    default:
                        return noTargetStoppingDistance;
                }
            }

            [SerializeField]
            private float moveOnAttackOffset = 3.0f; //when the attack unit can move and attack, the range of attack increases by this value
            public float GetMoveOnAttackOffset () { return moveOnAttackOffset; }

            [SerializeField]
            private float updateMvtDistance = 2.0f; //if the unit is moving towards a target and it changes its position by more than this distance, the attacker's movement will be recalculated
            public float GetUpdateMvtDistance () { return updateMvtDistance; }

            [SerializeField]
            private MovementManager.Formations movemnetFormation = MovementManager.Formations.circular; //the movement formation that units from this range type will have when moving to attack
            public MovementManager.Formations GetFormation() { return movemnetFormation; }
        }
        [SerializeField]
        private UnitAttackRange[] rangeTypes = new UnitAttackRange[0];

        //when attack units which do not require a target are selected and the following key is held down by the player, a terrain attack can be launched
        [SerializeField]
        private bool terrainAttackEnabled = true;
        [SerializeField]
        private KeyCode terrainAttackKey = KeyCode.T;

        //returns the unit attack range type:
        public UnitAttackRange GetRangeType(string code)
        {
            foreach(UnitAttackRange uar in rangeTypes)
                if (uar.GetCode() == code) //if the code matches, return a pointer to the range type
                    return uar;
            return null;
        }

        GameManager gameMgr;

        public void Init (GameManager gameMgr)
        {
            this.gameMgr = gameMgr;
        }

        //a method called to launch a terrain attack
        public bool LaunchTerrainAttack (List<Unit> units, Vector3 attackPosition, bool direct = false)
        {
            //when direct is set to true, it will ignore whether or not the player is holding down the terrain attack key
            //if the terrain attack feature is disabled or the trigger key isn't pressed by the player
            if (!terrainAttackEnabled || ( !direct && !Input.GetKey(terrainAttackKey) ))
                return false;

            //get the units which do have an attack component and which do not require a target to be assigned
            List<Unit> attackUnits = units.Where(unit => unit.AttackComp != null && !unit.AttackComp.RequireTarget()).ToList();

            if (attackUnits.Count > 0) //if there are still units allowed to launch a terrain attack
            {
                gameMgr.MvtMgr.LaunchAttack(attackUnits, null, attackPosition, MovementManager.AttackModes.full, true);
                return true;
            }

            return false;
        }
    }
}