﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using UnityEngine;
using System.Reflection;

namespace MuMech
{
    public class MechJebCore : PartModule, IComparable<MechJebCore>
    {
        private const int windowIDbase = 60606;

        private List<ComputerModule> computerModules = new List<ComputerModule>();
        private bool modulesUpdated = false;

        private static List<Type> moduleRegistry;

        public MechJebModuleAttitudeController attitude;
        public MechJebModuleStagingController staging;
        public MechJebModuleThrustController thrust;
        public MechJebModuleTargetController target;
        public MechJebModuleWarpController warp;
        public MechJebModuleRCSController rcs;

        public VesselState vesselState = new VesselState();

        private Vessel controlledVessel; //keep track of which vessel we've added our onFlyByWire callback to

        public string version = "";

        [KSPField(isPersistant = false)]
        public string blacklist = "";

        //Returns whether the vessel we've registered OnFlyByWire with is the correct one. 
        //If it isn't the correct one, fixes it before returning false
        bool CheckControlledVessel()
        {
            if (controlledVessel == vessel) return true;

            //else we have an onFlyByWire callback registered with the wrong vessel:
            //handle vessel changes due to docking/undocking
            if (controlledVessel != null) controlledVessel.OnFlyByWire -= OnFlyByWire;
            vessel.OnFlyByWire += OnFlyByWire;
            controlledVessel = vessel;
            return false;
        }

        public int GetImportance()
        {
            if (part.State == PartStates.DEAD)
            {
                return 0;
            }
            else
            {
                return GetInstanceID();
            }
        }

        public int CompareTo(MechJebCore other)
        {
            if (other == null) return 1;
            return GetImportance().CompareTo(other.GetImportance());
        }

        public T GetComputerModule<T>() where T : ComputerModule
        {
            return (T)computerModules.First(a => a is T);
        }

        public List<T> GetComputerModules<T>() where T : ComputerModule
        {
            return computerModules.FindAll(a => a is T).Cast<T>().ToList();
        }

        public ComputerModule GetComputerModule(string type)
        {
            return computerModules.First(a => a.GetType().Name.ToLowerInvariant() == type.ToLowerInvariant());
        }

        public void AddComputerModule(ComputerModule module)
        {
            computerModules.Add(module);
            modulesUpdated = true;
        }

        public override void OnStart(PartModule.StartState state)
        {
            if (moduleRegistry == null)
            {
                moduleRegistry = (from ass in AppDomain.CurrentDomain.GetAssemblies() from t in ass.GetTypes() where t.IsSubclassOf(typeof(ComputerModule)) select t).ToList();
            }

            Version v = Assembly.GetAssembly(typeof(MechJebCore)).GetName().Version;
            version = v.Major.ToString() + "." + v.Minor.ToString() + "." + v.Build.ToString();

            foreach (Type t in moduleRegistry)
            {
                if ((t != typeof(ComputerModule)) && (t != typeof(DisplayModule)) && !blacklist.Contains(t.Name))
                {
                    AddComputerModule((ComputerModule)(t.GetConstructor(new Type[] { typeof(MechJebCore) }).Invoke(new object[] { this })));
                }
            }

            attitude = GetComputerModule<MechJebModuleAttitudeController>();
            staging = GetComputerModule<MechJebModuleStagingController>();
            thrust = GetComputerModule<MechJebModuleThrustController>();
            target = GetComputerModule<MechJebModuleTargetController>();
            warp = GetComputerModule<MechJebModuleWarpController>();
            rcs = GetComputerModule<MechJebModuleRCSController>();

            foreach (ComputerModule module in computerModules)
            {
                module.OnStart(state);
            }

            vessel.OnFlyByWire += Drive;
            controlledVessel = vessel;
        }

        public override void OnActive()
        {
            foreach (ComputerModule module in computerModules)
            {
                module.OnActive();
            }
        }
        
        public override void OnInactive()
        {
            foreach (ComputerModule module in computerModules)
            {
                module.OnInactive();
            }
        }

        public override void OnAwake()
        {
            foreach (ComputerModule module in computerModules)
            {
                module.OnAwake();
            }
        }

        public void FixedUpdate()
        {
            CheckControlledVessel(); //make sure our onFlyByWire callback is registered with the right vessel

            if (this != vessel.GetMasterMechJeb())
            {
                return;
            }

            vesselState.Update(vessel);

            if (modulesUpdated)
            {
                computerModules.Sort();
                modulesUpdated = false;
            }

            foreach (ComputerModule module in computerModules)
            {
                module.OnFixedUpdate();
            }


        }

        public void Update()
        {
            if (this != vessel.GetMasterMechJeb())
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha7))
            {
                MechJebModuleLandingAutopilot lander = GetComputerModule<MechJebModuleLandingAutopilot>();
                lander.enabled = !lander.enabled;
                Debug.Log("lander.enabled = " + lander.enabled);
            }

            if (modulesUpdated)
            {
                computerModules.Sort();
                modulesUpdated = false;
            }

            foreach (ComputerModule module in computerModules)
            {
                module.OnUpdate();
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node); //is this necessary?

