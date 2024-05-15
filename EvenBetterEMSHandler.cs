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
        private static float parkHeading;
        private static Vehicle ambul;
        private static Blip ambulBlip;
        private static Ped medicPed;
        private static Ped patientPed;
        private static bool isParked;
        private static bool hasWarped;

        //probabilities
        private static double prob_aliveIfDead = .5;
        private static double prob_stableIfDead = .5;
        private static double prob_patientLivesIfDead = .5;
        private static double prob_stableIfAlive = .7;
        private static double prob_patientLivesIfAlive = .7;

        //randoms
        private static Random r_livesIfDead = new Random();
        private static Random r_stableIfDead = new Random();
        private static Random r_stableIfAlive = new Random();

        //outcomes
        private static bool b_aliveIfDead;
        private static bool b_stableIfDead;
        private static bool b_stableIfAlive;

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
                    Game.DisplaySubtitle("~b~You~w~: Go ahead and lay down, the medic will be here ASAP.");
                    GameFiber.Wait(2000);
                    patientPed.Tasks.PlayAnimation("combat@damage@rb_writhe", "rb_writhe_loop", -1f, AnimationFlags.Loop);
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
            Rage.Task drivingTask = medicPed.Tasks.DriveToPosition(location, 20f, VehicleDrivingFlags.Emergency);
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
            
            medicPed.Tasks.GoToOffsetFromEntity(patientPed, .1f, 0f, 10f).WaitForCompletion();

            patientPed.Tasks.PlayAnimation("combat@damage@rb_writhe", "rb_writhe_loop", -1f, AnimationFlags.Loop);

            Game.DisplaySubtitle("~r~Medic~w~: Give us some room, I'm goin' in.");

            if (patientPed.IsDead)
            {
                if (r_livesIfDead.NextDouble() < prob_aliveIfDead)
                {
                    b_aliveIfDead = true;
                }
                else
                {
                    b_aliveIfDead = false;
                }

                Game.LogTrivial(b_aliveIfDead ? "Patient is: alive!" : "Patient is: dead :(");

                patientPed.Health = (patientPed.MaxHealth)/2;
                patientPed.Resurrect();
                GameFiber.Wait(250);

                patientPed.Tasks.ClearImmediately();

                patientPed.IsCollisionProof = true;
                medicPed.IsCollisionProof = true;

                medicPed.SetRotationYaw(0);
                patientPed.SetPositionZ(medicPed.Position.Z);
                patientPed.SetPositionX(medicPed.Position.X + .35f);
                patientPed.SetPositionY(medicPed.Position.Y + .7f);
                patientPed.SetRotationYaw(180f);    

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
                if (b_aliveIfDead)
                {
                    medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_success", 8f, AnimationFlags.None);
                    patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_success", 8f, AnimationFlags.None);
                    GameFiber.Wait(22000);

                    Game.DisplayNotification("Animation over.");

                    double percentageOfHealth = .5;
                    double patientMaxHealth = (double)patientPed.MaxHealth;

                    patientPed.Health = (int)(patientMaxHealth * percentageOfHealth);

                    if (r_stableIfDead.NextDouble() < prob_stableIfDead)
                    {
                        Game.DisplaySubtitle("~r~Medic~w~: It looks like they're ~g~awake ~w~and ~g~stable~w~. We're gonna head to the hospital.");
                        Game.DisplayHelp("If you'd like to check on a patient later, hit ~r~" + KeyBinding_menuKey.ToString() + " ~w~to call the ~r~hospital~w~. Here you can check on any patients you've called EMS for ~y~10 minutes ~w~after the incident to get an ~g~incident report~w~.");
                        b_stableIfDead = true;
                    }
                    else
                    {
                        Game.DisplaySubtitle("~r~Medic~w~: It looks like they're ~g~awake but in ~r~critical condition~w~. I better get them back to a hospital.");
                        b_stableIfDead = false;
                    }

                    
                    Game.LogTrivial(b_stableIfAlive.ToString());
                }
                else
                {
                    medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_fail", 8f, AnimationFlags.None);
                    patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_fail", 8f, AnimationFlags.None);
                    GameFiber.Wait(5000);
                    Game.DisplaySubtitle("~r~Medic~w~: It's no use... They're gone. Better call the coroner.");
                    GameFiber.Wait(13000);
                    patientPed.Kill();
                }

                //Now is when the leaveScene function would run.
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
                GameFiber.Wait(3000);
                medicPed.Tasks.PlayAnimation("amb@medic@standing@tendtodead@exit", "exit", 8.0F, AnimationFlags.None);
                GameFiber.Wait(2000);
                medicPed.Tasks.Clear();

                if (r_stableIfAlive.NextDouble() < prob_stableIfAlive)
                {
                    Game.DisplaySubtitle("~r~Medic~w~: It looks like they're ~g~stable~w~. We should get them checked out at the hospital, though.");
                    double percentageOfHealth = .85;
                    double patientMaxHealth = (double)patientPed.MaxHealth;

                    patientPed.Health = (int)(patientMaxHealth * percentageOfHealth);
                    b_stableIfAlive = true;
                }
                else
                {
                    Game.DisplaySubtitle("~r~Medic~w~: The patient is in ~r~critical condition~w~, we need to go now!");
                    double percentageOfHealth = .4;
                    double patientMaxHealth = (double)patientPed.MaxHealth;

                    patientPed.Health = (int)(patientMaxHealth * percentageOfHealth);
                    b_stableIfAlive = false;
                }

                //Now is when the leaveScene function would run
            }
        }

        internal static void Initialize()
        {
            GameFiber.StartNew(delegate
            {
                mainLoop();
                Game.LogTrivial("~r~EvenBetterEMS ~w~" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " ~w~has loaded ~g~successfully~w~!");
                GameFiber.Wait(6000);
                Game.DisplayNotification("~r~EvenBetterEMS ~w~" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " ~w~has loaded ~g~successfully~w~!");
            });
        }
    }
}
