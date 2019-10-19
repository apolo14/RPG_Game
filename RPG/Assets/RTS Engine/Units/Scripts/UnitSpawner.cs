using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RTSEngine
{
    public class UnitSpawner : MonoBehaviour
    {
        [SerializeField]
        private Unit[] units = new Unit[0]; //an array of units from which one will be chosen randomly each time to be spawned.
        [SerializeField]
        private bool playerFaction = false;
        [SerializeField]
        private int factionID = 0;
        [SerializeField]
        private bool freeUnits = false;
        [SerializeField]
        private bool updatePopulation = false;

        [SerializeField]
        private Transform[] spawnPoints = new Transform[0]; //an array of transform components from which one will be chosen each time to spawn the unit at

        [SerializeField]
        private IntRange amountRange = new IntRange(1, 5); //a random target amount between the min and max amount specified in the range will be the amount to spawn from the chosen units
        private int targetAmount;

        [SerializeField]
        private FloatRange spawnReloadRange = new FloatRange(1.0f, 5.0f); //how often will the units be spawned?
        private float spawnReload;

        [SerializeField]
        private GameManager gameMgr;

        private void Awake()
        {
            targetAmount = amountRange.getRandomValue(); //set the target amount of units to spawn
            spawnReload = spawnReloadRange.getRandomValue();

            if (gameMgr == null) //if the Game Manager component hasn't been set, get it
                gameMgr = FindObjectOfType(typeof(GameManager)) as GameManager;
        }

        private void Update()
        {
            if (gameMgr == null || targetAmount <= 0)
                return;

            if (spawnReload > 0)
                spawnReload -= Time.deltaTime;
            else
            {
                //create a new unit using the chosen settings
                gameMgr.UnitMgr.CreateUnit(
                    units[Random.Range(0, units.Length)],
                    spawnPoints[Random.Range(0, spawnPoints.Length)].position,
                    playerFaction ? GameManager.PlayerFactionID : factionID,
                    null,
                    freeUnits,
                    updatePopulation
                    );

                targetAmount--;
                spawnReload = spawnReloadRange.getRandomValue();
            }
        }
    }
}
