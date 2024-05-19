using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using RAGENativeUI;
using RAGENativeUI.PauseMenu;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EvenBetterEMS
{
    internal static class Menus
    {
        private static MenuPool _menuPool;

        public static TabView hospitalMenu;

        public static TabSubmenuItem pendingResultsList;
        public static TabSubmenuItem publishedResultsList;

        public static List<TabItem> EmptyItems = new List<TabItem>() { new TabItem(" ") };

        public static void initializeMenus()
        {
            Game.FrameRender += Process;
            _menuPool = new MenuPool();

            hospitalMenu = new TabView("~b~~h~Los Santos Hospital");

            hospitalMenu.AddTab(pendingResultsList = new TabSubmenuItem("Pending Results", EmptyItems));
            hospitalMenu.AddTab(publishedResultsList = new TabSubmenuItem("Results", EmptyItems));

            hospitalMenu.RefreshIndex();

            mainLogic();
        }

        private static void mainLogic()
        {
            GameFiber.StartNew(delegate
            {
                try
                {
                    while (true)
                    {
                        GameFiber.Yield();
                        if (Game.IsKeyDown(hospitalSystem.openHospitalKey) && (Game.IsKeyDownRightNow(hospitalSystem.openHospitalModifierKey) || hospitalSystem.openHospitalModifierKey == Keys.None))
                        {
                            if (!hospitalMenu.Visible) { hospitalMenu.Visible = true; }
                        }

                        if (_menuPool.IsAnyMenuOpen()) { NativeFunction.Natives.SET_PED_STEALTH_MOVEMENT(Game.LocalPlayer.Character, 0, 0); }

                        if (hospitalMenu.Visible)
                        {

                            if (!HospitalMenuPaused)
                            {
                                HospitalMenuPaused = true;
                                Game.IsPaused = true;
                            }
                            if (Game.IsKeyDown(Keys.Delete))
                            {
                                if (pendingResultsList.Selected)
                                {
                                    if (Patient.pendingResultsMenuCleared)
                                    {
                                        hospitalSystem.deleteCase(hospitalSystem.PendingCases[pendingResultsList.Index]);
                                        pendingResultsList.Index = 0;
                                    }
                                }
                                else if (publishedResultsList.Selected)
                                {
                                    if (Patient.resultsMenuCleared)
                                    {
                                        hospitalSystem.deleteCase(hospitalSystem.PublishedCases[publishedResultsList.Index]);

                                        publishedResultsList.Index = 0;
                                    }
                                }
                            }

                            if (Game.IsKeyDown(Keys.Insert))
                            {
                                if (pendingResultsList.Selected)
                                {
                                    if (Patient.pendingResultsMenuCleared)
                                    {
                                        hospitalSystem.PendingCases[pendingResultsList.Index].resultPublishTime = DateTime.Now;
                                        pendingResultsList.Index = 0;
                                    }
                                }
                            }
                        }
                        else if (HospitalMenuPaused)
                        {
                            HospitalMenuPaused = false;
                            Game.IsPaused = false;
                        }
                    }
                }
                catch (System.Threading.ThreadAbortException e) { Game.LogTrivial(e.ToString()); }
                catch (Exception e) { Game.LogTrivial(e.ToString()); }
            });
        }

        private static bool HospitalMenuPaused = false;
        private static void Process(object sender, GraphicsEventArgs e)
        {
            try
            {
                _menuPool.ProcessMenus();
                if (hospitalMenu.Visible)
                {
                    Game.IsPaused = true;
                    hospitalMenu.Update();
                }
            }
            catch (Exception exception)
            {
                Game.LogTrivial($"Handled {exception}");
            }
        }
    }
}
