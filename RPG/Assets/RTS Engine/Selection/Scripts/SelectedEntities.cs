using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RTSEngine
{
    public enum SelectionTypes {single, multiple}; //type of adding selection

    [System.Serializable]
    public class SelectedEntities
    {
        private List<SelectionEntity> selectedList = new List<SelectionEntity>();
        private EntityTypes lastEntityType = EntityTypes.none; //keep the last selected entity type in mind
        private bool currExclusive = false; //is the current selection type exclusive?

        //each entity type has their own selection options:
        [System.Serializable]
        public struct SelectionOptions
        {
            public EntityTypes entityType;
            public bool allowMultiple; //allow the associated entity type to be multiply selected
            public bool exclusive; //when true, then the associated entity type can't be selected with other entities
        }
        [SerializeField]
        private SelectionOptions[] selectionOptions = new SelectionOptions[0];

        SelectionManager manager;

        public void Init (SelectionManager manager) //method to init this instance
        {
            this.manager = manager;
        }

        public bool IsSelected(SelectionEntity entity) {
            return selectedList.Contains(entity);
        } //is the input entity selected?

        public int GetCount()
        {
            return selectedList.Count; //get the amount of currently selected entites
        }
        
        //get a list of the selected entities of a certain type
        public List<Entity> GetEntities (EntityTypes type, bool exclusive, bool playerFaction)
        {
            List<Entity> entities = new List<Entity>();
            foreach(SelectionEntity entity in selectedList)
            {
                if (entity.FactionEntity != null) //there's a faction entity component
                {
                    if (playerFaction && entity.FactionEntity.FactionID != GameManager.PlayerFactionID) //if we requested player faction units only and this entity doesn't belong to player's faction
                        return new List<Entity>();
                }

                if(type == EntityTypes.none || entity.Source.Type == type) //if this matches the type we're looking for
                {
                    entities.Add(entity.Source); //add to list
                    continue;
                }

                if (exclusive == true) //the entity is not a unit, return an empty list 
                    return new List<Entity>();
            }

            return entities;
        }

        //returns the requested type if there's only one selected
        public Entity GetSingleEntity (EntityTypes type, bool playerFaction)
        {
            if (selectedList.Count != 1 || (selectedList[0].Source.Type != type && type != EntityTypes.none)) //not one single entity is selected or the type does not match the input
                return null;

            //if we're requesting this single entity to be in the player's faction
            if (playerFaction && selectedList[0].FactionEntity != null && selectedList[0].FactionEntity.FactionID != GameManager.PlayerFactionID)
                return null;

            return selectedList[0].Source;
        }

        //is the input selection entity is selected?
        public virtual bool Contains (Entity entity)
        {
            SelectionEntity selectionEntity = entity.GetSelection();
            foreach (SelectionEntity e in selectedList)
                if (e == selectionEntity)
                    return true;

            return false;
        }

        //add an entity to the selection
        public virtual bool Add (SelectionEntity newEntity, SelectionTypes type)
        {
            if (newEntity == null) //invalid entity
                return false;

            if (manager.MultipleSelectionKeyDown == true) //if the player is holding down the multiple selection key
                type = SelectionTypes.multiple; //multiple selection incoming

            if (currExclusive && newEntity.Source.Type != lastEntityType) //if the last selection type was exclusive and this doesn't match the last selected entity type
                type = SelectionTypes.single; //single selection now (and all previous elements will be deselected)

            bool exclusiveOnSuccess = false; //will the selection be marked as exclusive in case the entity is successfully selected? by default no

            foreach(SelectionOptions options in selectionOptions) //go through all the selection options
            {
                if (newEntity.Source.Type == options.entityType) //if the entity type matches
                {
                    if (options.exclusive == true) //if this entity type can be selected only exclusively
                    {
                        exclusiveOnSuccess = true; //mark selection as exclusive on success

                        if(newEntity.Source.Type != lastEntityType) //the last selected entity does not match with the current type
                            type = SelectionTypes.single; //all previous selected elements will be deselected
                    }

                    if (type == SelectionTypes.multiple && options.allowMultiple == false) //if the selection type is multiple but that's not allowed for this entity type
                        type = SelectionTypes.single; //set type back to single to deselect previous elements

                    break; //entity type match found, no need to see the rest of the options
                }
            }

            switch(type)
            {
                case SelectionTypes.single: //single selection

                    RemoveAll(); //remove all selected entities
                    break;
                case SelectionTypes.multiple: //multiple selection:

                    if (manager.MultipleSelectionKeyDown && IsSelected(newEntity)) //if the multiple selection key is down & entity is already selected, selecting a new entity -> removing it from the already selected group
                    {
                        Remove(newEntity);
                        return false;
                    }
                    break;
            }

            if(newEntity.CanSelect() == true && IsSelected(newEntity) == false) //can be selected and not already selected? 
            {
                selectedList.Add(newEntity); //only new entity is selected

                int selectionCount = selectedList.Count;
                if (selectedList.Count == 1) //first entity to get selected?
                    selectedList[0].IsSelectedOnly = true; //mark as selected only
                else if (selectionCount == 2) //only one entity was selected before
                    selectedList[0].IsSelectedOnly = false; //not the only selected entity any more.

                newEntity.OnSelected();

                lastEntityType = newEntity.Source.Type; //set the last selected entity type
                currExclusive = exclusiveOnSuccess; //is the selection exclusive now?

                return true;
            }

            return false;
        }

        //remove an entity from the selected list
        public void Remove (SelectionEntity entity)
        {
            selectedList.Remove(entity);
            entity.OnDeselected();
        }

        //remove all selected entities from the selected list
        public void RemoveAll ()
        {
            while(selectedList.Count > 0) //go through all selected entities and deselect them
            {
                SelectionEntity currEntity = selectedList[0];
                selectedList.RemoveAt(0);
                currEntity.OnDeselected();
            }
        }
    } 
}
