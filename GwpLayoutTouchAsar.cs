//-----------------------------------------------------------------------------
// File Name        : GwpLayoutTouchAsar.cs
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
        private const string Dictionary = "Dictionary.bin";
        private static string nameBackupDirectory = "";

        private const string KEY_DYKY = "DYKY";
        private const string KEY_DYTX = "DYTX";
        private const string KEY_PD0 = "PD0";
        private const string KEY_PRES = "PRES";
        private const string KEY_VER = "VER0";


        public GwpLayoutTouchAsar()
        {
            InitializeComponent();
        }


        protected override void OnStart(string[] args)
        {

#if DEBUG
            // Only for debbugging mode
            Console.Out.WriteLine("***** SONO IN DEBUG MODE *****");
            System.Diagnostics.Debugger.Launch();
#endif
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
            
            Log.Information("\r\n");
            Log.Information("\r\n");
            Log.Information("\r\n");
            Log.Information("************************************************************");
            Log.Information("** Starting GwpLayoutTouchAsar Service ver 1.0.0.7-P-3129 **");
            Log.Information("************************************************************");

            string fileJasonDaElaborare = ConfigurationManager.AppSettings["FilenameJson"].ToString();
            string fullPath = ConfigurationManager.AppSettings["Directory_Incoming"].ToString() + "\\";
            Log.Information("controllo presenza di " + fullPath + fileJasonDaElaborare);
            if (File.Exists(fullPath + fileJasonDaElaborare))
            {
                Log.Information("file esiste procedo");
                inizioElaborazione(fullPath+ fileJasonDaElaborare, fileJasonDaElaborare);
            }
            else
                Log.Information("cartella vuota, delego al Watcher");

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
                inizioElaborazione(e.FullPath, e.Name);
                /*
                Log.Information("**** Keyboard.Json file found ****\r\n");

                Log.Information("Coping new file into Processing directory");
                Log.Information("e.FullPath: " + e.FullPath + " - e.Name : " + e.Name);
                FileOperations(ActonFile.Coping, e.FullPath, (ConfigurationManager.AppSettings["Directory_Processing"].ToString() + "\\" + e.Name));

                Log.Information("Reading Json file");
                var json = File.ReadAllText(e.FullPath);             

                try
                {
                    Log.Information("Validation Json file...");
                    Keyboard JsonObj = JsonConvert.DeserializeObject<Keyboard>(json);
                    Log.Information("Json validation OK");
                    
                    ProcessingJSON(JsonObj);

                    Log.Information("Cleaning directory");
                    CleanUP();
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
                */
                
            }
        }

        private static void inizioElaborazione(string fullPath, string fileName)
        {
            Log.Information("**** Keyboard.Json file found ****\r\n");

            Log.Information("Coping new file into Processing directory");
            FileOperations(ActonFile.Coping, fullPath, (ConfigurationManager.AppSettings["Directory_Processing"].ToString() + "\\" + fileName));

            Log.Information("Reading Json file");
            var json = File.ReadAllText(fullPath);

            try
            {
                Log.Information("Validation Json file...");
                Keyboard JsonObj = JsonConvert.DeserializeObject<Keyboard>(json);
                Log.Information("Json validation OK");

                ProcessingJSON(JsonObj);

                Log.Information("Cleaning directory");
                CleanUP();
            }
            catch (Exception ex)
            {
                // TODO LOG
                Log.Error("Json validation KO");
                Log.Error("Exception occurred : " + ex.Message);
                UpdateStatus(string.Empty, string.Empty, 0, 0, 0);
                string newDirectory = (ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + DateTime.Now.ToString("ddMMyyyyHHmm"));
                Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + newDirectory);
                FileOperations(ActonFile.Moving, fullPath, (newDirectory + "\\" + fileName));
            }

        }

        private static void CleanUP()
        {
            Log.Information("Cleaning directory...");
            Log.Information("Cleaning Incoming directory");
            FileOperations(ActonFile.Deleting, (ConfigurationManager.AppSettings["Directory_Incoming"].ToString() + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()), string.Empty);
            Log.Information("Cleaning Processing directory");
            FileOperations(ActonFile.Deleting, (ConfigurationManager.AppSettings["Directory_Processing"].ToString() + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()), string.Empty);
            Log.Information("Cleaning Temporary directory");
            Directory.EnumerateFiles(ConfigurationManager.AppSettings["Directory_Temporary"].ToString(), "*.Dat").ToList().ForEach(x => File.Delete(x));
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


        private static void ProcessingJSON(Keyboard  keyboards)
        {

            bool chiusuraAutomatica = false;
            List<string> P_RegParData = new List<string>();
            List<string> S_PluRefData = new List<string>();
            Dictionary<string, string> flussoCasse = new Dictionary<string, string>();

            try
            {
                int recordInError = 0;
                nameBackupDirectory = ConfigurationManager.AppSettings["Directory_Backup"].ToString() + "\\" + keyboards.NomeFlusso + DateTime.Now.ToString("ddMMyyyyHHmm");
                Log.Information("Creating Backup directory " + nameBackupDirectory);
                Directory.CreateDirectory(nameBackupDirectory);

                Log.Information("*** Processing Json file begin ***");

                int numeroPulsante = 0;
                string pulsante = "";
                string tastieraDYKY3 = "";
                string tastieraDYTX3 = "";


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
                            Log.Information("Coping default image into Image directory");
                            FileOperations(ActonFile.Coping, ConfigurationManager.AppSettings["Image_Default"].ToString(), ConfigurationManager.AppSettings["Directory_Image"].ToString());
                        }
                    }
                }
                catch (Exception)
                {
                    Log.Error("Exception occurred into processing image list");
                    recordInError++;
                }

                Log.Information("Processing Layouts list...");               

                foreach (Layout lt in keyboards.Layouts)
                {
                    Log.Information("\r\n");
                    Log.Information("PROCESSO Layout: " + lt.CodiceLayout);
                    P_RegParData = new List<string>();
                    try
                    {
                        foreach (Pagine pg in lt.Pagine)                        
                        {
                            Log.Information("PROCESSO Pagina: " + pg.Codice);
                            numeroPulsante = 0;
                            string data = "";
                            foreach (Pulsante pl in pg.Pulsante)
                            {
                                if (pl != null)
                                {
                                    numeroPulsante++;
                                    data = pg.Codice.ToString().PadLeft(4, '0') + ":" + (numeroPulsante).ToString().PadLeft(4, ' ') + ":" + pl.Valore.PadLeft(16, ' ') + ":0000:" + pl.Descrizione.PadRight(20, ' ');
                                    if (!S_PluRefData.Contains(data))
                                    {
                                        S_PluRefData.Add(data);
                                    }
                                    
                                }
                            }
                        }

                        if (CreateS_PLUREF_file(S_PluRefData, keyboards.PulisciTutto))
                        {
                            Log.Information("Created directory old");
                            Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + "old");                            
                            Log.Information("Copied current file S_PLUREF.DAT into old directory...");
                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + S_PLUREF), (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\old\\" + S_PLUREF));
                            Log.Information("Copied new file S_PLUREF.dat into backup directory...");
                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + S_PLUREF), nameBackupDirectory + "\\" + S_PLUREF);
                            Log.Information("Copied new file S_PLUREF.dat into casse directory...");
                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + S_PLUREF), (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" +  S_PLUREF));
                            
                            //DMA-P-3129#A BEG
                            Log.Information("Created directory old (casseLan)");
                            Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_CasseLan"].ToString() + "\\" + "old");

                            Log.Information("Copied current file S_PLUREF.DAT into old directory (casseLan)...");                            
                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_CasseLan"].ToString() + "\\" + S_PLUREF), (ConfigurationManager.AppSettings["Directory_CasseLan"].ToString() + "\\old\\" + S_PLUREF));

                            Log.Information("Copied new file S_PLUREF.dat into casse directory...");
                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + S_PLUREF), (ConfigurationManager.AppSettings["Directory_CasseLan"].ToString() + "\\" + S_PLUREF));
                            //DMA-P-3129#A END
                        }
                        else
                        {
                            Log.Information("S_PLUREF.DAT file creation failed");
                            string newDirectory = (ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + DateTime.Now.ToString("ddMMyyyyHHmm"));
                            Log.Information("Directory creation " + newDirectory);
                            Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + newDirectory);
                            Log.Information("Cancelled files");
                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Processing"].ToString() + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()), (newDirectory + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()));
                            Directory.EnumerateFiles(ConfigurationManager.AppSettings["Directory_Temporary"].ToString(), "*.Dat").ToList().ForEach(x => File.Delete(x));
                            return;
                        }
                    }
                    catch (Exception)
                    {
                        Log.Error("Exception occurred into processing Layouts list");
                        recordInError++;
                    }

                    Log.Information("Processing Tastiere list...\r\n");

                    numeroPulsante = 0;
                    pulsante = "";
                    tastieraDYKY3 = "DYKY3:";
                    tastieraDYTX3 = "DYTX3:";

                    try
                    {
                        foreach (PulsanteTastiera pl in lt.TastieraPrincipale.Pulsante)
                        {
                            if (pl != null)
                            {
                                numeroPulsante++;
                                tastieraDYKY3 += "D" + numeroPulsante.ToString();
                                tastieraDYTX3 += "D0" + numeroPulsante.ToString();

                                if (!string.IsNullOrEmpty(pl.AltriDettagli))
                                {
                                    chiusuraAutomatica = pl.AltriDettagli.Split('=').ElementAt(1).ToLower().Equals("true") ? true : false;
                                }

                                switch (pl.Azione)
                                {
                                    case 0:
                                        pulsante = "PRES" + numeroPulsante.ToString() + ":DYNA:" + " " +  pl.Valore.PadLeft(2, '0').PadLeft(15, ' ') + ":" + pl.Descrizione.PadRight(18, ' ');
                                        break;
                                    case 1:
                                        pulsante = "PRES" + numeroPulsante.ToString() + ":LIST:" + (chiusuraAutomatica ? "1" : " ") + pl.Valore.PadLeft(4, '0').PadLeft(15, ' ') + ":" + pl.Descrizione.PadRight(18, ' ');
                                        break;
                                    case 2:
                                        pulsante = "PRES" + numeroPulsante.ToString() + ":0000:" + " " + pl.Valore.PadLeft(15, ' ') + ":" + pl.Descrizione.PadRight(18, ' ');
                                        break;
                                }

                                P_RegParData.Add(pulsante);
                            }
                        }

                        tastieraDYKY3 = tastieraDYKY3.PadRight(46, '0');
                        P_RegParData.Add(tastieraDYKY3);
                        tastieraDYTX3 = tastieraDYTX3.PadRight(46, '0');
                        P_RegParData.Add(tastieraDYTX3);


                        if (lt.TastiereDestra != null)
                        {
                            foreach (Tastieredestra td in lt.TastiereDestra)
                            {
                                numeroPulsante = 0;
                                pulsante = "";

                                foreach (PulsanteTastiera pl in td.Pulsante)
                                {
                                    if (pl != null)
                                    {

                                        if (!string.IsNullOrEmpty(pl.AltriDettagli))
                                        {
                                            chiusuraAutomatica = pl.AltriDettagli.Split('=').ElementAt(1).ToLower().Equals("true") ? true : false;
                                        }

                                        switch (pl.Azione)
                                        {
                                            case 1:                                              
                                                pulsante = "PD" + td.Codice.PadLeft(2, '0') + numeroPulsante.ToString() + ":LIST:" + (chiusuraAutomatica ? "1" : " ") + pl.Valore.PadLeft(4, '0').PadLeft(15, ' ') + ":" + pl.Descrizione.PadRight(18, ' ');
                                                numeroPulsante++;
                                                break;
                                            case 2:
                                                pulsante = "PD" + td.Codice.PadLeft(2, '0') + numeroPulsante.ToString() + ":0000: " + pl.Valore.PadLeft(15, ' ') + ":" + pl.Descrizione.PadRight(18, ' ');
                                                numeroPulsante++;
                                                break;
                                        }

                                        P_RegParData.Add(pulsante);
                                    }
                                }
                            }
                        }

                        string versione = "VER00:" + keyboards.NomeFlusso.PadLeft(40, '0');
                        P_RegParData.Add(versione);

                        if (CreateP_REGPAR_file(P_RegParData, (lt.Casse != null ? lt.Casse.ToList<int>() : null), lt.Tipo))
                        {
                            Log.Information("Created directory old");
                            Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + "old");

                            Log.Information("Created directory old (casseLan)");
                            Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_CasseLan"].ToString() + "\\" + "old");

                            if (fileTipico.Count > 0)
                            {
                                foreach (string filename in fileTipico)
                                {
                                    Log.Information("Copied current " + filename + " into old directory");
                                    FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + filename), (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\old\\" + filename));
                                    
                                    Log.Information("Copied current " + filename + " into backup directory");
                                    FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + filename), nameBackupDirectory + "\\" + filename);
                                    
                                    Log.Information("Copied new " + filename + " into casse directory");
                                    FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + filename), (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + filename));

                                    //DMA-P-3129#A BEG
                                    Log.Information("Copied current " + filename + " into old directory (casseLan)");
                                    FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_CasseLan"].ToString() + "\\" + filename), (ConfigurationManager.AppSettings["Directory_CasseLan"].ToString() + "\\old\\" + filename));

                                    Log.Information("Copied new " + filename + " into casse directory (casseLan)");
                                    FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + filename), (ConfigurationManager.AppSettings["Directory_CasseLan"].ToString() + "\\" + filename));
                                    //DMA-P-3129#A END
                                }

                            }
                            else
                            {
                                Log.Information("Copied current file P_REGPAR.DAT into old directory");
                                FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + P_REGPAR), (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\old\\" + P_REGPAR));

                                Log.Information("Copied current file P_REGPAR.DAT into backup directory");
                                FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), nameBackupDirectory + "\\" + P_REGPAR);

                                Log.Information("Copied new P_REGPAR.DAT into casse directory");
                                FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + P_REGPAR));

                                //DMA-P-3129#A BEG
                                Log.Information("Copied current file P_REGPAR.DAT into old directory (casseLan)");
                                FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_CasseLan"].ToString() + "\\" + P_REGPAR), (ConfigurationManager.AppSettings["Directory_CasseLan"].ToString() + "\\old\\" + P_REGPAR));

                                Log.Information("Copied new P_REGPAR.DAT into casse directory (casseLan)");
                                FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), (ConfigurationManager.AppSettings["Directory_CasseLan"].ToString() + "\\" + P_REGPAR));
                                //DMA-P-3129#A END

                            }


                            if (fileTipico.Count > 0)
                            {
                                if (File.Exists(ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + Dictionary))
                                {
                                    flussoCasse = Read((ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + Dictionary));
                                }

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

                                Write(flussoCasse, (ConfigurationManager.AppSettings["Directory_Casse"].ToString() + "\\" + Dictionary));                               
                            }                          
                        }
                        else
                        {
                            Log.Information("P_REGPAR.DAT file creation failed");
                            string newDirectory = (ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + DateTime.Now.ToString("ddMMyyyyHHmm"));
                            Log.Information("Directory creation " + newDirectory);
                            Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + newDirectory);
                            Log.Information("Cancelled files");
                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Processing"].ToString() + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()), (newDirectory + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()));
                            Directory.EnumerateFiles(ConfigurationManager.AppSettings["Directory_Temporary"].ToString(), "*.Dat").ToList().ForEach(x => File.Delete(x));
                            return;
                        }
                    }
                    catch (Exception)
                    {
                        Log.Error("Exception occurred into processing Tastiere list");
                        recordInError++;
                    }

                    // Sending status update
                    UpdateStatus(keyboards.NomeFlusso, "SERVER", 0, 3, recordInError);
                }

            }
            catch (Exception ex)
            {
                Log.Error("Exception occured 1 : " + ex.Message);
                string newDirectory = (ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + DateTime.Now.ToString("ddMMyyyyHHmm"));
                Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + newDirectory);
                Directory.EnumerateFiles(ConfigurationManager.AppSettings["Directory_Temporary"].ToString(), "*.Dat").ToList().ForEach(x => File.Copy(x, (newDirectory + "\\" + x)));
                Directory.EnumerateFiles(ConfigurationManager.AppSettings["Directory_Temporary"].ToString(), "*.Dat").ToList().ForEach(x => File.Delete(x));
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
                Log.Error("ImgChecked exception occured: " + ex.Message);
                Log.Error("ImgChecked exception occured: " + ex.StackTrace);
                imageStream = null;
                return false;
            }
        }

        //originale
        //private static bool CreateP_REGPAR_file(List<string> data, List<int> casse, bool tipico)
        //{
        //    try
        //    {             
        //        if (casse != null)
        //        {
        //            fileTipico.Clear();
        //            foreach (int value in casse)
        //            {
        //                Log.Information("cassa: " + value + " - tipico: " + tipico);
        //                if (!tipico)
        //                {                          
        //                    string filename = "P_" + value.ToString().PadLeft(3, '0') + "PAR.DAT";
        //                    string path = ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename;
        //                    if (File.Exists(path))
        //                    {
        //                         if (!CompareWithStandard(filename))
        //                         {
        //                            Log.Information("File Tipico is not equal to Standard so using it: " + filename);
        //                            List<string> dataWorked = new List<string>(data);
        //                            string[] pagine = dataWorked.ToArray();
        //                            string[] content = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename);

        //                            for (int indexP = 0; indexP < pagine.Length; indexP++)
        //                            {
        //                                for (int indexC = 0; indexC < content.Length; indexC++)
        //                                {
        //                                    if (content[indexC].StartsWith(pagine[indexP].Substring(0, 5)))
        //                                    {
        //                                        content[indexC] = pagine[indexP];
        //                                        dataWorked.Remove(pagine[indexP]);
        //                                    }
        //                                }
        //                            }

        //                            List<string> fileNewContent = new List<string>(content.ToArray<string>());
        //                            if (dataWorked.Count > 0)
        //                            {
        //                                fileNewContent.AddRange(dataWorked.AsEnumerable<string>());
        //                            }

        //                            fileNewContent.Sort();
        //                            File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + filename), fileNewContent.ToArray());
        //                            Log.Information("Saving " + filename + " file to directory: " + ConfigurationManager.AppSettings["Directory_Temporary"].ToString());
        //                            fileTipico.Add(filename);
        //                            fileTipico.Sort();
        //                        }
        //                        else //compare true
        //                        {
        //                            // Create canc_P_XXXPar.DAT into temporary directory
        //                            FileOperations(ActonFile.Creating, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + "canc" + filename), string.Empty);
        //                            Log.Information("Create canc_P_XXXPar.DAT into temporary directory: " + filename);

        //                            // Delete tipico file from directory casse
        //                            FileOperations(ActonFile.Deleting, (ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename), string.Empty);
        //                            Log.Information("File Tipico is equal to Standard so deleting it: " + filename);

        //                            // Copy canc_P_XXXParDAT into backup directory
        //                            FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + "canc" + filename), nameBackupDirectory + "\\" + "canc" + filename);
        //                            Log.Information("Coping canc_P_XXXPar.DAT into backup directory: " + filename);

        //                            List<string> dataWorked = new List<string>(data);
        //                            string[] pagine = dataWorked.ToArray();
        //                            string[] content = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + P_REGPAR);

        //                            for (int indexP = 0; indexP < pagine.Length; indexP++)
        //                            {
        //                                for (int indexC = 0; indexC < content.Length; indexC++)
        //                                {
        //                                    if (content[indexC].StartsWith(pagine[indexP].Substring(0, 5)))
        //                                    {
        //                                        content[indexC] = pagine[indexP];
        //                                        dataWorked.Remove(pagine[indexP]);
        //                                    }
        //                                }
        //                            }

        //                            List<string> newContent = new List<string>(content.ToArray<string>());
        //                            newContent.AddRange(dataWorked.AsEnumerable<string>());
        //                            newContent.Sort();
        //                            File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), newContent.ToArray());
        //                            Log.Information("Saving P_REGPAR.DAT file to directory: " + ConfigurationManager.AppSettings["Directory_Temporary"].ToString());
        //                        }
        //                    }
        //                    else { //file tipico non esiste
        //                            List<string> dataWorked = new List<string>(data);
        //                            string[] pagine = dataWorked.ToArray();
        //                            string[] content = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + P_REGPAR);

        //                            for (int indexP = 0; indexP < pagine.Length; indexP++)
        //                            {
        //                                for (int indexC = 0; indexC < content.Length; indexC++)
        //                                {
        //                                    if (content[indexC].StartsWith(pagine[indexP].Substring(0, 5)))
        //                                    {
        //                                        content[indexC] = pagine[indexP];
        //                                        dataWorked.Remove(pagine[indexP]);
        //                                    }
        //                                }
        //                            }

        //                            List<string> newContent = new List<string>(content.ToArray<string>());
        //                            newContent.AddRange(dataWorked.AsEnumerable<string>());
        //                            newContent.Sort();
        //                            File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + filename), newContent.ToArray());
        //                            Log.Information("Saving " + filename +" file to directory: " + ConfigurationManager.AppSettings["Directory_Temporary"].ToString());

        //                        //qui
        //                            //se il file tipico non esiste non lo aggiungo
        //                            fileTipico.Add(filename);
        //                            fileTipico.Sort();
        //                    }
        //                }
        //                else //tipico
        //                {
        //                    Log.Information("entro nell else . tipico vale true");
        //                    string filename = "P_" + value.ToString().PadLeft(3, '0') + "PAR.DAT";
        //                    if (File.Exists(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename))
        //                    {
        //                        List<string> dataWorked = new List<string>(data);
        //                        string[] pagine = dataWorked.ToArray();
        //                        string[] content = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename);

        //                        for (int indexP = 0; indexP < pagine.Length; indexP++)
        //                        {
        //                            for (int indexC = 0; indexC < content.Length; indexC++)
        //                            {
        //                                if (content[indexC].StartsWith(pagine[indexP].Substring(0, 5)))
        //                                {
        //                                    content[indexC] = pagine[indexP];
        //                                    dataWorked.Remove(pagine[indexP]);
        //                                }
        //                            }
        //                        }

        //                        List<string> fileNewContent = new List<string>(content.ToArray<string>());
        //                        if (dataWorked.Count > 0)
        //                        {
        //                            fileNewContent.AddRange(dataWorked.AsEnumerable<string>());
        //                        }

        //                        fileNewContent.Sort();
        //                        File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + filename), fileNewContent.ToArray());
        //                        Log.Information("Saving " + filename + " file to directory: " + ConfigurationManager.AppSettings["Directory_Temporary"].ToString());
        //                        fileTipico.Add(filename);
        //                        fileTipico.Sort();
        //                    }
        //                    else
        //                    {
        //                        List<string> dataWorked = new List<string>(data);
        //                        string[] pagine = dataWorked.ToArray();
        //                        string[] content = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + P_REGPAR);

        //                        for (int indexP = 0; indexP < pagine.Length; indexP++)
        //                        {
        //                            for (int indexC = 0; indexC < content.Length; indexC++)
        //                            {
        //                                if (content[indexC].StartsWith(pagine[indexP].Substring(0, 5)))
        //                                {
        //                                    content[indexC] = pagine[indexP];
        //                                    dataWorked.Remove(pagine[indexP]);
        //                                }
        //                            }
        //                        }

        //                        List<string> newContent = new List<string>(content.ToArray<string>());
        //                        newContent.AddRange(dataWorked.AsEnumerable<string>());
        //                        newContent.Sort();
        //                        File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + filename), newContent.ToArray());
        //                        Log.Information("Saving " + filename + " file to directory: " + ConfigurationManager.AppSettings["Directory_Temporary"].ToString());
        //                        fileTipico.Add(filename);
        //                        fileTipico.Sort();
        //                    }
        //                }
        //            }
        //            return true;
        //        }
        //        else {
        //                List<string> dataWorked = new List<string>(data);
        //                string[] pagine = dataWorked.ToArray();
        //                string[] content = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + P_REGPAR);                      

        //                for (int indexP = 0; indexP < pagine.Length; indexP++)
        //                {
        //                    for (int indexC = 0; indexC < content.Length; indexC++)
        //                    {
        //                        if (content[indexC].StartsWith(pagine[indexP].Substring(0, 4)))
        //                        {
        //                            content[indexC] = pagine[indexP];
        //                            dataWorked.Remove(pagine[indexP]);
        //                        }
        //                    }
        //                }

        //                List<string> newContent = new List<string>(content.ToArray<string>());
        //                newContent.AddRange(dataWorked.AsEnumerable<string>());
        //                newContent.Sort();
        //                File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), newContent.ToArray());
        //                return true;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Error("Exception occured: " + ex.InnerException);
        //        return false;
        //    }
        //}

        private static string[] aggiornaRighePar(List<string> data, string filename, List<string> dataWorked)
        {
            Log.Information("ENTRO in aggiornaRighePar : " + filename + " dataWorked.len: " + dataWorked.Count);

            string[] righeNuove = dataWorked.ToArray();
            string[] righeP_regpar = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename);

            //pulisco il p_regpar, tolgo le righe "PD0", e "PRES"
            for (int indexP = 0; indexP < righeP_regpar.Length; indexP++)
            {
                if (righeP_regpar[indexP].StartsWith(KEY_PD0)  || 
                    righeP_regpar[indexP].StartsWith(KEY_PRES) ||
                    righeP_regpar[indexP].StartsWith(KEY_VER))
                {
                    Log.Information("elimino : >" + righeP_regpar[indexP] + "<" + " - indexP : " + indexP);
                    righeP_regpar[indexP] = "";
                }
            }

            //inserisco le righe "DYKY" e "DYTX"
            
            for (int i=0; i<righeNuove.Length; i++)
            {
                if (righeNuove[i].StartsWith(KEY_DYKY)  || 
                    righeNuove[i].StartsWith(KEY_DYTX))    {
                    //cerco la riga nel P_REGPAR per sostituirla

                for (int j=0; j<righeP_regpar.Length; j++)
                    {
                        if (righeP_regpar[j].StartsWith(righeNuove[i].Substring(0, 5)))
                        {
                            Log.Debug("sostituisco : >" + righeP_regpar[j] + "<");
                            Log.Debug("        con : >" + righeNuove[i] + "<");
                            righeP_regpar[j] = righeNuove[i];
                        }
                    }
                }
            }
            
            //metto il p_regpar in una lista
            List<string> lista_pregpar = new List<string>();
            for (int i = 0; i < righeP_regpar.Length; i++)
            {
                if (righeP_regpar[i].Length == 46)
                {
                    //Log.Information("inserisco in lista : >" + righeP_regpar[i] + "<" + " - i : " + i + " - len: " + righeP_regpar[i].Length);
                    lista_pregpar.Add(righeP_regpar[i]);
                }
            }

            //aggiungo i nuovi elementi
            for (int indexP = 0; indexP < righeNuove.Length; indexP++)
            {                
                if ( !(righeNuove[indexP].StartsWith(KEY_DYKY) || righeNuove[indexP].StartsWith(KEY_DYTX)) ) 
                { 
                    Log.Information("aggiungo i nuovi elementi : >" + righeNuove[indexP] + "<");
                    lista_pregpar.Add(righeNuove[indexP]);
                }
            }

            lista_pregpar.Sort();
          
            string[] p_regpar_finale;

            p_regpar_finale = lista_pregpar.ToArray();

            Log.Information("ESCO da aggiornaRighePar : ");
            return p_regpar_finale;
        }
