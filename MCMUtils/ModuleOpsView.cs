using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

namespace WildBlueIndustries
{
    public delegate void NextModule();
    public delegate void PrevModule();
    public delegate void NextPreviewModule(string templateName);
    public delegate void PrevPreviewModule(string templateName);
    public delegate void ChangeModuleType(string templateName);
    public delegate string GetModuleInfo(string templateName);
    public delegate Texture GetModuleLogo(string templateName);

    class ModuleOpsView : Window<ModuleOpsView>
    {
        public List<PartModule> converters = null;
        public PartModule mks;
        public Part part;
        public PartResourceList resources;
        public string shortName;
        public string nextName, nextRequirements;
        public string prevName, prevRequirements;
        public string previewName, previewRequirements;
        public NextModule nextModuleDelegate = null;
        public PrevModule prevModuleDelegate = null;
        public NextPreviewModule nextPreviewDelegate = null;
        public PrevPreviewModule prevPreviewDelegate = null;
        public ChangeModuleType changeModuleTypeDelegate = null;
        public GetModuleInfo getModuleInfoDelegate = null;
        public GetModuleLogo getModuleLogoDelegate = null;

        private Vector2 _scrollPosConverters;
        private Vector2 _scrollPosResources;

        public ModuleOpsView() :
        base("Module Operations Manager (MOM)", 600, 300)
        {
            Resizable = false;
            _scrollPosConverters = new Vector2(0, 0);
            _scrollPosResources = new Vector2(0, 0);
        }

        protected override void DrawWindowContents(int windowId)
        {
            GUILayout.BeginHorizontal();

            //Left pane : Module management controls
            drawModuleManagementPane();

            //Right pane: resource pane
            drawResourcePane();

            GUILayout.EndHorizontal();
        }

        protected void drawResourcePane()
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Resources");

            _scrollPosResources = GUILayout.BeginScrollView(_scrollPosResources, new GUIStyle(GUI.skin.textArea));
            foreach (PartResource resource in this.part.Resources)
            {
                GUILayout.Label(resource.resourceName);
                GUILayout.Label(String.Format("{0:#,##0.00}/{1:#,##0.00}", resource.amount, resource.maxAmount));
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        protected void drawModuleManagementPane()
        {
            string moduleInfo;
            Texture moduleLabel;
            bool governorActive = false;

            GUILayout.BeginVertical(GUILayout.MaxWidth(350f));

            //Efficiency Label
            GUILayout.Label(shortName + " efficiency: " + Utils.GetField("efficiency", mks));

            //More info button to display detailed info on the module
            if (GUILayout.Button("More Info"))
            {
                if (getModuleInfoDelegate != null)
                {
                    moduleInfo = getModuleInfoDelegate(shortName);
                    ModuleInfoView modSummary = new ModuleInfoView();
                    modSummary.ModuleInfo = moduleInfo;
                    if (this.getModuleLogoDelegate != null)
                    {
                        moduleLabel = getModuleLogoDelegate(shortName);
                        modSummary.moduleLabel = moduleLabel;
                    }
                    modSummary.ToggleVisible();
                }
            }

            //KolonyConverters
            drawKolonyConverters();

            //Governer toggle
            governorActive = (bool)Utils.GetField("governorActive", mks);
            governorActive = GUILayout.Toggle(governorActive, "Enable Governor");
            Utils.SetField("governorActive", governorActive, mks);

            //Depending upon loaded scene, we'll either show the module next/prev buttons and labels
            //or we'll show the module preview buttons.
            if (!HighLogic.LoadedSceneIsEditor)
                drawPreviewGUI();
            else
                drawEditorGUI();

            GUILayout.EndVertical();
        }

        protected void drawEditorGUI()
        {
            //Next module label
            GUILayout.Label("Next: " + nextName + " requires " + nextRequirements, GUILayout.MinHeight(50));

            //Previous module label
            GUILayout.Label("Prev: " + prevName + " requires " + prevRequirements, GUILayout.MinHeight(50));

            //Next/Prev buttons
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Prev Type"))
                if (prevModuleDelegate != null)
                    prevModuleDelegate();

            if (GUILayout.Button("Next Type"))
                if (nextModuleDelegate != null)
                    nextModuleDelegate();

            GUILayout.EndHorizontal();
        }

        protected void drawPreviewGUI()
        {
            string moduleInfo;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Module Previews");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            //Make sure we have something to display
            if (string.IsNullOrEmpty(previewName))
            {
                previewName = nextName;
                previewRequirements = nextRequirements;
            }

            //Name and requirements
            GUILayout.Label(previewName + " requires " + previewRequirements, GUILayout.MinHeight(50));

            GUILayout.BeginHorizontal();
            //Prev preview button
            if (GUILayout.Button("Prev Module"))
            {
                if (prevPreviewDelegate != null)
                    prevPreviewDelegate(previewName);
            }

            //Next preview button
            if (GUILayout.Button("Next Module"))
            {
                if (nextPreviewDelegate != null)
                    nextPreviewDelegate(previewName);
            }
            GUILayout.EndHorizontal();

            //More info button
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("More Info"))
            {
                if (getModuleInfoDelegate != null)
                {
                    moduleInfo = getModuleInfoDelegate(previewName);
                    ModuleInfoView modSummary = new ModuleInfoView();
                    Texture moduleLabel;

                    modSummary.ModuleInfo = moduleInfo;

                    if (this.getModuleLogoDelegate != null)
                    {
                        moduleLabel = getModuleLogoDelegate(previewName);
                        modSummary.moduleLabel = moduleLabel;
                    }
                    modSummary.ToggleVisible();

                }
            }

            if (GUILayout.Button("Reconfigure"))
            {
                if (nextName == shortName)
                    ScreenMessages.PostScreenMessage("No need to redecorate to the same module type.", 5.0f, ScreenMessageStyle.UPPER_CENTER);

                else if (changeModuleTypeDelegate != null)
                    changeModuleTypeDelegate(previewName);
            }
            GUILayout.EndHorizontal();
        }

        protected void drawKolonyConverters()
        {
            GUILayout.BeginVertical(GUILayout.MinHeight(110));
            bool converterEnabled = false;
            string converterName;
            string converterStatus;
            bool showRemainingTime = false;
            string remainingTime;
            string constraintDisplay;
 
            _scrollPosConverters = GUILayout.BeginScrollView(_scrollPosConverters, new GUIStyle(GUI.skin.textArea));

            foreach (PartModule converter in converters)
            {
                converterEnabled = (bool)Utils.GetField("converterEnabled", converter);
                converterName = (string)Utils.GetField("converterName", converter);
                converterStatus = (string)Utils.GetField("converterStatus", converter);
                showRemainingTime = (bool)Utils.GetField("showRemainingTime", converter);
                remainingTime = (string)Utils.GetField("remainingTimeDisplay", converter);
                constraintDisplay = (string)Utils.GetField("constraintDisplay", converter);

                GUILayout.BeginVertical();
                //Toggle, name and status message
                if (!HighLogic.LoadedSceneIsEditor)
                    converterEnabled = GUILayout.Toggle(converterEnabled, converterName + ": " + converterStatus);
                else
                    converterEnabled = GUILayout.Toggle(converterEnabled, converterName);
                Utils.SetField("converterEnabled", converterEnabled, converter);

                //Time remaining
                if (showRemainingTime)
                    GUILayout.Label("Remaining: " + remainingTime);

                //Constraints
                GUILayout.Label("Constraints: " + constraintDisplay);
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
    }
}