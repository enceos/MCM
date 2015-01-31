using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KolonyTools;

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
        public List<KolonyConverter> converters = null;
        public MKSEfficiencyModule mks;
        public Part part;
        public PartResourceList resources;
        public bool canBeReconfigured;
        public string shortName;
        public string nextName;
        public string prevName;
        public string previewName;
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
            GUILayout.BeginVertical(GUILayout.MaxWidth(350f));

            //Efficiency Label
            if (converters.Count<KolonyConverter>() > 0)
                GUILayout.Label(shortName + " efficiency: " + mks.efficiency);

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
            //Next/Prev buttons
            if (GUILayout.Button("Next: " + nextName))
                if (nextModuleDelegate != null)
                    nextModuleDelegate();

            if (GUILayout.Button("Prev: " + prevName))
                if (prevModuleDelegate != null)
                    prevModuleDelegate();
        }

        protected void drawPreviewGUI()
        {
            //Only allow reconfiguring of the module if it allows field reconfiguration.
            if (canBeReconfigured == false)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("This module cannot be reconfigured. Research more technology.");
                GUILayout.FlexibleSpace();
                return;
            }

            string moduleInfo;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Current Preview: " + previewName);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            //Make sure we have something to display
            if (string.IsNullOrEmpty(previewName))
                previewName = nextName;

            //Next preview button
            if (GUILayout.Button("Next: " + nextName))
            {
                if (nextPreviewDelegate != null)
                    nextPreviewDelegate(previewName);
            }

            //Prev preview button
            if (GUILayout.Button("Prev: " + prevName))
            {
                if (prevPreviewDelegate != null)
                    prevPreviewDelegate(previewName);
            }

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
            string converterName = "??";
            string converterStatus = "??";
            bool isActivated;

            _scrollPosConverters = GUILayout.BeginScrollView(_scrollPosConverters, new GUIStyle(GUI.skin.textArea));

            foreach (KolonyConverter converter in converters)
            {
                converterName = converter.ConverterName;
                converterStatus = converter.status;
                isActivated = converter.IsActivated;

                GUILayout.BeginVertical();
                //Toggle, name and status message
                if (!HighLogic.LoadedSceneIsEditor)
                    isActivated = GUILayout.Toggle(isActivated, converterName + ": " + converterStatus);
                else
                    isActivated = GUILayout.Toggle(isActivated, converterName);

                if (converter.IsActivated != isActivated)
                {
                    converter.IsActivated = isActivated;
                    if (converter.IsActivated)
                    {
                        Debug.Log("FRED converter is active");
                        mks.GetEfficiencyRate();
                        converter.OnAwake();
                    }
                }

                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
    }
}