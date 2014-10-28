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
    public class MKSModuleController : ExtendedPartModule
    {
        private static string MAIN_TEXTURE = "_MainTex";
        private static string EMISSIVE_TEXTURE = "_Emissive";

        //We need this to override the part's mass upon start.
        [KSPField(isPersistant = true)]
        protected float moduleMass = -1.0f;

        [KSPField]
        public string resourcesToReplace;

        //Decal names (these are the names of the graphics assets, including file path)
        protected string logoPanelName;
        protected string glowPanelName;

        //Name of the transform(s) for the colony decal.
        //These names come from the model itself.
        private string _logoPanelTransforms;

        //List of MKS resources that we need to clear out when switching module types
        private string _resourcesToReplace;

        //Used when, say, we're in the editor, and we don't get no game-saved values from perisistent.
        private string _defaultTemplate;

        //Base location to the decals
        private string _decalBasePath;

        //Location of the decals that will be shown in the module info window
        private string _infoDecalsPath;

        //Animation for the floodlights
        private string _floodlightsAnim;

        //Name of mesh with no floodlights
        private string _noFloodlightsMesh;

        //Index of the current module template we're using.
        private int _currentTemplateIndex;

        //Helper objects
        private MultiConverterModel _multiConverter;
        private MKSTemplatesModel _mksTemplates;
        private ModuleOpsView _moduleOpsView;
        private List<PartModule> _nonMKSModules = new List<PartModule>();
        private Dictionary<string, ConfigNode> _decalNames = new Dictionary<string, ConfigNode>();
        private List<PartResource> _templateResources = new List<PartResource>();

        #region Display Fields
        //We use this field to identify the template config node as well as have a GUI friendly name for the user.
        //When the module starts, we'll use the shortName to find the template and get the info we need.
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Module Type")]
        public string shortName;
        #endregion

        #region User Events & API
        public Texture GetModuleLogo(string templateName)
        {
            Texture moduleLogo = null;
            string panelName;

            panelName = _decalNames[templateName].GetValue("infoDecal");
            if (panelName != null)
                moduleLogo = GameDatabase.Instance.GetTexture(_infoDecalsPath + "/" + panelName, false);

            return moduleLogo;
        }

        public string GetModuleInfo(string templateName)
        {
            StringBuilder moduleInfo = new StringBuilder();
            ConfigNode nodeTemplate = _mksTemplates[templateName];
            string value;
            PartModule converter;
            bool addConverterHeader = true;

            value = nodeTemplate.GetValue("title");
            if (!string.IsNullOrEmpty(value))
                moduleInfo.Append(value + "\r\n");

            value = nodeTemplate.GetValue("description");
            if (!string.IsNullOrEmpty(value))
                moduleInfo.Append("\r\n" + value + "\r\n");

            value = nodeTemplate.GetValue("mass");
            if (!string.IsNullOrEmpty(value))
                moduleInfo.Append("Mass: " + nodeTemplate.GetValue("mass") + "\r\n");

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
                        moduleInfo.Append("\r\n");
                        addConverterHeader = false;
                    }

                    converter = this.part.AddModule("KolonyConverter");
                    converter.Load(nodeConverter);
                    moduleInfo.Append(Utils.GetField("converterName", converter) + "\r\n");
                    moduleInfo.Append(converter.GetInfo() + "\r\n");
                    this.part.RemoveModule(converter);
                }
            }

            return moduleInfo.ToString();
        }

        public void PreviewNextModule(string templateName)
        {
            int templateIndex = _mksTemplates.FindIndexOfTemplate(templateName);
            templateIndex = _mksTemplates.GetNextTemplateIndex(templateIndex);

            _moduleOpsView.previewName = _mksTemplates[templateIndex].GetValue("shortName");
            _moduleOpsView.previewRequirements = _multiConverter.GetRequirements(templateIndex);
        }

        public void PreviewPrevModule(string templateName)
        {
            int templateIndex = _mksTemplates.FindIndexOfTemplate(templateName);
            templateIndex = _mksTemplates.GetPrevTemplateIndex(templateIndex);

            _moduleOpsView.previewName = _mksTemplates[templateIndex].GetValue("shortName");
            _moduleOpsView.previewRequirements = _multiConverter.GetRequirements(templateIndex);
        }

        public void SwitchModuleType(string templateName)
        {
            //Can we use the index?
            EInvalidTemplateReasons reasonCode = _mksTemplates.CanUseTemplate(templateName);
            if (reasonCode == EInvalidTemplateReasons.TemplateIsValid)
            {
                updateContentsAndGui(templateName);
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
            _moduleOpsView.shortName = shortName;

            _moduleOpsView.ToggleVisible();
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Next Module Type", active = true)]
        public void NextModuleType()
        {
            int templateIndex = _mksTemplates.GetNextTemplateIndex(_currentTemplateIndex);
            int maxTries = _mksTemplates.templateNodes.Count<ConfigNode>();

            do //Find a template that we can use
            {
                if (_mksTemplates.CanUseTemplate(templateIndex) == EInvalidTemplateReasons.TemplateIsValid)
                {
                    updateContentsAndGui(templateIndex);
                    return;
                }

                //Try another one.
                maxTries -= 1;
                templateIndex = _mksTemplates.GetNextTemplateIndex(templateIndex);
            }
            while (maxTries > 0);

            //If we reach here then something went wrong.
            ScreenMessages.PostScreenMessage("Unable to find a module type to switch to.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Prev Module Type", active = true)]
        public void PrevModuleType()
        {
            int templateIndex = _mksTemplates.GetPrevTemplateIndex(_currentTemplateIndex);
            int maxTries = _mksTemplates.templateNodes.Count<ConfigNode>();

            do //Find a template that we can use
            {
                if (_mksTemplates.CanUseTemplate(templateIndex) == EInvalidTemplateReasons.TemplateIsValid)
                {
                    //if we're not in the editor then we just want to set the previous fields for the ops window.
                    updateContentsAndGui(templateIndex);
                    return;
                }

                //Try another one
                maxTries -= 1;
                templateIndex = _mksTemplates.GetPrevTemplateIndex(templateIndex);
            }
            while (maxTries > 0);

            //If we reach here then something went wrong.
            ScreenMessages.PostScreenMessage("Unable to find a module type to switch to.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
        }

        #endregion

        #region Module Overrides

        public override void OnLoad(ConfigNode node)
        {
            ConfigNode[] resourceNodes = node.GetNodes("RESOURCE");
            PartResource resource;
            base.OnLoad(node);
            Log("OnLoad: " + getMyPartName() + " " + node + " Scene: " + HighLogic.LoadedScene.ToString());

            //Create the multiConverter & mksTemplates
            _multiConverter = new MultiConverterModel(this.part, this.vessel, new LogDelegate(Log));
            _mksTemplates = new MKSTemplatesModel(this.part, this.vessel, new LogDelegate(Log));

            //Load node info for the MultiConverterModel
            _multiConverter.Load(node);

            //If we have resources in our node then load them.
            if (resourceNodes != null)
            {
                //Clear any existing resources. We shouldn't have any...
                _templateResources.Clear();

                foreach (ConfigNode resourceNode in resourceNodes)
                {
                    resource = this.part.AddResource(resourceNode);
                    _templateResources.Add(resource);
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            ConfigNode resourceNode;
            base.OnSave(node);

            if (_multiConverter != null)
                _multiConverter.Save(node);

            foreach (PartResource resource in _templateResources)
            {
                resourceNode = ConfigNode.CreateConfigFromObject(resource);
                resourceNode.name = "RESOURCE";
                resource.Save(resourceNode);
                node.AddNode(resourceNode);
            }
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
            bool loadTemplateResources = _templateResources.Count<PartResource>() > 0 ? false : true;
            base.OnStart(state);
            Log("OnStart - State: " + state + "  Part: " + getMyPartName());

            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
                return;

            //Get proto node values (decals path, etc.)
            getProtoNodeValues();

            //Initialize the templates
            initTemplates();

            //Create the multiconverter
            if (_multiConverter == null)
                _multiConverter = new MultiConverterModel(this.part, this.vessel, new LogDelegate(Log));
            _multiConverter.OnStart(state);

            //Create the module ops window.
            createModuleOpsView();

            //Hide GUI only shown in the editor
            hideEditorGUI(state);

           //Override part mass with the actual module's part mass (taken from the template file)
            if (moduleMass > 0f)
                this.part.mass = moduleMass;

            //Since the module will be loaded as it was originally created, we won't have 
            //the proper decals and converter settings when the module and part are loaded in flight.
            //Thus, we must redecorate to configure the module and part correctly.
            //When we do, we don't make the player pay for the redecoration, and we want to preserve
            //the part's existing resources, not to mention the current settings for the converters.
            //Also, if we have converters already then we've loaded their states during the OnLoad method call.
            bool loadConvertersFromTemplate = _multiConverter.converters.Count > 0 ? false : true;
            redecorateModule(false, loadTemplateResources, loadConvertersFromTemplate);

            //Init the module GUI
            initModuleGUI();
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
        protected void loadNonMKSModulesFromTemplate(ConfigNode nodeTemplate)
        {
            Log("loadNonMKSModulesFromTemplate called");
            ConfigNode[] moduleNodes = nodeTemplate.GetNodes("MODULE");
            string mksModules = "KolonyConverter, MKSModule, ExWorkshop";
            string moduleName;
            PartModule module;
            int containerIndex = -1;
            ModuleScienceLab sciLab;

            try
            {
                //Remove any previously added modules
                foreach (PartModule doomed in _nonMKSModules)
                {
                    this.part.RemoveModule(doomed);
                }
                _nonMKSModules.Clear();

                //Add the non-mks modules
                foreach (ConfigNode moduleNode in moduleNodes)
                {
                    moduleName = moduleNode.GetValue("name");

                    //Special case: ModuleScienceLab
                    //If we add ModuleScienceLab in the editor, even if we fix up its index for the ModuleScienceContainer,
                    //We get an NRE. The fix below does not work in the editor, and the right-click menu will be broken.
                    //Why? I dunno, so when in the editor we won't dynamically add the ModuleScienceLab.
                    if (moduleName == "ModuleScienceLab" && HighLogic.LoadedSceneIsEditor)
                        continue;

                    //If we find a non-MKS module then add it
                    if (mksModules.Contains(moduleName) == false)
                    {
                        //Add the module to the part's module list
                        module = this.part.AddModule(moduleNode);

                        //Add the module to our non-mks modules list
                        _nonMKSModules.Add(module);
                    }
                }

                /*
                 * Special case: ModuleScienceLab
                 * ModuleScienceLab has a field called "containerModuleIndex"
                 * which is the index into the part's array of PartModule objects.
                 * When you specify a number like, say 0, then the MobileScienceLab
                 * expects that the very first PartModule in the array of part.Modules
                 * will be a ModuleScienceContainer. If the ModuleScienceContainer is NOT
                 * the first element in the part.Modules array, then the part's right-click menu
                 * will fail to work and you'll get NullReferenceException errors.
                 * It's important to know that the part.cfg file that contains a bunch of MODULE
                 * nodes will have its MODULE nodes loaded in the order that they appear in the file.
                 * So if the first MODULE in the file is, say, a ModuleLight, the second is a ModuleScienceContainer,
                 * and the third is a ModuleScienceLab, then make sure that containerModuleIndex is set to 1 (the array of PartModules is 0-based).
                 * 
                 * Now, with MKSModuleController, we have the added complexity of dynamically adding the ModuleScienceContainer.
                 * We won't know what the index of the ModuleScienceContainer is at runtime until after we're done
                 * dynamically adding the PartModules identified in the template. That's why we add the KolonyConverter modules first. 
                 * So, now we will go through all the PartModules and find the index of the ModuleScienceContainer, and then we'll go through and find the
                 * ModuleScienceLab. If we find one, then we'll set its containerModuleIndex to the index we recorded for
                 * the ModuleScienceContainer. This code makes the assumption that the part author added a ModuleScienceContainer to the config file and then
                 * immediately after, added a ModuleScienceLab. It would get ugly if that wasn't the case.
                 */
                for (int curIndex = 0; curIndex < this.part.Modules.Count; curIndex++)
                {
                    //Get the module
                    module = this.part.Modules[curIndex];

                    //If we have a ModuleScienceContainer, then record its index.
                    if (module.moduleName == "ModuleScienceContainer")
                    {
                        containerIndex = curIndex;
                        Log("Container Index: " + containerIndex);
                    }

                    //If we have a MobileScienceLab and we found the container index
                    //Then set the science lab's containerModuleIndex to the proper index value
                    else if (module.moduleName == "ModuleScienceLab" && containerIndex != -1)
                    {
                        //Set the index
                        sciLab = (ModuleScienceLab)module;
                        sciLab.containerModuleIndex = containerIndex;

                        Log("Science lab container index: " + sciLab.containerModuleIndex);
                        Log("scilab index " + curIndex);

                        //Reset the recorded index
                        containerIndex = -1;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("loadNonMKSModulesFromTemplate encountered an error: " + ex);
            }
        }

        protected void loadResourcesFromTemplate(ConfigNode nodeTemplate)
        {
            PartResource resource = null;
            List<PartResource> resourceList = this.part.Resources.list;
            List<PartResource> savedResources = new List<PartResource>();

            Log("loadResourcesFromTemplate called");
            ConfigNode[] templateResourceNodes = nodeTemplate.GetNodes("RESOURCE");
            if (templateResourceNodes == null)
                return;

            //Find all the non-MKS resources that need to be saved.
            foreach (PartResource resourceToCheck in resourceList)
            {
                //Find all the MKS-related resources
                if (_resourcesToReplace.Contains(resourceToCheck.resourceName) == false)
                    savedResources.Add(resourceToCheck);
            }

            //Clear the list
            //Much quicker than removing individual resources...
            this.part.Resources.list.Clear();

            //Add resources from template
            foreach (ConfigNode resourceNode in templateResourceNodes)
            {
                resource = this.part.AddResource(resourceNode);
            }

            //Put back the non-mks resources that aren't already in the list
            foreach (PartResource savedResource in savedResources)
            {
                if (this.part.Resources.Contains(savedResource.resourceName) == false)
                    this.part.Resources.list.Add(savedResource);
            }
        }

        protected void redecorateModule(bool payForRedecoration = true, bool loadTemplateResources = true, bool loadConvertersFromTemplate = true)
        {
            try
            {
                Log("redecorateModule called.");
                if (_mksTemplates.templateNodes == null)
                    return;

                ConfigNode mksTemplate = _mksTemplates[_currentTemplateIndex];
                if (mksTemplate == null)
                    return;

                string value;
                float mass;

                //Play a nice construction sound effect

                //Reduce the vessel's inventory of the resources required to redecorate.
                //NOTE: Only applies when not in editor mode, and only when payForRedecoration = true.

                //Set the part's mass and cost based upon the template.
                value = mksTemplate.GetValue("mass");
                if (value != null)
                {
                    mass = float.Parse(value);
                    moduleMass = mass;
                    this.part.mass = mass;
                }

                //Next, create MKS converters as specified in the template and set their values.
                if (loadConvertersFromTemplate)
                    _multiConverter.LoadConvertersFromTemplate(mksTemplate);

                //Load non-MKS modules
                //Note: we do some science lab trickery here, so make sure to call this AFTER
                //loading the KolonyConverters.
                loadNonMKSModulesFromTemplate(mksTemplate);

                //Load the template resources into the module.
                if (loadTemplateResources)
                    loadResourcesFromTemplate(mksTemplate);

                //Now setup MKSModule parameters
                updateMKSModuleFromTemplate(mksTemplate);

                //Finally, change the decals on the part.
                updateDecalsFromTemplate(mksTemplate);

                Log("Module redecorated.");
            }
            catch (Exception ex)
            {
                Log("redecorateModule encountered an ERROR: " + ex);
            }
        }

        protected void updateDecalsFromTemplate(ConfigNode mksTemplate)
        {
            Log("updateDecalsFromTemplate called");
            string value;

            value = mksTemplate.GetValue("shortName");
            if (!string.IsNullOrEmpty(shortName))
            {
                //Set shortName
                shortName = value;

                //Logo panel
                value = _decalNames[shortName].GetValue("logoPanel");
                Log("value logoPanel " + value);
                if (!string.IsNullOrEmpty(value))
                    logoPanelName = value;
                else
                    Log("logoPanel name not found");

                //Glow panel
                value = _decalNames[shortName].GetValue("glowPanel");
                Log("value glowPanel " + value);
                if (!string.IsNullOrEmpty(value))
                    glowPanelName = value;
                else
                    Log("glowPanel name not found");

                //Change the decals
                changeDecals();
            }
        }

        protected void updateMKSModuleFromTemplate(ConfigNode mksTemplate)
        {
            string value;

            PartModule mksModule = this.part.Modules["MKSModule"];
            if (mksModule)
            {
                //Has generators
                if (_multiConverter.converters.Count > 0)
                    Utils.SetField("hasGenerators", true, mksModule);
                else
                    Utils.SetField("hasGenerators", false, mksModule);

                //workspace
                value = mksTemplate.GetValue("workspace");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("workSpace", int.Parse(value), mksModule);

                //livingSpace
                value = mksTemplate.GetValue("livingSpace");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("livingSpace", int.Parse(value), mksModule);

                //efficiencyPart
                value = mksTemplate.GetValue("efficiencyPart");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("efficiencyPart", value, mksModule);
            }
        }

        protected void changeDecals()
        {
            Log("changeDecals called.");

            if (string.IsNullOrEmpty(_logoPanelTransforms))
            {
                Log("changeDecals has no named transforms to change.");
                return;
            }

            char[] delimiters = {','};
            string[] transformNames = _logoPanelTransforms.Replace(" ", "").Split(delimiters);
            Transform[] targets;
            Texture textureForDecal;
            Renderer rendererMaterial;

            //Sanity checks
            if (transformNames == null)
            {
                Log("transformNames are null");
                return;
            }
            if (string.IsNullOrEmpty(_decalBasePath))
            {
                Log("decalBasePath is null");
                return;
            }

            //Go through all the named panels and find their transforms.
            //Then replace their textures.
            foreach (string transformName in transformNames)
            {
                //Get the targets
                targets = part.FindModelTransforms(transformName);
                if (targets == null)
                {
                    Log("No targets found for " + transformName);
                    continue;
                }

                //Now, replace the textures in each target
                foreach (Transform target in targets)
                {
                    rendererMaterial = target.GetComponent<Renderer>();

                    textureForDecal = GameDatabase.Instance.GetTexture(_decalBasePath + "/" + logoPanelName, false);
                    if (textureForDecal != null)
                        rendererMaterial.material.SetTexture(MAIN_TEXTURE, textureForDecal);
                    else
                        Log("Main texture textureForDecal for " + _decalBasePath + "/" + logoPanelName + " is null");

                    textureForDecal = GameDatabase.Instance.GetTexture(_decalBasePath + "/" + glowPanelName, false);
                    if (textureForDecal != null)
                        rendererMaterial.material.SetTexture(EMISSIVE_TEXTURE, textureForDecal);
                    else
                        Log("Emissive texture textureForDecal for " + _decalBasePath + "/" + glowPanelName + " is null");
                }
            }
        }

        protected void updateContentsAndGui(string templateName)
        {
            int index = _mksTemplates.FindIndexOfTemplate(templateName);

            updateContentsAndGui(index);
        }

        protected void updateContentsAndGui(int templateIndex)
        {
            string name;
            if (_mksTemplates.templateNodes == null)
            {
                Log("NextModuleType templateNodes == null!");
                return;
            }

            //Make sure we have a valid index
            if (templateIndex == -1 || templateIndex == _currentTemplateIndex)
                return;

            //Ok, we're good
            _currentTemplateIndex = templateIndex;

            //Set the current template name
            shortName = _mksTemplates[templateIndex].GetValue("shortName");
            _moduleOpsView.shortName = shortName;

            //Change the toggle buttons' names
            templateIndex = _mksTemplates.GetNextTemplateIndex(_currentTemplateIndex);
            if (templateIndex != -1 && templateIndex != _currentTemplateIndex)
            {
                name = _mksTemplates[templateIndex].GetValue("shortName");
                Events["NextModuleType"].guiName = "Next: " + name;
                _moduleOpsView.nextName = name;
                _moduleOpsView.nextRequirements = _multiConverter.GetRequirements(_mksTemplates.templateNodes[templateIndex]);
            }

            else
            {
                _moduleOpsView.nextName = "none available";
                _moduleOpsView.nextRequirements = "";
            }

            templateIndex = _mksTemplates.GetPrevTemplateIndex(_currentTemplateIndex);
            if (templateIndex != -1 && templateIndex != _currentTemplateIndex)
            {
                name = _mksTemplates[templateIndex].GetValue("shortName");
                Events["PrevModuleType"].guiName = "Prev: " + name;
                _moduleOpsView.prevName = name;
                _moduleOpsView.prevRequirements = _multiConverter.GetRequirements(_mksTemplates.templateNodes[templateIndex]);
            }

            else
            {
                _moduleOpsView.prevName = "none available";
                _moduleOpsView.prevRequirements = "";
            }

            //Set up the module in its new configuration
            redecorateModule();
        }

        protected void getProtoNodeValues()
        {
            Log("getProtoNodeValues called");
            string myPartName;
            ConfigNode protoNode = null;
            ConfigNode[] decalNodes = null;
            string value;

            myPartName = getMyPartName();

            if (protoPartNodes.ContainsKey(myPartName))
            {
                Log("Loading proto-node values.");

                //Get the proto config node
                protoNode = protoPartNodes[myPartName];

                //Set the defaults. We'll need them when we're in the editor
                //because the persistent KSP field seems to only apply to savegames.
                _defaultTemplate = protoNode.GetValue("defaultTemplate");

                //Get the list of MKS resources involved in creating colony modules
                _resourcesToReplace = protoNode.GetValue("resourcesToReplace");

                //Info decals path
                _infoDecalsPath = protoNode.GetValue("infoDecalsPath");

                //Location to the decals
                _decalBasePath = protoNode.GetValue("decalBasePath");

                //Floodlights animation
                _floodlightsAnim = protoNode.GetValue("floodlightsAnim");

                //Mesh with no floodlights
                _noFloodlightsMesh = protoNode.GetValue("noFloodlightsMesh");

                //Build dictionary of decal names
                decalNodes = protoNode.GetNodes("DECAL");
                foreach (ConfigNode decalNode in decalNodes)
                {
                    value = decalNode.GetValue("shortName");
                    if (string.IsNullOrEmpty(value) == false)
                    {
                        if (_decalNames.ContainsKey(value) == false)
                            _decalNames.Add(value, decalNode);
                    }
                }

                //Get the list of transforms for the logo panels.
                if (_logoPanelTransforms == null)
                    _logoPanelTransforms = protoNode.GetValue("logoPanelTransform");
            }
        }

        protected void createModuleOpsView()
        {
            Log("createModuleOpsView called");
            PartModule mks = this.part.Modules["MKSModule"];

            _moduleOpsView = new ModuleOpsView();
            _moduleOpsView.converters = _multiConverter.converters;
            _moduleOpsView.part = this.part;
            _moduleOpsView.resources = this.part.Resources;
            _moduleOpsView.mks = mks;
            _moduleOpsView.nextModuleDelegate = new NextModule(NextModuleType);
            _moduleOpsView.prevModuleDelegate = new PrevModule(PrevModuleType);
            _moduleOpsView.nextPreviewDelegate = new NextPreviewModule(PreviewNextModule);
            _moduleOpsView.prevPreviewDelegate = new PrevPreviewModule(PreviewPrevModule);
            _moduleOpsView.getModuleInfoDelegate = new GetModuleInfo(GetModuleInfo);
            _moduleOpsView.changeModuleTypeDelegate = new ChangeModuleType(SwitchModuleType);
            _moduleOpsView.getModuleLogoDelegate = new GetModuleLogo(GetModuleLogo);
        }

        protected void hideEditorGUI(PartModule.StartState state)
        {
            Log("hideEditorGUI called");
            PartModule mks = this.part.Modules["MKSModule"];
            if (mks == null)
                return;

            //Hide my irrelevant GUI when not in editor mode.
            //Functionality is handled by the module operations manager.
            if (state != StartState.Editor)
            {
                this.Events["NextModuleType"].guiActive = false;
                this.Events["PrevModuleType"].guiActive = false;
            }

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

        protected void initModuleGUI()
        {
            Log("initModuleGUI called");
            int index;
            string value;

            //Change the toggle button's name
            index = _mksTemplates.GetNextTemplateIndex(_currentTemplateIndex);
            if (index != -1 && index != _currentTemplateIndex)
            {
                value = _mksTemplates.templateNodes[index].GetValue("shortName");
                Events["NextModuleType"].guiName = "Next: " + value;
                _moduleOpsView.nextName = value;
                _moduleOpsView.nextRequirements = _multiConverter.GetRequirements(_mksTemplates.templateNodes[index]);
            }

            index = _mksTemplates.GetPrevTemplateIndex(_currentTemplateIndex);
            if (index != -1 && index != _currentTemplateIndex)
            {
                value = _mksTemplates.templateNodes[index].GetValue("shortName");
                Events["PrevModuleType"].guiName = "Prev: " + value;
                _moduleOpsView.prevName = value;
                _moduleOpsView.prevRequirements = _multiConverter.GetRequirements(_mksTemplates.templateNodes[index]);
            }

            initFloodlights();
        }

        protected void initTemplates()
        {
            Log("initTemplates called");
            //Create templates object if needed.
            //This can happen when the object is cloned in the editor (On Load won't be called).
            if (_mksTemplates == null)
                _mksTemplates = new MKSTemplatesModel(this.part, this.vessel, new LogDelegate(Log));
            if (_mksTemplates.templateNodes == null)
            {
                Log("OnStart templateNodes == null!");
                return;
            }

            //Set default template if needed
            //This will happen when we're in the editor.
            if (string.IsNullOrEmpty(shortName))
                shortName = _defaultTemplate;

            //Set current template index
            _currentTemplateIndex = _mksTemplates.FindIndexOfTemplate(shortName);
        }
        #endregion

    }
}
