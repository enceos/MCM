using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using USITools;
using KolonyTools;
using System.Collections.Generic;

namespace WildBlueIndustries
{
    public class MKSModuleX : MKSModule
    {
        private int _numConverters;
        private float _efficiencyRate;

        public override float GetEfficiencyRate()
        {
            var curConverters = GetActiveKolonyModules(vessel);
            if (curConverters != _numConverters)
            {
                _numConverters = curConverters;
                EfficiencySetup();
            }
            return _efficiencyRate;
        }

        private void EfficiencySetup()
        {
            _efficiencyRate = GetEfficiency();
        }

        private float GetCrewHappiness()
        {
            if (vessel == null)
                return 0;
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

        private int GetKolonyLivingSpace(Vessel v)
        {
            try
            {
                if (v == null)
                    return 0;
                int numberOfLivingSpaces = 0;
                List<MKSModule> mksModules;
                WBIInflatablePartModule inflatablePart;
                USIAnimation usiPart;

                foreach (Part part in vessel.parts)
                {
                    mksModules = part.FindModulesImplementing<MKSModule>();
                    usiPart = part.FindModuleImplementing<USIAnimation>();
                    inflatablePart = part.FindModuleImplementing<WBIInflatablePartModule>();

                    //Look for living spaces for the MKS module.
                    if (mksModules != null)
                    {
                        foreach (MKSModule mks in mksModules)
                        {
                            if (usiPart == null && inflatablePart == null)
                            {
                                numberOfLivingSpaces += mks.livingSpace;
                            }

                            else if (usiPart != null)
                            {
                                if (usiPart.isDeployed)
                                    numberOfLivingSpaces += mks.livingSpace;
                            }

                            else if (inflatablePart != null)
                            {
                                if (inflatablePart.isDeployed && inflatablePart.isInflatable)
                                    numberOfLivingSpaces += mks.livingSpace;
                            }
                        }
                    }
                }

                return numberOfLivingSpaces;
            }
            catch (Exception ex)
            {
                print(String.Format("[MKS] - ERROR in GetKolonyLivingSpace - {0}", ex.Message));
                return 0;
            }
        }

        private int GetKolonyWorkspaces(Vessel vessel)
        {
            try
            {
                print("GetKolonyWorkspaces called");
                if (vessel == null)
                    return 0;
                int numberOfWorkspaces = 0;
                List<MKSModule> mksModules;

                foreach (Part part in vessel.parts)
                {
                    mksModules = part.FindModulesImplementing<MKSModule>();

                    if (mksModules != null)
                    {
                        print("found MKSModules");
                        foreach (MKSModule mks in mksModules)
                            numberOfWorkspaces += mks.workSpace;
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
                if (v == null)
                    return 0;
                int numberOfKonverters = 0;
                List<KolonyConverter> converters = null;

                foreach (Part part in vessel.parts)
                {
                    converters = part.FindModulesImplementing<KolonyConverter>();
                    if (converters != null)
                    {
                        foreach (KolonyConverter converter in converters)
                        {
                            if (converter.IsActivated)
                                numberOfKonverters += 1;
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

        private float GetKerbalFactor(ProtoCrewMember k)
        {
            var kerbalFactor = k.experienceLevel / 2f;
            //A level 0 Kerbal is not quite zero - it.s 0.1
            if (kerbalFactor < 0.1)
                kerbalFactor = 0.1f;

            // Level 0 Pilot:       0.05
            // Level 0 Engineer:    0.15
            // Level 1 Pilot:       0.25
            // Level 1 Engineer:    0.75
            // Level 2 Pilot:       0.50
            // Level 2 Engineer:    1.50
            // Level 5 Pilot:       1.25
            // Level 5 engineer:    3.25

            //(0.025 - 3.25)
            if (k.experienceTrait.Title == PrimarySkill)
            {
                kerbalFactor *= 1.5f;
            }
            else if (k.experienceTrait.Title == SecondarySkill)
            {
                kerbalFactor *= 1f;
            }
            else
            {
                kerbalFactor *= 0.5f;
            }
            return kerbalFactor;
        }

        private float GetEfficiency()
        {
            try
            {
                print("GetEfficiency called");
                //Efficiency is a function of:
                //  - Workspaces                [numWorkspaces]
                //  - 25% of Crew Cap           [numWorkSpaces]
                //  - Active MKS Module count   [numModules]
                //  - Crew in the module itself [modKerbalFactor]   (0.05 - 3.5 per Kerbal)
                //  - All Kerbals in the crew   [numWeightedKerbals]
                //  - efficiency parts          [added to eff]
                //          Bonus equal to 100 * number of units - 1

                float numWorkspaces = GetKolonyWorkspaces(vessel);
                print("NumWorkspaces: " + numWorkspaces);

                //Plus 25% of Crew Cap as low efficiency workspaces
                numWorkspaces += vessel.GetCrewCapacity() * .25f;
                print("AdjNumWorkspaces: " + numWorkspaces);

                //Number of active modules
                var numModules = GetActiveKolonyModules(vessel);
                print("numModules: " + numModules);

                //Kerbals in the module
                float modKerbalFactor = part.protoModuleCrew.Sum(k => GetKerbalFactor(k));
                print("modKerbalFactor: " + modKerbalFactor);
                modKerbalFactor *= GetCrewHappiness();
                print("HappymodKerbalFactor: " + modKerbalFactor);

                //Kerbals in the ship
                float numWeightedKerbals = vessel.GetVesselCrew().Sum(k => GetKerbalFactor(k));
                print("numWeightedKerbals: " + numWeightedKerbals);
                numWeightedKerbals *= GetCrewHappiness();
                print("HappynumWeightedKerbals: " + numWeightedKerbals);

                //Worst case, 25% (if crewed).  Uncrewed vessels will be at 0%
                //You need crew for these things, no robo ships.
                float eff = .0f;
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
                    if (eff < .25) eff = .25f;
                }

                //Add in efficiencyParts 
                if (efficiencyPart != "")
                {
                    print("Efficiency before adding parts: " + eff);
                    eff += GetPartEfficiency();
                    print("Efficiency after adding parts: " + eff);
                    if (eff < 0.25)
                        eff = 0.25f;  //We can go as low as 25% as these are almost mandatory.
                }

                if (!calculateEfficiency)
                {
                    eff = 1f;
                    efficiency = String.Format("100% [Fixed]");
                }

                efficiency = String.Format("{0}% [{1}k/{2}s/{3}m/{4}c]", Math.Round((eff * 100), 1), Math.Round(modKerbalFactor, 1), numWorkspaces, numModules, Math.Round(numWeightedKerbals, 1));

                return eff;
            }
            catch (Exception ex)
            {
                print(String.Format("[MKS] - ERROR in GetEfficiency - {0}", ex.Message));
                return 1f;
            }
        }

        public virtual float GetPartEfficiency()
        {
            return 0;
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
}