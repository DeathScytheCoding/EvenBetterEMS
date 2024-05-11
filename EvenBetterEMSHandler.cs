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
        private static Rage.Task drivingTask;

        private static readonly Keys KeyBinding_menuKey = Keys.F6;
        private static readonly Keys KeyBinding_callEMSKey = Keys.OemSemicolon;
        private static readonly Keys KeyBinding_warpEMSKey = Keys.Divide;

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
                callEMS();
            }
            else
            {
                Game.LogTrivial("No ped found.");
                Game.DisplayNotification("No injured peds found, try walking closer to them.");
            }
            /*
            Ped[] nearbyPeds = Game.LocalPlayer.Character.GetNearbyPeds(1);
            if (nearbyPeds[0] != null)
            {
                Game.LogTrivial(nearbyPeds[0].Health.ToString());
            }
            if (nearbyPeds[0] != null) 
            {
                patientPed = nearbyPeds[0];
                Game.LogTrivial(patientPed.IsAlive.ToString());

                if (patientPed.Health < patientPed.MaxHealth)
                {
                    patientPed.MakePersistent();
                    patientPed.BlockPermanentEvents = true;
                    if (!patientPed.IsDead)
                    {
                        patientPed.Tasks.Clear();
                    }
                    callEMS(patientPed);
                }
                else
                {
                    Game.DisplayNotification("Ped is not injured, try walking closer to them.");
                }
            }
            else
            {
                Game.LogTrivial("No ped found.");
                Game.DisplayNotification("No injured peds found, try walking closer to them.");
            }
            */
        }

        //function to send EMS to the location
        private static void callEMS()
        {
            if (patientPed != null)
            {
                Game.LogTrivial(patientPed.Health.ToString());
                spawnPoint = World.GetNextPositionOnStreet(patientPed.Position.Around(250f));
                location = World.GetNextPositionOnStreet(patientPed.Position.Around(20f));

                if (ambul.Exists())
                {
                    ambul.Delete();
                }

                if (medicPed.Exists())
                {
                    medicPed.Delete();
                }

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

                medicTask();
            }
            else
            {
                Game.DisplayNotification("~r~EvenBetterEMS did not detect an injured ped. Try standing closer to them.");
            }

            /*
            TaskSequence ts = new TaskSequence(medicPed);

            ts.Tasks.DriveToPosition(location, 40f, VehicleDrivingFlags.Emergency);
            parkPosition = World.GetNextPositionOnStreet(location.Around(5f));
            parkHeading = ambul.Heading;
            ts.Tasks.ParkVehicle(parkPosition, parkHeading);

            ts.Tasks.LeaveVehicle(LeaveVehicleFlags.None);
            Vector3 directionToPlayer = Game.LocalPlayer.Character.Position;
            directionToPlayer.Normalize();
            
            float runHeading = MathHelper.ConvertDirectionToHeading(directionToPlayer);
            ts.Tasks.GoStraightToPosition(Game.LocalPlayer.Character.Position.Around(2f), 10f, runHeading, 2f, 15000);

            ts.Execute();

            Game.DisplayNotification("~r~EMS~/r~" + " are en-route to your location!");
            */
            //Rage.Task drivingTask = medicPed.Tasks.DriveToPosition(location, 40f, VehicleDrivingFlags.Emergency);
            //GameFiber.Wait(5000);
            /*if (!drivingTask.IsActive)
            {
                GameFiber.StartNew(EMSParkandRun);
            }*/
        }

        /*
        private static void EMSParkandRun()
        {
            bool carIsParked = false;
            Rage.Task parkingTask;

            while (true)
            {
                GameFiber.Wait(500);
                if (!drivingTask.IsActive && !carIsParked)
                {
                    ambul.IsSirenSilent = true;
                    parkPosition = World.GetNextPositionOnStreet(ambul.Position.Around(5f));
                    parkHeading = ambul.Heading;
                    parkingTask = medicPed.Tasks.ParkVehicle(parkPosition, parkHeading);
                    carIsParked = true;*/
                    /*while (true)
                    {
                        GameFiber.Wait(500);
                        if (!parkingTask.IsActive)
                        {
                            medicPed.Tasks.LeaveVehicle(LeaveVehicleFlags.None);
                            Vector3 directionToPlayer = Game.LocalPlayer.Character.Position;
                            directionToPlayer.Normalize();
                            float runHeading = MathHelper.ConvertDirectionToHeading(directionToPlayer);
                            medicPed.Tasks.GoStraightToPosition(Game.LocalPlayer.Character.Position.Around(2f), 10f, runHeading, 0, 1000);
                        }
                    }
                }
                if (!drivingTask.IsActive && carIsParked)
                {
                    
                    GameFiber.Wait(5000);
                    if (!parkingTask.IsActive)
                    {
                        medicPed.Tasks.LeaveVehicle(LeaveVehicleFlags.None);
                        Vector3 directionToPlayer = Game.LocalPlayer.Character.Position;
                        directionToPlayer.Normalize();
                        float runHeading = MathHelper.ConvertDirectionToHeading(directionToPlayer);
                        medicPed.Tasks.GoStraightToPosition(Game.LocalPlayer.Character.Position.Around(2f), 10f, runHeading, 0, 1000);
                    }
                }
            }
        }*/

        public static void EvenBetterCleanup()
        {
            medicPed.Delete();
            ambul.Delete();
            ambulBlip.Delete();
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

        private static void medicTask()
        {
            drivingTask = medicPed.Tasks.DriveToPosition(location, 30f, VehicleDrivingFlags.Emergency);
            GameFiber.Wait(3000);
            GameFiber.StartNew(warpEMSCloserChecker);
            drivingTask.WaitForCompletion();

            parkPosition = World.GetNextPositionOnStreet(location.Around(5f));
            parkHeading = ambul.Heading;
            Rage.Task parkTask = medicPed.Tasks.ParkVehicle(parkPosition, parkHeading);
            parkTask.WaitForCompletion(5000);

            Rage.Task leaveVehicle = medicPed.Tasks.LeaveVehicle(LeaveVehicleFlags.None);
            leaveVehicle.WaitForCompletion(10000);

            if (patientPed.IsDead)
            {
                medicPed.Tasks.GoToOffsetFromEntity(patientPed, 0f, 0f, 10f).WaitForCompletion();
                medicPed.Tasks.PlayAnimation("amb@medic@standing@tendtodead@idle_a", "idle_a", -1f, AnimationFlags.Loop);
            }
            else
            {
                medicPed.Tasks.GoToOffsetFromEntity(patientPed, 1f, 0f, 10f).WaitForCompletion();
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
                    medicTask();
                    WarpGameTimer = true;
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
