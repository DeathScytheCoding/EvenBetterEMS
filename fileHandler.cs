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

namespace EvenBetterEMS
{
    internal class fileHandler
    {
        public static string PatientsFilePath { get; set; } = "Plugins/LSPDFR/EvenBetterEMS/PatientsFile.xml";
        public static List<Patient> PendingCases { get; set; } = new List<Patient>();
        public static List<Patient> PublishedCases { get; set; } = new List<Patient>();
        public static bool LoadingXMLFileCases { get; set; } = true;

        public static void loadINIFile()
        {

        }

        public static void iniPatientsFile()
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
                List<Patient> AllCourtCases = xdoc.Descendants("CourtCase").Select(x => new Patient()
                {
                    patientName = ((string)x.Element("patientName").Value).Trim(trim),
                    wasDead = bool.Parse(((string)x.Element("wasDead").Value).Trim(trim)),
                    wasStable = bool.Parse(((string)x.Element("wasDead").Value).Trim(trim)),
                    probWasAlive = int.Parse(x.Element("probWasAlive") != null ? ((string)x.Element("probWasAlive").Value).Trim(trim) : "100"),
                    probWasDead = int.Parse(x.Element("probWasDead") != null ? ((string)x.Element("probWasDead").Value).Trim(trim) : "100"),
                    caseTime = DateTime.FromBinary(long.Parse(x.Element("caseTime").Value)),
                    caseResult = bool.Parse(((string)x.Element("caseResult").Value).Trim(trim)),
                    resultPublishTime = DateTime.FromBinary(long.Parse(x.Element("resultPublishTime").Value)),
                    resultsPublished = bool.Parse(((string)x.Element("resultPublished").Value).Trim(trim)),
                    resultsNotifShown = bool.Parse(((string)x.Element("ResultsPublishedNotificationShown").Value).Trim(trim))

                }).ToList<Patient>();

                foreach (Patient patient in AllCourtCases)
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
    }

    internal class Patient
    {
        public string patientName { get; set; }
        public bool wasDead { get; set; }
        public bool wasStable { get; set; }
        public int probWasDead { get; set; }
        public int probWasAlive {  get; set; }
        public DateTime caseTime { get; set; }
        public bool caseResult { get; set; }

        public DateTime resultPublishTime { get; set; }
        public bool resultsPublished { get; set; }
        public bool resultsNotifShown { get; set; }

        public bool pendingResultsMenuCleared {  get; set; }

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
            s += "~b~" + caseTime.ToBinary().ToString();
            return s;
        }

        public Patient() { }
        public Patient(string patientName, bool wasDead, bool wasStable, int probWasDead, int probWasAlive, DateTime caseTime, bool caseResult, DateTime resultPublishTime, bool resultsPublished, bool resultsNotifShown)
        {
            this.patientName = patientName;
            this.wasDead = wasDead;
            this.wasStable = wasStable;
            this.probWasDead = probWasDead;
            this.probWasAlive = probWasAlive;
            this.caseTime = caseTime;
            this.caseResult = caseResult;
            this.resultPublishTime = resultPublishTime;
            this.resultsPublished = resultsPublished;
            this.resultsNotifShown = resultsNotifShown;
        }

        public void addToHospitalMenuAndLists()
        {
            if (resultsPublished || DateTime.Now < resultPublishTime) 
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
            if (!resultsPublished && DateTime.Now < resultPublishTime) 
            {
                publishCaseResults();
            }
        }

        public void publishCaseResults()
        {
            if (!fileHandler.PublishedCases.Contains(this))
            {
                if (fileHandler.PendingCases.Contains(this))
                {
                    Menus.pendingResultsList.Items.RemoveAt(fileHandler.PendingCases.IndexOf(this));
                    if (Menus.pendingResultsList.Items.Count == 0) { Menus.pendingResultsList.Items.Add(new TabItem(" ")); pendingResultsMenuCleared = false; }
                    Menus.pendingResultsList.Index = 0;
                    fileHandler.PendingCases.Remove(this);
                }
                fileHandler.PublishedCases.Insert(0, this);
                if (probWasAlive < 0) { probWasAlive = 0; } else if (probWasAlive > 100) { probWasAlive = 100; }
                if (probWasDead < 0) { probWasDead = 0; } else if (probWasDead > 100) { probWasDead = 100; }
                if (wasDead)
                {
                    if (random.NextDouble() >=  (probWasDead / 100) && !resultsNotifShown) 
                    {
                        caseResult = true;
                    }
                    else
                    {
                        caseResult = false;
                    }    
                }
                else if (!wasDead)
                {
                    if (random.NextDouble() >= (probWasAlive / 100) && !resultsNotifShown)
                    {
                        caseResult = true;
                    }
                    else
                    {
                        caseResult = false;
                    }
                }
            }
        }
    }
}
