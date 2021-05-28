using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
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
        private static FileSystemWatcher watcher = null;
        private const String S_PLUREF = "S_PLUREF.DAT";
        private const String P_REGPAR = "P_REGPAR.DAT";

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
                    GwpFlusso request = new GwpFlusso();
                    request.Step = "ERROR";
                    request.Response = "File JSON non valido";
                    SendHTTPRequest(request);
                    string newDirectory = (ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + DateTime.Now.ToString("ddMMyyyyHHmm"));
                    Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + newDirectory);
                    FileOperations(ActonFile.Moving, e.FullPath, (newDirectory + "\\" + e.Name));
                }
            }
        }


        private static bool ProcessingJSON(Keyboard  keyboards)
        {
            List<String> P_RegParData = new List<string>();
            List<String> S_PluRefData = new List<string>();

            try
            {

                Log.Information("Creating Backup directory " + (ConfigurationManager.AppSettings["Directory_Backup"].ToString() + "\\" + keyboards.NomeFlusso + DateTime.Now.ToString("ddMMyyyyHHmm")));
                Directory.CreateDirectory((ConfigurationManager.AppSettings["Directory_Backup"].ToString() + "\\" + keyboards.NomeFlusso + DateTime.Now.ToString("ddMMyyyyHHmm")));

                Log.Information("Processing Json file starting...");

                int numeroPulsante = 0;
                String pulsante = "";
                String tastiera = "";

                Log.Information("Processing image list...");

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

                Log.Information("Processing Layouts list...");

                foreach (Layout lt in keyboards.Layouts)
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
                        Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Pos"].ToString() + "\\" + "old");
                        Log.Information("Copied current file S_PLUREF.DAT into old directory...");
                        FileOperations(ActonFile.Coping, (ConfigurationManager.AppSettings["Directory_Pos"].ToString() + "\\" + S_PLUREF), (ConfigurationManager.AppSettings["Directory_Pos"].ToString() + "\\old" + S_PLUREF));
                        Log.Information("Moved new file S_PLUREF.dat into POS directory...");
                        FileOperations(ActonFile.Moving, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + S_PLUREF), (ConfigurationManager.AppSettings["Directory_Pos"].ToString() + "\\" + S_PLUREF));
                    }
                    else
                    {
                        Log.Information("S_PLUREF.DAT file creation failed");
                        string newDirectory = (ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + DateTime.Now.ToString("ddMMyyyyHHmm"));
                        Log.Information("Directory creation " + newDirectory);
                        Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + newDirectory);
                        Log.Information("Cancelled files");
                        FileOperations(ActonFile.Moving, (ConfigurationManager.AppSettings["Directory_Processing"].ToString() + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()), (newDirectory + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()));
                        FileOperations(ActonFile.Deleting, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + S_PLUREF), "");
                        return false;
                    }


                    numeroPulsante = 0;
                    pulsante = "";
                    tastiera = "DYKY3:";

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

                    String versione = "VER00:" + keyboards.NomeFlusso.PadLeft(40, '0');
                    P_RegParData.Add(versione);

                    if (CreateP_REGPAR_file(P_RegParData, (lt.Casse != null ? lt.Casse.ToList<int>() : null)))
                    {
                        GwpFlusso request = new GwpFlusso();
                        request.Step = "APPLIED";
                        request.Response = "Flusso: " + keyboards.NomeFlusso;
                        SendHTTPRequest(request);
                    }
                    else
                    {
                        Log.Information("P_REGPAR.DAT file creation failed");
                        string newDirectory = (ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + DateTime.Now.ToString("ddMMyyyyHHmm"));
                        Log.Information("Directory creation " + newDirectory);
                        Directory.CreateDirectory(ConfigurationManager.AppSettings["Directory_Error"].ToString() + "\\" + newDirectory);
                        Log.Information("Cancelled files");
                        FileOperations(ActonFile.Moving, (ConfigurationManager.AppSettings["Directory_Processing"].ToString() + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()), (newDirectory + "\\" + ConfigurationManager.AppSettings["FilenameJson"].ToString()));
                        FileOperations(ActonFile.Deleting, (ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + S_PLUREF), "");
                    }

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


        private static bool CreateP_REGPAR_file(List<String> data, List<int> casse)
        {
            try
            {
                if (casse != null)
                {
                    foreach (int value in casse)
                    {
                        string filename = "P_REG" + value.ToString().PadLeft(3, '0') + "PAR.DAT";
                        if (File.Exists(ConfigurationManager.AppSettings["Directory_Pos"].ToString() + "\\" + filename))
                        {
                            List<String> dataWorked = new List<string>(data);
                            String[] pagine = dataWorked.ToArray();
                            String[] content = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Pos"].ToString() + "\\" + filename);

                            if (CompareWithStandard(content.ToList<String>()))
                            { 
                                
                            }

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

                            List<string> fileNewContent = new List<string>(content.ToArray<String>());
                            if (dataWorked.Count > 0)
                            {
                                fileNewContent.AddRange(dataWorked.AsEnumerable<String>());
                            }

                            File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Pos"].ToString() + "\\" + filename), fileNewContent.ToArray());
                        }
                        else
                        {
                            List<String> dataWorked = new List<string>(data);
                            String[] pagine = dataWorked.ToArray();
                            String[] content = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Pos"].ToString() + "\\" + P_REGPAR);

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

                            List<string> newContent = new List<string>(content.ToArray<String>());
                            newContent.AddRange(dataWorked.AsEnumerable<String>());
                            newContent.Sort();
                            File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + P_REGPAR), newContent.ToArray());
                        }
                    }
                    return true;
                }
                else {
                        List<String> dataWorked = new List<string>(data);
                        String[] pagine = dataWorked.ToArray();
                        String[] content = File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Pos"].ToString() + "\\" + P_REGPAR);
                      

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

                        List<string> newContent = new List<string>(content.ToArray<String>());
                        newContent.AddRange(dataWorked.AsEnumerable<String>());
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

        private static Boolean CompareWithStandard(List<String> regParData)
        {
            // Make a list copy...
            List<String> workedData = regParData.ToList<String>();

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
            return (regParData.SequenceEqual(File.ReadAllLines(ConfigurationManager.AppSettings["Directory_Pos"].ToString() + "\\" + P_REGPAR)));
        }


        private static bool CreateS_PLUREF_file(List<String> data, bool newCreation)
        {
            try
            {
                if (newCreation)
                {
                    List<string> newContent = new List<string>(data.ToArray<String>());
                    File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + S_PLUREF), newContent.ToArray());
                    return true;
                }
                else
                {
                    String s = String.Empty;
                    String[] content = File.ReadAllLines((ConfigurationManager.AppSettings["Directory_Pos"].ToString() + "\\" + S_PLUREF));
                    String[] pagine = data.ToArray();

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

                    List<string> newContent = new List<string>(content.ToArray<String>());
                    newContent.AddRange(data.AsEnumerable<String>());
                    newContent.Sort();
                    File.WriteAllLines((ConfigurationManager.AppSettings["Directory_Temporary"].ToString() + "\\" + S_PLUREF), newContent.ToArray());
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
    }
}
