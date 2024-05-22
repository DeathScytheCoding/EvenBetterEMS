using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Drawing.Text;
using System.Data;
using System.Windows.Forms;
using Rage;
using System.IO;
using System.Xml.Linq;
using RAGENativeUI.PauseMenu;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using LSPD_First_Response.Engine.Scripting.Entities;

namespace EvenBetterEMS
{
    internal class hospitalSystem
    {
        public static string PatientsFilePath { get; set; } = "Plugins/LSPDFR/EvenBetterEMS/PatientsFile.xml";
        public static List<Patient> PendingCases { get; set; } = new List<Patient>();
        public static List<Patient> PublishedCases { get; set; } = new List<Patient>();
        public static bool LoadingXMLFileCases { get; set; } = true;

        //Keybinds
        public static Keys openHospitalKey = Keys.F6;
        public static Keys openHospitalModifierKey = Keys.None;

        //mainLoop
        public static void hospitalSystemMainLoop()
        {
            GameFiber.StartNew(delegate
            {
                Directory.CreateDirectory(Directory.GetParent(PatientsFilePath).FullName);
                if (!File.Exists(PatientsFilePath))
                {
                    new XDocument(
                        new XElement("EvenBetterEMS")
                    )
                    .Save(PatientsFilePath);
                }
                LoadPatientsFile(PatientsFilePath);
                while (true)
                {
                    GameFiber.Yield();
                    foreach (Patient patient in PendingCases.ToArray())
                    {
                        patient.checkForCaseUpdatedStatus();
                    }

                }
            });
        }

        private static void LoadPatientsFile(string File)
        {
            try
            {
                XDocument xdoc = XDocument.Load(File);
                char[] trim = new char[] { '\'', '\"', ' ' };
                List<Patient> AllPatients = xdoc.Descendants("Patient").Select(x => new Patient()
                {
                    patientName = ((string)x.Element("patientName").Value).Trim(trim),
                    wasDead = bool.Parse(((string)x.Element("wasDead").Value).Trim(trim)),
                    wasStable = bool.Parse(((string)x.Element("wasStable").Value).Trim(trim)),
                    causeOfDeath = ((string)x.Element("causeOfDeath").Value).Trim(trim),
                    caseTime = DateTime.FromBinary(long.Parse(x.Element("caseTime").Value)),
                    probWasAlive = int.Parse(x.Element("probWasAlive") != null ? ((string)x.Element("probWasAlive").Value).Trim(trim) : "100"),
                    probWasDead = int.Parse(x.Element("probWasDead") != null ? ((string)x.Element("probWasDead").Value).Trim(trim) : "100"),
                    caseResult = bool.Parse(((string)x.Element("caseResult").Value).Trim(trim)),
                    resultPublishTime = DateTime.FromBinary(long.Parse(x.Element("resultPublishTime").Value)),
                    resultsPublished = bool.Parse(((string)x.Element("resultsPublished").Value).Trim(trim)),
                    resultsNotifShown = bool.Parse(((string)x.Element("resultsNotifShown").Value).Trim(trim))

                }).ToList<Patient>();

                foreach (Patient patient in AllPatients)
                {
                    patient.addToHospitalMenuAndLists();
                }

            }
            catch (System.Threading.ThreadAbortException e) { }
            catch (Exception e)
            {
                Game.LogTrivial("LSPDFR+ encountered an exception reading \'" + File + "\'. It was: " + e.ToString());
                Game.DisplayNotification("~r~LSPDFR+: Error reading CourtCases.xml. Setting default values.");
            }
            finally
            {
                LoadingXMLFileCases = false;
            }
        }

        public static void overwriteCase(Patient patient)
        {
            deleteCaseFromXMLFile(PatientsFilePath, patient);
            addCaseToXMLFile(PatientsFilePath, patient);
        }
        public static void deleteCase(Patient patient)
        {
            deleteCaseFromXMLFile(PatientsFilePath, patient);
            if (hospitalSystem.PublishedCases.Contains(patient))
            {
                if (Menus.publishedResultsList.Items.Count == 1) { Menus.publishedResultsList.Items.Add(new TabItem(" ")); Patient.resultsMenuCleared = false; }
                Menus.publishedResultsList.Items.RemoveAt(hospitalSystem.PublishedCases.IndexOf(patient));
                hospitalSystem.PublishedCases.Remove(patient);

            }
            if (hospitalSystem.PendingCases.Contains(patient))
            {
                if (Menus.pendingResultsList.Items.Count == 1) { Menus.pendingResultsList.Items.Add(new TabItem(" ")); Patient.pendingResultsMenuCleared = false; }
                Menus.pendingResultsList.Items.RemoveAt(hospitalSystem.PublishedCases.IndexOf(patient));
                hospitalSystem.PendingCases.Remove(patient);

            }
        }

