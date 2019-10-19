using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RTSEngine
{
    public class ScenarioLoader : MonoBehaviour
    {
        //this component is an example on how to set up a campaign menu where the player can load maps to play scenarios in

        [System.Serializable]
        public struct ScenarioScene
        {
            public Scenario source; //the scenario that will be loaded in the target scene
            public string sceneName; //the target scene that will be loaded where the player will go through the missions of the above scenario
        }
        [SerializeField]
        private ScenarioScene[] scenarios = new ScenarioScene[0]; //campaign length = this array's length.
        //each scenario in this array is locked if the previous scenario hasn't been completed (except the first one)
        public Scenario LoadedSceneario { private set; get; } //when the map scene is loaded, the Game Manager refers to this field to get the scenario to play

        [SerializeField]
        private ScenarioMenuUI scenarioUIPrefab = null; //each scenario will be displayed using a ScenarioMenuUI
        [SerializeField]
        private GridLayoutGroup scenarioUIParent = null; //parent object with GridLayoutGroup component that includes scenario UI elements
        private List<ScenarioMenuUI> scenarioUIList = new List<ScenarioMenuUI>();

        //main menu
        [SerializeField]
        private string mainMenuScene = "main_menu";
        public void LoadMainMenu() { SceneManager.LoadScene(mainMenuScene); }

        private void Start()
        {
            Dictionary<string, bool> savedScenarios = MissionSaveLoad.LoadScenarios(); //get the saved scenarios

            //start by creating the assigned scenarios
            for(int i = 0; i < scenarios.Length; i++)
            {
                ScenarioMenuUI nextScenarioUI = Instantiate(scenarioUIPrefab.gameObject, scenarioUIParent.transform).GetComponent<ScenarioMenuUI>(); //create a new scenario UI menu element
                scenarioUIList.Add(nextScenarioUI);

                nextScenarioUI.Init(this, i); //initialise it

                bool unlocked = true;
                if (i > 0) //only if this is not the first scenario in the list
                    savedScenarios.TryGetValue(scenarios[i-1].source.GetCode(), out unlocked); //check if it's unlocked or not

                nextScenarioUI.Refresh(scenarios[i].source.GetName(), scenarios[i].source.GetDescription(), unlocked); //refresh the content of the scenario UI menu
            }
        }

        //load a map's scene with the scenario under the input index
        public void Load(int index)
        {
            DontDestroyOnLoad(this); //we need to move this object to move to the target scene

            LoadedSceneario = scenarios[index].source; //assign the scenario to play in the target map scene
            SceneManager.LoadScene(scenarios[index].sceneName); //load the associated scene 
        }

        //resets the player progress on all saved scenarios:
        public void Reset()
        {
            MissionSaveLoad.ClearSavedScenarios();

            for(int i = 1; i < scenarios.Length; i++) //go through all the scenario UI elements (except the first one)
                //make the buttons interactable so that none of them can be loaded
                scenarioUIList[i].Refresh(scenarios[i].source.GetName(), scenarios[i].source.GetDescription(), false);
        }
    }
}
