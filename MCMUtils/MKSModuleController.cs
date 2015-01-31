using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

/*
Source code copyrighgt 2014, by Michael Billard (Angel-125)
License: CC BY-NC-SA 4.0
License URL: https://creativecommons.org/licenses/by-nc-sa/4.0/
Wild Blue Industries is trademarked by Michael Billard and may be used for non-commercial purposes. All other rights reserved.
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace WildBlueIndustries
{
    public class MKSModuleController : WBIModuleSwitcher
    {
        //We need this to override the part's mass upon start.
        [KSPField(isPersistant = true)]
        protected float moduleMass = -1.0f;

        //Location of the decals that will be shown in the module info window
        private string _infoDecalsPath;

        //Animation for the floodlights
        private string _floodlightsAnim;

        //Name of mesh with no floodlights
        private string _noFloodlightsMesh;

        //Helper objects
        private bool _loadConvertersFromTemplate;
        private MultiConverterModel _multiConverter;
        private ModuleOpsView _moduleOpsView;

        #region User Events & API
        public Texture GetModuleLogo(string templateName)
        {
            Texture moduleLogo = null;
            string panelName;

            panelName = parameterOverrides[templateName].GetValue("infoDecal");
            if (panelName != null)
                moduleLogo = GameDatabase.Instance.GetTexture(_infoDecalsPath + "/" + panelName, false);

            return moduleLogo;
        }

        public string GetModuleInfo(string templateName)
        {
            StringBuilder moduleInfo = new StringBuilder();
            ConfigNode nodeTemplate = templatesModel[templateName];
            string value;
            PartModule converter;
            bool addConverterHeader = true;

            value = nodeTemplate.GetValue("title");
            if (!string.IsNullOrEmpty(value))
                moduleInfo.Append(value + "\r\n");

            value = nodeTemplate.GetValue("description");
            if (!string.IsNullOrEmpty(value))
                moduleInfo.Append("\r\n" + value + "\r\n");

//            value = nodeTemplate.GetValue("mass");
//            if (!string.IsNullOrEmpty(value))
//                moduleInfo.Append("Mass: " + nodeTemplate.GetValue("mass") + "\r\n");

            value = nodeTemplate.GetValue("CrewCapacity");
            if (!string.IsNullOrEmpty(value))
                moduleInfo.Append("Crew Capacity: " + nodeTemplate.GetValue("CrewCapacity") + "\r\n");

            foreach (ConfigNode nodeConverter in nodeTemplate.nodes)
            {
                if (nodeConverter.GetValue("name") == "KolonyConverter")
                {
                    if (addConverterHeader)
                    {
                        moduleInfo.Append("\r\nProduction\r\n");
                        addConverterHeader = false;
                    }

                    converter = this.part.AddModule("KolonyConverter");
                    converter.Load(nodeConverter);
                    moduleInfo.Append(converter.GetInfo());
                    this.part.RemoveModule(converter);
                }
            }

            return moduleInfo.ToString();
        }

        public void PreviewNextModule(string templateName)
        {
            //Get the template index associated with the template name
            int curTemplateIndex = templatesModel.FindIndexOfTemplate(templateName);

            //Get the next available template index
            int templateIndex = templatesModel.GetNextUsableIndex(curTemplateIndex);

            //Set preview name to the new template's name
            _moduleOpsView.previewName = templatesModel[templateIndex].GetValue("shortName");

            //Get next template name
            templateIndex = templatesModel.GetNextUsableIndex(templateIndex);
            if (templateIndex != -1 && templateIndex != curTemplateIndex)
                _moduleOpsView.nextName = templatesModel[templateIndex].GetValue("shortName");

            //Get previous template name
            _moduleOpsView.prevName = templateName;
        }

        public void PreviewPrevModule(string templateName)
        {
            //Get the template index associated with the template name
            int curTemplateIndex = templatesModel.FindIndexOfTemplate(templateName);

            //Get the previous available template index
            int templateIndex = templatesModel.GetPrevUsableIndex(curTemplateIndex);

            //Set preview name to the new template's name
            _moduleOpsView.previewName = templatesModel[templateIndex].GetValue("shortName");

            //Get next template name (which will be the current template)
            _moduleOpsView.nextName = templateName;

            //Get previous template name
            templateIndex = templatesModel.GetPrevUsableIndex(templateIndex);
            if (templateIndex != -1 && templateIndex != curTemplateIndex)
                _moduleOpsView.prevName = templatesModel[templateIndex].GetValue("shortName");
        }

        public void SwitchModuleType(string templateName)
        {
            Log("SwitchModuleType called.");
            //Can we use the index?
            EInvalidTemplateReasons reasonCode = templatesModel.CanUseTemplate(templateName);
            if (reasonCode == EInvalidTemplateReasons.TemplateIsValid)
            {
                Log("Template is valid.");
                bool prevLoadConverters = _loadConvertersFromTemplate;
                _loadConvertersFromTemplate = true;
                UpdateContentsAndGui(templateName);
                _loadConvertersFromTemplate = prevLoadConverters;
                return;
            }

            switch (reasonCode)
            {
                case EInvalidTemplateReasons.InvalidIndex:
                    ScreenMessages.PostScreenMessage("Cannot find a suitable template.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    break;

                case EInvalidTemplateReasons.ModuleHasCrew:
                    ScreenMessages.PostScreenMessage("Remove crew before redecorating.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    break;

                case EInvalidTemplateReasons.NoFactory:
                    ScreenMessages.PostScreenMessage("You need a Fabricator or Factory Model to remodel this module.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    break;

                case EInvalidTemplateReasons.NotEnoughParts:
                    ScreenMessages.PostScreenMessage("You don't have enough parts to remodel.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    break;

                case EInvalidTemplateReasons.TechNotUnlocked:
                    ScreenMessages.PostScreenMessage("More research is required to switch to the module.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    break;

                default:
                    ScreenMessages.PostScreenMessage("Could not switch the module.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    break;
            }
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Manage Operations", active = true)]
        public void ManageOperations()
        {
            Log("ManageOperations called");
            int templateIndex = CurrentTemplateIndex;
            bool hasRequiredTechToReconfigure = true;

            //Set short name
            _moduleOpsView.shortName = shortName;

            //Minimum tech
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER && string.IsNullOrEmpty(techRequiredToReconfigure) == false)
                hasRequiredTechToReconfigure = ResearchAndDevelopment.GetTechnologyState(techRequiredToReconfigure) == RDTech.State.Available ? true : false;
            _moduleOpsView.canBeReconfigured = fieldReconfigurable & hasRequiredTechToReconfigure;

            //Set preview, next, and previous
            if (HighLogic.LoadedSceneIsEditor == false)
            {
                _moduleOpsView.previewName = shortName;

                templateIndex = templatesModel.GetNextUsableIndex(CurrentTemplateIndex);
                if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
                    _moduleOpsView.nextName = templatesModel[templateIndex].GetValue("shortName");

                templateIndex = templatesModel.GetPrevUsableIndex(CurrentTemplateIndex);
                if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
                    _moduleOpsView.prevName = templatesModel[templateIndex].GetValue("shortName");
            }

            _moduleOpsView.ToggleVisible();
        }
        #endregion

        #region Module Overrides

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            //Create the multiConverter
            _multiConverter = new MultiConverterModel(this.part, this.vessel, new LogDelegate(Log));
            _multiConverter.Load(node);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            if (_multiConverter != null)
                _multiConverter.Save(node);
        }

        public override void OnActive()
        {
            base.OnActive();
        }

        public override void OnAwake()
        {
            base.OnAwake();
            if (_multiConverter != null)
                _multiConverter.OnAwake();
        }

        public override void OnStart(PartModule.StartState state)
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
                return;

            //Create the multiconverter
            if (_multiConverter == null)
                _multiConverter = new MultiConverterModel(this.part, this.vessel, new LogDelegate(Log));
            _loadConvertersFromTemplate = _multiConverter.converters.Count > 0 ? false : true;
            if (HighLogic.LoadedSceneIsEditor)
                _loadConvertersFromTemplate = true;

            //Create the module ops window.
            createModuleOpsView();

            //Now we can call the base method.
            base.OnStart(state);
            _multiConverter.OnStart(state);

            //Override part mass with the actual module's part mass (taken from the template file)
//            if (moduleMass > 0f)
//                this.part.mass = moduleMass;

            //Fix module indexes (for things like the science lab)
            fixModuleIndexes();

            //update MKSModule with one of the parts listed in overrides.
//            updateMKSEfficiencyParts();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            //If we're in the editor then make sure the floodlights
            //option is enabled/disabled based upon the mesh we are showing.
            //Only applies to the MCM.
            if (HighLogic.LoadedSceneIsEditor)
                initFloodlights();
        }

        #endregion

        #region Helpers
        public override void OnRedecorateModule(ConfigNode templateNode, bool payForRedecoration)
        {
            base.OnRedecorateModule(templateNode, payForRedecoration);
//            string value;
//            float mass;

            //Play a nice construction sound effect

            //Reduce the vessel's inventory of the resources required to redecorate.
            //NOTE: Only applies when not in editor mode, and only when payForRedecoration = true.

            //Set the part's mass and cost based upon the template.
            /*
            value = templateNode.GetValue("mass");
            if (value != null)
            {
                mass = float.Parse(value);
                moduleMass = mass;
                this.part.mass = mass;
            }
            */

            //Next, create MKS converters as specified in the template and set their values.
            if (_loadConvertersFromTemplate)
                _multiConverter.LoadConvertersFromTemplate(templateNode);

            //Now setup MKSModule parameters
            updateMKSModuleFromTemplate(templateNode);
        }


        protected void updateMKSModuleFromTemplate(ConfigNode nodeTemplate)
        {
            Log("updateMKSModuleFromTemplate called with template: " + nodeTemplate);
            string value = nodeTemplate.GetValue("shortName");
            ConfigNode[] moduleNodes = nodeTemplate.GetNodes("MODULE");
            ConfigNode mksNode = null;
            ConfigNode paramterOverride = parameterOverrides[shortName];

            //See if the template has an MKSModule
            foreach (ConfigNode nodeModule in moduleNodes)
            {
                if (nodeModule.GetValue("name") == "MKSModule")
                {
                    mksNode = nodeModule;
                    break;
                }
            }

            PartModule mksModule = this.part.Modules["MKSEfficiencyModule"];
            if (mksModule != null && mksNode != null)
            {
                //Has generators
                if (_multiConverter.converters.Count > 0)
                    Utils.SetField("hasGenerators", true, mksModule);
                else
                    Utils.SetField("hasGenerators", false, mksModule);

                //workspace
                value = paramterOverride.GetValue("workSpace");
                if (!string.IsNullOrEmpty(value))
                {
                    Utils.SetField("workSpace", int.Parse(value), mksModule);
                }
                else
                {
                    value = mksNode.GetValue("workspace");
                    if (!string.IsNullOrEmpty(value))
                        Utils.SetField("workSpace", int.Parse(value), mksModule);
                }

                //livingSpace
                value = paramterOverride.GetValue("livingSpace");
                if (!string.IsNullOrEmpty(value))
                {
                    Utils.SetField("livingSpace", int.Parse(value), mksModule);
                }
                else
                {
                    value = mksNode.GetValue("livingSpace");
                    if (!string.IsNullOrEmpty(value))
                        Utils.SetField("livingSpace", int.Parse(value), mksModule);
                }

                //efficiencyPart
                value = paramterOverride.GetValue("efficiencyPart");
                if (!string.IsNullOrEmpty(value))
                {
                    if (value != "none")
                        Utils.SetField("efficiencyPart", value, mksModule);
                    else
                        Utils.SetField("efficiencyPart", string.Empty, mksModule);
                }

                else //Use the template
                {
                    value = mksNode.GetValue("efficiencyPart");
                    if (!string.IsNullOrEmpty(value))
                        Utils.SetField("efficiencyPart", value, mksModule);
                    else
                        Utils.SetField("efficiencyPart", string.Empty, mksModule);
                }
            }
        }

        public override void UpdateContentsAndGui(int templateIndex)
        {
            base.UpdateContentsAndGui(templateIndex);
            string templateName;

            //Change the OpsView's names
            _moduleOpsView.shortName = shortName;

            templateIndex = templatesModel.GetNextTemplateIndex(CurrentTemplateIndex);
            if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
            {
                templateName = templatesModel[templateIndex].GetValue("shortName");
                _moduleOpsView.nextName = templateName;
            }

            else
            {
                _moduleOpsView.nextName = "none available";
            }

            templateIndex = templatesModel.GetPrevTemplateIndex(CurrentTemplateIndex);
            if (templateIndex != -1 && templateIndex != CurrentTemplateIndex)
            {
                templateName = templatesModel[templateIndex].GetValue("shortName");
                _moduleOpsView.prevName = templateName;
            }

            else
            {
                _moduleOpsView.prevName = "none available";
            }

            if (_moduleOpsView.IsVisible())
            {
                _moduleOpsView.converters = _multiConverter.converters;
                _moduleOpsView.resources = this.part.Resources;
            }
        }

        protected override void getProtoNodeValues(ConfigNode protoNode)
        {
            base.getProtoNodeValues(protoNode);

            //Info decals path
            _infoDecalsPath = protoNode.GetValue("infoDecalsPath");

            //Floodlights animation
            _floodlightsAnim = protoNode.GetValue("floodlightsAnim");

            //Mesh with no floodlights
            _noFloodlightsMesh = protoNode.GetValue("noFloodlightsMesh");
        }

        protected void createModuleOpsView()
        {
            Log("createModuleOpsView called");
            MKSEfficiencyModule mks = this.part.FindModuleImplementing<MKSEfficiencyModule>();

            _moduleOpsView = new ModuleOpsView();
            _moduleOpsView.converters = _multiConverter.converters;
            _moduleOpsView.part = this.part;
            _moduleOpsView.resources = this.part.Resources;
            _moduleOpsView.mks = mks;
            _moduleOpsView.nextModuleDelegate = new NextModule(NextType);
            _moduleOpsView.prevModuleDelegate = new PrevModule(PrevType);
            _moduleOpsView.nextPreviewDelegate = new NextPreviewModule(PreviewNextModule);
            _moduleOpsView.prevPreviewDelegate = new PrevPreviewModule(PreviewPrevModule);
            _moduleOpsView.getModuleInfoDelegate = new GetModuleInfo(GetModuleInfo);
            _moduleOpsView.changeModuleTypeDelegate = new ChangeModuleType(SwitchModuleType);
            _moduleOpsView.getModuleLogoDelegate = new GetModuleLogo(GetModuleLogo);
        }

        protected override void hideEditorGUI(PartModule.StartState state)
        {
            base.hideEditorGUI(state);
            PartModule mks = this.part.Modules["MKSEfficiencyModule"];
            if (mks == null)
                return;

            //Hide MKSModule gui
            if (mks.Fields["Efficiency"] != null)
                mks.Fields["Efficiency"].guiActive = false;

            if (mks.Events["ToggleGovernor"] != null)
                mks.Events["ToggleGovernor"].guiActive = false;
        }

        protected void initFloodlights()
        {
            Log("initFloodlights called");
            PartModule floodlightAnim = this.part.Modules["FSanimateGeneric"];
            PartModule meshSwitch = this.part.Modules["FSmeshSwitch"];
            string[] meshNames;
            string meshSwitchObjects;
            char[] delimiters = { ';' };

            //Sanity checks
            //Floodlights only apply to the Multipurpose Colony Module and only when it has no light meshes.
            if (floodlightAnim == null)
                return;
            if ((string)Utils.GetField("animationName", floodlightAnim) != _floodlightsAnim)
                return;

            if (meshSwitch == null)
                return;

            meshSwitchObjects = (string)Utils.GetField("objects", meshSwitch);
            if (meshSwitchObjects == null)
                return;
            if (meshSwitchObjects.Contains(_noFloodlightsMesh) == false)
                return;

            //The last item in the array of mesh names is considered to be the mesh with no lights.
            //If we're showing that mesh then hide the floodlights buttons.
            meshNames = meshSwitchObjects.Split(delimiters);
            if ((int)Utils.GetField("selectedObject", meshSwitch) == meshNames.Count<string>() - 1)
            {
                Utils.SetField("availableInVessel", false, floodlightAnim);
                Utils.SetField("availableInEVA", false, floodlightAnim);
            }

            //Make sure they are available
            else
            {
                Utils.SetField("availableInVessel", true, floodlightAnim);
                Utils.SetField("availableInEVA", true, floodlightAnim);
            }
        }

        protected override void initModuleGUI()
        {
            base.initModuleGUI();
            int index;
            string value;
            PartModule mks = this.part.Modules["MKSEfficiencyModule"];

            //Hide MKSModule gui
            if (mks.Fields["Efficiency"] != null)
                mks.Fields["Efficiency"].guiActive = false;

            if (mks.Events["ToggleGovernor"] != null)
                mks.Events["ToggleGovernor"].guiActive = false;

            //Change the toggle button's name
            index = templatesModel.GetNextTemplateIndex(CurrentTemplateIndex);
            if (index != -1 && index != CurrentTemplateIndex)
            {
                value = templatesModel.templateNodes[index].GetValue("shortName");
                _moduleOpsView.nextName = value;
            }

            index = templatesModel.GetPrevTemplateIndex(CurrentTemplateIndex);
            if (index != -1 && index != CurrentTemplateIndex)
            {
                value = templatesModel.templateNodes[index].GetValue("shortName");
                _moduleOpsView.prevName = value;
            }

            initFloodlights();
        }
        #endregion

    }
}