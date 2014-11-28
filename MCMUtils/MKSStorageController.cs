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
    class MKSStorageController : WBIResourceSwitcher
    {
//        [KSPField(isPersistant = true)]
        public bool isDeployed;

        private bool _isInflatable = false;
        private ModuleAnimateGeneric _genericAnim;
        private Dictionary<string, double> _resourceMaxAmounts = new Dictionary<string, double>();

        #region Events
        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "ToggleAnimation", active = true)]
        public void ToggleAnimation()
        {
            List<PartResource> resourceList = this.part.Resources.list;

            //Toggle state
            isDeployed = !isDeployed;
            if (isDeployed)
                Events["ToggleAnimation"].guiName = _genericAnim.endEventGUIName;
            else
                Events["ToggleAnimation"].guiName = _genericAnim.startEventGUIName;

            //Toggle the animation
            _genericAnim.Toggle();

            //If the module is now inflated, re-add the max resource amounts to the list of resources.
            //If it isn't inflated, set max amount to 1.
            foreach (PartResource resource in resourceList)
            {
                //If we are deployed then reset the max amounts.
                if (isDeployed)
                {
                    if (_resourceMaxAmounts.ContainsKey(resource.resourceName))
                    {
                        resource.amount = 0;
                        resource.maxAmount = _resourceMaxAmounts[resource.resourceName];
                    }
                }

                else //No longer deployed.
                {
                    resource.amount = 0;
                    resource.maxAmount = 1;
                }
            }
        }
        #endregion

        #region Overrides
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.AddValue("isDeployed", isDeployed.ToString());
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            string value = node.GetValue("isDeployed");

            if (string.IsNullOrEmpty(value) == false)
                isDeployed = bool.Parse(value);
        }

        public override void OnRedecorateModule(ConfigNode nodeTemplate, bool payForRedecoration)
        {
            Log("OnRedecorateModule called");
            base.OnRedecorateModule(nodeTemplate, payForRedecoration);
            List<PartResource> resourceList = this.part.Resources.list;
            ConfigNode[] resourceNodes = nodeTemplate.GetNodes("RESOURCE");

            //Clear our dictionary
            _resourceMaxAmounts.Clear();

            //Set the max amounts into our dictionary.
            foreach (ConfigNode nodeResource in resourceNodes)
                _resourceMaxAmounts.Add(nodeResource.GetValue("name"), double.Parse(nodeResource.GetValue("maxAmount")) * capacityFactor);

            foreach (PartResource resource in resourceList)
            {
                Log("Resource " + resource.resourceName + " amount " + resource.amount + " max amount " + resource.maxAmount);

                //If we aren't deployed then set the current and max amounts
                if (isDeployed == false && _isInflatable)
                {
                    resource.maxAmount = 1.0f;
                    resource.amount = 0f;
                }
            }
        }

        protected override void loadResourcesFromTemplate(ConfigNode nodeTemplate)
        {
            //If there are resources in the part, should we warn the user?

            base.loadResourcesFromTemplate(nodeTemplate);
        }

        protected override void getProtoNodeValues(ConfigNode protoNode)
        {
            Log("MIKE getProtoNodeValues called.");
            base.getProtoNodeValues(protoNode);
            string value = protoNode.GetValue("isInflatable");

            if (string.IsNullOrEmpty(value) == false)
                _isInflatable = bool.Parse(value);
            Log("MIKE is inflattable: " + _isInflatable.ToString());
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            if (_isInflatable == false)
            {
                Log("MIKE Hiding toggle button");

                //Hide our toggle button
                Events["ToggleAnimation"].guiActive = false;
                Events["ToggleAnimation"].active = false;

                return;
            }

            //If this is an inflatable storage module then get the animation module and hide its toggle button.
            //First, get the animation module
            _genericAnim = this.part.FindModuleImplementing<ModuleAnimateGeneric>();
            if (_genericAnim == null)
                return;
            Log("MIKE Found ModuleAnimateGeneric");

            //Now, get the toggle event and hide its GUI.
            BaseEvent baseEvent = _genericAnim.Events["Toggle"];
            if (baseEvent != null)
            {
                Log("MIKE Hiding generic animation toggle button");
                baseEvent.guiActiveEditor = false;
                baseEvent.guiActive = false;

                Events["ToggleAnimation"].guiName = _genericAnim.startEventGUIName;
            }

            //Now we want to show our own button
            Log("MIKE showing our toggle button");
            Events["ToggleAnimation"].guiActive = true;
            Events["ToggleAnimation"].active = true;

            if (isDeployed)
                Events["ToggleAnimation"].guiName = _genericAnim.endEventGUIName;
            else
                Events["ToggleAnimation"].guiName = _genericAnim.startEventGUIName;
        }
        #endregion
    }
}
