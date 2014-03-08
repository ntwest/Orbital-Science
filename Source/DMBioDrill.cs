﻿/* DMagic Orbital Science - Bio Drill data
 * Setup bio drill for limited reuse function.
 *
 * Copyright (c) 2014, DMagic
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 * 1. Redistributions of source code must retain the above copyright notice, 
 * this list of conditions and the following disclaimer.
 * 
 * 2. Redistributions in binary form must reproduce the above copyright notice, 
 * this list of conditions and the following disclaimer in the documentation and/or other materials 
 * provided with the distribution.
 * 
 * 3. Neither the name of the copyright holder nor the names of its contributors may be used 
 * to endorse or promote products derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF 
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT 
 * OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *  
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Collections;

namespace DMagic
{
    class DMBioDrill : PartModule, IScienceDataContainer
    {
        [KSPField(isPersistant = false)]
        public string animationName = null;
        [KSPField(isPersistant = false)]
        public string verticalDrillName = null;
        [KSPField(isPersistant = false)]
        public string sampleAnim = null;
        [KSPField(isPersistant = false)]
        public string indicatorAnim = null;
        [KSPField(isPersistant = false)]
        public string sampleEmptyAnim = null;

        [KSPField(isPersistant = true)]
        public int experimentNumber = 0;        
        [KSPField(isPersistant = false)]
        public string expID = null;
        [KSPField(isPersistant = false)]
        public float xmitDataValue = 0f;

        public int labSource = 0;
        public bool labPresent = false;
        public int storedDataCount = 0;
        public int labint = 0;

        protected Animation anim;
        protected Animation animSample;

        protected List<ModuleScienceLab> labList = new List<ModuleScienceLab>();
        //Science data lists to store initial and stored science results.
        protected List<ScienceData> scienceList = new List<ScienceData>();
        protected List<ScienceData> storedScienceList = new List<ScienceData>();
        
        public override void OnStart(PartModule.StartState state)
        {
                base.OnStart(state);
                this.part.force_activate();
                anim = part.FindModelAnimators(animationName)[0];
                anim = part.FindModelAnimators(verticalDrillName)[0];
                animSample = part.FindModelAnimators(sampleAnim)[0];
                animSample = part.FindModelAnimators(indicatorAnim)[0];
                animSample = part.FindModelAnimators(sampleEmptyAnim)[0];
                if (state == StartState.Editor)
                {
                    editorRetract();
                }
                else
                {
                    labList = vessel.FindPartModulesImplementing<ModuleScienceLab>();
                    labPresent = labList.Count > 0;
                    Events["ReviewStoredDataEvent"].active = storedScienceList.Count > 0;
                    Events["EVACollect"].active = storedScienceList.Count > 0;
                    sampleAnimation(sampleAnim, 0f, 0.3f * storedScienceList.Count, 1f);
                    sampleAnimation(indicatorAnim, 0f, 0.15f * experimentNumber, 1f);
                    if (experimentNumber > 0)
                    {
                        Events["labCleanExperiment"].active = labList.Count > 0;
                    }
                    else
                    {
                        Events["labCleanExperiment"].active = false;
                    }
                }        
        }

        # region Animator stuff

        //Primary drill animator, takes input from startDrill() to determine which animation to play.
        public void deployDrill(string drillAnimator, float drillSpeed, float drillTime)
        {
            if (anim != null)
            {
                anim[drillAnimator].speed = drillSpeed;
                anim[drillAnimator].normalizedTime = drillTime;
                anim.Blend(drillAnimator, 1f);
            }
        }
        
        //Sample container and indicator animations.
        public void sampleAnimation(string whichAnim, float sampleSpeed, float sampleTime, float waitTime)
        {
            if (animSample != null)
            {
                animSample[whichAnim].speed = sampleSpeed;
                animSample[whichAnim].normalizedTime = sampleTime;
                animSample.Blend(whichAnim, 1f);
                StartCoroutine(WaitForSampleAnimation(whichAnim, waitTime));
            }
        }
 
        IEnumerator WaitForSampleAnimation(string whichAnimCo, float waitTimeCo)
        {
            {
                yield return new WaitForSeconds(waitTimeCo);
                animSample[whichAnimCo].enabled = false;
            }
        }
        
        public void startDrill(float drillStartSpeed, float drillStartTime) //Determine drill orientation relative to parent part, set angle to -90 to 90.
        {
            double cosineAngle = 0;
            double processedRot = 0;
            cosineAngle = Mathf.Rad2Deg * Math.Acos(Vector3d.Dot(part.transform.up, part.parent.transform.up));
            if (cosineAngle > 180)
            {
                cosineAngle = 360 - cosineAngle;  
            }
            if (cosineAngle > 90)
            {
                cosineAngle -= 180;
            }            
            processedRot = Math.Abs(cosineAngle);
            
                if (processedRot < 90 && processedRot >= 50)
            {
                deployDrill(verticalDrillName, drillStartSpeed, drillStartTime);
            }
                else
                {
                    deployDrill(animationName, drillStartSpeed, drillStartTime);
                }
            if (HighLogic.LoadedSceneIsEditor)
            {
                print("Current rot is: " + Math.Round(processedRot, 2).ToString() + ". Values between 0 and 50 play the horizontal drill animation, values between 50 and 90 play the vertical animation.");            
            }
        }
        
        # endregion 

        # region Event and Action Groups

        [KSPEvent(guiActive = true, guiName = "Clean Experiment", active = false)]
        public void labCleanExperiment()
        {
            bool labOperating = checkLabOps();
            if (labList.Count > 0 && labOperating)
            {
                experimentNumber = 0;
                ScreenMessages.PostScreenMessage("Resetting XKCD experiment count.", 4f, ScreenMessageStyle.UPPER_CENTER);
                sampleAnimation(indicatorAnim, 0f, 0.15f * experimentNumber, 1f);
                Events["labCleanExperiment"].active = experimentNumber > 0;
            }
            else
            {
                ScreenMessages.PostScreenMessage("No operational science lab available, cannot resupply the drill.", 4f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        [KSPEvent(guiActive = true, guiName = "Review Stored Data", active = false)]
        public void ReviewStoredDataEvent()
        {
            GetData(); //Make science lab behave correctly
            storedDataCount = 0;
            foreach (ScienceData data in storedScienceList) //Open all stored science reports
            {
                ReviewData();
                storedDataCount++;
            }
        }

        [KSPEvent(guiActive = true, guiName = "Review Primary Data", active = false)]
        public void ReviewDataEvent()
        {
            ReviewPrimaryData();
        }

        [KSPAction("Test Drill")]
        public void testDrillAnimator(KSPActionParam param)
        {
            if (anim.IsPlaying(animationName) || anim.IsPlaying(verticalDrillName)) { return; }
            startDrill(1f, 0f);
        }

        [KSPAction("Review Stored Sample Data")]
        public void reviewStoredDataAG(KSPActionParam param)
        {
            ReviewStoredDataEvent();
        }

        [KSPEvent(guiActiveUnfocused = true, guiName = "Collect Stored Data", externalToEVAOnly = true, unfocusedRange = 1.5f, active = false)]
        public void EVACollect()
        {
            List<ModuleScienceContainer> EVACont = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>();
            if (storedScienceList.Count > 0)
            {
                if (EVACont.First().StoreData(new List<IScienceDataContainer>() { this }, false))
                {
                    ScreenMessages.PostScreenMessage("Sample data transferred to " + FlightGlobals.ActiveVessel.name, 3f, ScreenMessageStyle.UPPER_CENTER);
                    sampleAnimation(sampleEmptyAnim, 1f, 1f - (storedScienceList.Count / 3f), 3f * storedScienceList.Count);
                    Events["ReviewStoredDataEvent"].active = storedScienceList.Count > 0;
                    Events["EVACollect"].active = storedScienceList.Count > 0;
                }
            }
        }

        [KSPEvent(guiActiveEditor = true, guiName = "Preview Drill", active = true)]
        public void editorDeploy()
        {
                startDrill(0f, 0.43f);
                Events["editorDeploy"].active = false;
                Events["editorRetract"].active = true;
        }

        [KSPEvent(guiActiveEditor = true, guiName = "Reset Drill", active = false)]
        public void editorRetract()
        {            
            anim[animationName].enabled = false;
            anim[verticalDrillName].enabled = false;
            startDrill(-1f, 0f);
            Events["editorDeploy"].active = true;
            Events["editorRetract"].active = false;
        }

        # endregion
               
        # region Science data setup
        
        //Determine current biome based on vessel position, special cases for KSC area.
        public string BiomeCheck()
        {
            switch (vessel.landedAt)
            {
                case "LaunchPad":
                    return vessel.landedAt;
                case "Runway":
                    return vessel.landedAt;
                case "KSC":
                    return vessel.landedAt;
                default:
                    return FlightGlobals.currentMainBody.BiomeMap.GetAtt(vessel.latitude * Mathf.Deg2Rad, vessel.longitude * Mathf.Deg2Rad).name;
            }
        }

        //Create new science results, find current vessel position and biome to get the right result.
        public ScienceData makeNewScience() 
        {
            string currentBiome = BiomeCheck();
            ScienceData data = null;
            ScienceExperiment exp = ResearchAndDevelopment.GetExperiment(expID);
            ScienceSubject sub = ResearchAndDevelopment.GetExperimentSubject(exp, ExperimentSituations.SrfLanded, vessel.mainBody, currentBiome);
            if (vessel.mainBody.name == "Eve") // Big boost for reports returned from Eve.
            {
                data = new ScienceData(exp.baseValue * sub.dataScale * 2.5f, xmitDataValue / 2.5f, 0.15f, expID, exp.experimentTitle + " of " + vessel.mainBody.theName + " " + currentBiome);
            }
            else
            {
                data = new ScienceData(exp.baseValue * sub.dataScale, xmitDataValue, 0.15f, expID, exp.experimentTitle + " of " + vessel.mainBody.theName + " " + currentBiome);
            }
            data.subjectID = sub.id;
            return data;
        }
        
        //Data collection event, plays drill animation and collects science result. Not functional after six uses.
        [KSPEvent(guiActive = true, guiName = "Collect Core Sample", active = true)]
        public void StartExperiment()
        {
            if (vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                if (vessel.mainBody.name == "Eve" || vessel.mainBody.name == "Kerbin" || vessel.mainBody.name == "Duna" || vessel.mainBody.name == "Laythe" || vessel.mainBody.name == "Bop" || vessel.mainBody.name == "Vall")
                {
                    if (experimentNumber >= 6)
                    {
                        ScreenMessages.PostScreenMessage("All sample resources have been used, please return to Kerbin or resupply with a science lab.", 4f, ScreenMessageStyle.UPPER_CENTER);
                    }
                    else
                    {
                        if (scienceList.Count > 0)
                        {
                            ScreenMessages.PostScreenMessage("Initial data must be stored or transmitted before more can be collected.", 4f, ScreenMessageStyle.UPPER_CENTER);
                        }
                        else
                        {
                            if (anim.IsPlaying(animationName) || anim.IsPlaying(verticalDrillName)) { return; }
                            else
                            {
                                startDrill(1f, 0f);
                                StartCoroutine(WaitForAnimation(anim[animationName].length));
                            }
                        }
                    }
                }
                else
                {
                    ScreenMessages.PostScreenMessage("The XKCD is only meant to be used on atmospheric planets.", 4f, ScreenMessageStyle.UPPER_CENTER);
                }
            }
            else
            {
                ScreenMessages.PostScreenMessage("The XKCD can only be used on the surface.", 4f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        [KSPAction("Collect Core Sample")]
        public void StartExperimentAG(KSPActionParam param)
        {
            StartExperiment();
        }

        IEnumerator WaitForAnimation(float animTime)
            {
                {
                    yield return new WaitForSeconds(animTime);
                    ScienceData data = makeNewScience();
                    scienceList.Add(data);
                    ReviewPrimaryData();
                    Events["ReviewDataEvent"].active = scienceList.Count > 0;
                }
            }

        # endregion

        # region IScienceDataContainer and ExperimentResultDialogPage stuff

        //Other IScienceDataContainer stuff
        public override void OnSave(ConfigNode node) //Save results in the stored data list.
        {
            base.OnSave(node);
            foreach (ScienceData storedData in storedScienceList)
            {
                ConfigNode storedDataNode = node.AddNode("ScienceData");
                storedData.Save(storedDataNode);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasNode("ScienceData"))
            {
                foreach (ConfigNode storedDataNode in node.GetNodes("ScienceData"))
                {
                    ScienceData data = new ScienceData(storedDataNode);
                    storedScienceList.Add(data);
                }
            }
        }

        public override void OnUpdate() //Check for labs after docking.
        {
            labList = vessel.FindPartModulesImplementing<ModuleScienceLab>();
            labPresent = labList.Count > 0;
            if (experimentNumber > 0)
            {
                Events["labCleanExperiment"].active = labList.Count > 0;
            }
        }
       
        public ScienceData[] GetData()     //Only return stored data to main SciencData list.
        {
            return storedScienceList.ToArray();
        }

        public int GetScienceCount()
        {
            return storedScienceList.Count;
        }

        public bool IsRerunnable()
        {
            return true;
        }
              
        public bool checkLabOps()  //Make sure any science labs present are operational.
        {
            bool labOp = false;
            for (labint = 0; labint < labList.Count; labint++)
            {
                if (labList[labint].IsOperational())
                {
                    labOp = true;
                    break;
                }
            }
            return labOp;
        }

        //IScienceDataContainer functions for initial Science Data list.
        public void DumpPrimaryData(ScienceData data)
        {
            scienceList.Remove(data);
            Events["ReviewDataEvent"].active = scienceList.Count > 0;
        }

        private void onPrimaryPageDiscard(ScienceData data)
        {
            DumpPrimaryData(data);
        }

        //Transfer data from primary Science data list to stored data list. Only allow three stored samples.
        private void onKeepPrimaryData(ScienceData data)
        {
            if (storedScienceList.Count > 2)
            {
                ScreenMessages.PostScreenMessage("All sample incubation chambers full, data can only be transmitted.", 4f, ScreenMessageStyle.UPPER_CENTER);
                ReviewPrimaryData();
            }
            else
            {
                sampleAnimation(sampleAnim, 1f, 0.3f * storedScienceList.Count, 3f);
                storedScienceList.Add(scienceList[0]);
                scienceList.Remove(data);
                bool labOperating = checkLabOps();
                if (!labOperating)
                {
                    sampleAnimation(indicatorAnim, 0.5f, 0.15f * experimentNumber, 3f);
                    experimentNumber++;           //Experiment infinitely repeatable if operational science lab is connected.
                    Events["labCleanExperiment"].active = labList.Count > 0;
                }
                Events["ReviewStoredDataEvent"].active = storedScienceList.Count > 0;
                Events["EVACollect"].active = storedScienceList.Count > 0;
                Events["ReviewDataEvent"].active = scienceList.Count > 0;
            }
        }
       
        private void onTransmitPrimaryData(ScienceData data)
        {
            bool tranBusy = false;
            List<IScienceDataTransmitter> tranList = vessel.FindPartModulesImplementing<IScienceDataTransmitter>();
            if (tranList.Count > 0 && scienceList.Contains(data))
            {
                foreach (IScienceDataTransmitter tran in tranList)
                {
                    if (tran.CanTransmit())
                    {
                        if (!tran.IsBusy()) //Check for non-busy transmitters to use.
                        {
                            List<ScienceData> tranData = new List<ScienceData>();
                            tranData.Add(data);
                            tran.TransmitData(tranData);
                            scienceList.Remove(data);
                            bool labOperating = checkLabOps();
                            if (!labOperating)
                            {
                                sampleAnimation(indicatorAnim, 0.5f, 0.15f * experimentNumber, 3f);
                                experimentNumber++;        
                                Events["labCleanExperiment"].active = labList.Count > 0;
                            }
                            Events["ReviewDataEvent"].active = scienceList.Count > 0;
                            tranBusy = false;
                            break;
                        }
                        else
                        {
                            tranBusy = true; 
                        }
                    }
                }
                if (tranBusy)   //If all transmitters are busy add data to queue for first transmitter.
                {
                    List<ScienceData> tranData = new List<ScienceData>();
                    tranData.Add(data);
                    tranList.First().TransmitData(tranData);
                    scienceList.Remove(data);
                    bool labOperating = checkLabOps();
                    if (!labOperating)
                    {
                        sampleAnimation(indicatorAnim, 0.5f, 0.15f * experimentNumber, 3f);
                        experimentNumber++;        
                        Events["labCleanExperiment"].active = labList.Count > 0;
                    }
                    Events["ReviewDataEvent"].active = scienceList.Count > 0;
                }
            }            
        }        

        private void onSendPrimaryToLab(ScienceData data)
        {
            bool labOperating = checkLabOps();
            if (labOperating)          //Process data if operational science lab is present.
            {
                labSource = 1;      //Mark data source as the primary experiment dialog.
                labList[labint].StartCoroutine(labList[labint].ProcessData(data, new Callback<ScienceData>(onComplete)));
            }
            else
            {
                ScreenMessages.PostScreenMessage("No operational lab modules on this vessel, cannot analyze data.", 4f, ScreenMessageStyle.UPPER_CENTER);
            }            
        }

        public void ReviewPrimaryData()
        {
            ScienceData exp = scienceList[0];
            ExperimentResultDialogPage page = new ExperimentResultDialogPage(part, exp, exp.transmitValue, 0.15f, experimentNumber >= 5, "The drill will not be functional after transmitting this data.", true, labPresent, new Callback<ScienceData>(onPrimaryPageDiscard), new Callback<ScienceData>(onKeepPrimaryData), new Callback<ScienceData>(onTransmitPrimaryData), new Callback<ScienceData>(onSendPrimaryToLab));
            ExperimentsResultDialog.DisplayResult(page);
        }        
        
        public void ReviewPrimaryDataItem(ScienceData data)
        {
            ScienceData exp = scienceList[0];
            ExperimentResultDialogPage page = new ExperimentResultDialogPage(part, exp, exp.transmitValue, 0.15f, experimentNumber >= 5, "The drill will not be functional after transmitting this data.", true, labPresent, new Callback<ScienceData>(onPrimaryPageDiscard), new Callback<ScienceData>(onKeepPrimaryData), new Callback<ScienceData>(onTransmitPrimaryData), new Callback<ScienceData>(onSendPrimaryToLab));
            ExperimentsResultDialog.DisplayResult(page);
        }
        
        //Science lab data processing function.
        private void onComplete(ScienceData data)
        {
            if (labSource == 2)  //Restart the correct experiment dialog page, remove the science lab boost option.
            {
                ReviewStoredDataEvent();
            }
            else if (labSource == 1)
            {
                labPresent = false;
                ReviewPrimaryData();
            }
        }

        //IScienceDataContainer functions for stored science data.
        public void DumpData(ScienceData data)
        {
            sampleAnimation(sampleEmptyAnim, 1f, 1f - (storedScienceList.Count/3f), 3f);
            sampleAnimation(indicatorAnim, -0.5f, 0.15f * experimentNumber, 3f);
            experimentNumber--;
            storedScienceList.Remove(data);
            Events["ReviewStoredDataEvent"].active = storedScienceList.Count > 0;
            Events["EVACollect"].active = storedScienceList.Count > 0;
            if (experimentNumber <= 0)
            {
                experimentNumber = 0;     //Don't allow negative experimentNumber. 
                Events["labCleanExperiment"].active = false;
            }
        }

        private void onPageDiscard(ScienceData data)
        {
            DumpData(data);
        }

        private void onKeepData(ScienceData data)
        {
        }

        private void onTransmitData(ScienceData data)
        {
            bool tranBusy = false;
            List<IScienceDataTransmitter> tranList = vessel.FindPartModulesImplementing<IScienceDataTransmitter>();
            if (tranList.Count > 0 && storedScienceList.Contains(data))
            {
                foreach (IScienceDataTransmitter tran in tranList)
                {
                    if (tran.CanTransmit())
                    {
                        if (!tran.IsBusy())
                        {
                            List<ScienceData> tranData = new List<ScienceData>();
                            tranData.Add(data);
                            tran.TransmitData(tranData);
                            sampleAnimation(sampleEmptyAnim, 1f, 1f - (storedScienceList.Count / 3f), 3f);
                            storedScienceList.Remove(data);
                            Events["ReviewStoredDataEvent"].active = storedScienceList.Count > 0;
                            Events["EVACollect"].active = storedScienceList.Count > 0;
                            tranBusy = false;
                            break;
                        }
                        else
                        {
                            tranBusy = true;
                        }
                    }
                }
                if (tranBusy)
                {
                    List<ScienceData> tranData = new List<ScienceData>();
                    tranData.Add(data);
                    tranList.First().TransmitData(tranData);
                    sampleAnimation(sampleEmptyAnim, 1f, 1f - (storedScienceList.Count / 3f), 3f);
                    storedScienceList.Remove(data);
                    Events["ReviewStoredDataEvent"].active = storedScienceList.Count > 0;
                    Events["EVACollect"].active = storedScienceList.Count > 0;
                }
            }
        }

        private void onSendToLab(ScienceData data)
        {
            bool labOperating = checkLabOps();
            if (labOperating)
            {
                labSource = 2; //Mark source as the stored data dialog.
                labList[labint].StartCoroutine(labList[labint].ProcessData(data, new Callback<ScienceData>(onComplete)));
            }
            else
            {
                ScreenMessages.PostScreenMessage("No operational lab modules on this vessel, cannot analyze data.", 4f, ScreenMessageStyle.UPPER_CENTER);
            }              
        }

        public void ReviewData()
        {
            ScienceData storedExp = storedScienceList[storedDataCount];
            ExperimentResultDialogPage page = new ExperimentResultDialogPage(part, storedExp, storedExp.transmitValue, 0.15f, experimentNumber == 6, "No more samples can be collected or stored after this data is transmitted.", false, labPresent, new Callback<ScienceData>(onPageDiscard), new Callback<ScienceData>(onKeepData), new Callback<ScienceData>(onTransmitData), new Callback<ScienceData>(onSendToLab));
            ExperimentsResultDialog.DisplayResult(page);
        }

        public void ReviewDataItem(ScienceData data)
        {
            ReviewStoredDataEvent();
        }

        # endregion

    }
}


