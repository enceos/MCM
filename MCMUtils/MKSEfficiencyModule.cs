using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using USITools;
using KolonyTools;
using System.Collections.Generic;

namespace WildBlueIndustries
{
    public class MKSEfficiencyModule : MKSModule
    {
        private int _numConverters;
        private float _efficiencyRate;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            if (this.part.vessel != null)
            {
                Debug.Log("FRED");
                Debug.Log("Colony workspaces: " + GetKolonyWorkspaces(this.part.vessel));
                Debug.Log("Colony livingspaces: " + GetKolonyLivingSpace(this.part.vessel));
                Debug.Log("Active converters: " + GetActiveKolonyModules(this.part.vessel));
                Debug.Log("Crew Happiness: " + GetCrewHappiness());
            }

        }

        public override float GetEfficiencyRate()
        {
            /*
            var curConverters = GetActiveKolonyModules(vessel);
            if (curConverters != _numConverters)
            {
                _numConverters = curConverters;
                _efficiencyRate = GetEfficiency();
            }
            return _efficiencyRate;
             */

            return 0;
        }
        
        private int GetKolonyWorkspaces(Vessel vessel)
        {
            try
            {
                int numberOfWorkspaces = 0;
                PartModule mksModule;

                foreach (Part part in vessel.parts)
                {
                    if (part.Modules.Contains("MKSModule") || part.Modules.Contains("MKSEfficiencyModule"))
                    {
                        if (part.Modules.Contains("MKSModule"))
                        {
                            mksModule = part.Modules["MKSModule"];
                            numberOfWorkspaces += (int)Utils.GetField("workSpace", mksModule);
                        }

                        if (part.Modules.Contains("MKSEfficiencyModule"))
                        {
                            mksModule = part.Modules["MKSEfficiencyModule"];
                            numberOfWorkspaces += (int)Utils.GetField("workSpace", mksModule);
                        }
                    }
                }

                return numberOfWorkspaces;
            }
            catch (Exception ex)
            {
                print(String.Format("[MKS] - ERROR in GetKolonyWorkspaces - {0}", ex.Message));
                return 0;
            }
        }

        private int GetActiveKolonyModules(Vessel v)
        {
            try
            {
                int numberOfKonverters = 0;
                bool isActivated;

                foreach (Part part in vessel.parts)
                {
                    if (part.Modules.Contains("KolonyConverter"))
                    {
                        foreach (PartModule partModule in part.Modules)
                        {
                            if (partModule.moduleName == "KolonyConverter")
                            {
                                isActivated = (bool)Utils.GetField("IsActivated", partModule);

                                if (isActivated)
                                    numberOfKonverters += 1;
                            }
                        }
                    }
                }

                return numberOfKonverters;
            }
            catch (Exception ex)
            {
                print(String.Format("[MKS] - ERROR in GetActiveKolonyModules - {0}", ex.Message));
                return 0;
            }
        }

        private int GetKolonyLivingSpace(Vessel v)
        {
            try
            {
                int numberOfLivingSpaces = 0;
                bool isInflated = false;
                bool isInflatable = false;
                PartModule inflatableModule, mksModule;

                foreach (Part part in vessel.parts)
                {
                    if (part.Modules.Contains("MKSModule") || part.Modules.Contains("MKSEfficiencyModule"))
                    {
                        if (part.Modules.Contains("MKSModule"))
                        {
                            mksModule = part.Modules["MKSModule"];

                            //Inflatable?
                            if (part.Modules.Contains("USIAnimation"))
                            {
                                inflatableModule = part.Modules["USIAnimation"];
                                isInflated = (bool)Utils.GetField("isDeployed", inflatableModule);

                                if (isInflated)
                                    numberOfLivingSpaces += (int)Utils.GetField("livingSpace", mksModule);
                            }

                            //Not inflatable but we do have an MKSModule
                            else
                            {
                                numberOfLivingSpaces += (int)Utils.GetField("livingSpace", mksModule);
                            }
                        }

                        if (part.Modules.Contains("MKSEfficiencyModule"))
                        {
                            mksModule = part.Modules["MKSEfficiencyModule"];

                            //Inflatable?
                            if (part.Modules.Contains("WBIInflatablePartModule"))
                            {
                                inflatableModule = part.Modules["WBIInflatablePartModule"];
                                isInflated = (bool)Utils.GetField("isDeployed", inflatableModule);
                                isInflatable = (bool)Utils.GetField("isInflatable", inflatableModule);

                                if (isInflated && isInflatable)
                                    numberOfLivingSpaces += (int)Utils.GetField("livingSpace", mksModule);
                            }

                            //Not inflatable but we do have the efficiency module
                            else
                            {
                                numberOfLivingSpaces += (int)Utils.GetField("livingSpace", mksModule);
                            }
                        }
                    }
                }

                return numberOfLivingSpaces;
            }
            catch (Exception ex)
            {
                print(String.Format("[MKS] - ERROR in GetKolonyWorkspaces - {0}", ex.Message));
                return 0;
            }
        }

