using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rage;
using LSPD_First_Response.Mod.API;
using System.Reflection;
using System.Diagnostics;
using RAGENativeUI.Elements;
using RAGENativeUI;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.CompilerServices;
using RAGENativeUI.PauseMenu;
using LSPD_First_Response.Engine;
using System.ComponentModel;

namespace EvenBetterEMS
{
    public class EntryPoint
    {
        public static void Main()
        {
            Game.DisplayNotification("You have installed EvenBetterEMS incorrectly and in the wrong folder: you must install it in Plugins/LSPDFR. It will then be automatically loaded when going on duty - you must NOT load it yourself via RAGEPluginHook. This is also explained in the Readme.");
            return;
        }
    }

    internal class EvenBetterEMSHandler
    {
        //Definitions
        private static List<Ped> patientsBeingTreated = new List<Ped>();
        private static MenuPool EvenBetterMenus;
        private static UIMenu mainMenu;
        private static UIMenu callHospitalMenu;
        private static UIMenu settingsMenu;
        private static Vector3 spawnPoint;
        private static Vector3 parkPosition;
        private static Vector3 location;
        private static Vector3 directionToPlayer;
        private static float parkHeading;
        private static Vehicle ambul;
        private static Blip ambulBlip;
        private static Ped medicPed;
        private static Ped patientPed;
        private static bool isParked;
        private static bool hasWarped;

        //probabilities
        private static int prob_aliveIfDead = 50;
        private static int prob_stabileIfDead = 50;
        private static int prob_patientLivesIfDead = 50;
        private static int prob_stabileIfAlive = 70;
        private static int prob_patientLivesIfAlive = 70;

        //Keybinds
        private static readonly Keys KeyBinding_menuKey = Keys.F6;
        private static readonly Keys KeyBinding_callEMSKey = Keys.OemSemicolon;
        private static readonly Keys KeyBinding_warpEMSKey = Keys.Divide;
        private static readonly Keys KeyBinding_parkHere = Keys.Subtract;

        //Main plugin functions
        internal static void mainLoop()
        {
            Game.LogTrivial("EvenBetterEMS.Mainloop started");
            EvenBetterMenus = new MenuPool();
            mainMenu = new UIMenu("EvenBetterEMS", "EvenEvenBetter coming soon!");
            callHospitalMenu = new UIMenu("Call Hospital", "Check on past patients.");
            settingsMenu = new UIMenu("Settings", "The place where all the ini goods stay.");

            {
                var callHospitalMenuButton = new UIMenuItem("Call Hospital");
                var settingsMenuButton = new UIMenuItem("Settings");

                mainMenu.AddItems(callHospitalMenuButton, settingsMenuButton);
                mainMenu.BindMenuToItem(callHospitalMenu, callHospitalMenuButton);
                mainMenu.BindMenuToItem(settingsMenu, settingsMenuButton);
            }

            EvenBetterMenus.Add(mainMenu, callHospitalMenu, settingsMenu);

            //Start game fibers
            GameFiber.StartNew(processMenus);
            GameFiber.StartNew(callEMSButtonChecker); 
        }

        //function containing menu behavior
        private static void processMenus()
        {
            while (true)
            {
                GameFiber.Yield();
                EvenBetterMenus.ProcessMenus();

                if (Game.IsKeyDown(KeyBinding_menuKey) && !UIMenu.IsAnyMenuVisible && !TabView.IsAnyPauseMenuVisible)
                {
                    mainMenu.Visible = true;
                }
            }

        }

