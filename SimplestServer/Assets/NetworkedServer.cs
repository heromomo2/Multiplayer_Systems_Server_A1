using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;


    int[] RematchAgree = new int [2];
    LinkedList<GameRoom> ListOfgamerooms;
    LinkedList<PlayerAccount> playerAccounts;
    LinkedList<PlayerAccount> ListOfPlayerConnected;

    int PlayerWaitingForMatchWithID = -1;

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);


        ListOfgamerooms = new LinkedList<GameRoom>();
        ListOfPlayerConnected = new LinkedList<PlayerAccount>();
        playerAccounts = new LinkedList<PlayerAccount>();
        // read in player accounts from wherever
        LoadPlayerManagementFile();
    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);

               // ClientList.AddLast(new Clinet(recConnectionID));

                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);

                PlayerDisconnect(recConnectionID);
                SendClearListofPlayersToClient();
                SendToListofPlayersToClient();
                break;
        }

    }
  
    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }
    
    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');

        int signifier = int.Parse(csv[0]);

        if (signifier == ClientToServerSignifiers.CreateAcount)
        {
            CreatedAccount(csv[1], csv[2], id);
        }
        else if (signifier == ClientToServerSignifiers.Login)
        {

            Login(csv[1], csv[2], id);
        }
        else if (signifier == ClientToServerSignifiers.SendChatMsg) 
        {
            string Msg = csv[1];
            
            SendingGlobalMessageInChat( Msg);
        }
        else if (signifier == ClientToServerSignifiers.EnterTheChatRoom)
        {
            AddPlayerToTheChat(csv[1], id);
        }
        else if (signifier == ClientToServerSignifiers.Logout)
        {
            LogOutPlayer(id);
        }
        else if (signifier == ClientToServerSignifiers.SendChatPrivateMsg)
        {
            SendIngPrivateMessageInChat(csv[1], csv[2], id);
        }
        else if(signifier == ClientToServerSignifiers.JoinQueueForGameRoom) 
        {
            Debug.Log(" We need to get this player in a waiting Queue!!!");

            if(PlayerWaitingForMatchWithID == -1) 
            {
                PlayerWaitingForMatchWithID = id;
            }
            else 
            {
                // so what if the player with their id being stored in PlayWaitingForMatchWithID has left???
                GameRoom Gr = new GameRoom (PlayerWaitingForMatchWithID, id);
                ListOfgamerooms.AddLast(Gr);


                SendMessageToClient(ServerToClientSignifiers.GameStart + ", 2", Gr.PlayerTwoID);
                SendMessageToClient(ServerToClientSignifiers.GameStart + ", 1", Gr.playerOneID);
                PlayerWaitingForMatchWithID = -1;
            }
        }
        else if (signifier == ClientToServerSignifiers.TicTacToesSomethingSomthing) 
        {
            GameRoom gr = GetGameRoomClientId(id);
            if(gr != null) 
            {
                if (gr.playerOneID == id)
                {

                    SendMessageToClient(ServerToClientSignifiers.OpponentPlayed+","+ csv[1] , gr.PlayerTwoID);

                    SendMessageToClient(ServerToClientSignifiers.WaitForOppentMoved + ",0", gr.playerOneID);// make the play wait
                }
                else 
                {
                    SendMessageToClient(ServerToClientSignifiers.OpponentPlayed + "," + csv[1], gr.playerOneID);

                    SendMessageToClient(ServerToClientSignifiers.WaitForOppentMoved + ",0", gr.PlayerTwoID);// make the play wait
                }
                //Bug:we never clean up our GameRooms, even One players leaves
                // we need to 
            }

        }
        else if (signifier == ClientToServerSignifiers.ReMatchOfTicTacToe) 
        {
            GameRoom gr = GetGameRoomClientId(id);


            if (gr.playerOneID == id) 
            {
                RematchAgree[0] = 1;
            }
            else 
            {
                RematchAgree[1] = 1;
            }

            if (RematchAgree[0] == 1 && RematchAgree[1] == 1) 
            {
                SendMessageToClient(ServerToClientSignifiers.ReMatchOfTicTacToeComplete + ",2", gr.PlayerTwoID);
                SendMessageToClient(ServerToClientSignifiers.ReMatchOfTicTacToeComplete + ",1", gr.playerOneID);

                RematchAgree[0] = 0;
                RematchAgree[1] = 0;
            }

        }
        else if (signifier == ClientToServerSignifiers.ExitTacTacToe)
        {
            GameRoom gr = GetGameRoomClientId(id);

           // ListOfgamerooms.Remove(gr);

            if (gr.playerOneID == id)
            {
                SendMessageToClient(ServerToClientSignifiers.PreventRematch + ",2", gr.PlayerTwoID);
                SendMessageToClient(ServerToClientSignifiers.ExitTacTacToeComplete + ",1", gr.playerOneID);
                gr.playerOneID = -1;
            }
            else
            {
                SendMessageToClient(ServerToClientSignifiers.ExitTacTacToeComplete + ",2", gr.PlayerTwoID);
                SendMessageToClient(ServerToClientSignifiers.PreventRematch + ",1", gr.playerOneID);
                gr.PlayerTwoID = -1;
            }
           
            if(gr.playerOneID == -1 && gr.PlayerTwoID == -1) 
            {
                ListOfgamerooms.Remove(gr);
            }

        }

    }

    private GameRoom GetGameRoomClientId (int id) 
    {
        foreach(GameRoom gr in ListOfgamerooms)
        {
            if (gr.playerOneID == id || gr.PlayerTwoID == id) 
            {
                return gr;
            }
        }
        return null;
    }

    public void SavePlayerManagementFile() 
    {
        StreamWriter sw = new StreamWriter(Application.dataPath + Path.DirectorySeparatorChar + "PlayerManagementFile.txt");
        foreach (PlayerAccount pa in playerAccounts)
        {
            sw.WriteLine(PlayerAccount.PlayerIdSinifier + "," + pa.name + "," + pa.password);
        }
        sw.Close();
    }

    public void LoadPlayerManagementFile()
    {
        if (File.Exists(Application.dataPath + Path.DirectorySeparatorChar + "PlayerManagementFile.txt"))
        {
            StreamReader sr = new StreamReader(Application.dataPath + Path.DirectorySeparatorChar + "PlayerManagementFile.txt");
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');

                int signifier = int.Parse(csv[0]);
                if (signifier == PlayerAccount.PlayerIdSinifier)
                {

                    playerAccounts.AddLast(new PlayerAccount ( csv[1], csv[2] ) );
                }
            }
        }
    }


    public void CreatedAccount(string userName, string Password, int id) 
    {
        Debug.Log("create an Account");
        // check if player  account name already exists,

        bool nameInUse = false;

        foreach (PlayerAccount pa in playerAccounts)
        {
            if (pa.name == userName)
            {
                nameInUse = true;
                break;
            }
        }

        if (nameInUse)
        {
            SendMessageToClient(ServerToClientSignifiers.CreateAcountFailed + ",8", id);
            Debug.LogWarning("This Account already exist");
        }
        else
        {
            // Create  new account, add to list

            PlayerAccount newPlayAccount = new PlayerAccount(userName, Password);
            playerAccounts.AddLast(newPlayAccount);
            SendMessageToClient(ServerToClientSignifiers.CreateAcountComplete + ",8", id);

            // save list to HD
            SavePlayerManagementFile();
            Debug.LogWarning("This Account has created and add");
        }
        // If not, 
        // send to success/ failure
    }


    public void Login (string userName, string Password, int id)
    {
        Debug.Log("Login to an account");
        // check if player  account name already exists,
        
        bool nameInUse = false;
        bool Ispassward = false;

        foreach (PlayerAccount pa in playerAccounts)
        {
            if (pa.name == userName)
            {
                nameInUse = true;
                break;
            }
        }

        if (nameInUse)
        {
            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.name == userName && pa.password == Password)
                {
                    Ispassward = true;
                    break;
                }
            }

            if (Ispassward)
            {
                Debug.LogWarning("Password was right. You are in your Account");
                SendMessageToClient(ServerToClientSignifiers.LoginComplete + "," + userName, id);
                
            }
            else
            {
                SendMessageToClient(ServerToClientSignifiers.LoginFailedPassword + ",8", id);
                Debug.LogWarning("Password was wrong");
            }
        }
        else
        {
            Debug.LogWarning("This Account doesn't exist");
            SendMessageToClient(ServerToClientSignifiers.LoginFailedAccount + ", 8", id);
        }

        // send to success/ failure
    }

    public void SendingGlobalMessageInChat(string Msg) 
    {
        foreach ( PlayerAccount pa in ListOfPlayerConnected)
        {
            SendMessageToClient(ServerToClientSignifiers.ChatView + "," + Msg, pa.ConnectionID);
        }
    }

    public void SendIngPrivateMessageInChat(string Msg, string UserName, int id ) 
    {
        PlayerAccount SpecifierPlayer = new PlayerAccount();
        bool isPlayerReal = false;

        foreach (PlayerAccount pa in ListOfPlayerConnected)
        {
            if(pa.name == UserName) 
            {
                SpecifierPlayer = pa;
                isPlayerReal = true;
                break;
            }
        }
        if (isPlayerReal) 
        {
            if (SpecifierPlayer.ConnectionID != id)
            {
                SendMessageToClient(ServerToClientSignifiers.ReceivePrivateChatMsg + "," + Msg, id);
                SendMessageToClient(ServerToClientSignifiers.ReceivePrivateChatMsg + "," + Msg, SpecifierPlayer.ConnectionID);
            }
            else 
            {
                SendMessageToClient(ServerToClientSignifiers.ReceivePrivateChatMsg + "," + Msg, id);
            }
        }


    }
    public void SendToListofPlayersToClient()
    {
        foreach (PlayerAccount pa in ListOfPlayerConnected)
        {
            foreach (PlayerAccount C in ListOfPlayerConnected) 
            {
                SendMessageToClient(ServerToClientSignifiers.ReceiveListOFPlayerInChat + "," + pa.name, C.ConnectionID);
            }
        }
    }
    public void SendClearListofPlayersToClient()
    {
        foreach (PlayerAccount pa in ListOfPlayerConnected)
        {
            SendMessageToClient(ServerToClientSignifiers.ReceiveClearListOFPlayerInChat + ",8" , pa.ConnectionID);
        }
    }
    public void PlayerDisconnect(int recConnectionID)
    {
        bool IsplayerInChat = false;
       PlayerAccount TempPlayerAccount = new PlayerAccount ();
       

        foreach (PlayerAccount pa in ListOfPlayerConnected)
        {
            if (recConnectionID == pa.ConnectionID)
            {
                TempPlayerAccount = pa;
                IsplayerInChat = true;
                break;
            }
        }

        ListOfPlayerConnected.Remove(TempPlayerAccount);
        Debug.LogWarning("TempPlayerAccount : " + TempPlayerAccount.ConnectionID.ToString());

        /// 
        if (TempPlayerAccount.name != ""&& TempPlayerAccount.name != null && IsplayerInChat)
        {
            string DisconnectMsg = "< " + TempPlayerAccount.name + " > Have been disconnected from the chat.";
            SendingGlobalMessageInChat(DisconnectMsg);
            return;
        }
    }

    public void LogOutPlayer(int recConnectionID)
    {
        PlayerAccount TempPlayerAccount = new PlayerAccount();
        bool IsplayerInChat = false;

        foreach (PlayerAccount pa in ListOfPlayerConnected)
        {
            if (recConnectionID == pa.ConnectionID)
            {
                TempPlayerAccount = pa;
                IsplayerInChat = true;
                break;
            }
        }

        ListOfPlayerConnected.Remove(TempPlayerAccount);
        Debug.LogWarning("TempPlayerAccount : " + TempPlayerAccount.ConnectionID.ToString());

        /// 
        if (TempPlayerAccount.name != "" && TempPlayerAccount.name != null && IsplayerInChat)
        {
            string LogOutMsgOfChat = "< " + TempPlayerAccount.name + " > Has Logout.";
            SendingGlobalMessageInChat(LogOutMsgOfChat);

            LogOutMsgOfChat = ServerToClientSignifiers.LogOutComplete + ",8";
            SendClearListofPlayersToClient();
            SendToListofPlayersToClient();
            SendMessageToClient(LogOutMsgOfChat, recConnectionID);
            return;

        }
    }



    public void AddPlayerToTheChat(string userName, int id)
    {
        ListOfPlayerConnected.AddLast(new PlayerAccount(userName,  id));
        SendClearListofPlayersToClient();
        SendToListofPlayersToClient();
        // join chat msg
        string JoinChatMsg = "< " + userName + " > Have just join the chat.";
        SendingGlobalMessageInChat(JoinChatMsg);
    }

    //public void CreateGameRoom(string userName,string GameRoomName, int id)
    //{
    //    GameRoomList.AddLast(new GameRoom(GameRoomName,new PlayerAccount(userName,id)));
    //}
    //public void JoinGameRoom(string userName, string GameRoomName, int id)
    //{
    //    GameRoom tempGameroom = new GameRoom();

    //    foreach (GameRoom gr in GameRoomList)
    //    {
    //        if(gr.RoomName == GameRoomName) 
    //        {
    //            if (gr.Players.Length < 2)
    //            {
    //                gr.Players[1] = new PlayerAccount(userName,id);
    //                break;
    //            }

    //        }
    //    }


    //}

    public class GameRoom
    {
        //public string RoomName;
        public int playerOneID, PlayerTwoID;

        public GameRoom()
        {

        }
        public GameRoom( int PlayerID1, int PlayerID2)
        {
            playerOneID = PlayerID1;
            PlayerTwoID = PlayerID2;
        }
    }



    public class PlayerAccount
    {
        public const int PlayerIdSinifier = 1;
        public string name, password;
        public int ConnectionID;

        public PlayerAccount(string Name, string PassWord)
        {
            name = Name;
            password = PassWord;

        }
        public PlayerAccount(string Name, string PassWord, int ConId)
        {
            name = Name;
            password = PassWord;
            ConnectionID = ConId;
        }
        public PlayerAccount(string Name, int ConId)
        {
            name = Name;
            ConnectionID = ConId;
        }
        public PlayerAccount()
        {

        }

    }

}

    public class ClientToServerSignifiers
    {
        public const int CreateAcount = 1;

        public const int Login = 2;

        public const int SendChatMsg = 3; // send a globle chat message

        public const int SendChatPrivateMsg = 4;// send a chat private msg

        public const int EnterTheChatRoom = 5; // enter the chat room

        public const int Logout = 6;//

        public const int JoinQueueForGameRoom = 7;

        public const int TicTacToesSomethingSomthing = 8;

        public const int ReMatchOfTicTacToe = 9;

        public const int ExitTacTacToe = 10;

    }

public class ServerToClientSignifiers
    {


    public const int LoginComplete = 1;

    public const int LoginFailedAccount = 2;

    public const int LoginFailedPassword = 3;

    public const int CreateAcountComplete = 4;

    public const int CreateAcountFailed = 5;

    public const int ChatView = 6; // all the receive globe chatmessage.

    public const int ReceivePrivateChatMsg = 7;//  receive a private chat message.

    public const int ReceiveListOFPlayerInChat = 8;// all the list of players in the chat

    public const int ReceiveClearListOFPlayerInChat = 9;// all the list of players 

    public const int LogOutComplete = 10;

    public const int OpponentPlayed = 11;

    public const int GameStart = 12;

    public const int WaitForOppentMoved = 13;

    public const int ReMatchOfTicTacToeComplete = 14;

    public const int ExitTacTacToeComplete = 15;

    public const int PreventRematch = 16;
}
    
