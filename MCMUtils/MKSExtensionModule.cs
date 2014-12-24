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
    public class MKSExtensionModule : WBIResourceSwitcher
    {
        #region Helpers
        public override void OnRedecorateModule(ConfigNode templateNode, bool payForRedecoration)
        {
            base.OnRedecorateModule(templateNode, payForRedecoration);
            string value = templateNode.GetValue("shortName");

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
        }
        #endregion
    }
}
