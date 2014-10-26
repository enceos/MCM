﻿using System;
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
    public class MKSStorageController : ExtendedPartModule
    {
        private static string MAIN_TEXTURE = "_MainTex";
        private static string EMISSIVE_TEXTURE = "_Emissive";

        //We need this to override the part's mass upon start.
        [KSPField(isPersistant = true)]
        protected float moduleMass = -1.0f;

        //Decal names (these are the names of the graphics assets, including file path)
        protected string logoPanelName;
        protected string glowPanelName;

        //Name of the transform(s) for the colony decal.
        //These names come from the model itself.
        private string _logoPanelTransforms;

        //List of resources that we are allowed to clear when performing a template switch.
        //If set to ALL, then all of the part's resources will be cleared.
        private string _resourcesToReplace = "ALL";

        //Name of the template nodes.
        private string _mksTemplateNodes;

        //Used when, say, we're in the editor, and we don't get no game-saved values from perisistent.
        private string _defaultTemplate;

        //Base location to the decals
        private string _decalBasePath;

        //Index of the current module template we're using.
        private int _currentTemplateIndex;

        //Since not all storage containers are equal, the
        //capacityFactor is used to determine how much of the template's base resource amount
        //applies to the container.
        private float _capacityFactor = 1.0f;

        //Helper objects
        private MKSTemplatesModel _mksTemplates;
        private Dictionary<string, ConfigNode> _decalNames = new Dictionary<string, ConfigNode>();
        private List<PartResource> _templateResources = new List<PartResource>();

        #region Display Fields
        //We use this field to identify the template config node as well as have a GUI friendly name for the user.
        //When the module starts, we'll use the shortName to find the template and get the info we need.
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Module Type")]
        public string shortName;
        #endregion

        #region User Events & API
        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Next Type", active = true)]
        public void NextType()
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
            ScreenMessages.PostScreenMessage("Unable to find a template to switch to.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Prev Type", active = true)]
        public void PrevType()
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
            ScreenMessages.PostScreenMessage("Unable to find a template to switch to.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
        }

        #endregion

        #region Module Overrides

        public override void OnLoad(ConfigNode node)
        {
            ConfigNode[] resourceNodes = node.GetNodes("RESOURCE");
            PartResource resource;
            base.OnLoad(node);
            Log("OnLoad: " + getMyPartName() + " " + node + " Scene: " + HighLogic.LoadedScene.ToString());

            //Name of the nodes to use as templates
            _mksTemplateNodes = node.GetValue("mksTemplateNodes");

            //Create the mksTemplates
            _mksTemplates = new MKSTemplatesModel(this.part, this.vessel, new LogDelegate(Log), _mksTemplateNodes);

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

            foreach (PartResource resource in _templateResources)
            {
                resourceNode = ConfigNode.CreateConfigFromObject(resource);
                resourceNode.name = "RESOURCE";
                resource.Save(resourceNode);
                node.AddNode(resourceNode);
            }
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
            redecorateModule(false, loadTemplateResources);

            //Init the module GUI
            initModuleGUI();
        }

        #endregion

        #region Helpers
        protected void loadResourcesFromTemplate(ConfigNode nodeTemplate)
        {
            PartResource resource = null;
            List<PartResource> resourceList = this.part.Resources.list;
            List<PartResource> savedResources = new List<PartResource>();

            Log("loadResourcesFromTemplate called");
            ConfigNode[] templateResourceNodes = nodeTemplate.GetNodes("RESOURCE");
            if (templateResourceNodes == null)
                return;

            //Find all the that need to be saved.
            //Saved resources are all the ones that are NOT on the list
            //Resources that ARE on the list are found in the templates and will be replaced.
            //If set to "ALL" then no resources will be saved.
            if (string.IsNullOrEmpty(_resourcesToReplace) == false)
            {
                if (_resourcesToReplace != "ALL")
                {
                    foreach (PartResource resourceToCheck in resourceList)
                    {
                        if (_resourcesToReplace.Contains(resourceToCheck.resourceName) == false)
                            savedResources.Add(resourceToCheck);
                    }
                }
            }

            //Clear the list
            //Much quicker than removing individual resources...
            this.part.Resources.list.Clear();
            _templateResources.Clear();

            //Add resources from template
            foreach (ConfigNode resourceNode in templateResourceNodes)
            {
                resource = this.part.AddResource(resourceNode);

                //Apply the capacity factor
                resource.amount *= _capacityFactor;
                resource.maxAmount *= _capacityFactor;
                Log("FRED resouce amount " + resource.amount + " max " + resource.maxAmount);

                _templateResources.Add(resource);
            }

            //Put back the resources that aren't already in the list
            foreach (PartResource savedResource in savedResources)
            {
                if (this.part.Resources.Contains(savedResource.resourceName) == false)
                {
                    this.part.Resources.list.Add(savedResource);
                    _templateResources.Add(resource);
                }
            }
        }

        protected void redecorateModule(bool payForRedecoration = true, bool loadTemplateResources = true)
        {
            try
            {
                Log("redecorateModule called.");
                if (_mksTemplates.templateNodes == null)
                    return;

                ConfigNode mksTemplate = _mksTemplates[_currentTemplateIndex];
                if (mksTemplate == null)
                    return;

                //TODO: If the template has a USIAnimation module, add one and copy the template's stats.
                //Don't forget to apply the capacityFactor.

                //Load the template resources into the module.
                if (loadTemplateResources)
                    loadResourcesFromTemplate(mksTemplate);

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

        protected void changeDecals()
        {
            Log("changeDecals called.");

            if (string.IsNullOrEmpty(_logoPanelTransforms))
            {
                Log("changeDecals has no named transforms to change.");
                return;
            }

            char[] delimiters = { ',' };
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

            //Change the toggle buttons' names
            templateIndex = _mksTemplates.GetNextTemplateIndex(_currentTemplateIndex);
            if (templateIndex != -1 && templateIndex != _currentTemplateIndex)
            {
                name = _mksTemplates[templateIndex].GetValue("shortName");
                Events["NextType"].guiName = "Next: " + name;
            }

            templateIndex = _mksTemplates.GetPrevTemplateIndex(_currentTemplateIndex);
            if (templateIndex != -1 && templateIndex != _currentTemplateIndex)
            {
                name = _mksTemplates[templateIndex].GetValue("shortName");
                Events["PrevType"].guiName = "Prev: " + name;
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

                //capacity factor
                _capacityFactor = float.Parse(protoNode.GetValue("capacityFactor"));

                //Name of the nodes to use as templates
                _mksTemplateNodes = protoNode.GetValue("mksTemplateNodes");

                //Set the defaults. We'll need them when we're in the editor
                //because the persistent KSP field seems to only apply to savegames.
                _defaultTemplate = protoNode.GetValue("defaultTemplate");

                //Get the list of resources that may be replaced when switching templates
                //If empty, then all of the part's resources will be cleared during a template switch.
                _resourcesToReplace = protoNode.GetValue("resourcesToReplace");

                //Location to the decals
                _decalBasePath = protoNode.GetValue("decalBasePath");

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

        protected void hideEditorGUI(PartModule.StartState state)
        {
            Log("hideEditorGUI called");

            //Hide my irrelevant GUI when not in editor mode.
            //Functionality is handled by the module operations manager.
            if (state != StartState.Editor)
            {
                this.Events["NextType"].guiActive = false;
                this.Events["PrevType"].guiActive = false;
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
                Events["NextType"].guiName = "Next: " + value;
            }

            index = _mksTemplates.GetPrevTemplateIndex(_currentTemplateIndex);
            if (index != -1 && index != _currentTemplateIndex)
            {
                value = _mksTemplates.templateNodes[index].GetValue("shortName");
                Events["PrevType"].guiName = "Prev: " + value;
            }
        }

        protected void initTemplates()
        {
            Log("initTemplates called");
            //Create templates object if needed.
            //This can happen when the object is cloned in the editor (On Load won't be called).
            if (_mksTemplates == null)
                _mksTemplates = new MKSTemplatesModel(this.part, this.vessel, new LogDelegate(Log));

            _mksTemplates.templateNodeName = _mksTemplateNodes;

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
