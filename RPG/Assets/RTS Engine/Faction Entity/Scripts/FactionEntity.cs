using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if RTSENGINE_FOW
using FoW;
#endif

namespace RTSEngine
{
    public abstract class FactionEntity : Entity
    {
        [SerializeField]
        protected int factionID = 0; //the faction ID that this entity belongs to.
        public int FactionID { set { factionID = value; } get { return factionID; } }
        public FactionManager FactionMgr { set; get; } //the faction manager that this entity belongs to.

        [System.Serializable]
        public struct ColoredRenderer
        {
            public Renderer renderer;
            public int materialID;

            //a method that updates the renderer's material color
            public void UpdateColor (Color color)
            {
                renderer.materials[materialID].color = color;
            }
        }
        [SerializeField]
        private ColoredRenderer[] coloredRenderers = new ColoredRenderer[0]; //The materials of the assigned Renderer components in this array will be colored by the faction entity's faction color

        //double clicking on the unit allows to select all units of the same type within a certain range
        private float doubleClickTimer;
        private bool clickedOnce = false;

        public TaskLauncher TaskLauncherComp { private set; get; }
        public APC APCComp { private set; get; }
        public MultipleAttackManager MultipleAttackMgr { private set; get; }
        public FactionEntityHealth EntityHealthComp { private set; get; }

#if RTSENGINE_FOW
        public FogOfWarUnit FoWUnit { private set; get; }
#endif

        public abstract void UpdateAttackComp(AttackEntity attackEntity);

        //initialize the faction entity
        public virtual void Init(GameManager gameMgr, int fID, bool free)
        {
            base.Init(gameMgr);

            //get the components that are attached to the faction entity:
            TaskLauncherComp = GetComponent<TaskLauncher>();
            APCComp = GetComponent<APC>();
            MultipleAttackMgr = GetComponent<MultipleAttackManager>();
            EntityHealthComp = GetComponent<FactionEntityHealth>();

            //initialize these components
            //task launcher must be initialized separately on units and buildings
            if (APCComp)
                APCComp.Init(gameMgr, this);
            if (MultipleAttackMgr)
                MultipleAttackMgr.Init(this);
            EntityHealthComp.Init(gameMgr, this);

#if RTSENGINE_FOW
            FoWUnit = GetComponent<FogOfWarUnit>();
#endif

            selection.FactionEntity = this; //assign as the selection's source faction entity

            //initial settings for the double click
            clickedOnce = false;
            doubleClickTimer = 0.0f;

            this.free = free;

            if (this.free == false) //if the entity belongs to a faction
            {
                factionID = fID; //set the faction ID.
                FactionMgr = gameMgr.GetFaction(factionID).FactionMgr; //get the faction manager
                UpdateFactionColors(gameMgr.GetFaction(factionID).GetColor()); //update the faction colors on the unit
            }
            else
                factionID = -1;

        }

        //method called to set a faction entity's faction colors:
        protected void UpdateFactionColors(Color newColor)
        {
            color = newColor; //set the faction color

            foreach (ColoredRenderer cr in coloredRenderers) //go through all renderers that can be colored
                cr.UpdateColor(color);
        }

        protected override void Update()
        {
            base.Update();

            //double click timer:
            if (clickedOnce == true)
            {
                if (doubleClickTimer > 0)
                    doubleClickTimer -= Time.deltaTime;
                if (doubleClickTimer <= 0)
                    clickedOnce = false;
            }
        }

        //a method that is called when a mouse click on this unit is detected
        public virtual void OnMouseClick()
        {
            if (clickedOnce == false)
            { //if the player hasn't clicked on this portal shortly before this click
                DisableSelectionFlash(); //disable the selection flash

                if (gameMgr.SelectionMgr.MultipleSelectionKeyDown == false) //if the player doesn't have the multiple selection key down (not looking to select multiple units one by one)
                { 
                    //start the double click timer
                    doubleClickTimer = 0.5f;
                    clickedOnce = true;
                }
            }
            else //if this is the second click (double click), select all units of the same type within a certain range
                gameMgr.SelectionMgr.SelectFactionEntitiesInRange(this);
        }

        //a method called to disable the faction entity related components
        public virtual void Disable(bool destroyed)
        {
            if (TaskLauncherComp != null)  //if the unit has a task manager and there are pending tasks there
                TaskLauncherComp.CancelAllInProgressTasks(); //cancel all the in progress tasks

            if (APCComp != null) //if this unit has an APC component attached to it
                APCComp.EjectAll(destroyed); //remove all the units stored in the APC
        }

    }
}
