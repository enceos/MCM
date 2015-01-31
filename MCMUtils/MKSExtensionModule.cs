using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KolonyTools;

/*
Source code copyrighgt 2014, by Michael Billard (Angel-125)
License: CC BY-NC-SA 4.0
License URL: https://creativecommons.org/licenses/by-nc-sa/4.0/
Wild Blue Industries is trademarked by Michael Billard and may be used for non-commercial purposes. All other rights reserved.
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

Portions of this software use code from the Firespitter plugin by Snjo, used with permission. Thanks Snjo for sharing how to switch meshes. :)
 
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace WildBlueIndustries
{
    public class MKSExtensionModule : WBIResourceSwitcher
    {
        [KSPField]
        public string objects = string.Empty;

        [KSPField(isPersistant = true)]
        public int selectedObject = 0;

        protected Dictionary<string, int> templateMeshIndexes = new Dictionary<string, int>();
        protected string academyShortName;

        private List<List<Transform>> objectTransforms = new List<List<Transform>>();

        #region Helpers
        private void parseObjectNames()
        {
            string[] objectBatchNames = objects.Split(';');
            if (objectBatchNames.Length < 1)
                Log("Found no object names in the object list");
            else
            {
                objectTransforms.Clear();
                for (int batchCount = 0; batchCount < objectBatchNames.Length; batchCount++)
                {
                    List<Transform> newObjects = new List<Transform>();
                    string[] objectNames = objectBatchNames[batchCount].Split(',');
                    for (int objectCount = 0; objectCount < objectNames.Length; objectCount++)
                    {
                        Transform newTransform = part.FindModelTransform(objectNames[objectCount].Trim(' '));
                        if (newTransform != null)
                        {
                            newObjects.Add(newTransform);
                            Log("Added object to list: " + objectNames[objectCount]);
                        }
                        else
                        {
                            Log("Could not find object " + objectNames[objectCount]);
                        }
                    }
                    if (newObjects.Count > 0) objectTransforms.Add(newObjects);
                }
            }
        }

        private void setObject(int objectNumber)
        {
            for (int i = 0; i < objectTransforms.Count; i++)
            {
                for (int j = 0; j < objectTransforms[i].Count; j++)
                {
                    Log("Setting object enabled");
                    objectTransforms[i][j].gameObject.SetActive(false);

                    Log("setting collider states");
                    if (objectTransforms[i][j].gameObject.collider != null)
                        objectTransforms[i][j].gameObject.collider.enabled = false;
                }
            }

            // enable the selected one last because there might be several entries with the same object, and we don't want to disable it after it's been enabled.
            for (int i = 0; i < objectTransforms[objectNumber].Count; i++)
            {
                objectTransforms[objectNumber][i].gameObject.SetActive(true);

                if (objectTransforms[objectNumber][i].gameObject.collider != null)
                {
                    Log("Setting collider true on new active object");
                    objectTransforms[objectNumber][i].gameObject.collider.enabled = true;
                }
            }

            selectedObject = objectNumber;
        }
        #endregion

        #region Overrides
        public override void OnStart(StartState state)
        {
            parseObjectNames();
            base.OnStart(state);
            setObject(selectedObject);
        }

        protected override void getProtoNodeValues(ConfigNode protoNode)
        {
            base.getProtoNodeValues(protoNode);
            string value;
            char[] delimiters = { ';' };
            char[] meshDelimiters = { ',' };
            string[] meshIndexes;
            string[] meshIndexFields;

            //Get the template mesh indexes
            value = protoNode.GetValue("templateMeshIndexes");
            if (string.IsNullOrEmpty(value) == false)
            {
                //Split the templateMeshIndexes up into individual entries: <template name>,<mesh index>
                meshIndexes = value.Split(delimiters);

                //Go through each entry and split up the entry into its template name and mesh index
                foreach (string templateMeshIndex in meshIndexes)
                {
                    //Split the entry into its corresponding fields: index 0: <template name> index 1: <mesh index>
                    meshIndexFields = templateMeshIndex.Split(meshDelimiters);

                    //Add the new entry
                    templateMeshIndexes.Add(meshIndexFields[0], int.Parse(meshIndexFields[1]));
                }
            }

            //Akademy template name
            academyShortName = protoNode.GetValue("academyShortName");
        }

        public override void OnRedecorateModule(ConfigNode templateNode, bool payForRedecoration)
        {
            base.OnRedecorateModule(templateNode, payForRedecoration);
            string templateShortName = templateNode.GetValue("shortName");
            string value;
            SpaceAcademy academy = (SpaceAcademy)this.part.Modules["SpaceAcademy"];

            PartModule mksModule = this.part.Modules["MKSModule"];
            if (mksModule)
            {
                //workspace
                value = templateNode.GetValue("workspace");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("workSpace", int.Parse(value), mksModule);
                else
                    Utils.SetField("workSpace", 0, mksModule);

                //livingSpace
                value = templateNode.GetValue("livingSpace");
                if (!string.IsNullOrEmpty(value))
                    Utils.SetField("livingSpace", int.Parse(value), mksModule);
                else
                    Utils.SetField("livingSpace", 0, mksModule);
            }

            //Make sure we have a dictionary of mesh indexes
            if (templateMeshIndexes == null)
                return;

            //If the template short name is in the dictionary, then use the corresponding mesh index specified by the dictionary.
            //Otherwise, use the default mesh, which the first object name in the objects field.
            if (templateMeshIndexes.ContainsKey(templateShortName))
                setObject(templateMeshIndexes[templateShortName]);
            else
                setObject(0);

            //Clear resources if none in the template
            if (templateNode.GetNodes("RESOURCE") == null)
                this.part.Resources.list.Clear();

            //If the extension module is an academy, then show the academy controls
            if (academy != null)
            {
                if (templateShortName == academyShortName)
                {
                    academy.Events["Training"].active = true;
                    academy.Events["Training"].guiActive = true;
                }

                else
                {
                    academy.Events["Training"].active = false;
                    academy.Events["Training"].guiActive = false;
                }
            }
        }
        #endregion
    }
}