        private static void addCaseToXMLFile(string File, Patient patient)
        {
            try
            {

                XDocument xdoc = XDocument.Load(File);
                char[] trim = new char[] { '\'', '\"', ' ' };


                XElement EvenBetterEMSElement = xdoc.Element("EvenBetterEMS");
                XElement caseElement = new XElement("Patient",
                    new XAttribute("ID", patient.XMLIdentifier),
                    new XElement("patientName", patient.patientName),
                    new XElement("wasDead", patient.wasDead.ToString()),
                    new XElement("wasStable", patient.wasStable.ToString()),
                    new XElement("causeOfDeath", patient.causeOfDeath),
                    new XElement("caseTime", patient.caseTime.ToBinary()),
                    new XElement("probWasAlive", patient.probWasAlive.ToString()),
                    new XElement("probWasDead", patient.probWasDead.ToString()),
                    new XElement("caseResult", patient.caseResult),
                    new XElement("resultPublishTime", patient.resultPublishTime.ToBinary()),
                    new XElement("resultsPublished", patient.resultsPublished.ToString()),
                    new XElement("resultsNotifShown", patient.resultsNotifShown.ToString()));
                EvenBetterEMSElement.Add(caseElement);
                xdoc.Save(File);


            }
            catch (Exception e)
            {
                Game.LogTrivial("LSPDFR+ encountered an exception writing a court case to \'" + File + "\'. It was: " + e.ToString());
                Game.DisplayNotification("~r~LSPDFR+: Error while working with PatientsFile.xml.");
            }
        }

        private static void deleteCaseFromXMLFile(string File, Patient patient)
        {
            try
            {
                XDocument xdoc = XDocument.Load(File);
                char[] trim = new char[] { '\'', '\"', ' ' };
                List<XElement> CourtCasesToBeDeleted = new List<XElement>();
                CourtCasesToBeDeleted = (from x in xdoc.Descendants("Patient") where (((string)x.Attribute("ID")).Trim(trim) == patient.XMLIdentifier) select x).ToList<XElement>();

                if (CourtCasesToBeDeleted.Count > 0)
                {


                    foreach (XElement ele in CourtCasesToBeDeleted)
                    {



                        ele.Remove();


                    }


                }
                xdoc.Save(File);
            }
            catch (Exception e)
            {
                Game.LogTrivial("LSPDFR+ encountered an exception deleting an element from \'" + File + "\'. It was: " + e.ToString());
                Game.DisplayNotification("~r~LSPDFR+: Error while working with CourtCases.xml.");
            }
        }
        public static void createNewCase(string patientName, bool wasDead, bool wasStable, int probWasDead, int probWasAlive, DateTime caseTime, string causeOfDeath, bool caseResult, DateTime resultPublishTime, bool resultsPublished, bool resultsNotifShown)
        {

            Patient patient = new Patient(patientName, wasDead, wasStable, probWasDead, probWasAlive, caseTime, causeOfDeath, caseResult, resultPublishTime, resultsPublished, resultsNotifShown);
            addCaseToXMLFile(PatientsFilePath, patient);
            patient.addToHospitalMenuAndLists();


        }

        public static void createNewCase(string patientName, bool wasDead, bool wasStable, int probWasDead, int probWasAlive, DateTime caseTime, string causeOfDeath, bool caseResult, DateTime resultPublishTime, bool resultsPublished)
        {
            createNewCase(patientName, wasDead, wasStable, probWasDead, probWasAlive, caseTime, causeOfDeath, caseResult, resultPublishTime, resultsPublished, false);
        }

        public static void createNewCase(string patientName, bool wasDead, bool wasStable, int probWasDead, int probWasAlive, string causeOfDeath, bool caseResult)
        {
            createNewCase(patientName, wasDead, wasStable, probWasDead, probWasAlive, DateTime.Now, causeOfDeath, caseResult, determinePublishTime(wasDead, wasStable), false, false);
        }

        public static bool determineOutcome(int probWasAlive, int probWasDead, bool wasDead)
        {
            Random random = new Random();

            if (wasDead)
            {
                if (random.NextDouble() >= (probWasDead / 100))
                {
                    bool caseResult = true;
                    return caseResult;
                }
                else
                {
                    bool caseResult = false;
                    return caseResult;
                }
            }
            else if (!wasDead)
            {
                if (random.NextDouble() >= (probWasAlive / 100))
                {
                    bool caseResult = true;
                    return caseResult;
                }
                else
                {
                    bool caseResult = false;
                    return caseResult;
                }
            }
            else
            {
                Game.LogTrivial("wasDead bool error on determineOutcome function");
                bool caseResult = false;
                return caseResult;
            }
        }

