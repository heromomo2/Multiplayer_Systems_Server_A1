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

    LinkedList<GameRoom> GameRoomList;
    LinkedList<PlayerAccount> playerAccounts;
    LinkedList<PlayerAccount> ListOfPlayerConnected;

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);


        // ClientList = new LinkedList<Clinet>();
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
            
            SendToAllClient( Msg);
        }
        else if (signifier == ClientToServerSignifiers.EnterTheChatRoom)
        {
            AddPlayerToTheChat(csv[1], id);
        }
        else if (signifier == ClientToServerSignifiers.Logout)
        {
            LogOutPlayer(id);
        }
        else if (signifier == ClientToServerSignifiers.CreateGameRoom)
        {
            LogOutPlayer(id);
        }
        else if (signifier == ClientToServerSignifiers.SendChatPrivateMsg)
        {
            SendToSpecificClient(csv[1], csv[2], id);
        }
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

    public void SendToAllClient(string Msg) 
    {
        foreach ( PlayerAccount pa in ListOfPlayerConnected)
        {
            SendMessageToClient(ServerToClientSignifiers.ChatView + "," + Msg, pa.ConnectionID);
        }
    }

    public void SendToSpecificClient(string Msg, string UserName, int id ) 
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
            SendToAllClient(DisconnectMsg);
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
            SendToAllClient(LogOutMsgOfChat);

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
        SendToAllClient(JoinChatMsg);
    }

    public void CreateGameRoom(string userName,string GameRoomName, int id)
    {
        GameRoomList.AddLast(new GameRoom(GameRoomName,new PlayerAccount(userName,id)));
    }
    public void JoinGameRoom(string userName, string GameRoomName, int id)
    {
        GameRoom tempGameroom = new GameRoom();

        foreach (GameRoom gr in GameRoomList)
        {
            if(gr.RoomName == GameRoomName) 
            {
                if (gr.Players.Length < 2)
                {
                    gr.Players[1] = new PlayerAccount(userName,id);
                    break;
                }
                
            }
        }

        
    }

    public class GameRoom
    {
        public string RoomName;
        public PlayerAccount[] Players = new PlayerAccount[1];
        public GameRoom()
        {
          
        }
        public  GameRoom (string RN, PlayerAccount A) 
        {
            RoomName = RN;
            Players[0] = A;
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

        public const int Logout = 6;

        public const int CreateGameRoom = 7; /// create a gameroom

        public const int JoinGameRoom = 8; /// join a gameroom

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
        
        public const int CreateGameRoomComplete = 11;

        public const int JoinGameRoomComplete = 12;

    }
    