            ConfigNode type = new ConfigNode(KSP.IO.File.Exists<MechJebCore>("mechjeb_settings.cfg", vessel) ? KSP.IO.File.ReadAllText<MechJebCore>("mechjeb_settings.cfg", vessel) : "");
            ConfigNode global = new ConfigNode(KSP.IO.File.Exists<MechJebCore>("mechjeb_settings.cfg") ? KSP.IO.File.ReadAllText<MechJebCore>("mechjeb_settings.cfg") : "");

            foreach (ComputerModule module in computerModules)
            {
                module.OnLoad(node, type, global);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node); //is this necessary?

            ConfigNode type = new ConfigNode(KSP.IO.File.Exists<MechJebCore>("mechjeb_settings.cfg", vessel) ? KSP.IO.File.ReadAllText<MechJebCore>("mechjeb_settings.cfg", vessel) : "");
            ConfigNode global = new ConfigNode(KSP.IO.File.Exists<MechJebCore>("mechjeb_settings.cfg") ? KSP.IO.File.ReadAllText<MechJebCore>("mechjeb_settings.cfg") : "");

            foreach (ComputerModule module in computerModules)
            {
                module.OnSave(node, type, global);
            }

            KSP.IO.File.WriteAllText<MechJebCore>(type.ToString(), "mechjeb_settings.cfg", vessel);
            KSP.IO.File.WriteAllText<MechJebCore>(global.ToString(), "mechjeb_settings.cfg");
        }

        public void OnDestroy()
        {
            foreach (ComputerModule module in computerModules)
            {
                module.OnDestroy();
            }

            vessel.OnFlyByWire -= OnFlyByWire;
            controlledVessel = null;
        }

        private void OnFlyByWire(FlightCtrlState s)
        {
            if (!CheckControlledVessel() || this != vessel.GetMasterMechJeb())
            {
                return;
            }

            Drive(s);

            CheckFlightCtrlState(s);

            if (vessel == FlightGlobals.ActiveVessel)
            {
                FlightInputHandler.state.mainThrottle = s.mainThrottle; //so that the on-screen throttle gauge reflects the autopilot throttle
            }
        }

        private void Drive(FlightCtrlState s)
        {
            if (this == vessel.GetMasterMechJeb())
            {
                foreach (ComputerModule module in computerModules)
                {
                    if (module.enabled) module.Drive(s);
                }
            }
        }

        //Setting FlightCtrlState fields to bad values can really mess up the game.
        //Here we do some error checking.
        public void CheckFlightCtrlState(FlightCtrlState s)
        {
            if (float.IsNaN(s.mainThrottle)) s.mainThrottle = 0;
            if (float.IsNaN(s.yaw)) s.yaw = 0;
            if (float.IsNaN(s.pitch)) s.pitch = 0;
            if (float.IsNaN(s.roll)) s.roll = 0;
            if (float.IsNaN(s.X)) s.X = 0;
            if (float.IsNaN(s.Y)) s.Y = 0;
            if (float.IsNaN(s.Z)) s.Z = 0;
            
            s.mainThrottle = Mathf.Clamp01(s.mainThrottle);
            s.yaw = Mathf.Clamp(s.yaw, -1, 1);
            s.pitch = Mathf.Clamp(s.pitch, -1, 1);
            s.roll = Mathf.Clamp(s.roll, -1, 1);
            s.X = Mathf.Clamp(s.X, -1, 1);
            s.Y = Mathf.Clamp(s.Y, -1, 1);
            s.Z = Mathf.Clamp(s.Z, -1, 1);            
        }

        private void OnGUI()
        {
            if ((HighLogic.LoadedSceneIsEditor) || ((FlightGlobals.ready) && (vessel == FlightGlobals.ActiveVessel) && (part.State != PartStates.DEAD) && (this == vessel.GetMasterMechJeb())))
            {
                int wid = 0;
                foreach (DisplayModule module in GetComputerModules<DisplayModule>())
                {
                    if (module.enabled) module.DrawGUI(windowIDbase + wid, HighLogic.LoadedSceneIsEditor);
                    wid++;
                }
            }

            //Todo: move this to the post-render queue since as it is it draws on top of windows
            MechJebModuleLandingPredictions p = GetComputerModule<MechJebModuleLandingPredictions>();
            ReentrySimulation.Result result = p.GetResult();
            if (MapView.MapIsEnabled && p.enabled && result != null)
            {
                //GLUtils.DrawPath(p.result.body, p.result.WorldTrajectory(), Color.cyan, false);
                if(result.outcome == ReentrySimulation.Outcome.LANDED) 
                {
                    GLUtils.DrawMapViewGroundMarker(result.body, result.endPosition.latitude, result.endPosition.longitude, Color.blue, 60);
                }
                else if (result.outcome == ReentrySimulation.Outcome.AEROBRAKED)
                {
                    Debug.Log("Drawing post-aerobrake orbit");
                    Orbit o = MuUtils.OrbitFromStateVectors(result.WorldEndPosition(), result.WorldEndVelocity(), result.body, result.endUT);
                    GLUtils.DrawOrbit(o, Color.blue);
                }
            }
        }

        // VAB/SPH description
        public override string GetInfo()
        {
            return "Attitude control by MechJeb™";
        }
    }
}