        //function to check if the nearby ped is injured.
        private static void pedChecker()
        {
            List<Ped> pedsToTreat = new List<Ped>();
            foreach (Ped ped in World.EnumeratePeds())
            {
                if (ped.Exists() && !patientsBeingTreated.Contains(ped) && ped.DistanceTo(Game.LocalPlayer.Character.Position) < 10f && (ped.IsDead || ped.Health < ped.MaxHealth))
                {
                    pedsToTreat.Add(ped);
                }
            }
            
            if (pedsToTreat.Count != 0)
            {
                patientsBeingTreated = pedsToTreat;
                foreach (Ped ped in patientsBeingTreated)
                {
                    if (!ped.IsDead)
                    {
                        ped.Tasks.Clear();
                        ped.IsPersistent = true;
                        ped.BlockPermanentEvents = true;
                    }
                }
                patientPed = patientsBeingTreated[0];
                if (!patientPed.IsDead)
                {
                    Game.DisplaySubtitle("You: Go ahead and lay down, the medic will be here ASAP.");
                    GameFiber.Wait(2000);
                    patientPed.Tasks.PlayAnimation("move_injured_ground", "back_outro", 8f, AnimationFlags.None).WaitForCompletion();
                    patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_def", "cpr_chestpump_idle", -1f, AnimationFlags.Loop);
                }
                
                callEMS();
            }
            else
            {
                Game.LogTrivial("No ped found.");
                Game.DisplayNotification("No injured peds found, try walking closer to them.");
            }
        }

        //function to send EMS to the location
        private static void callEMS()
        {
            if (patientPed != null)
            {
                Game.LogTrivial(patientPed.Health.ToString());
                spawnPoint = World.GetNextPositionOnStreet(patientPed.Position.Around(250f));
                location = World.GetNextPositionOnStreet(patientPed.Position.Around(20f));

                EvenBetterCleanup();

                ambul = new Vehicle("AMBULANCE", spawnPoint);
                medicPed = new Ped("s_m_m_paramedic_01", spawnPoint.Around(10f), 0f);

                medicPed.IsPersistent = true;
                medicPed.BlockPermanentEvents = true;

                ambul.IsPersistent = true;

                medicPed.WarpIntoVehicle(ambul, -1);

                if (ambulBlip.Exists())
                {
                    ambulBlip.Delete();
                }

                ambulBlip = medicPed.AttachBlip();
                ambulBlip.Color = System.Drawing.Color.Green;

                ambul.IsSirenOn = true;

                drivingTasks();
            }
            else
            {
                Game.DisplayNotification("~r~EvenBetterEMS did not detect an injured ped. Try standing closer to them.");
            }
        }

        public static void EvenBetterCleanup()
        {
            if (medicPed.Exists())
            {
                medicPed.Delete();
            }
            if (ambul.Exists())
            {
                ambul.Delete();
            }
            if(ambulBlip.Exists())
            {
                ambulBlip.Delete();
            }

            isParked = false;
            hasWarped = false;
        }

        private static void callEMSButtonChecker()
        {
            bool EMSGameTimer = false;

            while (true)
            {
                GameFiber.Yield();

                if (Game.IsKeyDown(KeyBinding_callEMSKey) && !EMSGameTimer)
                {
                    pedChecker();
                    
                    EMSGameTimer = true;
                    GameFiber.Wait(10000);
                    EMSGameTimer = false;
                } else if (Game.IsKeyDown(KeyBinding_callEMSKey) && EMSGameTimer)
                {
                    Game.DisplayHelp("You must wait 10 seconds before calling EMS again.");
                }
            }
        }

        private static void warpEMSCloserChecker()
        {
            bool WarpGameTimer = false;

            Game.DisplayHelp("EMS taking too long? press / to warp them closer!");

            while (true)
            {
                GameFiber.Yield();

                if (Game.IsKeyDown(KeyBinding_warpEMSKey) && ambul.Exists() && !WarpGameTimer)
                {
                    Game.DisplayNotification("Warping EMS closer to you!");
                    Vector3 new_spawnPoint = World.GetNextPositionOnStreet(Game.LocalPlayer.Character.Position.Around(40f));
                    ambul.SetPositionWithSnap(new_spawnPoint);
                    drivingTasks();
                    WarpGameTimer = true;
                    hasWarped = true;
                    GameFiber.Sleep(5000);
                    WarpGameTimer = false;
                }

                if (Game.IsKeyDown(KeyBinding_warpEMSKey) && !ambul.Exists())
                {
                    Game.DisplayHelp("You must call the medics before you can warp them to you!");
                }

                if (Game.IsKeyDown(KeyBinding_warpEMSKey) && ambul.Exists() && WarpGameTimer)
                {
                    Game.DisplayHelp("You must wait 5 seconds before warping again.");
                }
            }
        }

