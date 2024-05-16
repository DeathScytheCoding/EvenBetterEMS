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
        public static bool isParked;
        public static bool hasWarped;

        //Keybinds
        public static readonly Keys KeyBinding_menuKey = Keys.F6;
        public static readonly Keys KeyBinding_callEMSKey = Keys.OemSemicolon;
        public static readonly Keys KeyBinding_warpEMSKey = Keys.Divide;
        public static readonly Keys KeyBinding_parkHere = Keys.Subtract;

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

                SceneHandler.drivingTasks(medicPed, patientPed, location, ambul);
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

        public static void warpEMSCloserChecker()
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
                    SceneHandler.drivingTasks(medicPed, patientPed, location, ambul);
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

        public static void parkHereChecker()
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
                    SceneHandler.medicTasks(medicPed, patientPed);
                }
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
