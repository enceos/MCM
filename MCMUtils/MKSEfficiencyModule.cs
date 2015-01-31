using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using USITools;
using KolonyTools;
using System.Collections.Generic;

/*
 * This code is a modified version of the MKSModule that originally appeared in the KolonyTools
*/
namespace WildBlueIndustries
{
    public class MKSEfficiencyModule : MKSModuleX
    {
        public override float GetPartEfficiency()
        {
            //First thing we do is find the MCMs
            int mcmParts = 0;
            var mcmPartList = vessel.Parts.FindAll(p => p.name == part.name);
            MKSModuleController mksModuleController = this.part.FindModuleImplementing<MKSModuleController>();
            string shortName = mksModuleController.shortName;
            print("Searchng for MCMs configured as: " + shortName);

            //Go through all the MCMs in the vessel and find the ones that that have short names matching shortName.
            foreach (Part mcm in mcmPartList)
            {
                mksModuleController = mcm.FindModuleImplementing<MKSModuleController>();
                if (mksModuleController.shortName == shortName)
                {
                    mcmParts += 1;
                }
            }
            print("Number of " + shortName + " modules: " + mcmParts);

            char[] delimiters = { ',' };
            string[] efficiencyParts = efficiencyPart.Split(delimiters);
            var effParts = 0;
            foreach (string effPartName in efficiencyParts)
            {
                print("Searching for: " + effPartName);
                var effPartList = vessel.Parts.FindAll(p => p.name == (effPartName.Replace('_', '.')));

                foreach (var ep in effPartList)
                {
                    //If the efficiency part has an extension module, then make sure that the short names match.
                    var extensionModule = ep.FindModuleImplementing<MKSExtensionModule>();
                    if (extensionModule != null)
                    {
                        if (extensionModule.shortName != shortName)
                        {
                            print(extensionModule.shortName + " != " + shortName);
                            continue;
                        }
                    }
                    var wbiMod = ep.FindModuleImplementing<WBIInflatablePartModule>();
                    var usiMod = ep.FindModuleImplementing<USIAnimation>();

                    if (wbiMod == null && usiMod == null)
                    {
                        effParts++;
                        print("Found a " + effPartName);
                    }

                    //Check for inflated parts
                    else
                    {
                        if (wbiMod != null)
                        {
                            if (wbiMod.isDeployed)
                            {
                                effParts++;
                                print("Found a " + effPartName);
                            }
                        }
                        else if (usiMod != null)
                        {
                            if (usiMod.isDeployed)
                            {
                                effParts++;
                                print("Found a " + effPartName);
                            }
                        }
                    }
                }
            }

            print("effParts count before accounting for MCMs: " + effParts);
            //We need a minimum of one efficiency part per colony module of type shortName.
            //So if we have two kerbitats then we need two hab domes/OKS rings/IMEMs.
            effParts = (effParts - mcmParts) / mcmParts;

            return effParts;
        }

    }
}