        private float GetCrewHappiness()
        {
            //Prototype.  Crew Happiness is a function of the ratio of living space to Kerbals.
            float ls = GetKolonyLivingSpace(vessel);
            //We can add in a limited number for crew capacity - 10%
            ls += vessel.GetCrewCapacity() * .1f;

            var hap = ls / vessel.GetCrewCount();
            //Range is 50% - 150%
            if (hap < .5f) hap = .5f;
            if (hap > 1.5f) hap = 1.5f;
            return hap;
        }
    }
    /*
    public class MKSEfficiencyModule : MKSModule
    {
        [KSPField]
        public bool calculateEfficiency = true;

        [KSPField]
        public string efficiencyPart = "";

        [KSPField]
        public int workSpace = 0;

        [KSPField]
        public int livingSpace = 0;

        [KSPField]
        public bool hasGenerators = true;

        [KSPField(guiActive = true, guiName = "Efficiency")]
        public string efficiency = "Unknown";

        [KSPEvent(guiActive = true, guiName = "Governor", active = true)]
        public void ToggleGovernor()
        {
            governorActive = !governorActive;
            EfficiencySetup();
        }

        [KSPField(isPersistant = true)]
        public bool governorActive;

        private bool _showGUI = true;
        private int _numConverters;
        private float _efficiencyRate;
        private void EfficiencySetup()
        {
            _efficiencyRate = GetEfficiency();
        }

        public bool ShowGUI
        {
            get
            {
                return _showGUI;
            }

            set
            {
                _showGUI = value;

                //Hide/show MKSModule gui
                if (Fields["Efficiency"] != null)
                    Fields["Efficiency"].guiActive = _showGUI;

                if (Events["ToggleGovernor"] != null)
                    Events["ToggleGovernor"].guiActive = _showGUI;
            }
        }

        private float GetEfficiency()
        {
            try
            {
                //Efficiency is a function of:
                //  - Crew Capacity (any module will work for this)
                //  - Workspaces
                //  - Crew Count
                //  - Active MKS Module count
                //  - module bonuses

                //  - efficiency parts
                //          Bonus equal to 100 * number of units - 1

                float numWorkspaces = GetKolonyWorkspaces(vessel);
                print("NumWorkspaces: " + numWorkspaces);
                //Plus 25% of Crew Cap as low efficiency workspaces
                numWorkspaces += vessel.GetCrewCapacity() * .25f;
                print("AdjNumWorkspaces: " + numWorkspaces);
                var numModules = GetActiveKolonyModules(vessel);
                print("numModules: " + numModules);

                //  Part (x1.5):   0   2   1   
                //  Ship (x0.5):   2   0   1
                //  Total:         1   3   2

                float modKerbalFactor = 0;
                foreach (var k in part.protoModuleCrew)
                {
                    modKerbalFactor = k.experienceLevel / 2f;
                    //(0.25 - 3.25)
                    if (k.experienceTrait.Title == "Pilot")
                    {
                        modKerbalFactor *= .5f;
                    }
                    if (k.experienceTrait.Title == "Engineer")
                    {
                        modKerbalFactor *= 1.5f;
                    }
                }
                print("modKerbalFactor: " + modKerbalFactor);

                var numModuleKerbals = part.protoModuleCrew.Count();
                print("NumModuleKerbals: " + numModuleKerbals);

                var numShipKerbals = vessel.GetCrewCount() - numModuleKerbals;
                print("ShipKerbals: " + numShipKerbals);

                float numWeightedKerbals = (numShipKerbals * 0.5f) + modKerbalFactor;
                print("ShipKerbals: " + numShipKerbals);

                print("numWeightedKerbals: " + numWeightedKerbals);
                numWeightedKerbals *= GetCrewHappiness();
                print("numWeightedKerbals: " + numWeightedKerbals);

                //Worst case, 50% crewed, 25% uncrewed
                float eff = .25f;

                if (vessel.GetCrewCount() > 0)
                {
                    float WorkSpaceKerbalRatio = numWorkspaces / vessel.GetCrewCount();
                    if (WorkSpaceKerbalRatio > 3) WorkSpaceKerbalRatio = 3;
                    print("WorkSpaceKerbalRatio: " + WorkSpaceKerbalRatio);

                    float WorkUnits = WorkSpaceKerbalRatio * numWeightedKerbals;
                    print("WorkUnits: " + WorkUnits);
                    eff = WorkUnits / numModules;
                    print("eff: " + eff);
                    if (eff > 2.5) eff = 2.5f;
                    if (eff < .5) eff = .5f;
                }

                print("effpartname: " + efficiencyPart);
                //Add in efficiencyParts 
                if (efficiencyPart != "")
                {
                    var genParts = vessel.Parts.Count(p => p.name == part.name);
                    var effPartList = vessel.Parts.Where(p => p.name == (efficiencyPart.Replace('_', '.')));
                    var effParts = 0;

                    foreach (var ep in effPartList)
                    {
                        var mod = ep.FindModuleImplementing<USIAnimation>();
                        if (mod == null)
                        {
                            effParts++;
                        }
                        else
                        {
                            if (mod.isDeployed)
                                effParts++;
                        };
                    }

                    effParts = (effParts - genParts) / genParts;
                    print("effParts: " + effParts);
                    print("oldEff: " + eff);
                    eff += effParts;
                    print("newEff: " + eff);
                    if (eff < 0.25)
                        eff = 0.25f;  //We can go as low as 25% as these are almost mandatory.
                }

                if (!calculateEfficiency)
                {
                    eff = 1f;
                    efficiency = String.Format("100% [Fixed]");
                }
                else if (governorActive)
                {
                    if (eff > 1f) eff = 1f;
                    efficiency = String.Format("G:{0}% [{1}k/{2}s/{3}m/{4}c]", Math.Round((eff * 100), 1), numShipKerbals, numWorkspaces, numModules, Math.Round(numWeightedKerbals, 1));
                }
                else
                {
                    efficiency = String.Format("{0}% [{1}k/{2}s/{3}m/{4}c]", Math.Round((eff * 100), 1), numShipKerbals, numWorkspaces, numModules, Math.Round(numWeightedKerbals, 1));
                }
                return eff;
            }
            catch (Exception ex)
            {
                print(String.Format("[MKS] - ERROR in GetEfficiency - {0}", ex.Message));
                return 1f;
            }
        }








        public float GetEfficiencyRate()
        {
            var curConverters = GetActiveKolonyModules(vessel);
            if (curConverters != _numConverters)
            {
                _numConverters = curConverters;
                EfficiencySetup();
            }
            return _efficiencyRate;
        }

        public override void OnLoad(ConfigNode node)
        {
            try
            {
                if (!hasGenerators)
                {
                    Fields["efficiency"].guiActive = false;
                    Events["ToggleGovernor"].active = false;
                }
            }
            catch (Exception ex)
            {
                print("ERROR IN MKSModuleOnLoad - " + ex.Message);
            }
        }
    }

*/ 
}