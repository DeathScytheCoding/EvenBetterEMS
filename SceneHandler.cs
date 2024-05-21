using Rage;
using System;
using LSPD_First_Response;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LSPD_First_Response.Engine.Scripting.Entities;

namespace EvenBetterEMS
{
    internal class SceneHandler
    {
        //definitions
        private static Vector3 parkPosition;
        private static float parkHeading;

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

        public static void drivingTasks(Ped medicPed, Ped patientPed, Vector3 location, Vehicle ambul)
        {
            Rage.Task drivingTask = medicPed.Tasks.DriveToPosition(location, 20f, VehicleDrivingFlags.Emergency);
            if (!EvenBetterEMSHandler.hasWarped)
            {
                GameFiber.Wait(3000);
                GameFiber.StartNew(EvenBetterEMSHandler.warpEMSCloserChecker);
            }

            if (!EvenBetterEMSHandler.isParked)
            {
                GameFiber.Wait(5000);
                GameFiber.StartNew(EvenBetterEMSHandler.parkHereChecker);
            }

            drivingTask.WaitForCompletion();
            ambul.IsSirenSilent = true;

            parkPosition = World.GetNextPositionOnStreet(location.Around(4f));
            parkHeading = ambul.Heading;
            medicPed.Tasks.ParkVehicle(parkPosition, parkHeading).WaitForCompletion(5000);

            EvenBetterEMSHandler.isParked = true;

            medicTasks(medicPed, patientPed);
        }

