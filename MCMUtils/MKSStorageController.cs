﻿////using System;
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
        #region Events
        #endregion

        #region Overrides
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnRedecorateModule(ConfigNode nodeTemplate, bool payForRedecoration)
        {
            Log("OnRedecorateModule called");
            base.OnRedecorateModule(nodeTemplate, payForRedecoration);
        }

        public override void loadResourcesFromTemplate(ConfigNode nodeTemplate)
        {
            base.loadResourcesFromTemplate(nodeTemplate);
        }

        protected override void getProtoNodeValues(ConfigNode protoNode)
        {
            base.getProtoNodeValues(protoNode);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
        }
        #endregion
    }
}
