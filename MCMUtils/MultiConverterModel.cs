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
    public delegate void LogDelegate(object message);

    class MultiConverterModel
    {
        public LogDelegate logDelegate = null;
        public Part part = null;
        public Vessel vessel = null;
        public List<PartModule> converters;

        #region API
        public MultiConverterModel(Part part, Vessel vessel, LogDelegate logDelegate)
        {
            this.part = part;
            this.vessel = vessel;
            this.logDelegate = logDelegate;

            this.converters = new List<PartModule>();
        }

        public void Clear()
        {
            List<PartModule> doomedModules = new List<PartModule>();

            foreach (PartModule module in this.part.Modules)
            {
                if (module.name.Contains("KolonyConverter"))
                    doomedModules.Add(module);
            }

            foreach (PartModule doomed in doomedModules)
                this.part.RemoveModule(doomed);

            this.converters.Clear();
        }

        public void Load(ConfigNode node)
        {
            ConfigNode[] converterNodes = node.GetNodes("KolonyConverter");
            PartModule genericConverter;

            if (converterNodes == null)
            {
                Log("converter nodes are null");
                return;
            }

            //Clear the list of converters (if any)
            Clear();

            //Go through all the nodes, create a new converter, and tell it to load its data.
            foreach (ConfigNode converterNode in converterNodes)
            {
                //Create the converter
                genericConverter = this.part.AddModule("KolonyConverter");

                //Load its data from the node
                LoadFromNode(genericConverter, converterNode);

                //Remove its gui elements
                RunHeadless(genericConverter);

                //Add to the list
                converters.Add(genericConverter);
            }
        }

        public void Save(ConfigNode node)
        {
            ConfigNode converterNode;

            foreach (PartModule converter in converters)
            {
                //Generate a new config node
                converterNode = ConfigNode.CreateConfigFromObject(converter);
                converterNode.name = "KolonyConverter";

                //Save the converter's data
                SaveToNode(converter, converterNode);

                //Add converter node to the node we're saving to.
                node.AddNode(converterNode);
            }
        }

        public PartModule AddFromTemplate(ConfigNode node)
        {
            Log("AddFromTemplate called");
            PartModule converter = this.part.AddModule(node);

            //Remove the converter's GUI
            RunHeadless(converter);

            //Add it to the list
            this.converters.Add(converter);

            return converter;
        }

        public void LoadConvertersFromTemplate(ConfigNode nodeTemplate)
        {
            Log("LoadConvertersFromTemplate called.");
            ConfigNode[] templateModules = nodeTemplate.GetNodes("MODULE");
            string value;

            //Sanity check
            if (templateModules == null)
                return;

            //Clear existing nodes and states
            Clear();

            //Go through each module node and look for the KolonyConverter module name.
            //If found, retain a reference to the node, and set up a new converter.
            foreach (ConfigNode nodeModule in templateModules)
            {
                value = nodeModule.GetValue("name");
                if (string.IsNullOrEmpty(value))
                    continue;

                //Found a converter?
                //load up a new converter using the template's parameters.
                if (value == "KolonyConverter")
                    AddFromTemplate(nodeModule);
            }
        }

        public string GetRequirements(int index)
        {
            ConfigNode[] templateNodes = GameDatabase.Instance.GetConfigNodes("MKSTEMPLATE");

            if (templateNodes == null)
                return "";
            if (index < 0 || index > templateNodes.Count<ConfigNode>())
                return "";

            return GetRequirements(templateNodes[index]);
        }

        public string GetRequirements(ConfigNode templateNode)
        {
            StringBuilder requirements = new StringBuilder();
            Dictionary<string, float> totalRequirements = new Dictionary<string, float>();
            char[] delimiters = { ' ', ',', '\t', ';' };
            string[] tokens;
            string required;
            float amount;
            int curIndex;
            string converterRequirements = null;
            string value;

            try
            {
                //Find all the KolonyConverter nodes and sum up their require resources.
                foreach (ConfigNode converterNode in templateNode.nodes)
                {
                    //Need a KolonyConverter
                    value = converterNode.GetValue("name");
                    if (string.IsNullOrEmpty(value))
                        continue;
                    if (value != "KolonyConverter")
                        continue;

                    //Ok, now get the required resources
                    required = converterNode.GetValue("requiredResources");
                    if (string.IsNullOrEmpty(required))
                        continue;

                    //Get the tokens
                    tokens = required.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

                    //Format should be name and then amount.
                    for (curIndex = 0; curIndex < tokens.Length - 1; curIndex += 2)
                    {
                        //Get the name of the required resource and its amount
                        required = tokens[curIndex];
                        if (!float.TryParse(tokens[curIndex + 1], out amount))
                            continue;

                        //Either add the resource to the dictionary, or set the greater of new amount or existing amount
                        if (totalRequirements.ContainsKey(required))
                            totalRequirements[required] = amount > totalRequirements[required] ? amount : totalRequirements[required];
                        else
                            totalRequirements.Add(required, amount);
                    }
                }

                //Now fill out the stringbuilder
                foreach (string key in totalRequirements.Keys)
                {
                    requirements.Append(String.Format("{0:#,###.##}", totalRequirements[key]));
                    requirements.Append(" " + key + " , ");
                }

                //Strip off the last " , " characters
                converterRequirements = requirements.ToString();
                if (!string.IsNullOrEmpty(converterRequirements))
                {
                    converterRequirements = converterRequirements.Substring(0, converterRequirements.Length - 3);
                    return converterRequirements;
                }
            }
            catch (Exception ex)
            {
                Log("getConverterRequirements ERROR: " + ex);
            }

            return "nothing";
        }

        public void OnAwake()
        {
        }

        public void OnStart(PartModule.StartState state)
        {
            foreach (PartModule converter in converters)
            {
                RunHeadless(converter);
            }
        }

        public void OnFixedUpdate()
        {
        }
        #endregion

        #region Helpers

        /* Important Note about how modules are loaded and saved and why this kludge exists.
         * Each part.cfg file contains a list of modules defined by MODULE config nodes.
         * When a part is first loaded upon startup, KSP looks through each MODULE config node and
         * uses it to instantiate the corresponding PartModule-derived object. 
         * For instance, if your part file has an MKSModule, then KSP will instantiate an MKSModule object for you.
         * 
         * When a part is saved into a craft file or a savegame file, KSP searches through a part's
         * list of modules and tells them to save their data into the provided ConfigNode object.
         * If we add new modules at runtime, they too will be saved into the craft file/savegame file.
         * 
         * When a craft file/savegame file is loaded, KSP goes through all the modules listed in the part
         * and tries to instantiate them. Therein lies the problem. If we added modules at runtime, and
         * they are not part of the original part's part.cfg file, then KSP skips the module data. This is
         * bad because you lose all of your persistent data. Adding KolonyConverter modules at runtime is
         * no exception. Fortunately, we have a workaround.
         * 
         * When MKSModuleController is loaded and saved, we have access to its ConfigNode object. By calling
         * SaveToNode for each KolonyConverter that we created at runtime, we can pass in that ConfigNode object
         * and save the KolonyConverter's persistent data into MKSModuleController's node. That way,
         * persistent data such as the enabled/disabled status and last updated time are all preserved.
         * 
         * The only caveat to this approach is that KSP calls KolonyConverter's OnSave method and it duplicates
         * the converter's persistent data within the craft file/savegame file. I plan to do a pull request on
         * KolonyConverter and set a flag to disregard the OnSave/OnLoad calls except when I flip the flag.
         * That, and add a RunHeadless method so I don't have to do that in MultiConverterModel.
         * 
         * So right now this solution is kludgy but it does work, and I have runtime configurable colony modules. :)
         */
        public void SaveToNode(PartModule converter, ConfigNode node)
        {
            bool boolValue;
            float floatValue;

            //We'll get the private fields saved this way
            converter.Save(node);

            node.AddValue("converterName", (string)Utils.GetField("converterName", converter));

            floatValue = (float)Utils.GetField("conversionRate", converter);
            node.AddValue("conversionRate", floatValue.ToString());

            node.AddValue("inputResources", (string)Utils.GetField("inputResources", converter));

            node.AddValue("outputResources", (string)Utils.GetField("outputResources", converter));

            node.AddValue("requiredResources", (string)Utils.GetField("requiredResources", converter));

            boolValue = (bool)Utils.GetField("SurfaceOnly", converter);
            node.AddValue("SurfaceOnly", boolValue.ToString());

            boolValue = (bool)Utils.GetField("converterEnabled", converter);
            node.AddValue("converterEnabled", boolValue.ToString());

            boolValue = (bool)Utils.GetField("alwaysOn", converter);
            node.AddValue("alwaysOn", boolValue.ToString());

            boolValue = (bool)Utils.GetField("requiresOxygenAtmo", converter);
            node.AddValue("requiresOxygenAtmo", boolValue.ToString());

            boolValue = (bool)Utils.GetField("shutdownIfAllOutputFull", converter);
            node.AddValue("shutdownIfAllOutputFull", boolValue.ToString());

            boolValue = (bool)Utils.GetField("showRemainingTime", converter);
            node.AddValue("showRemainingTime", boolValue.ToString());
        }

        public void LoadFromNode(PartModule converter, ConfigNode node)
        {
            string value;

            try
            {
                //This will load our private fields
                converter.Load(node);

                //Set its parameters
                value = node.GetValue("converterName");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("converterName", value, converter);

                value = node.GetValue("conversionRate");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("conversionRate", float.Parse(value), converter);

                value = node.GetValue("inputResources");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("inputResources", value, converter);

                value = node.GetValue("outputResources");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("outputResources", value, converter);

                value = node.GetValue("requiredResources");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("requiredResources", value, converter);

                value = node.GetValue("SurfaceOnly");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("SurfaceOnly", bool.Parse(value), converter);

                value = node.GetValue("converterEnabled");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("converterEnabled", bool.Parse(value), converter);

                value = node.GetValue("alwaysOn");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("alwaysOn", bool.Parse(value), converter);

                value = node.GetValue("requiresOxygenAtmo");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("requiresOxygenAtmo", bool.Parse(value), converter);

                value = node.GetValue("shutdownIfAllOutputFull");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("shutdownIfAllOutputFull", bool.Parse(value), converter);

                value = node.GetValue("showRemainingTime");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("showRemainingTime", bool.Parse(value), converter);
            }

            catch (Exception ex)
            {
                Log("Error during load: " + ex.Message);
            }
        }

        public void RunHeadless(PartModule converter)
        {
            if (converter.Events["ActivateConverter"] != null)
            {
                converter.Events["ActivateConverter"].active = false;
                converter.Events["ActivateConverter"].guiActive = false;
                converter.Events["ActivateConverter"].guiActiveEditor = false;
            }

            if (converter.Events["DeactivateConverter"] != null)
            {
                converter.Events["DeactivateConverter"].active = false;
                converter.Events["DeactivateConverter"].guiActiveEditor = false;
                converter.Events["DeactivateConverter"].guiActive = false;
            }

            if (converter.Fields["remainingTimeDisplay"] != null)
            {
                converter.Fields["remainingTimeDisplay"].guiActive = false;
                converter.Fields["remainingTimeDisplay"].guiActiveEditor = false;
            }

            if (converter.Fields["constraintDisplay"] != null)
            {
                converter.Fields["constraintDisplay"].guiActive = false;
                converter.Fields["constraintDisplay"].guiActiveEditor = false;
            }

            if (converter.Fields["converterStatus"] != null)
            {
                converter.Fields["converterStatus"].guiActive = false;
                converter.Fields["converterStatus"].guiActiveEditor = false;
            }

            if (converter.Fields["converterName"] != null)
            {
                converter.Fields["converterName"].guiActive = false;
                converter.Fields["converterName"].guiActiveEditor = false;
            }

            if (converter.Actions["ToggleConverter"] != null)
                converter.Actions["ToggleConverter"].active = false;
        }

        public virtual void Log(object message)
        {
            if (logDelegate != null)
                logDelegate(message);
        }
        #endregion
    }
}