        private static void parkHereChecker()
        {
            Game.DisplayHelp("Taking too long to park while your patient is bleeding out? Hit - on the numpad to make them park where they are!");

            while (true)
            {
                GameFiber.Yield();

                if (Game.IsKeyDown(KeyBinding_parkHere) && ambul.Exists() && !isParked)
                {
                    isParked = true;

                    medicPed.Tasks.ParkVehicle(ambul.Position, ambul.Heading);
                    medicPed.Tasks.ClearImmediately();
                    medicTasks();
                }
            }
        }

        private static void drivingTasks()
        {
            Rage.Task drivingTask = medicPed.Tasks.DriveToPosition(location, 30f, VehicleDrivingFlags.Emergency);
            if (!hasWarped)
            {
                GameFiber.Wait(3000);
                GameFiber.StartNew(warpEMSCloserChecker);
            }
           
            if (!isParked)
            {
                GameFiber.Wait(5000);
                GameFiber.StartNew(parkHereChecker);
            }
           
            drivingTask.WaitForCompletion();
            ambul.IsSirenSilent = true;

            parkPosition = World.GetNextPositionOnStreet(location.Around(4f));
            parkHeading = ambul.Heading;
            medicPed.Tasks.ParkVehicle(parkPosition, parkHeading).WaitForCompletion(5000);

            isParked = true;

            medicTasks();
        }