/*
        private static string[] aggiornaRighePar(List<string> data, string filename, List<string> dataWorked)
        {
            Log.Information("ENTRO in aggiornaRighePar : " + filename);
         
            string[] righeNuove = dataWorked.ToArray();
            string[] righeP_regpar = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename);

            for (int indexP = 0; indexP < righeNuove.Length; indexP++)
            {
                for (int indexC = 0; indexC < righeP_regpar.Length; indexC++)
                {
                    if (righeP_regpar[indexC].StartsWith(righeNuove[indexP].Substring(0, 5)))
                    {
                        Log.Information("inserisco : >" + righeNuove[indexP] + "<" + " - indexP : " + indexP);
                        righeP_regpar[indexC] = righeNuove[indexP];
                        dataWorked.Remove(righeNuove[indexP]);
                    }else if( righeP_regpar[indexC].StartsWith("PD0") || righeP_regpar[indexC].StartsWith("PRES"))
                    {
                        Log.Information("elimino : >" + righeP_regpar[indexC] + "<" + " - indexP : " + indexP);
                        righeP_regpar[indexC] = "";
                    }
                }
            }

            List<string> righeP_regpar_finale = new List<string>();
            string[] p_regpar_finale;
            for (int i=0; i< righeP_regpar.Length; i++)
            {
                if (righeP_regpar[i].Length > 40)
                {
                    righeP_regpar_finale.Add (righeP_regpar[i]);
                    
                }
            }

            p_regpar_finale = righeP_regpar_finale.ToArray();
            Log.Information("ESCO da aggiornaRighePar : ");
            return p_regpar_finale;
        }
        */
        private static bool CreateP_REGPAR_file(List<string> data, List<int> casse, bool tipico)
        {
            Log.Information("\r\n");
            Log.Information("ENTRO IN  CreateP_REGPAR_file - data.len: " + data.Count);
            try
            {
                if (casse != null)
                {
                    fileTipico.Clear();
                    foreach (int value in casse) 
                    {
                        Log.Information("\r\n");
                        Log.Information("cassa: " + value + " - tipico: " + tipico);
                        if (!tipico)
                        {
                            string filename = "P_" + value.ToString().PadLeft(3, '0') + "PAR.DAT";
                            string path = ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename;
                            if (File.Exists(path))
                            {
                                if (!CompareWithStandard(filename))
                                {
                                    Log.Information("File Tipico is not equal to Standard so using it: " + filename);

                                    List<string> dataWorked = new List<string>(data);
                                    string[] content = aggiornaRighePar(data, filename, dataWorked);
                                    
                                    List<string> fileNewContent = new List<string>(content.ToArray<string>());
                                    //if (dataWorked.Count > 0)
                                    //{
                                    //    fileNewContent.AddRange(dataWorked.AsEnumerable<string>());
                                    //}

                                    fileNewContent.Sort();
                                    File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + filename), fileNewContent.ToArray());
                                    Log.Information("Saving " + filename + " file to directory: " + ConfigurationManager.AppSettings["Directory_Temporary"].ToString());
                                    fileTipico.Add(filename);
                                    fileTipico.Sort();
                                }
                                else //compare true
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

                                    string[] content = aggiornaRighePar(data, P_REGPAR, dataWorked);
                                   
                                    List<string> newContent = new List<string>(content.ToArray<string>());
                                    //newContent.AddRange(dataWorked.AsEnumerable<string>());
                                    newContent.Sort();
                                    File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), newContent.ToArray());
                                    Log.Information("Saving P_REGPAR.DAT file to directory: " + ConfigurationManager.AppSettings["Directory_Temporary"].ToString());
                                }
                            }
                            else
                            { //file tipico non esiste
                                List<string> dataWorked = new List<string>(data);

                                string[] content = aggiornaRighePar(data, P_REGPAR, dataWorked);

                                List<string> newContent = new List<string>(content.ToArray<string>());
                                //newContent.AddRange(dataWorked.AsEnumerable<string>());
                                newContent.Sort();
                                //File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + filename), newContent.ToArray());
                                File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), newContent.ToArray());
                                Log.Information("Saving " + filename + " file to directory: " + ConfigurationManager.AppSettings["Directory_Temporary"].ToString());

                                //qui
                                //se il file tipico non esiste non lo aggiungo
                                //fileTipico.Add(filename);
                                //fileTipico.Sort();
                            }
                        }
                        else //tipico
                        {
                            Log.Information("entro nell else . tipico vale true");
                            string filename = "P_" + value.ToString().PadLeft(3, '0') + "PAR.DAT";
                            if (File.Exists(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename))
                            {
                                List<string> dataWorked = new List<string>(data);

                                string[] content = aggiornaRighePar(data, filename, dataWorked);

                                List<string> fileNewContent = new List<string>(content.ToArray<string>());
                                //if (dataWorked.Count > 0)
                                //{
                                //    fileNewContent.AddRange(dataWorked.AsEnumerable<string>());
                                //}

                                fileNewContent.Sort();
                                File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + filename), fileNewContent.ToArray());
                                Log.Information("Saving " + filename + " file to directory: " + ConfigurationManager.AppSettings["Directory_Temporary"].ToString());
                                fileTipico.Add(filename);
                                fileTipico.Sort();
                            }
                            else
                            {
                                List<string> dataWorked = new List<string>(data);

                                string[] content = aggiornaRighePar(data, P_REGPAR, dataWorked);

                                List<string> newContent = new List<string>(content.ToArray<string>());
                                //newContent.AddRange(dataWorked.AsEnumerable<string>());
                                newContent.Sort();
                                File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + filename), newContent.ToArray());
                                Log.Information("Saving " + filename + " file to directory: " + ConfigurationManager.AppSettings["Directory_Temporary"].ToString());
                                fileTipico.Add(filename);
                                fileTipico.Sort();
                            }
                        }
                    }
                    Log.Information("ESCO DA CreateP_REGPAR_file 1 - true");
                    return true;
                }
                else
                {
                    List<string> dataWorked = new List<string>(data);
                    
                    string[] content = aggiornaRighePar(data, P_REGPAR, dataWorked);

                    List<string> newContent = new List<string>(content.ToArray<string>());
                    //newContent.AddRange(dataWorked.AsEnumerable<string>());
                    newContent.Sort();
                    File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), newContent.ToArray());
                    Log.Information("ESCO DA CreateP_REGPAR_file 2 - true");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception occured 2 : " + ex.Message);
                Log.Information("ESCO DA CreateP_REGPAR_file 1 - false");
                return false;
            }
        }

        private static Boolean CompareWithStandard(string filename)
        {
            Log.Information("ENTRO in CompareWithStandard. filename: " + filename);
            Boolean flag = false;
            // Make a list copy...
            List<string> regParData = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + filename).ToList<string>();
            List<string> workedData = regParData.ToList<string>();
            List<string> pregParData_sotto_server = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + P_REGPAR).ToList<string>();
            List<string> workedPregParData_sotto_server = pregParData_sotto_server.ToList<string>();

            // Delete the extra row from tipico file...

            Log.Information(filename);
            foreach (string data in workedData)
            {
                if (data.StartsWith(KEY_DYKY + "3") ||
                    data.StartsWith(KEY_DYTX + "3") ||
                    data.StartsWith(KEY_PRES) ||
                    data.StartsWith(KEY_PD0) ||
                    data.StartsWith(KEY_VER))
                {
                    Log.Information(data + "rimosso");
                    regParData.Remove(data);
                }
                else
                {
                    //Log.Debug(data);
                }
            }

            Log.Information("P_REGPAR SOTTO SERVER");
            foreach (string data in workedPregParData_sotto_server)
            {
                if (data.StartsWith(KEY_DYKY + "3") ||
                    data.StartsWith(KEY_DYTX + "3") ||
                    data.StartsWith(KEY_PRES) ||
                    data.StartsWith(KEY_PD0) ||
                    data.StartsWith(KEY_VER))
                {
                    Log.Information(data + "rimosso");
                    pregParData_sotto_server.Remove(data);
                }
                else
                {
                    //Log.Debug(data);
                }
            }

            // Compared tipico file with standard file...
            //flag = regParData.SequenceEqual(File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Asar"].ToString() + "\\" + P_REGPAR));
            flag = regParData.SequenceEqual(pregParData_sotto_server);
            Log.Information("ESCO Da CompareWithStandard. flag: " + flag);
            return flag;
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
                Log.Error("Exception occured 3 : " + ex.Message);
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
            catch (Exception ex)
            {
                Log.Error("Exception occured 4: " + ex.Message);
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
                Log.Information("Send JSON: " + JsonConvert.SerializeObject(contentRequest));
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
                // Put count...
                writer.Write(dictionary.Count);
                // Write pairs...
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