        public static void medicTasks(Ped medicPed, Ped patientPed)
        {
            medicPed.Tasks.LeaveVehicle(LeaveVehicleFlags.None).WaitForCompletion(5000);

            medicPed.Tasks.GoToOffsetFromEntity(patientPed, .1f, 0f, 10f).WaitForCompletion();

            patientPed.Tasks.PlayAnimation("combat@damage@rb_writhe", "rb_writhe_loop", -1f, AnimationFlags.Loop);

            Game.DisplaySubtitle("~r~Medic~w~: Give us some room, I'm goin' in.");

            if (patientPed.IsDead) //if patient is a dead ped
            {
                if (r_livesIfDead.NextDouble() < prob_aliveIfDead) //roll probability of dead ped getting revived.
                {
                    b_aliveIfDead = true;
                }
                else
                {
                    b_aliveIfDead = false;
                }

                Game.LogTrivial(b_aliveIfDead ? "Patient is: alive!" : "Patient is: dead :(");

                patientPed.Health = (patientPed.MaxHealth) / 2;
                patientPed.Resurrect();
                patientPed.Tasks.ClearImmediately();
                GameFiber.Wait(200);

                patientPed.IsCollisionProof = true;
                medicPed.IsCollisionProof = true;

                medicPed.SetRotationYaw(0);
                patientPed.SetPositionZ(medicPed.Position.Z);
                patientPed.SetPositionX(medicPed.Position.X - 1f);
                patientPed.SetPositionY(medicPed.Position.Y + .5f);
                patientPed.SetRotationYaw(90f);

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

                //if they will get revived
                if (b_aliveIfDead)
                {
                    medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_success", 8f, AnimationFlags.None);
                    patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_success", 8f, AnimationFlags.None);
                    GameFiber.Wait(22000);

                    Game.DisplayNotification("Animation over.");

                    double patientMaxHealth = (double)patientPed.MaxHealth;



                    if (r_stableIfDead.NextDouble() < prob_stableIfDead)
                    {
                        double percentageOfHealth = .5;
                        patientPed.Health = (int)(patientMaxHealth * percentageOfHealth);
                        Game.DisplaySubtitle("~r~Medic~w~: It looks like they're ~g~awake ~w~and ~g~stable~w~. We're gonna head to the hospital.");
                        Game.DisplayHelp("If you'd like to check on a patient later, hit ~r~" + EvenBetterEMSHandler.KeyBinding_menuKey.ToString() + " ~w~to call the ~r~hospital~w~. Here you can check on any patients you've called EMS for ~y~10 minutes ~w~after the incident to get an ~g~incident report~w~.");
                        b_stableIfDead = true;
                    }
                    else
                    {

                        double percentageOfHealth = .3;
                        patientPed.Health = (int)(patientMaxHealth * percentageOfHealth);
                        Game.DisplaySubtitle("~r~Medic~w~: It looks like they're ~g~awake but in ~r~critical condition~w~. I better get them back to a hospital.");
                        b_stableIfDead = false;
                    }

                    Persona patientPersona = LSPD_First_Response.Mod.API.Functions.GetPersonaForPed(patientPed);
                    string patientName = patientPersona.FullName;

                    Random rndCOD = new Random();
                    List<string> CODByPed = new List<string>();
                    List<string> CODByVic = new List<string>();
                    List<string> CODByOther = new List<string>();
                    CODByPed.Add("Got shot.");
                    CODByVic.Add("Got hit by a car.");
                    CODByOther.Add("Had a heart-attack.");

                    string patientCOD;

                    if (patientPed.HasBeenDamagedByAnyPed)
                    {
                        patientCOD = CODByPed[rndCOD.Next(CODByPed.Count)];
                    }
                    else if (patientPed.HasBeenDamagedByAnyVehicle)
                    {
                        patientCOD = CODByVic[rndCOD.Next(CODByVic.Count)];
                    }
                    else
                    { 
                        patientCOD = CODByOther[rndCOD.Next(CODByOther.Count)];
                    }

                    hospitalSystem.createNewCase(patientName, true, b_stableIfDead, (int)(prob_patientLivesIfDead * 100), (int)(prob_patientLivesIfAlive * 100), patientCOD, hospitalSystem.determineOutcome((int)(prob_patientLivesIfAlive*100), (int)(prob_patientLivesIfDead), true));
                    //leaveScene
                }
                else //if the patient can't be revived
                {
                    medicPed.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_fail", 8f, AnimationFlags.None);
                    patientPed.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_fail", 8f, AnimationFlags.None);
                    GameFiber.Wait(5000);
                    Game.DisplaySubtitle("~r~Medic~w~: It's no use... They're gone. Better call the coroner.");
                    GameFiber.Wait(10000);
                    patientPed.Kill();

                    Persona patientPersona = LSPD_First_Response.Mod.API.Functions.GetPersonaForPed(patientPed);
                    string patientName = patientPersona.FullName;

                    Random rndCOD = new Random();
                    List<string> CODByPed = new List<string>();
                    List<string> CODByVic = new List<string>();
                    List<string> CODByOther = new List<string>();
                    CODByPed.Add("Got shot.");
                    CODByVic.Add("Got hit by a car.");
                    CODByOther.Add("Had a heart-attack.");

                    string patientCOD;

                    if (patientPed.HasBeenDamagedByAnyPed)
                    {
                        patientCOD = CODByPed[rndCOD.Next(CODByPed.Count)];
                    }
                    else if (patientPed.HasBeenDamagedByAnyVehicle)
                    {
                        patientCOD = CODByVic[rndCOD.Next(CODByVic.Count)];
                    }
                    else
                    {
                        patientCOD = CODByOther[rndCOD.Next(CODByOther.Count)];
                    }
                    Game.LogTrivial(patientName + "" + (int)(prob_patientLivesIfAlive * 100) + "" + patientCOD);
                    hospitalSystem.createNewCase(patientName, true, false, (int)(prob_patientLivesIfDead * 100), (int)(prob_patientLivesIfAlive * 100), DateTime.Now, patientCOD, false, DateTime.Now.AddMinutes(1), false);
                    //leaveScene
                }

                //Now is when the leaveScene function would run.
            }
            else //if the patient is an alive ped
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

                if (r_stableIfAlive.NextDouble() < prob_stableIfAlive) //roll to decide if patient will be stable.
                {
                    Game.DisplaySubtitle("~r~Medic~w~: It looks like they're ~g~stable~w~. We should get them checked out at the hospital, though.");
                    double percentageOfHealth = .8;
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

                //leaveScene
            }
        }
    }
}
