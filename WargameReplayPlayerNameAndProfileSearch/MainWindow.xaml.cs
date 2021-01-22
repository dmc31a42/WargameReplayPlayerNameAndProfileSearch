using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WargameReplayPlayerNameAndProfileSearch
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<ObservableReplay> replayDataGridCollection = new ObservableCollection<ObservableReplay>();

        static public Dictionary<Int64, string> FindPersonanameFromSteamID64 = new Dictionary<long, string>();

        public class ObservableReplay
        {
            public DateTime Date { get; set; }
            public string PlayerName { get; set; }
            public string PlayerUserId { get; set; }
            public string Profile { get; set; }
            public string PlayerDeckName { get; set; }
            public string Players { get; set; }
            public string Map { get; set; }
            public string PlayerDeckContent { get; set; }

            protected static string api = "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/";
            protected static string key = "CC120FF043A9145840B78C115988F586";
            public ObservableReplay(ReplayInfo replayInfo, PlayerInfo playerInfo)
            {
                Date = replayInfo.dateTime;
                PlayerName = playerInfo.PlayerName;
                PlayerUserId = playerInfo.PlayerUserId.ToString() ;
                if (FindPersonanameFromSteamID64.ContainsKey(playerInfo.Profile))
                {
                    Profile = "[" + FindPersonanameFromSteamID64[playerInfo.Profile] + "] " + playerInfo.Profile.ToString();
                } else
                {
                    string tempPersonaName = "";
                    try
                    {
                        string rt;
                        WebRequest request = WebRequest.Create(api + "?key=" + key + "&steamids=" + playerInfo.Profile);
                        WebResponse response = request.GetResponse();
                        Stream dataStream = response.GetResponseStream();
                        StreamReader reader = new StreamReader(dataStream);
                        rt = reader.ReadToEnd();
                        reader.Close();
                        response.Close();
                        JObject PlayerSummariesJson = JObject.Parse(rt);
                        tempPersonaName = (string)PlayerSummariesJson["response"]["players"][0]["personaname"];
                        FindPersonanameFromSteamID64[playerInfo.Profile] = tempPersonaName;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                    Profile = "[" + tempPersonaName + "] " + playerInfo.Profile.ToString();
                }
                
                PlayerDeckName = playerInfo.PlayerDeckName;
                List<string> playerArray = replayInfo.playerInfos.ConvertAll(x => x.PlayerName);
                Players = string.Join(", ", playerArray.ToArray());
                Map = replayInfo.GameInfo.Map;
                PlayerDeckContent = playerInfo.PlayerDeckContent;
            }
        }
        public class GameInfo
        {
            public string Map;
            public GameInfo(JToken jToken)
            {
                if((int)jToken["IsNetworkMode"] != 1)
                {
                    throw new Exception("IsNetworkMode is not 1");
                }
                this.Map = (string)jToken["Map"];
            }
        }
        public class PlayerInfo
        {
            public int PlayerUserId;
            public string PlayerName;
            public Int64 Profile;
            public string PlayerDeckName;
            public string PlayerDeckContent;
            public PlayerInfo(JToken jToken)
            {
                PlayerUserId = (int)jToken["PlayerUserId"];
                PlayerName = (string)jToken["PlayerName"];
                Profile = Int64.Parse(((string)jToken["PlayerAvatar"]).Replace("VirtualData/GamerPicture/",""));
                PlayerDeckName = (string)jToken["PlayerDeckName"];
                PlayerDeckContent = (string)jToken["PlayerDeckContent"];
            }
        }
        public class ReplayInfo
        {
            public GameInfo GameInfo;
            public List<PlayerInfo> playerInfos = new List<PlayerInfo>();
            public DateTime dateTime;
            public JObject json;
            public ReplayInfo(JObject jObject, DateTime dateTime)
            {
                this.json = jObject;
                this.dateTime = dateTime;
                foreach(JProperty prop in jObject.Properties())
                {
                    if(prop.Name == "game")
                    {
                        this.GameInfo = new GameInfo(jObject[prop.Name]);
                    } else
                    {
                        playerInfos.Add(new PlayerInfo(jObject[prop.Name]));
                    }
                }
                
            }
        }

        List<ReplayInfo> replayInfos = new List<ReplayInfo>();
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            List<ReplayInfo> tempReplayInfos = new List<ReplayInfo>();
            var dialog = new CommonOpenFileDialog
            {
                EnsurePathExists = true,
                EnsureFileExists = false,
                AllowNonFileSystemItems = false,
                IsFolderPicker = true,
                Title = "Select Wargame Replay Folder"
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string dirToProcess = Directory.Exists(dialog.FileName) ? dialog.FileName : System.IO.Path.GetDirectoryName(dialog.FileName);
                DirectoryTextBox.Text = dirToProcess;
                System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(DirectoryTextBox.Text);

                foreach (var fileInfo in di.GetFiles("*.wargamerpl2"))
                {
                    using (FileStream replayFileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read))
                    {
                        byte[] JsonSizeBuffer = new byte[4];
                        replayFileStream.Seek(0x00000030, SeekOrigin.Begin);
                        replayFileStream.Read(JsonSizeBuffer, 0, 4);
                        int JsonSize = BitConverter.ToInt32(JsonSizeBuffer.Reverse().ToArray(), 0);
                        byte[] JsonBuffer = new byte[JsonSize];
                        replayFileStream.Seek(0x00000038, SeekOrigin.Begin);
                        replayFileStream.Read(JsonBuffer, 0, JsonSize);
                        string JsonStr = Encoding.UTF8.GetString(JsonBuffer, 0, JsonBuffer.Length);
                        JObject json = JObject.Parse(JsonStr);
                        //replayJsonList.Add(json);
                        try {
                            tempReplayInfos.Add(new ReplayInfo(json, fileInfo.LastWriteTime));
                        } catch (Exception exception)
                        {
                            Console.WriteLine(exception);
                        }
                        
                    }
                }
                replayInfos = tempReplayInfos;
            }
                
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            FindFromPlayerName();
        }

        private void PlayerNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FindFromPlayerName();
            }
        }

        private void FindFromPlayerName()
        {
            ObservableCollection<ObservableReplay> tempReplayDataGridCollection = new ObservableCollection<ObservableReplay>();
            string playerNameStr = PlayerNameTextBox.Text;
            string[] playerNames = playerNameStr.Split(',');
            List<ReplayInfo> SamePlayernameReplayinfos = new List<ReplayInfo>();
            foreach (string playerName in playerNames)
            {
                if(!string.IsNullOrWhiteSpace(playerName))
                {
                    foreach (ReplayInfo replayInfo in replayInfos)
                    {
                        int index = replayInfo.playerInfos.FindIndex(playerinfo => playerinfo.PlayerName.ToLower().Contains(playerName.ToLower()));
                        if (index != -1)
                        {
                            tempReplayDataGridCollection.Add(new ObservableReplay(replayInfo, replayInfo.playerInfos[index]));
                        }
                    }
                }
            }
            //replayDataGridCollection = tempReplayDataGridCollection;
            ReplayDataGrid.ItemsSource = tempReplayDataGridCollection;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            FindFromPlayerUserId();
        }

        private void PlayerUserIdTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FindFromPlayerUserId();
            }
        }

        private void FindFromProfile()
        {
            ObservableCollection<ObservableReplay> tempReplayDataGridCollection = new ObservableCollection<ObservableReplay>();
            string ProfileStr = ProfileTextBox.Text;
            string[] ProfileStrs = ProfileStr.Split(',');
            List<Int64> Profiles = new List<long>();
            foreach (string profile in ProfileStrs)
            {
                Int64 temp;
                if(Int64.TryParse(profile, out temp))
                {
                    Profiles.Add(temp);
                }
            }
            List<ReplayInfo> SamePlayernameReplayinfos = new List<ReplayInfo>();
            foreach (Int64 Profile in Profiles)
            {
                foreach (ReplayInfo replayInfo in replayInfos)
                {
                    int index = replayInfo.playerInfos.FindIndex(playerinfo => playerinfo.Profile == Profile);
                    if (index != -1)
                    {
                        tempReplayDataGridCollection.Add(new ObservableReplay(replayInfo, replayInfo.playerInfos[index]));
                    }
                }
            }
            //replayDataGridCollection = tempReplayDataGridCollection;
            ReplayDataGrid.ItemsSource = tempReplayDataGridCollection;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            FindFromProfile();
        }

        private void ProfileTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FindFromProfile();
            }
            
        }

        void FindFromPlayerUserId()
        {
            ObservableCollection<ObservableReplay> tempReplayDataGridCollection = new ObservableCollection<ObservableReplay>();
            string PlayerUserIdStr = PlayerUserIdTextBox.Text;
            string[] PlayerUserIdStrs = PlayerUserIdStr.Split(',');
            List<int> PlayerUserIds = new List<int>();
            foreach (string PlayerUserId in PlayerUserIdStrs)
            {
                if(!string.IsNullOrWhiteSpace(PlayerUserId))
                {
                    PlayerUserIds.Add(int.Parse(PlayerUserId));
                }
            }
            List<ReplayInfo> SamePlayernameReplayinfos = new List<ReplayInfo>();
            foreach (int PlayerUserId in PlayerUserIds)
            {
                foreach (ReplayInfo replayInfo in replayInfos)
                {
                    int index = replayInfo.playerInfos.FindIndex(playerinfo => playerinfo.PlayerUserId == PlayerUserId);
                    if (index != -1)
                    {
                        tempReplayDataGridCollection.Add(new ObservableReplay(replayInfo, replayInfo.playerInfos[index]));
                    }
                }
            }
            //replayDataGridCollection = tempReplayDataGridCollection;
            ReplayDataGrid.ItemsSource = tempReplayDataGridCollection;
        }
    }
}
