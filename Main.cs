using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rage;
using LSPD_First_Response.Mod.API;
using System.Reflection;

namespace EvenBetterEMS
{
    public class Main : Plugin
    {
        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += OnDutyStateHandler;
            fileHandler.iniPatientsFile();
            Game.LogTrivial("EvenBetterEMS " + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " has successfully initialized.");
            Game.LogTrivial("Go on duty to fully load EvenBetterEMS!");
            throw new NotImplementedException();
        }

        public override void Finally()
        {
            EvenBetterEMSHandler.EvenBetterCleanup();
            Game.LogTrivial("EvenBetterEMS has been cleaned up.");
            throw new NotImplementedException();
        }

        private static void OnDutyStateHandler(bool onDuty)
        {
            if (onDuty)
            {
                EvenBetterEMSHandler.Initialize();
            }
        }
    }
}
