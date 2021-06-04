//-------------------------------------     ----------------------------------------
// File Name        : BasketController.cs
// Project          : GwpLayoutTouchAsar
// Creation Date    : 09/05/2021
// Creation Author  : Simone Sambruni
//-----------------------------------------------------------------------------
// Copyright(C) Greencore srl 2021

using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace GwpLayoutTouchAsar
{
    enum ActonFile
    {
        Creating,
        Coping,
        Moving,
        Deleting
    };

    public partial class GwpLayoutTouchAsar : ServiceBase
    {
        private static Thread td = null;
        private static List<string> fileTipico = new List<string>();
        private static FileSystemWatcher watcher = null;
        private const string S_PLUREF = "S_PLUREF.DAT";
        private const string P_REGPAR = "P_REGPAR.DAT";
        private const string P_XXXPAR = "P_???PAR.DAT";
        private const string Dictionary = "Dictionary.bin";
        private static string nameBackupDirectory = "";

        public GwpLayoutTouchAsar()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // Only for debbugging mode
            System.Diagnostics.Debugger.Launch();

            td = new Thread(new ThreadStart(worker));
            td.Start();
        }

        protected override void OnStop()
        {
            if (td != null && td.IsAlive)
            {
                td.Abort();
            }
        }


        static void worker()
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(ConfigurationManager.AppSettings["Directory_Log"].ToString(), rollingInterval: RollingInterval.Day)
            .CreateLogger();

            Log.Information("Starting GwpLayoutTouchAsar Service ver 1.0.0.0");

            watcher = new FileSystemWatcher(ConfigurationManager.AppSettings["Directory_Incoming"].ToString(), "*.json");
            watcher.NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Size;

            watcher.Created += Watcher_Created;
            watcher.EnableRaisingEvents = true;

            Log.Information("Watching Incoming directory for new Json file...");
        }


        private static void Watcher_Created(object sender, FileSystemEventArgs e)
        {

            if (e.Name.Equals(ConfigurationManager.AppSettings["FilenameJson"].ToString()))
            {
                Log.Information("Keyboard.Json file founded in the directory");

                FileOperations(ActonFile.Coping, e.FullPath, (ConfigurationManager.AppSettings["Directory_Processing"].ToString() + "\\" + e.Name));

                var json = File.ReadAllText(e.FullPath);

                try
                {                   
                    Keyboard JsonObj = JsonConvert.DeserializeObject<Keyboard>(json);
                    Log.Information("Json validation OK");
                    bool ret = ProcessingJSON(JsonObj);
                }
                catch (Exception ex)
                {
                    // TODO LOG
                    Log.Error("Json validation KO");
                    Log.Error("Exception occurred : " + ex.Message);
                    UpdateStatus(string.Empty, string.Empty,  0, 0, 0);
                    string newDirectory = (ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + DateTime.Now.ToString("ddMMyyyyHHmm"));
                    Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + newDirectory);
                    FileOperations(ActonFile.Moving, e.FullPath, (newDirectory + "\\" + e.Name));
                }
            }
        }

        private static void UpdateStatus(string flussoName, string status, int cassa, int recordTotale, int recordInErrore)
        {
            GwpFlusso request = new GwpFlusso();
            request.Date = DateTime.Now;
            request.Terminal = 999;

            if (string.IsNullOrEmpty(flussoName))
            {
                request.Step = "ERROR";
                request.ReceivedRecords = 0;
                request.AppliedRecords = 0;
                request.ErrorRecords = 0;
                request.Response = "File JSON non valido";
            }
            else if (cassa != 0)
            {
                request.Terminal = cassa;
                request.Response = "KO";
            }
            else
                 {
                    request.Response = "OK";
                 }

            request.Step = status;
            request.ReceivedRecords = recordTotale;
            request.AppliedRecords = recordTotale - recordInErrore;
            request.ErrorRecords = recordInErrore;
            SendHTTPRequest(request);
        }

        private static bool ProcessingJSON(Keyboard  keyboards)
        {
            List<string> P_RegParData = new List<string>();
            List<string> S_PluRefData = new List<string>();

            try
            {
                int recordInError = 0;
                nameBackupDirectory = ConfigurationManager.AppSettings["Directory_Backup"].ToString() + "\\" + keyboards.NomeFlusso + DateTime.Now.ToString("ddMMyyyyHHmm");
                Log.Information("Creating Backup directory " + nameBackupDirectory);
                Directory.CreateDirectory(nameBackupDirectory);

                Log.Information("Processing Json file starting...");

                int numeroPulsante = 0;
                string pulsante = "";
                string tastiera = "";

                Log.Information("Processing image list...");

                try
                {
                    foreach (Immagini img in keyboards.Immagini)
                    {
                        MemoryStream data = null;
                        if (imgChecked(img, out data))
                        {
                            using (FileStream file = new FileStream(ConfigurationManager.AppSettings["Directory_Image"].ToString() + "\\" + img.Nome, FileMode.Create, FileAccess.Write))
                            {
                                data.WriteTo(file);
                            }
                        }
                        else
                        {

                        }
                    }
                }
                catch (Exception)
                {
                    recordInError++;
                }

                Log.Information("Processing Layouts list...");
               

                foreach (Layout lt in keyboards.Layouts)
                {
                    try
                    {
                        foreach (Pagine pg in lt.Pagine)
                        {
                            numeroPulsante = 0;
                            string data = "";
                            foreach (Pulsante pl in pg.Pulsante)
                            {
                                if (pl != null)
                                {
                                    numeroPulsante++;
                                    data = pg.Codice.ToString().PadLeft(4, '0') + ":" + (numeroPulsante).ToString().PadLeft(4, ' ') + ":" + pl.Valore.PadLeft(16, ' ') + ":0000:" + pl.Descrizione.PadRight(20, ' ');
                                    S_PluRefData.Add(data);
                                }
                            }
                        }

                        if (CreateS_PLUREF_file(S_PluRefData, keyboards.PulisciTutto))
                        {
                            Log.Information("Created directory old");
                            Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + "old");
                            Log.Information("Copied current file S_PLUREF.DAT into old directory...");
                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + S_PLUREF), (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\old\\" + S_PLUREF));
                            Log.Information("Moved new file S_PLUREF.dat into POS directory...");
                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + S_PLUREF), (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + S_PLUREF));
                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + S_PLUREF), nameBackupDirectory + "\\" + S_PLUREF);
                        }
                        else
                        {
                            Log.Information("S_PLUREF.DAT file creation failed");
                            string newDirectory = (ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + DateTime.Now.ToString("ddMMyyyyHHmm"));
                            Log.Information("Directory creation " + newDirectory);
                            Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + newDirectory);
                            Log.Information("Cancelled files");
                            FileOperations(ActonFile.Moving, (ConfigurationManager.AppSettings["Directory_Processing"].ToString() + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()), (newDirectory + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()));
                            FileOperations(ActonFile.Deleting, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\*.*"), "");
                            return false;
                        }
                    }
                    catch (Exception)
                    {
                        recordInError++;
                    }

                    numeroPulsante = 0;
                    pulsante = "";
                    tastiera = "DYKY3:";

                    try
                    {
                        foreach (Pulsante pl in lt.TastieraPrincipale.Pulsante)
                        {
                            if (pl != null)
                            {
                                numeroPulsante++;
                                tastiera += "D" + numeroPulsante.ToString();
                                switch (pl.Azione)
                                {
                                    case 0:
                                        pulsante = "PRES" + numeroPulsante.ToString() + ":DYNA:" + pl.Valore.PadLeft(2, '0').PadLeft(16, ' ') + ":" + pl.Descrizione.PadRight(18, ' ');
                                        break;
                                    case 1:
                                        pulsante = "PRES" + numeroPulsante.ToString() + ":LIST:" + pl.Valore.PadRight(4, '0').PadLeft(16, ' ') + ":" + pl.Descrizione.PadRight(18, ' ');
                                        break;
                                    case 2:
                                        pulsante = "PRES" + numeroPulsante.ToString() + ":0000:" + pl.Valore.PadLeft(16, ' ') + ":" + pl.Descrizione.PadRight(18, ' ');
                                        break;
                                }

                                P_RegParData.Add(pulsante);
                            }
                        }

                        tastiera = tastiera.PadRight(46, '0');
                        P_RegParData.Add(tastiera);

                        if (lt.TastiereDestra != null)
                        {
                            foreach (Tastieredestra td in lt.TastiereDestra)
                            {
                                numeroPulsante = 0;
                                pulsante = "";

                                foreach (Pulsante pl in td.Pulsante)
                                {
                                    if (pl != null)
                                    {
                                        switch (pl.Azione)
                                        {
                                            case 1:
                                                numeroPulsante++;
                                                pulsante = "PD" + td.Codice.PadLeft(2, '0') + numeroPulsante.ToString() + ":LIST:" + pl.Valore.PadLeft(4, '0').PadLeft(16, ' ') + ":" + pl.Descrizione.PadRight(18, ' ');
                                                break;
                                            case 2:
                                                numeroPulsante++;
                                                pulsante = "PD" + td.Codice.PadLeft(2, '0') + numeroPulsante.ToString() + ":0000:" + pl.Valore.PadLeft(16, ' ') + ":" + pl.Descrizione.PadRight(18, ' ');
                                                break;
                                        }

                                        P_RegParData.Add(pulsante);
                                    }
                                }
                            }
                        }

                        string versione = "VER00:" + keyboards.NomeFlusso.PadLeft(40, '0');
                        P_RegParData.Add(versione);

                        if (CreateP_REGPAR_file(P_RegParData, (lt.Casse != null ? lt.Casse.ToList<int>() : null)))
                        {
                            Log.Information("Created directory old");
                            Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + "old");
                              
                            Log.Information("Copied current file P_REGPAR.DAT into old directory");
                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + P_REGPAR), (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\old\\" + P_REGPAR));

                            Log.Information("Copied current file P_REGPAR.DAT into backup directory");
                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), nameBackupDirectory + "\\" + P_REGPAR);

                            
                            if (fileTipico.Count > 0)
                            {
                                if (File.Exists(ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + Dictionary))
                                {
                                    Dictionary<string, string> flussoCasse = Read(ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + Dictionary);

                                    foreach (string fileTp in fileTipico)
                                    {
                                        bool isCorrect = false;

                                        if (flussoCasse.ContainsKey(fileTp.Substring(2, 3)))
                                        {
                                            string value = string.Empty;
                                            flussoCasse.TryGetValue(fileTp.Substring(2, 3), out value);
                                            if (value == keyboards.NomeFlusso) isCorrect = true;
                                            else flussoCasse[fileTp.Substring(2, 3)] = keyboards.NomeFlusso;
                                        }
                                        else flussoCasse.Add(fileTp.Substring(2, 3), keyboards.NomeFlusso);

                                        if (!isCorrect)
                                        {
                                            UpdateStatus(keyboards.NomeFlusso, "NOTAPPLIED", Convert.ToInt32(fileTp.Substring(2, 3)), 0, 0);
                                        }
                                    }
                                }
                            }

                            Log.Information("Moved new file P_REGPAR.Dat into POS directory...");
                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + P_REGPAR));
                            
                        }
                        else
                        {
                            Log.Information("P_REGPAR.DAT file creation failed");
                            string newDirectory = (ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + DateTime.Now.ToString("ddMMyyyyHHmm"));
                            Log.Information("Directory creation " + newDirectory);
                            Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + newDirectory);
                            Log.Information("Cancelled files");
                            FileOperations(ActonFile.Moving, (ConfigurationManager.AppSettings["Directory_Processing"].ToString() + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()), (newDirectory + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()));
                            FileOperations(ActonFile.Deleting, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\*.*"), "");
                        }
                    }
                    catch (Exception)
                    {
                        recordInError++;
                    }

                    // Sending status update
                    UpdateStatus(keyboards.NomeFlusso, "SERVER", 0, 3, recordInError);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Exception occured: " + ex.InnerException);
                string newDirectory = (ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + DateTime.Now.ToString("ddMMyyyyHHmm"));
                Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + newDirectory);
                FileOperations(ActonFile.Moving, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + "*.*"), (newDirectory + "\\" + "*.*"));
                FileOperations(ActonFile.Deleting, ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\*.json", "");
                return false;
            }
        }


        private static void DeleteTipicoParFile(List<string> parTipicoToDelete)
        {
            if (parTipicoToDelete.Count > 0)
            {
                foreach (string file in parTipicoToDelete)
                {
                    if (File.Exists(ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + file)) 
                    {
                        FileOperations(ActonFile.Coping, nameBackupDirectory + "\\" + file, "");
                        FileOperations(ActonFile.Deleting, ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + file, "");
                        Log.Information("File: " + file + " deleted from directory casse");
                    }                    
                }
            }
        }


        private static bool imgChecked(Immagini img, out MemoryStream imageStream)
        {
            try
            {
                Image image = null;
                MemoryStream ms = null;
                byte[] bytes = Convert.FromBase64String(img.File);

                ms = new MemoryStream(bytes);
                image = Image.FromStream(ms, true);
                
                if (image.Width > Convert.ToInt32(ConfigurationManager.AppSettings["Image_Width"].ToString()) ||
                    image.Height > Convert.ToInt32(ConfigurationManager.AppSettings["Image_Height"].ToString()) ||
                    ms.Length > Convert.ToInt64(ConfigurationManager.AppSettings["Image_Size"].ToString()) )
                {
                    Log.Warning("Image checked KO: dimensions are wrong");
                    imageStream = null;
                    return false;
                }

                Log.Information("Image checked OK");
                imageStream = ms;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("ImgChecked exception occured: " + ex.StackTrace);
                imageStream = null;
                return false;
            }
        }


        private static bool CreateP_REGPAR_file(List<string> data, List<int> casse)
        {
            try
            {
             
                if (casse != null)
                {
                    fileTipico.Clear();
                    foreach (int value in casse)
                    {
                        string filename = "P_" + value.ToString().PadLeft(3, '0') + "PAR.DAT";
                        if (File.Exists(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename))
                        {
                            if (!CompareWithStandard(filename))
                            {
                                Log.Information("File Tipico is not equal to Standard so using it: " + filename);
                                List<string> dataWorked = new List<string>(data);
                                string[] pagine = dataWorked.ToArray();
                                string[] content = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename);

                                for (int indexP = 0; indexP < pagine.Length; indexP++)
                                {
                                    for (int indexC = 0; indexC < content.Length; indexC++)
                                    {
                                        if (content[indexC].StartsWith(pagine[indexP].Substring(0, 5)))
                                        {
                                            content[indexC] = pagine[indexP];
                                            dataWorked.Remove(pagine[indexP]);
                                        }
                                    }
                                }

                                List<string> fileNewContent = new List<string>(content.ToArray<string>());
                                if (dataWorked.Count > 0)
                                {
                                    fileNewContent.AddRange(dataWorked.AsEnumerable<string>());
                                }

                                fileNewContent.Sort();
                                File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + filename), fileNewContent.ToArray());
                                Log.Information("Saving P_XXXPAR.DAT file to directory: " + ConfigurationManager.AppSettings["Directory_Asar"].ToString());
                                fileTipico.Add(filename);
                            }
                            else
                            {
                                // Create canc_P_XXXPar.DAT into temporary directory
                                FileOperations(ActonFile.Creating, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + "canc" + filename), string.Empty);
                                Log.Information("Create canc_P_XXXPar.DAT into temporary directory: " + filename);

                                // Delete tipico file from directory casse
                                FileOperations(ActonFile.Deleting, (ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename), string.Empty);
                                Log.Information("File Tipico is equal to Standard so deleting it: " + filename);

                                // Copy canc_P_XXXParDAT into backup directory
                                FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + "canc" + filename), nameBackupDirectory + "\\" + "canc" + filename);
                                Log.Information("Coping canc_P_XXXPar.DAT into backup directory: " + filename);

                                List<string> dataWorked = new List<string>(data);
                                string[] pagine = dataWorked.ToArray();
                                string[] content = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + P_REGPAR);

                                for (int indexP = 0; indexP < pagine.Length; indexP++)
                                {
                                    for (int indexC = 0; indexC < content.Length; indexC++)
                                    {
                                        if (content[indexC].StartsWith(pagine[indexP].Substring(0, 5)))
                                        {
                                            content[indexC] = pagine[indexP];
                                            dataWorked.Remove(pagine[indexP]);
                                        }
                                    }
                                }

                                List<string> newContent = new List<string>(content.ToArray<string>());
                                newContent.AddRange(dataWorked.AsEnumerable<string>());
                                newContent.Sort();
                                File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), newContent.ToArray());
                                Log.Information("Saving P_REGPAR.DAT file to directory: " + ConfigurationManager.AppSettings["Directory_Asar"].ToString());
                            }
                        }
                        else
                        {
                            List<string> dataWorked = new List<string>(data);
                            string[] pagine = dataWorked.ToArray();
                            string[] content = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + P_REGPAR);

                            for (int indexP = 0; indexP < pagine.Length; indexP++)
                            {
                                for (int indexC = 0; indexC < content.Length; indexC++)
                                {
                                    if (content[indexC].StartsWith(pagine[indexP].Substring(0, 5)))
                                    {
                                        content[indexC] = pagine[indexP];
                                        dataWorked.Remove(pagine[indexP]);
                                    }
                                }
                            }

                            List<string> newContent = new List<string>(content.ToArray<string>());
                            newContent.AddRange(dataWorked.AsEnumerable<string>());
                            newContent.Sort();
                            File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), newContent.ToArray());
                            Log.Information("Saving P_REGPAR.DAT file to directory: " + ConfigurationManager.AppSettings["Directory_Temporary"].ToString());
                        }

                    }
                    return true;
                }
                else {
                        List<string> dataWorked = new List<string>(data);
                        string[] pagine = dataWorked.ToArray();
                        string[] content = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + P_REGPAR);
                      

                        for (int indexP = 0; indexP < pagine.Length; indexP++)
                        {
                            for (int indexC = 0; indexC < content.Length; indexC++)
                            {
                                if (content[indexC].StartsWith(pagine[indexP].Substring(0, 4)))
                                {
                                    content[indexC] = pagine[indexP];
                                    dataWorked.Remove(pagine[indexP]);
                                }
                            }
                        }

                        List<string> newContent = new List<string>(content.ToArray<string>());
                        newContent.AddRange(dataWorked.AsEnumerable<string>());
                        newContent.Sort();
                        File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), newContent.ToArray());
                        return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception occured: " + ex.InnerException);
                //string newDirectory = (ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + DateTime.Now.ToString("ddMMyyyyHHmm"));
                //Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + newDirectory);
                //FileOperations(ActonFile.Moving, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + "*.*"), (newDirectory + "\\" + "*.*"));
                return false;
            }
        }

        private static Boolean CompareWithStandard(string filename)
        {
            // Make a list copy...
            List<string> regParData = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename).ToList<string>();
            List<string> workedData = regParData.ToList<string>();

            // Delete the extra row from tipico file...
            foreach(string data in workedData)
            {
                if (data.StartsWith("DYKY3"))
                {
                    regParData.Remove(data);
                }
                else if (data.StartsWith("PRES"))
                {
                    regParData.Remove(data);
                }
                else if (data.StartsWith("PD"))
                {
                    regParData.Remove(data);
                }
                else if (data.StartsWith("VER"))
                {
                    regParData.Remove(data);
                }
            }

            // Compared tipico file with standard file...
            return (regParData.SequenceEqual(File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + P_REGPAR)));
        }


        private static bool CreateS_PLUREF_file(List<string> data, bool newCreation)
        {
            try
            {
                if (newCreation)
                {
                    List<string> newContent = new List<string>(data.ToArray<string>());
                    newContent.Sort();
                    Log.Information("Creating a new S_PLUREF.DAT file to temporary directory");
                    File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + S_PLUREF), newContent.ToArray());
                    return true;
                }
                else
                {
                    string s = string.Empty;
                    string[] content = File.ReadAllLines((ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + S_PLUREF));
                    string[] pagine = data.ToArray();

                    Log.Information("Opening S_PLUREF.DAT file from directory: " + ConfigurationManager.AppSettings["Directory_Asar"].ToString());

                    //FileOperations(ActonFile.Coping, "S_PLUREF.DAT", "S_PLUREF.DAT");

                    for (int indexP = 0; indexP < pagine.Length; indexP++)
                    {
                         for (int indexC = 0; indexC < content.Length; indexC++)
                        {
                            if (content[indexC].StartsWith(pagine[indexP].Substring(0, 5)))
                            {
                                content[indexC] = pagine[indexP];
                                data.Remove(pagine[indexP]);
                            }
                        }
                    }

                    List<string> newContent = new List<string>(content.ToArray<string>());
                    newContent.AddRange(data.AsEnumerable<string>());
                    newContent.Sort();
                    File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + S_PLUREF), newContent.ToArray());
                    Log.Information("Saving S_PLUREF.DAT file to directory: " + ConfigurationManager.AppSettings["Directory_Temporary"].ToString());
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception occured: " + ex.InnerException);                
                return false;
            }         
        }


        private static bool FileOperations(ActonFile actionFile, string fileA, string fileB)
        {
            try
            {
                switch (actionFile)
                {
                    case ActonFile.Creating:
                        File.Create(fileA);
                        break;

                    case ActonFile.Coping:
                        File.Copy(fileA, fileB, true);
                        break;

                    case ActonFile.Deleting:
                        File.Delete(fileA);
                        break;

                    case ActonFile.Moving:
                        File.Move(fileA, fileB);
                        break;

                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                //TODO LOG
                return false;
            }

            return true;
        }

        private static async void SendHTTPRequest(GwpFlusso contentRequest)
        {
            HttpResponseMessage response = null;

            try
            {
                HttpClient client = new HttpClient();
                Log.Information("Send update stato flusso");
                client.Timeout = new TimeSpan(0, 0, int.Parse(ConfigurationManager.AppSettings["TimeoutWS"].ToString()));
                response = await client.PostAsync(ConfigurationManager.AppSettings["GwpRestJsonEndpoint"].ToString(), new StringContent(JsonConvert.SerializeObject(contentRequest), Encoding.UTF8, "application/Json"));
            }
            catch (WebException wex) { Log.Error("ERR - SendHttpRequest: " + wex.Message); }
            catch (HttpRequestException hre) { Log.Error("ERR - SendHttpRequest: " + hre.Message); }
        }


        private static void Write(Dictionary<string, string> dictionary, string file)
        {
            using (FileStream fs = File.OpenWrite(file))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                // Put count.
                writer.Write(dictionary.Count);
                // Write pairs.
                foreach (var pair in dictionary)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value);
                }
            }
        }


        private static Dictionary<string, string> Read(string file)
        {
            var result = new Dictionary<string, string>();
            using (FileStream fs = File.OpenRead(file))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                // Get count.
                int count = reader.ReadInt32();
                // Read in all pairs.
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    string value = reader.ReadString();
                    result[key] = value;
                }
            }
            return result;
        }
    }
}