        public static DateTime determinePublishTime(bool wasDead, bool wasStable)
        {
            Random rndTime = new Random();
            int addedTime;
            if (wasDead && !wasStable)
            {
                addedTime = rndTime.Next(60, 90);
            }
            else if(wasDead && wasStable)
            {
                addedTime = rndTime.Next(30, 60);
            }
            else if(!wasDead && wasStable)
            {
                addedTime = rndTime.Next(20, 45);
            }
            else
            {
                addedTime = rndTime.Next(10, 30);
            }
            
            Game.LogTrivial("Time added: " + DateTime.Now.AddMinutes(addedTime));

            return DateTime.Now.AddMinutes(addedTime);
        }
    }

    internal class Patient
    {
        public string patientName { get; set; }
        public bool wasDead { get; set; }
        public bool wasStable { get; set; }
        public int probWasDead { get; set; }
        public int probWasAlive {  get; set; }
        public DateTime caseTime { get; set; }
        public string causeOfDeath { get; set; }
        public bool caseResult { get; set; }

        public DateTime resultPublishTime { get; set; }
        public bool resultsPublished { get; set; }
        public bool resultsNotifShown { get; set; } = false;

        public static bool pendingResultsMenuCleared { get; set; } = false;

        public static bool resultsMenuCleared { get; set; } = false;

        private Random random = new Random();

        public string XMLIdentifier
        {
            get
            {
                return patientName + (caseTime.ToBinary().ToString());
            }
        }

        public string MenuLabel(bool NewLine)
        {
            string s = "~r~" + patientName + "~s~, ";
            if (NewLine)
            {
                s += "~n~";
            }
            s += "~b~" + caseTime.ToShortDateString();
            return s;
        }

        public Patient() { }
        public Patient(string patientName, bool wasDead, bool wasStable, int probWasDead, int probWasAlive, DateTime caseTime, string causeOfDeath, bool caseResult, DateTime resultPublishTime, bool resultsPublished, bool resultsNotifShown)
        {
            this.patientName = patientName;
            this.wasDead = wasDead;
            this.wasStable = wasStable;
            this.probWasDead = probWasDead;
            this.probWasAlive = probWasAlive;
            this.causeOfDeath = causeOfDeath;
            this.caseTime = caseTime;
            this.caseResult = caseResult;
            this.resultPublishTime = resultPublishTime;
            this.resultsPublished = resultsPublished;
            this.resultsNotifShown = resultsNotifShown;
        }

        public void addToHospitalMenuAndLists()
        {
            if (resultsPublished || DateTime.Now > resultPublishTime) 
            {
                publishCaseResults();
            }
            else
            {
                addToPendingCases();
            }
        }

        public void checkForCaseUpdatedStatus()
        {
            if (!resultsPublished && DateTime.Now > resultPublishTime) 
            {
                publishCaseResults();
            }
        }

        public void publishCaseResults()
        {
            if (!hospitalSystem.PublishedCases.Contains(this))
            {
                if (hospitalSystem.PendingCases.Contains(this))
                {
                    Menus.pendingResultsList.Items.RemoveAt(hospitalSystem.PendingCases.IndexOf(this));
                    if (Menus.pendingResultsList.Items.Count == 0) { Menus.pendingResultsList.Items.Add(new TabItem(" ")); pendingResultsMenuCleared = false; }
                    Menus.pendingResultsList.Index = 0;
                    hospitalSystem.PendingCases.Remove(this);
                }
                hospitalSystem.PublishedCases.Insert(0, this);

                TabTextItem item = new TabTextItem(MenuLabel(false), "Patient Outcome", "~b~Patient: ~r~" + patientName + "~n~~b~Cause of death: ~r~" + causeOfDeath
                    + "~n~" + (caseResult ? rollAliveMsgs() : rollDeadMsgs())
                    + "~n~~n~~w~Patient treated at: ~b~" + caseTime.ToShortDateString() + "~w~ at ~b~" + caseTime.ToShortTimeString()
                    + "~n~~w~" + (caseResult ? "Surgery ended on: ~b~" : "Time of death: ~b~") + resultPublishTime.ToShortDateString() + "~w~ at ~b~" + resultPublishTime.ToShortTimeString()
                    + "~n~~n~~w~Select this case and press ~r~Delete ~w~to dismiss it.");

                Menus.publishedResultsList.Items.Insert(0, item);
                Menus.publishedResultsList.RefreshIndex();

                if (!resultsMenuCleared)
                {
                    Game.LogTrivial("Empty items, clearing menu at index 1.");
                    Menus.publishedResultsList.Items.RemoveAt(1);
                    resultsMenuCleared = true;
                }
                resultsPublished = true;
                if (!resultsNotifShown)
                {
                    if (hospitalSystem.LoadingXMLFileCases)
                    {
                        GameFiber.StartNew(delegate
                        {
                            GameFiber.Wait(25000);
                            Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~r~Los Santos Hospital", "~b~" + MenuLabel(false), "~w~" + "A patient you called EMS for has gotten out of surgery. Press " + hospitalSystem.openHospitalKey.ToString() + (hospitalSystem.openHospitalModifierKey == Keys.None ? " " : "+" + hospitalSystem.openHospitalModifierKey.ToString() + " ") + "to call the hospital."); //Add menu key to top of file
                        });
                    }
                    else
                    {
                        Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~r~Los Santos Hospital", "~b~" + MenuLabel(false), "~w~" + "A patient you called EMS for has gotten out of surgery. Press " + hospitalSystem.openHospitalKey.ToString() + (hospitalSystem.openHospitalModifierKey == Keys.None ? " " : "+" + hospitalSystem.openHospitalModifierKey.ToString() + " ") + "to call the hospital."); //Add menu key to top of file
                    }
                }
                resultsNotifShown = true;
                hospitalSystem.overwriteCase(this);
            }
        }

