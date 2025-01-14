﻿using Memory;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using YuukiPS_Launcher.Json;
using YuukiPS_Launcher.Json.GameClient;
using YuukiPS_Launcher.Yuuki;

namespace YuukiPS_Launcher
{
    public partial class Main : Form
    {
        Config configdata = new Config();

        // Main Function
        private Proxy? proxy;
        private Process? progress;

        // Server List
        Thread thServerList;
        List<List> ListServer;

        // Folder
        public static string CurrentlyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "");
        public static string DataConfig = Path.Combine(CurrentlyPath, "data");
        public static string Modfolder = Path.Combine(CurrentlyPath, "mod");

        // File Config
        public string ConfigPath = Path.Combine(DataConfig, "config.json");

        // Stats default
        public string WatchFile = "";
        public string HostName = "Unknown";
        public bool IsGameRun = false;
        public bool DoneCheck = true;
        public string VersionGame = "";
        public int GameChannel = 0;
        public int GameMetode = 1;

        // Extra
        Extra.Discord discord = new Extra.Discord();

        // Game
        public Game.Genshin.Settings settings_genshin;

        //KeyGS key;
        Patch get_version;

        public void LoadConfig()
        {
            // Create missing folder
            Directory.CreateDirectory(DataConfig);
            Directory.CreateDirectory(Modfolder);

            if (File.Exists(ConfigPath))
            {
                string data = File.ReadAllText(ConfigPath);
                try
                {
                    configdata = JsonConvert.DeserializeObject<Config>(data);
                    if (configdata != null)
                    {
                        // load config
                        Set_LA_GameFolder.Text = configdata.Game_Path;
                        GetHost.Text = configdata.Hostdefault;
                        GetPort.Text = configdata.ProxyPort.ToString();

                        checkModeOnline.Checked = configdata.MetodeOnline;
                        CheckProxyUseHTTPS.Checked = configdata.HostHTTPS;
                        Extra_AkebiGC.Checked = configdata.extra.Akebi;

                        Console.WriteLine("load config...");
                    }
                    else
                    {
                        Console.WriteLine("No config load...");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error load config: " + ex.Message);
                }
            }
            else
            {
                Console.WriteLine("No Config file found...");
            }

            // Check Game Version
            CheckVersionGame();
        }
        public void SaveConfig()
        {
            try
            {
                // save config
                configdata.Game_Path = Set_LA_GameFolder.Text;
                configdata.Hostdefault = GetHost.Text;

                int myInt;
                bool isValid = int.TryParse(GetPort.Text, out myInt);
                if (isValid)
                {
                    configdata.ProxyPort = myInt;
                }

                configdata.MetodeOnline = checkModeOnline.Checked;
                configdata.HostHTTPS = CheckProxyUseHTTPS.Checked;
                configdata.extra.Akebi = Extra_AkebiGC.Checked;

                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(configdata));

                Console.WriteLine("Done save config...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error save config: " + ex.Message);
            }
        }

        public Main()
        {
            InitializeComponent();
        }

        [Obsolete]
        private void Main_Load(object sender, EventArgs e)
        {
            Console.WriteLine("Loading....");

            // Before starting make sure proxy is turned off
            CheckProxy(true);

            // Load config and check version game
            LoadConfig();

            // Check Update
            CheckUpdate();

            // Server List
            GetServerList();
            UpdateServerListTimer();

            // Extra
            discord.Ready();
        }

