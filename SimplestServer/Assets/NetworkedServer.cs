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

    LinkedList<PlayerAccount> playerAccounts;

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

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
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
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
            Debug.Log("create an Account");
            // check if player  account name already exists,

            string n = csv[1];
            string p = csv[2];
            bool nameInUse = false;

            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.name == n)
                {
                    nameInUse = true;
                    break;
                }
            }

            if (nameInUse)
            {
                SendMessageToClient(ServerToClientSignifiers.CreateAcountFailed + "", id);
                Debug.LogWarning("This Account already exist");
            }
            else
            {
                // Create  new account, add to list

                PlayerAccount newPlayAccount = new PlayerAccount(n, p);
                playerAccounts.AddLast(newPlayAccount);
                SendMessageToClient(ServerToClientSignifiers.CreateAcountComplete + "", id);

                // save list to HD
                SavePlayerManagementFile();
                Debug.LogWarning("This Account has created and add");
            }
            // If not, 
            // send to success/ failure
        }
        else if (signifier == ClientToServerSignifiers.Login)
        {
            Debug.Log("Login to an account");
            // check if player  account name already exists,
            string n = csv[1];
            string p = csv[2];
            bool nameInUse = false;
            bool Ispassward = false;

            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.name == n)
                {
                    nameInUse = true;
                    break;
                }
            }

            if (nameInUse)
            {
                foreach (PlayerAccount pa in playerAccounts)
                {
                    if (pa.name == n && pa.password == p)
                    {
                        Ispassward = true;
                        break;
                    }
                }
                if (Ispassward)
                {
                    Debug.LogWarning("Password was right. You are in your Account");
                    SendMessageToClient(ServerToClientSignifiers.LoginComplete + "", id);
                }
                else
                {
                    SendMessageToClient(ServerToClientSignifiers.LoginFailedPassword + "", id);
                }
            }
            else
            {
                Debug.LogWarning("This Account doesn't exist");
                SendMessageToClient(ServerToClientSignifiers.LoginFailedAccount + "", id);
            }

            // send to success/ failure

        }
        else if (signifier == ClientToServerSignifiers.SendChatMsg) 
        {
            string Msg = csv[1];
            SendMessageToClient(ServerToClientSignifiers.ChatView + "fromTheServer: "+ Msg, id);
        }

    }


    public void SavePlayerManagementFile() 
    {
        StreamWriter sw = new StreamWriter(Application.dataPath + Path.DirectorySeparatorChar + "PlayerManagementFile.txt");
        foreach (PlayerAccount pa in playerAccounts)
        {
            sw.WriteLine(PlayerSignifiers.PlayerIdSinifier + "," + pa.name + "," + pa.password);
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
                if (signifier == PlayerSignifiers.PlayerIdSinifier)
                {

                    playerAccounts.AddLast(new PlayerAccount ( csv[1], csv[2] ) );
                }
            }
        }
    }




    public class PlayerSignifiers
    {
        public const int PlayerIdSinifier = 1;
    }

    public class PlayerAccount 
    {
        public string name, password;
        
       public  PlayerAccount (string Name, string PassWord) 
        {
            name = Name;
            password = PassWord;
        }
    }

    public class ClientToServerSignifiers
    {
        public const int CreateAcount = 1;

        public const int Login = 2;

        public const int SendChatMsg = 3;
    }

    public class ServerToClientSignifiers
    {

        public const int LoginComplete = 1;

        public const int LoginFailedAccount = 2;

        public const int LoginFailedPassword = 3;

        public const int CreateAcountComplete = 4;

        public const int CreateAcountFailed = 5;

        public const int ChatView = 6;
    }
    
}