        private void addToPendingCases()
        {
            
            if (!hospitalSystem.PendingCases.Contains(this))
            {
                if (hospitalSystem.PublishedCases.Contains(this))
                {
                    Menus.publishedResultsList.Items.RemoveAt(hospitalSystem.PublishedCases.IndexOf(this));
                    if (Menus.publishedResultsList.Items.Count == 0) { Menus.publishedResultsList.Items.Add(new TabItem(" ")); resultsMenuCleared = false; }
                    Menus.publishedResultsList.Index = 0;
                    hospitalSystem.PublishedCases.Remove(this);
                }
                hospitalSystem.PendingCases.Insert(0, this);
                TabTextItem item = new TabTextItem(MenuLabel(false), "Patient Status Pending", "~b~Patient: ~r~" + patientName + "~n~~w~Injury/COD: ~r~" + causeOfDeath + "~n~~n~~w~Patient treated on: ~b~"
                    + caseTime.ToShortDateString() + "~w~ at ~b~" + caseTime.ToShortTimeString() + "~n~" + timeEstimate()
                    + "~n~~n~~w~Select this case and press ~y~Insert ~w~to make the hearing take place immediately, or ~r~Delete ~w~to dismiss it.");
                Menus.pendingResultsList.Items.Insert(0, item);
                Menus.pendingResultsList.RefreshIndex();

                if (!pendingResultsMenuCleared)
                {
                    Game.LogTrivialDebug("Empty items, clearing menu at index 1.");
                    Menus.pendingResultsList.Items.RemoveAt(1);
                    pendingResultsMenuCleared = true;
                }
                if (!hospitalSystem.LoadingXMLFileCases)
                {
                    Game.DisplayNotification("3dtextures", "mpgroundlogo_cops", "~r~Los Santos Hospital", "~b~" + MenuLabel(false), "~w~You're now following a new pending patient case. Press ~b~F6 to call the hospital.~r~.");
                }
                resultsPublished = false;
            }
        }

        private string timeEstimate()
        {
            if(DateTime.Compare(DateTime.Now.AddMinutes(10), resultPublishTime) > 0)
            {
                return "Check back in about 10 minutes to see if the patient is out of surgery.";
            }
            else if(DateTime.Compare(DateTime.Now.AddMinutes(30), resultPublishTime) > 0)
            {
                return "Check again in about 30 minutes to check the patient's outcome.";
            }
            else if(DateTime.Compare(DateTime.Now.AddMinutes(45), resultPublishTime) > 0) 
            {
                return "They're in surgery, check again in about 45 minutes to see if the patient made it.";
            }
            else if(DateTime.Compare(DateTime.Now.AddMinutes(60), resultPublishTime) > 0)
            {
                return "They looked pretty rough, you'll probably want to check back in about an hour if you want to know their condition.";
            }
            else
            {
                return "The patient was in very bad condition. It'll be a while before they get out of surgery... if they get out of surgery.";
            }
        }

        public void deleteCase()
        {
            hospitalSystem.deleteCase(this);
        }

        private string rollAliveMsgs()
        {
            List<string> caseResultAliveMsgs = new List<string>();
            Random rnd = new Random();

            caseResultAliveMsgs.Add("~w~" + patientName + " pulled through and ~g~will be OK!");
            caseResultAliveMsgs.Add("~w~The doctor's say that " + patientName + " ~g~is gonna make it!");

            return caseResultAliveMsgs[rnd.Next(caseResultAliveMsgs.Count)];
        }

        private string rollDeadMsgs()
        {
            List<string> caseResultDeadMsgs = new List<string>();
            Random rnd = new Random();

            caseResultDeadMsgs.Add("~w~I'm so sorry... " + patientName + " ~r~didn't make it.");
            caseResultDeadMsgs.Add("~w~The doctors did everything they could but... ~r~they're gone.");

            return caseResultDeadMsgs[rnd.Next(caseResultDeadMsgs.Count)];
        }
    }
}