        public bool CheckVersionGame()
        {
            var cst_folder_game = Set_LA_GameFolder.Text;
            // Jika user tidak punya game folder
            if (String.IsNullOrEmpty(cst_folder_game))
            {
                // cari otomatis launcher
                var Get_Launcher = GetLauncherPath();
                Console.WriteLine("Folder Launcher: " + Get_Launcher);

                // Jika tidak ada launcher
                if (string.IsNullOrEmpty(Get_Launcher))
                {
                    Console.WriteLine("Please find game install folder!");
                    return false;
                }
                else
                {
                    // jika ada launcher, cari game path
                    cst_folder_game = GetGamePath(Get_Launcher);
                }
            }

            // Check sekali lagi
            if (string.IsNullOrEmpty(cst_folder_game))
            {
                Console.WriteLine("Please find game install folder!");
                return false;
            }
            if (!Directory.Exists(cst_folder_game))
            {
                Console.WriteLine("Please find game install folder! (2)");
                return false;
            }

            Console.WriteLine("Folder Game: " + cst_folder_game);

            string cn = Path.Combine(cst_folder_game, "YuanShen.exe");
            string os = Path.Combine(cst_folder_game, "GenshinImpact.exe");

            // Path
            string PathfileGame;
            string PathMetadata;
            string PathUA;

            // Pilih Channel
            if (File.Exists(cn))
            {
                // Jika game versi cina
                WatchFile = "YuanShen";
                GameChannel = 2;
                PathfileGame = cn;
                PathMetadata = Path.Combine(cst_folder_game, "YuanShen_Data", "Managed", "Metadata");
                PathUA = Path.Combine(cst_folder_game, "YuanShen_Data", "Native");
            }
            else if (File.Exists(os))
            {
                // jika game versi global
                WatchFile = "GenshinImpact";
                GameChannel = 1;
                PathfileGame = os;
                PathMetadata = Path.Combine(cst_folder_game, "GenshinImpact_Data", "Managed", "Metadata");
                PathUA = Path.Combine(cst_folder_game, "GenshinImpact_Data", "Native");
            }
            else
            {
                // jika game versi tidak di dukung atau tidak ada file
                Console.WriteLine("No game files found!!!");
                return false;
            }

            try
            {
                settings_genshin = new(GameChannel);
                if (settings_genshin != null)
                {
                    Console.WriteLine("Game Text Language: " + settings_genshin.GetGameLanguage());
                    Console.WriteLine("Game Voice Language: " + settings_genshin.GetVoiceLanguageID());
                    // TODO: need selectedServerName, inputData > scriptVersion
                    //Console.WriteLine("JSON: " + settings_genshin.GetDataGeneralString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error getting game settings: " + ex.ToString());
            }

            // Check MD5 Game
            string Game_LOC_Original_MD5 = Tool.CalculateMD5(PathfileGame);

            // Check MD5 in Server API
            get_version = API.GetMD5Game(Game_LOC_Original_MD5);
            if (get_version == null)
            {
                //0.0.0
                Console.WriteLine("No Support Game with MD5: " + Game_LOC_Original_MD5 + " (Send this log to admin)");
                return false;
            }

            VersionGame = get_version.version;

            if (VersionGame == "0.0.0")
            {
                Console.WriteLine("Version not supported: MD5 " + Game_LOC_Original_MD5);

                Set_Metadata_Folder.Text = "";
                Set_UA_Folder.Text = "";
                Set_LA_GameFile.Text = "";

                Get_LA_Version.Text = "Version: Unknown";
                Get_LA_CH.Text = "Channel: Unknown";
                Get_LA_REL.Text = "Release: Unknown";
                Get_LA_Metode.Text = "Metode: Unknown";
                Get_LA_MD5.Text = "MD5: Unknown";

                return false;
            }

            var get_channel = get_version.channel;
            var get_metode = get_version.patched.metode;

            // Set Folder Patch
            Set_Metadata_Folder.Text = PathMetadata;
            Set_UA_Folder.Text = PathUA;
            Set_LA_GameFile.Text = PathfileGame;

            // IF ALL OK
            Set_LA_GameFolder.Text = cst_folder_game;

            // Set Version
            Get_LA_Version.Text = "Version: " + get_version.version;
            Get_LA_CH.Text = "Channel: " + get_channel;
            Get_LA_REL.Text = "Release: " + get_version.release;
            Get_LA_Metode.Text = "Metode: " + get_metode;

            var md5_ori = "???";

            // Pilih Metode
            if (get_metode == "Metadata")
            {
                GameMetode = 1;
                if (get_channel == "CN")
                {
                    md5_ori = get_version.original.md5_check.cn.metadata;
                }
                if (get_channel == "OS")
                {
                    md5_ori = get_version.original.md5_check.os.metadata;
                }
            }
            else if (get_metode == "UserAssembly")
            {
                GameMetode = 2;
                if (get_channel == "CN")
                {
                    md5_ori = get_version.original.md5_check.cn.userassembly;
                }
                if (get_channel == "OS")
                {
                    md5_ori = get_version.original.md5_check.os.userassembly;
                }
            }
            else if (get_metode == "RSA")
            {
                GameMetode = 3;
                md5_ori = "No need to check";
            }

            Get_LA_MD5.Text = "MD5: " + md5_ori;

            Console.WriteLine("Currently using version game " + VersionGame);

            Console.WriteLine("Folder PathMetadata: " + PathMetadata);
            Console.WriteLine("File Game: " + PathfileGame);

            Console.WriteLine("MD5 Game Currently: " + Game_LOC_Original_MD5);

            return true;
        }

        public string PatchGame(bool patchit = true, bool online = true, int metode = 1, int ch = 1)
        {
            // Check Folder Game
            var cst_folder_game = Set_LA_GameFolder.Text;
            if (String.IsNullOrEmpty(cst_folder_game))
            {
                return "No game folder found (1)";
            }
            if (!Directory.Exists(cst_folder_game))
            {
                return "No game folder found (2)";
            }

            if (get_version == null)
            {
                return "Can't find version, try clicking 'Get Key' in config tab";
            }

            // Check version
            if (VersionGame == "0.0.0")
            {
                return "This Game Version is not compatible with this method patch";
            }

            if (get_version.patched == null)
            {
                return "Can't find config patch cloud";
            }

            if (get_version.original == null)
            {
                return "Can't find config original cloud";
            }

            // API STUFF
            var use_metode = get_version.patched.metode;
            var use_channel = get_version.channel;

            // LOCALHOST            
            string MD5_UA_API_Original;
            string MD5_UA_API_Patched;
            string MD5_Metadata_API_Original;
            string MD5_Metadata_API_Patched;
            string MD5_API_Patched;

            var DL_Patch = get_version.patched.resources + "Patch/";
            var DL_Original = get_version.original.resources;

            var Original_file_MA = "";
            var Original_file_UA = "";

            var key_to_patch = "";
            var key_to_find = "";

            // Select Metode (via API Cloud)
            if (use_channel == "OS")
            {
                MD5_UA_API_Original = get_version.original.md5_check.os.userassembly.ToUpper();
                MD5_UA_API_Patched = get_version.patched.md5_vaild.os.ToUpper();

                MD5_Metadata_API_Original = get_version.original.md5_check.os.metadata;
                MD5_Metadata_API_Patched = get_version.patched.md5_vaild.os.ToUpper();

                key_to_patch = get_version.patched.key_patch;
                key_to_find = get_version.original.key_find.os;

                Original_file_MA = DL_Original + "GenshinImpact_Data/Managed/Metadata/global-metadata.dat";
                Original_file_UA = DL_Original + "GenshinImpact_Data/Native/UserAssembly.dll";

                MD5_API_Patched = get_version.patched.md5_vaild.os.ToUpper();

            }
            else if (use_channel == "CN")
            {
                MD5_UA_API_Original = get_version.original.md5_check.cn.userassembly.ToUpper();
                MD5_UA_API_Patched = get_version.patched.md5_vaild.cn.ToUpper();

                MD5_Metadata_API_Original = get_version.original.md5_check.cn.metadata;
                MD5_Metadata_API_Patched = get_version.patched.md5_vaild.cn.ToUpper();

                key_to_patch = get_version.patched.key_patch;
                key_to_find = get_version.original.key_find.cn;

                Original_file_MA = DL_Original + "YuanShen_Data/Managed/Metadata/global-metadata.dat";
                Original_file_UA = DL_Original + "YuanShen_Data/Native/UserAssembly.dll";

                MD5_API_Patched = get_version.patched.md5_vaild.cn.ToUpper();
            }
            else
            {
                return "This Game Version is not compatible with Any Method Patch";
            }

            // >> Make sure MD5 API is not empty <<

            // skip check patch
            if (metode != 3)
            {                
                if (String.IsNullOrEmpty(MD5_UA_API_Patched))
                {
                    return "Game version is not supported (3)";
                }
                if (String.IsNullOrEmpty(MD5_Metadata_API_Patched))
                {
                    return "Game version is not supported (1)";
                }
            }

            if (String.IsNullOrEmpty(MD5_UA_API_Original))
            {
                return "Game version is not supported (4)";
            }
            if (String.IsNullOrEmpty(MD5_Metadata_API_Original))
            {
                return "Game version is not supported (2)";
            }
            

            // >> All <<

            // Check Folder UA
            var cst_folder_UA = Set_UA_Folder.Text;
            var cst_folder_Game = Set_LA_GameFolder.Text;

            if (String.IsNullOrEmpty(cst_folder_UA))
            {
                return "No UserAssembly folder found (1)";
            }
            if (!Directory.Exists(cst_folder_UA))
            {
                return "No UserAssembly folder found (2)";
            }
            // Check file UserAssembly
            string PathfileUA_Currently = Path.Combine(cst_folder_UA, "UserAssembly.dll");
            string PathfileUA_Patched = Path.Combine(cst_folder_UA, "UserAssembly-patched.dll");
            string PathfileUA_Original = Path.Combine(cst_folder_UA, "UserAssembly-original.dll");
            // Check MD5 local (First time)
            string MD5_UA_LOC_Currently = Tool.CalculateMD5(PathfileUA_Currently);
            string MD5_UA_LOC_Patched = Tool.CalculateMD5(PathfileUA_Patched);
            string MD5_UA_LOC_Original = Tool.CalculateMD5(PathfileUA_Original);

            // Check folder Metadata
            var cst_folder_metadata = Set_Metadata_Folder.Text;
            if (String.IsNullOrEmpty(cst_folder_metadata))
            {
                return "No MetaData folder found (1)";
            }
            if (!Directory.Exists(cst_folder_metadata))
            {
                return "No MetaData folder found (2)";
            }
            // Check file MetaData
            string PathfileMetadata_Currently = Path.Combine(cst_folder_metadata, "global-metadata.dat");
            string PathfileMetadata_Patched = Path.Combine(cst_folder_metadata, "global-metadata-patched.dat");
            string PathfileMetadata_Original = Path.Combine(cst_folder_metadata, "global-metadata-original.dat");
            // Get MD5 local Metadata (First time)
            string MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);
            string MD5_Metadata_LOC_Original = Tool.CalculateMD5(PathfileMetadata_Original);
            string MD5_Metadata_LOC_Patched = Tool.CalculateMD5(PathfileMetadata_Patched);

            // Two-method verification even when using offline mode

            // >> If UserAssembly is broken <<
            var download_ua = false;
            if (!File.Exists(PathfileUA_Currently))
            {
                // Check if found file original
                if (File.Exists(PathfileUA_Original))
                {
                    // Check if API Original same with Original LOC
                    if (MD5_UA_API_Original == MD5_UA_LOC_Original)
                    {
                        try
                        {
                            File.Copy(PathfileUA_Original, PathfileUA_Currently, true);
                            MD5_UA_LOC_Currently = Tool.CalculateMD5(PathfileUA_Currently);
                            Console.WriteLine("We copy PathfileUA_Original to PathfileUA_Currently (UserAssembly) (33)");
                        }
                        catch (Exception exx)
                        {
                            return "Error copy (1)";
                        }
                    }
                    else
                    {
                        Console.WriteLine("Download UserAssembly, because PathfileUA_Original with md5 " + MD5_UA_LOC_Original + " does not match " + MD5_UA_API_Original + " (5)");
                        download_ua = true;
                    }
                }
                else
                {
                    Console.WriteLine("Download UserAssembly, because file PathfileUA_Original was not found");
                    download_ua = true;
                }
            }
            else
            {
                // If file is found and original file doesn't match currently (Make sure current data is really original before patch)
                if (MD5_UA_API_Original != MD5_UA_LOC_Currently)
                {
                    // Check if found file original
                    if (File.Exists(PathfileUA_Original))
                    {
                        // Check if API Original same with Original LOC
                        if (MD5_UA_API_Original == MD5_UA_LOC_Original)
                        {
                            try
                            {
                                File.Copy(PathfileUA_Original, PathfileUA_Currently, true);
                                MD5_UA_LOC_Currently = Tool.CalculateMD5(PathfileUA_Currently);
                                Console.WriteLine("We copy PathfileUA_Original to PathfileUA_Currently (UserAssembly) (6)");
                            }
                            catch (Exception exx)
                            {
                                return "Error copy (2)";
                            }
                        }
                        else
                        {
                            // download if Original file unvaild
                            Console.WriteLine("Download UserAssembly in 'Currently', because 'PathfileUA_Original' it doesn't match " + MD5_UA_API_Original + " with " + MD5_UA_LOC_Original + " (7)");
                            download_ua = true;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Download UserAssembly in 'Currently' because file PathfileUA_Original not found and it doesn't match " + MD5_UA_API_Original + " with " + MD5_UA_LOC_Currently + " Currently file (8)");
                        download_ua = true;
                    }
                }
                else
                {
                    Console.WriteLine("Skip download UserAssembly, it's up-to-date (4)");
                }
            }
            // if need download ua
            if (download_ua)
            {
                try
                {
                    if (!patchit)
                    {
                        return "Unable to download files " + Original_file_UA + " in a closed game state, please try again or download manual and put it in " + PathfileUA_Currently;
                    }
                    var CEKDL1 = new Download(Original_file_UA, PathfileUA_Currently);
                    if (CEKDL1.ShowDialog() != DialogResult.OK)
                    {
                        return "Get Original UserAssembly failed";
                    }
                    else
                    {
                        MD5_UA_LOC_Currently = Tool.CalculateMD5(PathfileUA_Currently);
                        Console.WriteLine("Currently UserAssembly: " + MD5_UA_LOC_Currently);
                    }
                }
                catch (Exception exx)
                {
                    return "Error Get Original UserAssembly: " + exx.ToString();
                }
            }
            else
            {
                Console.WriteLine("Currently UserAssembly: " + MD5_UA_LOC_Currently);
            }

            // here current file should match so if original file is not found use current file to copy to original file
            if (!File.Exists(PathfileUA_Original))
            {
                try
                {
                    File.Copy(PathfileUA_Currently, PathfileUA_Original, true);
                    MD5_UA_LOC_Original = Tool.CalculateMD5(PathfileUA_Original);
                    Console.WriteLine("We copy file in PathfileUA_Currently to PathfileUA_Original files (22)");
                }
                catch (Exception exx)
                {
                    return "Error copy PathfileUA_Currently to PathfileUA_Original (1)";
                }
            }
            else
            {
                // if file found (skip)               
            }


            // >> If Metadata is broken <<
            var download_metadata = false;
            if (!File.Exists(PathfileMetadata_Currently))
            {
                // Check if found file original
                if (File.Exists(PathfileMetadata_Original))
                {
                    // Check if API Original same with Original LOC
                    if (MD5_Metadata_API_Original == MD5_Metadata_LOC_Original)
                    {
                        try
                        {
                            File.Copy(PathfileMetadata_Original, PathfileMetadata_Currently, true);
                            MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);
                            Console.WriteLine("We copy PathfileMetadata_Original to PathfileMetadata_Currently");
                        }
                        catch (Exception exx)
                        {
                            return "Error copy PathfileMetadata_Original to PathfileMetadata_Currently (111)";
                        }
                    }
                    else
                    {
                        // file not vaild so download
                        download_metadata = true;
                        Console.WriteLine("Download Metadata in Currently,because file Original with md5 " + MD5_Metadata_API_Original + " doesn't match " + MD5_Metadata_LOC_Original + " (5)");
                    }
                }
                else
                {
                    // file not found, so download
                    download_metadata = true;
                    Console.WriteLine("Download Metadata, because file PathfileMetadata_Original was not found");
                }
            }
            else
            {
                // If file is found and original file doesn't match currently (Make sure current data is really original before patch)
                if (MD5_Metadata_API_Original != MD5_Metadata_LOC_Currently)
                {
                    // Check if found file original
                    if (File.Exists(PathfileMetadata_Original))
                    {
                        // Check if API Original same with Original LOC
                        if (MD5_Metadata_API_Original == MD5_Metadata_LOC_Original)
                        {
                            try
                            {
                                File.Copy(PathfileMetadata_Original, PathfileMetadata_Currently, true);
                                MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);
                                Console.WriteLine("We copy file PathfileMetadata_Original to PathfileMetadata_Currently (6)");
                            }
                            catch (Exception exx)
                            {
                                return "Error copy PathfileMetadata_Original to PathfileMetadata_Currently (111)";
                            }
                        }
                        else
                        {
                            // file not vaild so download
                            download_metadata = true;
                            Console.WriteLine("Download Metadata in Currently, because PathfileMetadata_Original file does not match " + MD5_Metadata_API_Original + " with " + MD5_Metadata_LOC_Original + " (7)");
                        }
                    }
                    else
                    {
                        // file not found, so download
                        download_metadata = true;
                        Console.WriteLine("Download Metadata in Currently, because file PathfileMetadata_Original was not found (8)");
                    }
                }
                else
                {
                    Console.WriteLine("Skip download Metadata, it's up-to-date (4)");
                }
            }
            // if need download
            if (download_metadata)
            {
                try
                {
                    if (!patchit)
                    {
                        return "Unable to download files " + Original_file_MA + " in a closed game state, please try again or download manual and put it in " + PathfileMetadata_Currently;
                    }
                    var CEKDL2 = new Download(Original_file_MA, PathfileMetadata_Currently);
                    if (CEKDL2.ShowDialog() != DialogResult.OK)
                    {
                        return "Get Original Metadata failed";
                    }
                    else
                    {
                        MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);
                        Console.WriteLine("Currently Metadata: " + MD5_Metadata_LOC_Currently);
                    }
                }
                catch (Exception exx)
                {
                    return "Error Get Original Metadata: " + exx.ToString();
                }
            }
            else
            {
                Console.WriteLine("Currently Metadata: " + MD5_Metadata_LOC_Currently);
            }
            // here current file should match so if original file is not found use current file to copy to original file
            if (!File.Exists(PathfileMetadata_Original))
            {
                try
                {
                    File.Copy(PathfileMetadata_Currently, PathfileMetadata_Original, true);
                    MD5_Metadata_LOC_Original = Tool.CalculateMD5(PathfileMetadata_Original);
                    Console.WriteLine("We copy file in PathfileMetadata_Currently to PathfileMetadata_Original files (22)");
                }
                catch (Exception exx)
                {
                    return "Error copy PathfileMetadata_Currently to PathfileMetadata_Original (1)";
                }
            }
            else
            {
                // if file found (skip)
            }

            // >>> Final Tes <<<

            //UA
            MD5_UA_LOC_Original = Tool.CalculateMD5(PathfileUA_Original);
            MD5_UA_LOC_Currently = Tool.CalculateMD5(PathfileUA_Currently);
            if (MD5_UA_API_Original != MD5_UA_LOC_Original && MD5_UA_API_Original != MD5_UA_LOC_Currently)
            {
                try
                {
                    File.Delete(PathfileUA_Original);
                }
                catch (Exception exx)
                {
                    // skip
                }
                return MD5_UA_LOC_Original + " and " + MD5_UA_LOC_Currently + " value should be " + MD5_UA_API_Original + ", This might happen because file is corrupted, try again or manual update. (g1)";
            }
            // Metadata
            MD5_Metadata_LOC_Original = Tool.CalculateMD5(PathfileMetadata_Original);
            MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);
            if (MD5_Metadata_API_Original != MD5_Metadata_LOC_Original && MD5_Metadata_API_Original != MD5_Metadata_LOC_Currently)
            {
                try
                {
                    File.Delete(PathfileMetadata_Original);
                }
                catch (Exception exx)
                {
                    // skip
                }
                return MD5_Metadata_LOC_Original + " and " + MD5_Metadata_LOC_Currently + " value should be " + MD5_Metadata_API_Original + ", This might happen because file is corrupted, try again or manual update. (g1)";
            }

            // It should be here that all files already verified, unless you want to check other vaild files again.

            if (metode == 2)
            {
                // >> UA <<

                // debug
                //online = false;                

                if (online)
                {
                    // If original backup file is not found.
                    if (!File.Exists(PathfileUA_Original))
                    {
                        return "Why does this pass (1)";
                    }

                    // If current UA file doesn't exist use UserAssembly-Original.dll
                    if (!File.Exists(PathfileUA_Currently))
                    {
                        return "Why does this pass (2)";
                    }

                    if (MD5_UA_API_Original != MD5_UA_LOC_Currently)
                    {
                        return "Why does this pass (3)";
                    }
                    else
                    {
                        if (patchit)
                        {
                            // if current file is original and need patch

                            var download_patch = false;
                            if (!File.Exists(PathfileUA_Patched))
                            {
                                download_patch = true;
                            }
                            else
                            {
                                // If UA_API_Patches_MD5 (patch file from api) matches UA_LOC_Patched_MD5 (current patch file)
                                if (MD5_UA_API_Patched != MD5_UA_LOC_Patched)
                                {
                                    download_patch = true;
                                }
                            }

                            // If download_patch true, download it
                            if (download_patch)
                            {
                                var DL2 = new Download(DL_Patch + "UserAssembly-patched.dll", PathfileUA_Patched);
                                if (DL2.ShowDialog() != DialogResult.OK)
                                {
                                    return "No Found Patch file....";
                                }
                                else
                                {
                                    MD5_UA_LOC_Patched = Tool.CalculateMD5(PathfileUA_Patched);
                                }
                            }

                            if (MD5_UA_API_Patched != MD5_UA_LOC_Patched)
                            {
                                // Failed because file doesn't match from md5 api
                                return "(UA) Your version Game is not supported, or it needs latest update or file is corrupted.";
                            }
                            else
                            {
                                // Patch file                           
                                try
                                {
                                    File.Copy(PathfileUA_Patched, PathfileUA_Currently, true);
                                    MD5_UA_LOC_Currently = Tool.CalculateMD5(PathfileUA_Currently);

                                    Console.WriteLine("Patch PathfileUA_Patched to PathfileUA_Currently done");
                                    download_patch = false;
                                }
                                catch (Exception x)
                                {
                                    return "Failed Patch: " + x.ToString();
                                }
                            }
                        }
                        else
                        {
                            // No Patch
                            Console.WriteLine("Skip, because file is original (x1)");
                        }
                    }
                }
                else
                {
                    // Offline Mode UserAssembly
                    if (patchit)
                    {
                        // Use PathfileUA_Currently to backup PathfileUA_Original file, but since without md5 api we can't check is original or not.
                        if (!File.Exists(PathfileUA_Original))
                        {
                            return "Why does this pass (1-1)";
                        }

                        // Check PathfileMetadata_Currently if no found
                        if (!File.Exists(PathfileUA_Currently))
                        {
                            return "Why does this pass (1-2)";
                        }

                        // keep patch
                        var ManualUA = Game.Genshin.Patch.UserAssembly.Do(PathfileUA_Currently, PathfileUA_Patched, key_to_find, key_to_patch);
                        if (!String.IsNullOrEmpty(ManualUA))
                        {
                            return "Error Patch UserAssembly: " + ManualUA;
                        }

                        // If patch is successful
                        if (File.Exists(PathfileUA_Patched))
                        {
                            MD5_UA_LOC_Patched = Tool.CalculateMD5(PathfileUA_Patched);
                            try
                            {
                                File.Copy(PathfileUA_Patched, PathfileUA_Currently, true);
                                MD5_UA_LOC_Currently = Tool.CalculateMD5(PathfileUA_Currently);
                                Console.WriteLine("Patch UserAssembly...");
                            }
                            catch (Exception ex)
                            {
                                return "Failed copy PathfileUA_Patched to PathfileUA_Currently: " + ex.ToString();
                            }
                        }
                        else
                        {
                            return "Why does this pass (1-3)";
                        }
                    }
                    else
                    {
                        // No Patch
                        Console.WriteLine("Skip, because file is original (x2)");
                    }
                }

                Console.WriteLine("MD5 UA Currently: " + MD5_UA_LOC_Currently);
                Console.WriteLine("MD5 UA Original: " + MD5_UA_LOC_Original);
                Console.WriteLine("MD5 UA Patched: " + MD5_UA_LOC_Patched);
            }
            else if (metode == 1)
            {

                // debug
                //online = false;

                /*
                if (online)
                {
                    // If original backup file is not found, start backup process
                    if (!File.Exists(PathfileMetadata_Original))
                    {
                        // Check if MD5_Metadata_API_Original (original file from api) matches MD5_Metadata_LOC_Currently (file in current use)
                        if (MD5_Metadata_API_Original == MD5_Metadata_LOC_Currently)
                        {
                            try
                            {
                                File.Copy(PathfileMetadata_Currently, PathfileMetadata_Original, true);
                                MD5_Metadata_LOC_Original = Tool.CalculateMD5(PathfileMetadata_Original);

                                Console.WriteLine("Backup Metadata Original");
                            }
                            catch (Exception ex)
                            {
                                return "Failed: Backup Metadata Original1: " + ex.ToString();
                            }
                        }
                        else
                        {
                            // Download Original MetaData
                            var DL3 = new Download(Original_file_MA, PathfileMetadata_Original);
                            if (DL3.ShowDialog() != DialogResult.OK)
                            {
                                return "Original Backup failed because md5 doesn't match";
                            }
                            else
                            {
                                MD5_Metadata_LOC_Original = Tool.CalculateMD5(PathfileMetadata_Original);
                            }
                        }
                    }

                    // Jika file metadata sekarang tidak ada gunakan global-metadata-original.dat
                    if (!File.Exists(PathfileMetadata_Currently))
                    {
                        try
                        {
                            File.Copy(PathfileMetadata_Original, PathfileMetadata_Currently, true);
                            MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);

                            Console.WriteLine("Get Backup Original");
                        }
                        catch (Exception exx)
                        {
                            return "Error Get Backup Original: " + exx.ToString();
                        }
                    }

                    // Jik User tidak ingin patch kembalikan ke aslinya. (If MD5_Metadata_API_Original doesn't match MD5_Metadata_LOC_Currently)
                    if (!patchit)
                    {
                        if (MD5_Metadata_API_Original != MD5_Metadata_LOC_Currently)
                        {
                            try
                            {
                                File.Copy(PathfileMetadata_Original, PathfileMetadata_Currently, true);
                                MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);

                                Console.WriteLine(MD5_Metadata_API_Original + " doesn't match with " + MD5_Metadata_LOC_Currently + ", backup it....");
                            }
                            catch (Exception exx)
                            {
                                return "Failed: Backup Metadata Original: " + exx.ToString();
                            }
                        }
                        else
                        {
                            Console.WriteLine("Skip, it's up-to-date");
                        }
                    }
                    else if (MD5_Metadata_API_Patched != MD5_Metadata_LOC_Currently)
                    {
                        // Jika md5 patch tidak cocok dengan metadata sekarang

                        var download_patch = false;

                        // jIka file tidak ada
                        if (!File.Exists(PathfileMetadata_Patched))
                        {
                            download_patch = true;
                        }
                        else
                        {
                            // If Metadata_API_Patches_MD5 (patch file from api) matches Metadata_LOC_Patched_MD5 (current patch file)
                            if (MD5_Metadata_API_Patched != MD5_Metadata_LOC_Patched)
                            {
                                download_patch = true;
                            }
                        }

                        // re-download
                        if (download_patch)
                        {
                            // If you don't have PathfileMetadata_Patched, download it
                            var DL2 = new Download(DL_Patch + "global-metadata-patched.dat", PathfileMetadata_Patched);
                            if (DL2.ShowDialog() != DialogResult.OK)
                            {
                                // If PathfileMetadata_Patched (Patched file) doesn't exist
                                return "No Found Patch file....";
                            }
                            else
                            {
                                MD5_Metadata_LOC_Patched = Tool.CalculateMD5(PathfileMetadata_Patched);
                            }
                        }

                        if (MD5_Metadata_API_Patched != MD5_Metadata_LOC_Patched)
                        {
                            return "(MA) Your version Game is not supported, or it needs latest update or file is corrupted.";
                            // Failed because file doesn't match from md5 api
                        }
                        else
                        {
                            // Patch to PathfileMetadata_Now                            
                            try
                            {
                                File.Copy(PathfileMetadata_Patched, PathfileMetadata_Currently, true);
                                MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);

                                Console.WriteLine("Patch done...");
                                download_patch = false;
                            }
                            catch (Exception x)
                            {
                                return "Failed Patch: " + x.ToString();
                            }
                        }

                    }
                    else
                    {
                        // If Metadata_API_Patches_MD5 match Metadata_LOC_Now_MD5
                    }
                }
                else
                {
                    // Offline Mode Metadata
                    if (patchit)
                    {
                        // Backup PathfileMetadata_Original
                        if (!File.Exists(PathfileMetadata_Original))
                        {
                            try
                            {
                                if (!File.Exists(PathfileMetadata_Currently))
                                {
                                    return "Cannot find current metadata file in offline mode";
                                }

                                // Use PathfileMetadata_Now to backup PathfileMetadata_Original file, but since without md5 api we can't check is original or not.
                                File.Copy(PathfileMetadata_Currently, PathfileMetadata_Original, true);
                                MD5_Metadata_LOC_Original = Tool.CalculateMD5(PathfileMetadata_Original);

                                Console.WriteLine("Backup Metadata Original");
                            }
                            catch (Exception ex)
                            {
                                return ex.ToString();
                            }
                        }

                        // Check PathfileMetadata_Currently if no found
                        if (!File.Exists(PathfileMetadata_Currently))
                        {
                            if (File.Exists(PathfileMetadata_Original))
                            {
                                File.Copy(PathfileMetadata_Original, PathfileMetadata_Currently, true);
                                MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);

                                Console.WriteLine("We detected that you did not have files (Currently) so we returned them with Original");
                            }
                            else
                            {
                                return "Can't find Original Metadata file in offline mode (0)";
                            }
                        }
                        else
                        {
                            // jika PathfileMetadata_Currently ada coba cek PathfileMetadata_Original apakah sama
                            if (File.Exists(PathfileMetadata_Original))
                            {
                                MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);
                                MD5_Metadata_LOC_Original = Tool.CalculateMD5(PathfileMetadata_Original);
                                if (MD5_Metadata_LOC_Currently != MD5_Metadata_LOC_Original)
                                {
                                    File.Copy(PathfileMetadata_Original, PathfileMetadata_Currently, true);
                                    MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);

                                    Console.WriteLine("We detect you have non-original (Currently) files so we return them with Original");
                                }
                            }
                            else
                            {
                                return "Can't find Original Metadata file in offline mode (1)";
                            }
                        }

                        // Patch (Because we can't check md5 from api to check if this file is original or patched so to make it work we will always patch)
                        // TODO: Looking for another better method
                        var ManualMetadata = "???";
                        // Select CH
                        if (ch == 1)
                        {
                            ManualMetadata = Game.Genshin.Patch.Metadata.Do(PathfileMetadata_Currently, PathfileMetadata_Patched, "ORIKEY1", "PATCHKEY1", "ORIKEY2", "PATCHKEY2");
                        }
                        else if (ch == 2)
                        {
                            ManualMetadata = Game.Genshin.Patch.Metadata.Do(PathfileMetadata_Currently, PathfileMetadata_Patched, "ORIKEY1", "PATCHKEY1", "ORIKEY2", "PATCHKEY2");
                        }
                        if (!String.IsNullOrEmpty(ManualMetadata))
                        {
                            return "Error Patch: " + ManualMetadata;
                        }

                        // Patch PathfileMetadata_Patched ke PathfileMetadata_Currently
                        if (File.Exists(PathfileMetadata_Patched))
                        {
                            try
                            {
                                File.Copy(PathfileMetadata_Patched, PathfileMetadata_Currently, true);
                                MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);

                                Console.WriteLine("Patch Metadata...");
                            }
                            catch (Exception ex)
                            {
                                return "Failed Patch Metadata Original1: " + ex.ToString();
                            }
                        }
                        else
                        {
                            return "Failed Patch Metadata (0)";
                        }

                    }
                    else
                    {
                        // Jika PathfileMetadata_Original, Kembalikan PathfileMetadata_Original ke PathfileMetadata_Currently
                        if (File.Exists(PathfileMetadata_Original))
                        {
                            try
                            {
                                MD5_Metadata_LOC_Original = Tool.CalculateMD5(PathfileMetadata_Original);
                                MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);
                                if (MD5_Metadata_LOC_Original == MD5_Metadata_LOC_Currently)
                                {
                                    Console.WriteLine("Current file is Original");
                                }
                                else
                                {
                                    File.Copy(PathfileMetadata_Original, PathfileMetadata_Currently, true);
                                    MD5_Metadata_LOC_Currently = Tool.CalculateMD5(PathfileMetadata_Currently);
                                    Console.WriteLine("Back to Original Metadata Version...");
                                }
                            }
                            catch (Exception ex)
                            {
                                return "Failed: Backup Metadata Original2: " + ex.ToString();
                            }
                        }

                    }
                }

                // Make sure MD5 matches
                Console.WriteLine("MD5 Metadata Currently: " + MD5_Metadata_LOC_Currently);
                Console.WriteLine("MD5 Metadata Original: " + MD5_Metadata_LOC_Original);
                Console.WriteLine("MD5 Metadata Patched: " + MD5_Metadata_LOC_Patched);
                */

                return "I'm tired Checking Metadata";
            } else if(metode == 3) {

                var fileRSA = cst_folder_Game + "/version.dll";
                var fileRSA_BK = cst_folder_Game + "/version.bk";

                var fileRSA_Public = cst_folder_Game + "/PublicKey.txt";

                // TODO: ADD KEY SERVER
                File.WriteAllText(fileRSA_Public, "<RSAKeyValue><Modulus>xbbx2m1feHyrQ7jP+8mtDF/pyYLrJWKWAdEv3wZrOtjOZzeLGPzsmkcgncgoRhX4dT+1itSMR9j9m0/OwsH2UoF6U32LxCOQWQD1AMgIZjAkJeJvFTrtn8fMQ1701CkbaLTVIjRMlTw8kNXvNA/A9UatoiDmi4TFG6mrxTKZpIcTInvPEpkK2A7Qsp1E4skFK8jmysy7uRhMaYHtPTsBvxP0zn3lhKB3W+HTqpneewXWHjCDfL7Nbby91jbz5EKPZXWLuhXIvR1Cu4tiruorwXJxmXaP1HQZonytECNU/UOzP6GNLdq0eFDE4b04Wjp396551G99YiFP2nqHVJ5OMQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>");

                Console.WriteLine("Find file: "+ fileRSA);

                var dl_rsa = false;
                var RSAMD5 = "";

                if (patchit)
                {
                    if (File.Exists(fileRSA))
                    {
                        // check if source true
                        RSAMD5 = Tool.CalculateMD5(fileRSA);
                        if (RSAMD5 == MD5_API_Patched)
                        {
                            // if not found backup rsa key, copy dll to bk
                            if (!File.Exists(fileRSA_BK))
                            {
                                Console.WriteLine("No found backup file rsa key so copy");
                                File.Copy(fileRSA, fileRSA_BK, true);
                            }

                            Console.WriteLine("Skip download RSAKEY");
                        }
                        else
                        {
                            Console.WriteLine("file not same " + RSAMD5 + " download rsa key " + MD5_API_Patched);
                            dl_rsa = true;
                        }

                    }
                    else
                    {
                        if (File.Exists(fileRSA_BK))
                        {
                            if (RSAMD5 == MD5_API_Patched)
                            {
                                Console.WriteLine("found backup file rsa key so copy");
                                try
                                {
                                    File.Copy(fileRSA_BK, fileRSA, true);
                                }
                                catch  (Exception ex) {
                                    Console.WriteLine(ex.Message);
                                    return "error1";
                                }                                
                            }
                            else
                            {
                                Console.WriteLine("key rsa not same so just download it");
                                dl_rsa = true;
                            }
                        }
                        else
                        {
                            Console.WriteLine("No found file so just download it");
                            dl_rsa = true;
                        }
                    }
                }
                else
                {
                    // remove file
                    try
                    {
                        if (File.Exists(fileRSA))
                        {
                            Console.WriteLine("Remove file rsa key");
                            File.Delete(fileRSA);
                        }
                    }
                    catch (Exception ex) { 
                        Console.WriteLine(ex.Message);
                        return "error";
                    }
                }

                if (dl_rsa)
                {
                    var DL2 = new Download(DL_Patch+ "RSAPatch.dll", fileRSA);
                    if (DL2.ShowDialog() != DialogResult.OK)
                    {
                        return "No Found Patch file....";
                    }
                    else
                    {
                        RSAMD5 = Tool.CalculateMD5(fileRSA);
                    }
                }

                Console.WriteLine("RSA MD5: " + RSAMD5);
                //return "TODO: "+ DL_Patch;
            }
            else {
                return "No other method found";
            }

            if(metode != 3)
            {
                var fileRSA = cst_folder_Game + "/version.dll";
                if (File.Exists(fileRSA))
                {
                    try
                    {
                        Console.WriteLine("remove file rsa key (2)");
                        File.Delete(fileRSA);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            return "";
        }

        private void Set_LA_Select_Click(object sender, EventArgs e)
        {
            var Folder_Game_Now = SelectGamePath();
            if (!string.IsNullOrEmpty(Folder_Game_Now))
            {
                Set_LA_GameFolder.Text = Folder_Game_Now;
                CheckVersionGame();// TODO: hapus nanti
            }
            else
            {
                MessageBox.Show("No game folder found");
            }
        }

        public void GetServerList()
        {
            var GetDataServerList = API.ServerList();
            if (GetDataServerList == null)
            {
                MessageBox.Show("Error get server list");
                return;
            }

            ListServer = GetDataServerList.list;

            ServerList.BeginUpdate();
            ServerList.Items.Clear();

            for (int i = 0; i < ListServer.Count; i++)
            {
                ListViewItem lvi = new ListViewItem();
                lvi.Text = ListServer[i].name;
                lvi.SubItems.Add(ListServer[i].host);
                lvi.SubItems.Add("N/A");
                lvi.SubItems.Add("N/A");
                lvi.SubItems.Add("N/A");
                ServerList.Items.Add(lvi);
            }

            ServerList.EndUpdate();
        }

        public void UpdateServerListTimer()
        {
            Debug.Print("Start update..");
            if (thServerList != null)
            {
                thServerList.Interrupt();
            }
            thServerList = new Thread(() =>
            {
                if (ListServer == null)
                {
                    return;
                }
                for (int i = 0; i < ListServer.Count; i++)
                {
                    int s = i;
                    new Thread(() =>
                    {
                        var host = ListServer[s].host;
                        var ishttps = ListServer[s].https;
                        try
                        {
                            if (host == "official")
                            {
                                return;
                            }

                            string url_server_api = (ishttps ? "https" : "http") + "://" + host + "/status/server";
                            Debug.Print("Start update.. " + url_server_api);
                            VersionServer? ig = API.GetServerStatus(url_server_api);
                            ServerList.Invoke((Action)delegate
                            {
                                if (ig != null)
                                {
                                    ServerList.Items[s].SubItems[2].Text = ig.status.playerCount.ToString();
                                    ServerList.Items[s].SubItems[3].Text = ig.status.Version.ToString();
                                    if (ig.status.runMode != "HYBRID")
                                    {
                                        ServerList.Items[s].SubItems[2].Text = ig.status.runMode;
                                    }
                                }
                                else
                                {
                                    ServerList.Items[s].SubItems[2].Text = "N/A";
                                    ServerList.Items[s].SubItems[3].Text = "N/A";
                                }
                                try
                                {
                                    PingReply reply = new Ping().Send(host, 1000);
                                    if (reply.Status == IPStatus.Success)
                                        ServerList.Items[s].SubItems[4].Text = reply.RoundtripTime + "ms";
                                }
                                catch
                                {
                                    ServerList.Items[s].SubItems[4].Text = "N/A";
                                }
                            });
                        }
                        catch (Exception e)
                        {
                            Debug.Print("Error Host " + host + "" + e.Message);
                        }
                        finally
                        {
                            //
                        }
                    }).Start();
                }
            });
            thServerList.Start();
        }

        public void CheckUpdate()
        {
            Console.WriteLine("Cek update...");
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            string ver = "";
            if (version != null)
            {
                ver = version.ToString();
                Set_Version.Text = "Version: " + ver;
                var GetDataUpdate = API.GetUpdate();
                if (GetDataUpdate != null)
                {
                    var judul = GetDataUpdate.name; // is dev or nightly or name update
                    var name_version = GetDataUpdate.tag_name; // version name
                    var infobody = GetDataUpdate.body; // info

                    var version1 = new Version(name_version);
                    var version2 = new Version(ver);

                    var result = version1.CompareTo(version2);

                    if (result > 0)
                    {
                        // versi 1 lebih besar
                        Set_Version.Text = "Version: " + ver + " (New Update: " + name_version + " )";
                        var tes = MessageBox.Show(infobody, judul, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (tes == DialogResult.Yes)
                        {
                            var url_dl = "";
                            foreach (var file in GetDataUpdate.assets)
                            {
                                if (file.name == "YuukiPSLauncherPC.zip" || file.name == "YuukiPS.zip" || file.name == "update.zip")
                                {
                                    url_dl = file.browser_download_url;
                                    break;
                                }
                            }
                            if (!string.IsNullOrEmpty(url_dl))
                            {
                                var DL1 = new Download(url_dl, CurrentlyPath + @"\update.zip");
                                if (DL1.ShowDialog() == DialogResult.OK)
                                {
                                    // update
                                    var file_update = CurrentlyPath + @"\update.bat";
                                    try
                                    {
                                        //buat bat
                                        var w = new StreamWriter(file_update);
                                        w.WriteLine("@echo off");

                                        //kill all
                                        w.WriteLine("Taskkill /IM YuukiPS.exe /F");
                                        //w.WriteLine("Taskkill /IM YuukiPS.vshost.exe /F");

                                        // Unzip file
                                        w.WriteLine("echo unzip file...");
                                        w.WriteLine("tar -xf update.zip");

                                        //delete file old
                                        w.WriteLine("echo delete file old");
                                        w.WriteLine("del update.zip");

                                        //start bot
                                        w.WriteLine("echo Update done, start back...");
                                        w.WriteLine("timeout 5 > NUL");
                                        w.WriteLine("start YuukiPS.exe");
                                        w.WriteLine("del Update.bat");
                                        w.Close();
                                        //open bat
                                        Process.Start(file_update);
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show(ex.Message);
                                    }
                                }
                            }
                            else
                            {
                                // update batal
                            }

                        }
                    }
                    else if (result < 0)
                    {
                        Set_Version.Text = "Version: " + ver + " (latest nightly) (Official: " + name_version + ")";
                    }
                    else
                    {
                        Set_Version.Text = "Version: " + ver + " (latest public)";
                    }
                }
            }
            else
            {
                Set_Version.Text = "Version: Unknown";
            }
        }

        // Check Launcher
        private static string GetLauncherPath(String version = "Genshin Impact")
        {
            RegistryKey key = Registry.LocalMachine;
            if (key != null)
            {
                var tes1 = key.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + version);
                if (tes1 != null)
                {
                    var testes1 = tes1.GetValue("InstallPath");
                    if (testes1 != null)
                    {
                        var testestestes1 = testes1.ToString();
                        if (testestestes1 != null)
                        {
                            return testestestes1;
                        }

                    }
                }
            }
            return "";
        }

        // Check Game Install
        private static string GetGamePath(String launcherpath = "")
        {
            string startpath = "";

            if (launcherpath == "")
            {
                return "";
            }

            string cfgPath = Path.Combine(launcherpath, "config.ini");
            if (File.Exists(launcherpath) || File.Exists(cfgPath))
            {
                // baca file config
                using (StreamReader reader = new StreamReader(cfgPath))
                {
                    string[] abc = reader.ReadToEnd().Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    foreach (var item in abc)
                    {
                        // cari line install patch
                        if (item.IndexOf("game_install_path") != -1)
                        {
                            startpath += item.Substring(item.IndexOf("=") + 1);
                        }
                    }
                }
            }

            return startpath;
        }

        // Pilih Folder
        private static string SelectGamePath()
        {
            string foldPath = "";
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "Select Game Folder";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                foldPath = dialog.SelectedPath;
            }
            return foldPath;
        }

        [Obsolete]
        private void btStart_Click(object sender, EventArgs e)
        {
            // Jika game berjalan...
            if (IsGameRun)
            {
                AllStop();
                return;
            }

            // Get Extra
            bool isAkebiGC = Extra_AkebiGC.Checked;
            bool isProxyNeed = CheckProxyEnable.Checked;

            // Get Host
            string set_server_host = GetHost.Text;
            if (string.IsNullOrEmpty(set_server_host))
            {
                MessageBox.Show("Please select a server first, you can click on one in server list");
                return;
            }

            // Get Proxy
            int set_proxy_port = int.Parse(GetPort.Text);
            bool set_server_https = CheckProxyUseHTTPS.Checked;

            // Get Game
            var cst_gamefile = Set_LA_GameFile.Text;
            if (String.IsNullOrEmpty(cst_gamefile))
            {
                MessageBox.Show("No game file config found");
                return;
            }
            if (!File.Exists(cst_gamefile))
            {
                MessageBox.Show("Please find game install folder!");
                return;
            }

            bool patch = true;

            // Check progress
            if (!IsGameRun)
            {
                // if game is not running
                if (progress != null)
                {
                    Console.WriteLine("progress tes");
                }

                // if server is official 
                if (set_server_host == "official")
                {
                    patch = false;
                }

                // run patch
                var tes = PatchGame(patch, checkModeOnline.Checked, GameMetode, GameChannel);
                if (!string.IsNullOrEmpty(tes))
                {
                    if (tes.Contains("Key1") || tes.Contains("Key2"))
                    {
                        MessageBox.Show("This may happen because you have already patched or you are using an unsupported version game. The solution is you can use Online Method (you can find it in Config Tab) to make sure you have right file.", "Error Patch Offline");
                    }
                    else if (tes.Contains("corrupted"))
                    {
                        MessageBox.Show("Looks like you're using an unsupported version, try updating game data to latest version", "Game Version not supported (Online Mode)");
                        Process.Start(new ProcessStartInfo("https://ps.yuuki.me/genshin") { UseShellExecute = true });
                    }
                    else
                    {
                        MessageBox.Show(tes, "Error Patch");
                    }
                    return;
                }
            }

            // For Proxy
            if (proxy == null)
            {
                // skip proxy if official server
                if (set_server_host != "official")
                {
                    if (isProxyNeed)
                    {
                        proxy = new Proxy(set_proxy_port, set_server_host, set_server_https);
                        if (!proxy.Start())
                        {
                            MessageBox.Show("Maybe port is already use or Windows Firewall does not allow using port " + set_proxy_port + " or Windows Update sometimes takes that range", "Failed Start...");
                            proxy = null;
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Proxy is ignored, because you turned it off");
                    }
                }
                else
                {
                    Console.WriteLine("Proxy is ignored, because use official server");
                }
            }
            else
            {
                Console.WriteLine("Proxy is still running...");
            }

            if (isAkebiGC)
            {
                var set_AkebiGC = Path.Combine(Modfolder, "AkebiGC");
                Directory.CreateDirectory(set_AkebiGC);
                string get_AkebiGC = Path.Combine(set_AkebiGC, "injector.exe");
                string get_AkebiGC_zip = Path.Combine(set_AkebiGC, "update.zip");
                string get_AkebiGC_md5 = Path.Combine(set_AkebiGC, "md5.txt");

                var Update_AkebiGC = false;

                var version_akebi = "";
                var url_download = "";

                var cekAkebi = API.GetAkebi(GameChannel, VersionGame);
                if (string.IsNullOrEmpty(cekAkebi))
                {
                    MessageBox.Show("No update for this version so far or error check version, check console");
                    //return;
                }
                else
                {
                    string[] SplitAkebiGC = cekAkebi.Split("|");
                    version_akebi = SplitAkebiGC[0];
                    url_download = SplitAkebiGC[1];
                }                

                // Check file update, jika tidak ada
                if (!File.Exists(get_AkebiGC_md5))
                {
                    Console.WriteLine("Akebi file md5 not found");
                    if (string.IsNullOrEmpty(cekAkebi))
                    {
                        return;
                    }
                    Console.WriteLine("MD5 no found, update!!!");
                    Update_AkebiGC = true;
                }
                else
                {
                    if (!string.IsNullOrEmpty(version_akebi))
                    {
                        string readText = File.ReadAllText(get_AkebiGC_md5);
                        if (!readText.Contains(version_akebi))
                        {
                            Console.WriteLine("Found a new version, time to download");
                            Update_AkebiGC = true;
                        }
                    }
                    else
                    {
                        Console.WriteLine("No new Akebi found: "+ cekAkebi);
                    }
                }

                if (Update_AkebiGC)//!File.Exists(get_AkebiGC)
                {
                    var DL2 = new Download(url_download, get_AkebiGC_zip);
                    if (DL2.ShowDialog() != DialogResult.OK)
                    {
                        MessageBox.Show("Download Akebi failed");
                        return;
                    }
                    else
                    {
                        // if download done....
                        var file_update_AkebiGC = set_AkebiGC + @"\update.bat";
                        try
                        {
                            // Make bat file for update
                            var w = new StreamWriter(file_update_AkebiGC);
                            w.WriteLine("@echo off");

                            w.WriteLine("cd \"" + set_AkebiGC + "\" ");

                            // Kill Akebi
                            w.WriteLine("Taskkill /IM injector.exe /F");

                            // Unzip file
                            w.WriteLine("echo unzip file...");
                            w.WriteLine("tar -xvf update.zip");

                            //delete file old
                            w.WriteLine("echo delete file zip");
                            w.WriteLine("del /F update.zip");

                            // del update
                            w.WriteLine("del /F update.bat");
                            w.Close();

                            //open bat
                            //Process.Start(file_update_AkebiGC);

                            ProcessStartInfo pInfo = new ProcessStartInfo();
                            pInfo.FileName = file_update_AkebiGC;
                            Process p = Process.Start(pInfo);

                            p.WaitForExit();

                            // Update MD5
                            File.WriteAllText(get_AkebiGC_md5, version_akebi);

                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                            return;
                        }
                    }

                }

                //Update Path
                var file_config_AkebiGC = set_AkebiGC + @"\cfg.ini";
                try
                {
                    var w = new StreamWriter(file_config_AkebiGC);
                    w.WriteLine("[Inject]");
                    w.WriteLine("GenshinPath = " + cst_gamefile);
                    w.WriteLine("[System]");
                    w.WriteLine("InitializationDelayMS = 25000");
                    w.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return;
                }
                cst_gamefile = get_AkebiGC;
                Console.WriteLine("RUN: " + cst_gamefile);
            }

            // For Game
            if (progress == null)
            {
                progress = new Process();
                progress.StartInfo = new ProcessStartInfo
                {
                    FileName = cst_gamefile
                };
                try
                {
                    progress.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                    AllStop();
                }
            }
            else
            {
                Console.WriteLine("Progress is still running...");
            }

        }

        [Obsolete]
        private void CheckGameRun_Tick(object sender, EventArgs e)
        {
            var isrun = Process.GetProcesses().Where(pr => pr.ProcessName == "YuanShen" || pr.ProcessName == "GenshinImpact" || pr.ProcessName == "injector");
            if (!isrun.Any())
            {
                // Jika Game tidak berjalan....
                IsGameRun = false;
                btStart.Text = "Launch";

                AllStop();

                // Revert to original version every game close
                if (!DoneCheck)
                {
                    Console.WriteLine("Game detected stopped");
                    StopGame(); // this shouldn't be necessary but just let it be
                    var tes = PatchGame(false, checkModeOnline.Checked, GameMetode, GameChannel);
                    if (!string.IsNullOrEmpty(tes))
                    {
                        Console.WriteLine(tes);
                    }
                    DoneCheck = true;

                    if (Config_Discord_Enable.Checked)
                    {
                        discord.UpdateStatus("Not playing", "Stop", "sleep");
                    }
                }
            }
            else
            {
                // jika game jalan
                IsGameRun = true;
                btStart.Text = "Stop";
                DoneCheck = false;

                if (Config_Discord_Enable.Checked)
                {
                    discord.UpdateStatus($"Server: {HostName} Version: {VersionGame}", "In Game", "on", 1);
                }

                // Check Gay
                try
                {
                    if (Extra_AkebiGC.Checked == true)
                    {
                        Mem mem = new Mem();
                        if (mem.OpenProcess(WatchFile))
                        {
                            if (VersionGame == "3.4.0")
                            {
                                int value = mem.ReadInt("CLibrary.dll+395F68");
                                if (value != 256)
                                {
                                    mem.WriteMemory("CLibrary.dll+395F68", "int", "256");
                                    Console.WriteLine("Bruh, why would anyone like gay?");
                                }
                            }
                            else if (VersionGame == "3.6.0")
                            {
                                int value = mem.ReadInt("mhyprot.dll+377050");
                                if (value != 7)
                                {
                                    mem.WriteMemory("mhyprot.dll+377050", "int", "7");
                                    Console.WriteLine("Do you like Yuri Yuri? (1)");
                                }
                                int value1 = mem.ReadInt("mhyprot.dll+377064");
                                if (value1 != 7)
                                {
                                    mem.WriteMemory("mhyprot.dll+377064", "int", "7");
                                    Console.WriteLine("Do you like Yuri Yuri? (2)");
                                }
                            }
                            else
                            {
                                // skip
                            }                            
                        }
                        else
                        {
                            Console.WriteLine("Make sure you enable 'Disable Protection' in cheat settings");
                        }                        
                    }
                    else
                    {
                        // skip
                    }
                }
                catch (Exception ex)
                {
                    // I don't care
                    Debug.WriteLine(ex);
                }

            }
        }

        [Obsolete]
        public void AllStop()
        {
            StopProxy();
            StopGame();
        }

        [Obsolete]
        public void StopProxy()
        {
            if (proxy != null)
            {
                proxy.Stop();
                proxy = null;
                Console.WriteLine("Proxy Stop....");
            }
        }

        public void StopGame()
        {
            if (progress != null)
            {
                try
                {
                    // Normal Kill
                    progress.Kill();
                    progress.Close();
                    progress.Dispose();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                progress = null;
            }
            if (IsGameRun)
            {
                try
                {
                    // Foce Kill
                    if (!string.IsNullOrEmpty(WatchFile))
                    {
                        Tool.EndTask(WatchFile);
                        Console.WriteLine("EndTask Game: " + WatchFile);
                    }
                    else
                    {
                        Console.WriteLine("EndTask not supported");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        private void btReloadServer_Click(object sender, EventArgs e)
        {
            GetServerList();
        }

        private void ServerList_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var item = ServerList.GetItemAt(e.X, e.Y);
            if (item == null)
            {
                return;
            }
            var g = ListServer[item.Index];
            if (g != null)
            {
                GetHost.Text = g.host;
                CheckProxyUseHTTPS.Checked = g.https;
                HostName = g.name;
            }
        }

        private void linkDiscord_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("http://discord.yuuki.me/") { UseShellExecute = true });
        }

        private void linkGithub_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/YuukiPS/Launcher-PC") { UseShellExecute = true });
        }

        private void linkWeb_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://ps.yuuki.me/") { UseShellExecute = true });
        }

        private void CekUpdateTT_Tick(object sender, EventArgs e)
        {
            if (Is_ServerList_Autocheck.Checked)
            {
                UpdateServerListTimer();
            }
        }

        [Obsolete]
        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (IsGameRun)
            {
                MessageBox.Show("Can't close program while game is still running.");
                e.Cancel = true;
            }
        }

        private void Set_LA_Save_Click(object sender, EventArgs e)
        {
            SaveConfig();
        }

        [Obsolete]
        private void CheckProxyRun_Tick(object sender, EventArgs e)
        {
            CheckProxy(false);
        }

        [Obsolete]
        void CheckProxy(bool force_off = false)
        {
            try
            {
                // Metode 1
                RegistryKey? registry = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", true);

                if (registry != null)
                {
                    object? v = registry.GetValue("ProxyEnable");
                    if (v != null && (int)v == 1)
                    {
                        // Metode 2
                        if (proxy != null)
                        {
                            stIsRunProxy.Text = "Status: ON (Internal)";
                        }
                        else
                        {
                            stIsRunProxy.Text = "Status: ON (External)";
                            // If external is on and proxy app is enabled, make sure external proxy is off
                            if (CheckProxyEnable.Checked)
                            {
                                force_off = true;
                            }
                        }

                        if (force_off)
                        {
                            registry.SetValue("ProxyEnable", 0);
                        }

                    }
                    else
                    {
                        StopProxy();
                        stIsRunProxy.Text = "Status: OFF";
                    }
                }
                else
                {
                    Console.WriteLine("Does not support proxy check!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private void DEV_UA_bt_Selectfile_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Select file metadata...";
            dialog.DefaultExt = "dat";
            dialog.Multiselect = false;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                DEV_MA_get_file.Text = dialog.FileName;
            }
        }

        private void Server_Config_OpenFolder_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = Server.Serverfolder,
                UseShellExecute = true,
                Verb = "open"
            });
        }

        private void Server_Start_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Still PR :)");
        }

        private void Server_DL_JAVA_Click(object sender, EventArgs e)
        {
            var dl_java = Server.DLJava();
            if (!string.IsNullOrEmpty(dl_java))
            {
                MessageBox.Show(dl_java);
            }
            else
            {
                MessageBox.Show("Download is successful");
            }
        }

        private void Server_DL_DB_Click(object sender, EventArgs e)
        {

        }



        private void Server_DL_GC_Click(object sender, EventArgs e)
        {

        }

        private void bt_GetKey_Click(object sender, EventArgs e)
        {
            MessageBox.Show(UpdateKey());
        }

        public string UpdateKey()
        {
            if (get_version == null)
            {
                return "Maybe your internet has problems or there is an active proxy";
            }
            else
            {
                //MA
                DEV_MA_Set_Key1_NoPatch.Text = "";
                DEV_MA_Set_Key2_NoPatch.Text = "";
                DEV_MA_Set_Key1_Patch.Text = "";
                DEV_MA_Set_Key2_Patch.Text = "";

                //UA
                DEV_UA_Set_Key1_NoPatch.Text = get_version.original.key_find.os;
                DEV_UA_Set_Key2_Patch.Text = get_version.patched.key_patch;
            }
            return "Successfully got Key, you can see update in developer tab";
        }

        private void DEV_UA_bt_Selectfile_Click_1(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Select File UserAssembly...";
            dialog.DefaultExt = "dll";
            dialog.Multiselect = false;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                DEV_UA_get_file.Text = dialog.FileName;
            }
        }

        private void DEV_UA_bt_Patch_Click_1(object sender, EventArgs e)
        {
            // Check Folder Game
            var DEV_UA_file = DEV_UA_get_file.Text;
            if (String.IsNullOrEmpty(DEV_UA_file))
            {
                MessageBox.Show("No UA found (1)");
                return;
            }
            if (!File.Exists(DEV_UA_file))
            {
                MessageBox.Show("No file UA found (2)");
                return;
            }
            var DEV_UA_KEY1_NOPATCH = DEV_UA_Set_Key1_NoPatch.Text;
            var DEV_UA_KEY2_PATCH = DEV_UA_Set_Key2_Patch.Text;

            var IsPatchOK = Game.Genshin.Patch.UserAssembly.Do(DEV_UA_file, DEV_UA_file + ".patch", DEV_UA_KEY1_NOPATCH, DEV_UA_KEY2_PATCH);
            if (!string.IsNullOrEmpty(IsPatchOK))
            {
                MessageBox.Show(IsPatchOK);
            }
            else
            {
                MessageBox.Show("Patching is successful");
            }
        }

        private void DEV_MA_bt_Patch_Click(object sender, EventArgs e)
        {
            // Check Folder Game
            var DEV_metadata_file = DEV_MA_get_file.Text;
            if (String.IsNullOrEmpty(DEV_metadata_file))
            {
                MessageBox.Show("No metadata found (1)");
                return;
            }
            if (!File.Exists(DEV_metadata_file))
            {
                MessageBox.Show("No file metadata found (2)");
                return;
            }
            var DEV_MA_KEY1_NOPATCH = DEV_MA_Set_Key1_NoPatch.Text;
            var DEV_MA_KEY1_PATCH = DEV_MA_Set_Key1_Patch.Text;
            var DEV_MA_KEY2_NOPATCH = DEV_MA_Set_Key2_NoPatch.Text;
            var DEV_MA_KEY2_PATCH = DEV_MA_Set_Key2_Patch.Text;

            var IsPatchOK = Game.Genshin.Patch.Metadata.Do(DEV_metadata_file, DEV_metadata_file + ".patch", DEV_MA_KEY1_NOPATCH, DEV_MA_KEY1_PATCH, DEV_MA_KEY2_NOPATCH, DEV_MA_KEY2_PATCH);
            if (!string.IsNullOrEmpty(IsPatchOK))
            {
                MessageBox.Show(IsPatchOK);
            }
            else
            {
                MessageBox.Show("Patching is successful");
            }
        }

        private void DEV_MA_bt_Decrypt_Click(object sender, EventArgs e)
        {
            var DEV_metadata_file = DEV_MA_get_file.Text;
            if (String.IsNullOrEmpty(DEV_metadata_file))
            {
                MessageBox.Show("No metadata found (1)");
                return;
            }
            if (!File.Exists(DEV_metadata_file))
            {
                MessageBox.Show("No file metadata found (2)");
                return;
            }
            var IsPatchOK = Game.Genshin.Patch.Metadata.Decrypt(DEV_metadata_file, DEV_metadata_file + ".dec");
            if (!string.IsNullOrEmpty(IsPatchOK))
            {
                MessageBox.Show(IsPatchOK);
            }
            else
            {
                MessageBox.Show("Decrypt is successful");
            }
        }
    }
}