        private static void medicTasks()
        {
            medicPed.Tasks.LeaveVehicle(LeaveVehicleFlags.None).WaitForCompletion(5000);
            
            medicPed.Tasks.GoToOffsetFromEntity(patientPed, 0f, 0f, 10f).WaitForCompletion();

            Game.DisplaySubtitle("Medic: Give us some room, I'm goin' in.");

            if (patientPed.IsDead)
            {
                patientPed.Health = (patientPed.MaxHealth)/2;
                patientPed.Resurrect();               

                /*
                float patientZ = patientPed.Position.Z;
                patientPed.SetPositionZ(patientZ + 10);
                patientPed.SetPositionWithSnap(medicPed.Position);
                */
                Game.LogTrivial(patientPed.Heading.ToString());
                patientPed.SetRotationYaw(medicPed.Rotation.Yaw + 180f);
                
                patientPed.SetPositionX(medicPed.Position.X + .5f);
                patientPed.SetPositionY(medicPed.Position.Y);

                Game.LogTrivial(patientPed.Position.ToString());

                patientPed.Tasks.ClearImmediately();

                GameFiber.Wait(1000);

                //add currentMedicTask and currentPatientTask Rage.Task and move waitforcompletion below both animations

                Rage.Task currentMedicTask;
                Rage.Task currentPatientTask;

                currentMedicTask = medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_def", "cpr_intro", 8f, AnimationFlags.None);
                currentPatientTask = patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_def", "cpr_intro", 8f, AnimationFlags.None);
                currentMedicTask.WaitForCompletion();
                currentPatientTask.WaitForCompletion();

                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                GameFiber.Wait(500);
                currentMedicTask = medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentPatientTask = patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentMedicTask.WaitForCompletion();
                currentPatientTask.WaitForCompletion();

                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                GameFiber.Wait(500);
                currentMedicTask = medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentPatientTask = patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentMedicTask.WaitForCompletion();
                currentPatientTask.WaitForCompletion();
                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                GameFiber.Wait(500);
                currentMedicTask = medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentPatientTask = patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentMedicTask.WaitForCompletion();
                currentPatientTask.WaitForCompletion();
                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                GameFiber.Wait(500);
                currentMedicTask = medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentPatientTask = patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentMedicTask.WaitForCompletion();
                currentPatientTask.WaitForCompletion();
                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                GameFiber.Wait(1000);
                currentMedicTask = medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_cpr_to_kol", 8f, AnimationFlags.None);
                currentPatientTask = patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_cpr_to_kol", 8f, AnimationFlags.None);
                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_kol_idle", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_kol_idle", 8f, AnimationFlags.None);
                GameFiber.Wait(500);
                currentMedicTask = medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_kol", 8f, AnimationFlags.None);
                currentPatientTask = patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_kol", 8f, AnimationFlags.None);
                currentMedicTask.WaitForCompletion();
                currentPatientTask.WaitForCompletion();
                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_kol_idle", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_kol_idle", 8f, AnimationFlags.None);
                GameFiber.Wait(500);
                currentMedicTask = medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_kol", 8f, AnimationFlags.None);
                currentPatientTask = patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_kol", 8f, AnimationFlags.None);
                currentMedicTask.WaitForCompletion();
                currentPatientTask.WaitForCompletion();
                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_kol_idle", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_kol_idle", 8f, AnimationFlags.None);
                GameFiber.Wait(500);
                currentMedicTask = medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_kol_to_cpr", 8f, AnimationFlags.None);
                currentPatientTask = patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_kol_to_cpr", 8f, AnimationFlags.None);
                currentMedicTask.WaitForCompletion();
                currentPatientTask.WaitForCompletion();
                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                GameFiber.Wait(500);
                currentMedicTask = medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentPatientTask = patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentMedicTask.WaitForCompletion();
                currentPatientTask.WaitForCompletion();
                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                GameFiber.Wait(500);
                currentMedicTask = medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentPatientTask = patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentMedicTask.WaitForCompletion();
                currentPatientTask.WaitForCompletion();
                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                GameFiber.Wait(500);
                currentMedicTask = medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentPatientTask = patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentMedicTask.WaitForCompletion();
                currentPatientTask.WaitForCompletion();
                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                GameFiber.Wait(500);
                currentMedicTask = medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentPatientTask = patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.None);
                currentMedicTask.WaitForCompletion();
                currentPatientTask.WaitForCompletion();
                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_def", "cpr_pumpchest_idle", 8f, AnimationFlags.None);
                GameFiber.Wait(1000);

                //roll prob_patientLivesIfDead

                medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_success", 8f, AnimationFlags.None);
                patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_success", 8f, AnimationFlags.None);
                GameFiber.Wait(25000);

                double percentageOfHealth = .5;
                double patientMaxHealth = (double)patientPed.MaxHealth;

                patientPed.Health = (int)(patientMaxHealth * percentageOfHealth);

                Game.LogTrivial("Roll probabilities and give current status.");
                Game.LogTrivial("Now is when the leaveScene function would run.");
            }
            else
            { 
                medicPed.Tasks.PlayAnimation("amb@code_human_police_crowd_control@idle_b", "idle_d", 8f, AnimationFlags.None);
                GameFiber.Wait(3000);
                medicPed.Tasks.PlayAnimation("amb@medic@standing@tendtodead@enter", "enter", 8.0F, AnimationFlags.None);
                GameFiber.Wait(1000);
                medicPed.Tasks.PlayAnimation("amb@medic@standing@tendtodead@base", "base", 8.0F, AnimationFlags.None);
                GameFiber.Wait(1000);
                medicPed.Tasks.PlayAnimation("amb@medic@standing@tendtodead@idle_a", "idle_a", -1f, AnimationFlags.Loop);
                GameFiber.Wait(2000);
                medicPed.Tasks.PlayAnimation("amb@medic@standing@tendtodead@exit", "exit", 8.0F, AnimationFlags.None);
                GameFiber.Wait(2000);
                medicPed.Tasks.Clear();

                double percentageOfHealth = .85;
                double patientMaxHealth = (double)patientPed.MaxHealth;

                patientPed.Health = (int)(patientMaxHealth * percentageOfHealth);

                Game.LogTrivial("Roll probabilities and give current status.");
                Game.LogTrivial("Now is when the leaveScene function would run.");
            }
        }

        internal static void Initialize()
        {
            GameFiber.StartNew(delegate
            {
                mainLoop();
                Game.LogTrivial("EvenBetterEMS " + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " has loaded successfully!");
                GameFiber.Wait(6000);
                Game.DisplayNotification("EvenBetterEMS " + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " has loaded successfully!");
            });
        }
    }
}
